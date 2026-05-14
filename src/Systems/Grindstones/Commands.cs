using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Vintagestory;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Grindstones
{
	internal class Commands
	{

		#region const strings
		private const string settingWhitelist = "whitelist";
		private const string settingBlacklist = "blacklist";
		private const string settingAllowedMaterials = "allowedmaterials";
		private const string settingDisallowedTools = "disallowedtools";

		private const string actionSet = "set";
		private const string actionGet = "get";
		private const string actionAdd = "add";
		private const string actionRemove = "remove";
		private const string actionToDefault = "todefault";

		private static readonly ImmutableArray<string> ratioActions = [actionSet, actionGet, actionToDefault];
		private static readonly ImmutableArray<string> safteyActions = [actionSet, actionGet, actionToDefault];
		private static readonly ImmutableArray<string> setActions = [actionGet, actionAdd, actionRemove, actionToDefault];
		#endregion

		private readonly ICoreServerAPI sapi;
		private readonly IServerNetworkChannel serverChannel;

		private readonly GrindstonesConfigServer ConfigServer = ModGrindstones.ConfigServer;

		private TagSet validRepairTypes = TagSet.Empty;

		internal Commands(ICoreServerAPI sapi, IServerNetworkChannel serverChannel)
		{
			this.sapi = sapi;
			this.serverChannel = serverChannel;
		}

		internal void RegisterServerCommands(ICoreServerAPI sapi)
		{
			TagRegistryError err = sapi.CollectibleTagRegistry.TryCreateTagSet(out validRepairTypes, ["tool", "weapon"]);
			ModGrindstones.Logger.Warning("Tags had an error during [Command Registration]: \"{0}\"", err);

			CreateServerCommands(sapi);
		}

		// TODO Create helper class/method to generate commands
		private void CreateServerCommands(ICoreAPI api)
		{
			CommandArgumentParsers parsers = api.ChatCommands.Parsers;

			api.ChatCommands.Create("GConfig")
				.WithAlias("GSettings")
				.WithDescription(Lang.GetMatching("Change Grindstones mod config settings on the fly"))
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription(Lang.GetMatching("Change the ratio of MaxLoss to Gain."))
					.WithArgs([new WordArgParser("action", true, [..ratioActions]), new StringArgParser("ratio", false)])
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.BeginSubCommand("safety")
					.WithDescription(Lang.GetMatching("Change the state of the Safe Sharpening setting."))
					.WithArgs([new WordArgParser("action", true, [..safteyActions]), new BoolArgParser("safety", "enable", false)])
					.HandleWith(OnUpdateSafety)
					.EndSubCommand()
				.BeginSubCommand(settingWhitelist)
					.WithDescription(Lang.GetMatching("Edit the current overriding whitelist."))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingBlacklist)
					.WithDescription(Lang.GetMatching("Edit the current overriding blacklist."))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingAllowedMaterials)
					.WithDescription(Lang.GetMatching("Edit the currently repairable materials."))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingDisallowedTools)
					.WithDescription(Lang.GetMatching("Edit the currently disallowed tool types."))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string action = ((string) args[0]).ToLower();

			if (!ratioActions.Contains(action)) return TextCommandResult.Error(Lang.GetMatching("Unknown action \"{0}\"!", action));

			if (action == actionGet)
			{
				return TextCommandResult.Success(Lang.GetMatching("Current ratio: {0}", ConfigServer.RatioMaxDurabilityLossToDurabilityGain));
			}

			string oldRatio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = (action == actionToDefault) ? GrindstonesConfigServer.DefaultRepairRatio : args[1] as string ?? oldRatio;
			string player = args.Caller.Player.PlayerName;
			string message = Lang.GetMatching("{0} updated Grindstones repair ratio from {1} to {2}.", player, oldRatio, ratio);

			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = ratio;
			sapi.World.Config.SetString(IdentityKey.Ratio, ratio);
			sapi.StoreModConfig(ConfigServer, ModGrindstones.ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Ratio = ratio,
			});

			ModGrindstones.Logger.Notification(message);
			return TextCommandResult.Success(message);
		}

		private TextCommandResult OnUpdateSafety(TextCommandCallingArgs args)
		{
			string action = ((string) args[0]).ToLower();
			if (!safteyActions.Contains(action)) return TextCommandResult.Error(Lang.GetMatching("Unknown action \"{0}\"!", action));

			if (action == actionGet)
			{
				return TextCommandResult.Success(Lang.GetMatching("Safe sharpening is currently: {0}", Lang.GetMatching(ConfigServer.SafeSharpening ? "enabled" : "disabled")));
			}

			bool oldSafety = ConfigServer.SafeSharpening;
			bool safety = (action == actionToDefault) ? GrindstonesConfigServer.DefaultSafeSharpening : (bool) args[1];
			string player = args.Caller.Player.PlayerName;
			string message = Lang.GetMatching("{0} updated Grindstones repair safety from {1} to {2}.", player, oldSafety, safety);

			ConfigServer.SafeSharpening = safety;
			sapi.World.Config.SetBool(IdentityKey.Safe, safety);
			sapi.StoreModConfig(ConfigServer, ModGrindstones.ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Safe = safety,
			});

			ModGrindstones.Logger.Notification(message);
			return TextCommandResult.Success(message);
		}

		private TextCommandResult OnUpdateSet(TextCommandCallingArgs args)
		{
			string setting = args.SubCmdCode;
			string action = ((string) args[0]).ToLower();
			string value = "undefined";

			if (!setActions.Contains(action)) return TextCommandResult.Error(Lang.GetMatching("Unknown action \"{0}\"!", action));

			string player = args.Caller.Player.PlayerName;
			string set = "set undefined";
			string message = "message undefined";

			if (action != actionToDefault && action != actionGet)
			{
				string? type = (args[1] as string)?.ToLower();
				ItemSlot currentHotbar = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

				switch (setting)
				{
					case settingWhitelist:
					case settingBlacklist:
						if (!ValidateTool(out value, currentHotbar, type)) return TextCommandResult.Error(value);
						break;
					case settingAllowedMaterials:
						if (!ValidateMaterial(out value, currentHotbar, type)) return TextCommandResult.Error(value);
						break;
					case settingDisallowedTools:
						if (!ValidateToolType(out value, currentHotbar, type)) return TextCommandResult.Error(value);
						break;
				}
			}

			ref HashSet<string> config = ref ConfigServer.Whitelist;
			switch (setting)
			{
				case settingWhitelist:
					set = Lang.GetMatching("whitelist");
					break;
				case settingBlacklist:
					config = ref ConfigServer.Blacklist;
					set = Lang.GetMatching("blacklist");
					break;
				case settingAllowedMaterials:
					config = ref ConfigServer.AllowedRepairableMaterials;
					set = Lang.GetMatching("material whitelist");
					break;
				case settingDisallowedTools:
					config = ref ConfigServer.NotRepairableToolTypes;
					set = Lang.GetMatching("tool blacklist");
					break;
			}
			
			switch (action)
			{
				case actionGet:
					return TextCommandResult.Success(Lang.GetMatching("Current {0}: {1}", set, string.Join(", ", config)));
				case actionAdd:
					config.Add(value);
					message = Lang.GetMatching("{0} added {1} to the {2}.", player, value, set);
					break;
				case actionRemove:
					config.Remove(value);
					message = Lang.GetMatching("{0} removed {1} from the {2}.", player, value, set);
					break;
				case actionToDefault:
					config = setting switch
					{
						settingWhitelist => GrindstonesConfigServer.DefaultWhitelist.ToHashSet(),
						settingBlacklist => GrindstonesConfigServer.DefaultBlacklist.ToHashSet(),
						settingAllowedMaterials => GrindstonesConfigServer.DefaultAllowedMaterials.ToHashSet(),
						settingDisallowedTools => GrindstonesConfigServer.DefaultDisallowedTools.ToHashSet(),
						_ => config
					};
					message = Lang.GetMatching("{0} reset the {1} to default.", player, set);
					break;
			}

			UpdateConfig update = new UpdateConfig();			
			
			switch (setting)
			{
				case settingWhitelist:
					update.Whitelist = config.ToArray();
					break;
				case settingBlacklist:
					update.Blacklist = config.ToArray();
					break;
				case settingAllowedMaterials:
					update.AllowedMaterials = config.ToArray();
					break;
				case settingDisallowedTools:
					update.DisallowedTools = config.ToArray();
					break;
			}

			sapi.StoreModConfig(ConfigServer, ModGrindstones.ConfigFile);
			serverChannel.BroadcastPacket(update);
			
			ModGrindstones.Logger.Audit(message);
			return TextCommandResult.Success(message);
		}

		private bool ValidateTool (out string tool, ItemSlot itemSlot, string? type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					tool = Lang.GetMatching("No tool specified nor held!");
					return false;
				}

				if (!(itemSlot.Itemstack.Item.Tags.Overlaps(validRepairTypes) || itemSlot.Itemstack.Item?.Tool is not null))
				{
					tool = Lang.GetMatching("{0} is not a tool!", itemSlot.Itemstack.GetName());
					return false;
				}
				
				type = itemSlot.Itemstack.Item.Code;
			}
			else if (sapi.World.GetItem(type)?.Tool is null)
			{
				tool = Lang.GetMatching("{0} is not a tool!", type);
				return false;
			}

			tool = type;
			return true;
		}

		private static bool ValidateMaterial (out string material, ItemSlot itemSlot, string? type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					material = Lang.GetMatching("No material specified nor tool held!");
					return false;
				}

				type = itemSlot.Itemstack.Item?.Variant["material"] ?? itemSlot.Itemstack.Item?.Variant["metal"] ?? null;
				if (type is null)
				{
					material = Lang.GetMatching("{0} does not have a specified material type!", itemSlot.Itemstack.GetName());
					return false;
				}
			}

			material = type.ToLower();
			return true;
		}

		private static bool ValidateToolType (out string toolType, ItemSlot itemSlot, string? type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					toolType = Lang.GetMatching("No tool category specified nor tool held!");
					return false;
				}

				type = itemSlot.Itemstack.Item.Tool?.ToString();
				if (type is null)
				{
					toolType = Lang.GetMatching("{0} is not a tool!", itemSlot.Itemstack.GetName());
					return false;
				}
			}
			// Requires that all tools are registered with the vanilla enum tool type
			else if (!Enum.TryParse<EnumTool>(type, true, out _))
			{
				toolType = Lang.GetMatching("{0} is not a vaild tool type!", type);
				return false;
			}

			toolType = type.ToLower();
			return true;
		}
	}
}
