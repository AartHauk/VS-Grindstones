using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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

		internal Commands(ICoreServerAPI sapi, IServerNetworkChannel serverChannel)
		{
			this.sapi = sapi;
			this.serverChannel = serverChannel;
		}

		internal void RegisterServerCommands(ICoreServerAPI sapi)
		{
			CreateServerCommands(sapi);
		}

		// TODO Add the ability to change settings on the fly
		// TODO Create helper class/method to generate commands
		// TODO Allow for lang file translations
		private void CreateServerCommands(ICoreAPI api)
		{
			CommandArgumentParsers parsers = api.ChatCommands.Parsers;

			api.ChatCommands.Create("GConfig")
				.WithAlias("GSettings")
				.WithDescription("Change Grindstones mod config settings on the fly")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("Change the ratio of MaxLoss to Gain.")
					.WithArgs([new WordArgParser("action", true, [..ratioActions]), new StringArgParser("ratio", false)])
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.BeginSubCommand("safety")
					.WithDescription("Change the state of the Safe Sharpening setting.")
					.WithArgs([new WordArgParser("action", true, [..safteyActions]), new BoolArgParser("safety", "enable", false)])
					.HandleWith(OnUpdateSafety)
					.EndSubCommand()
				.BeginSubCommand(settingWhitelist)
					.WithDescription("Edit the current overriding whitelist.")
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingBlacklist)
					.WithDescription("Edit the current overriding blacklist.")
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingAllowedMaterials)
					.WithDescription("Edit the currently repairable materials.")
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingDisallowedTools)
					.WithDescription("Edit the currently disallowed tool types.")
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string action = ((string) args[0]).ToLower();

			if (!ratioActions.Contains(action)) return TextCommandResult.Error($"Unknown action \"{action}\"");

			if (action == actionGet)
			{
				return TextCommandResult.Success($"Current ratio: {ConfigServer.RatioMaxDurabilityLossToDurabilityGain}");
			}

			string oldRatio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = (action == actionToDefault) ? GrindstonesConfigServer.DefaultRepairRatio : args[1] as string ?? oldRatio;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair ratio from " + oldRatio + " to " + ratio + ".";

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
			if (!safteyActions.Contains(action)) return TextCommandResult.Error($"Unknown action \"{action}\"");

			if (action == actionGet)
			{
				return TextCommandResult.Success($"Safe sharpening is currently: {(ConfigServer.SafeSharpening ? "enabled" : "disabled")}");
			}

			bool oldSafety = ConfigServer.SafeSharpening;
			bool safety = (action == actionToDefault) ? GrindstonesConfigServer.DefaultSafeSharpening : (bool) args[1];
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair safety from " + oldSafety + " to " + safety + ".";

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

			if (!setActions.Contains(action)) return TextCommandResult.Error($"Unknown action \"{action}\"");

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
						if (!ValidateTool(out value, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
					case settingAllowedMaterials:
						if (!ValidateMaterial(out value, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
					case settingDisallowedTools:
						if (!ValidateToolType(out value, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
					default:
						return TextCommandResult.Error($"Unknown config setting '{setting}'.");
				}
			}

			ref HashSet<string> config = ref ConfigServer.Whitelist;
			switch (setting)
			{
				case settingWhitelist:
					set = "whitelist";
					break;
				case settingBlacklist:
					config = ref ConfigServer.Blacklist;
					set = "blacklist";
					break;
				case settingAllowedMaterials:
					config = ref ConfigServer.AllowedRepairableMaterials;
					set = "material whitelist";
					break;
				case settingDisallowedTools:
					config = ref ConfigServer.NotRepairableToolTypes;
					set = "tool blacklist";
					break;
				default:
					return TextCommandResult.Error($"Unknown config setting '{setting}'.");
			}
			
			switch (action)
			{
				case actionAdd:
					config.Add(value);
					message = $"{player} added {value} to the {set}.";
					break;
				case actionRemove:
					config.Remove(value);
					message = $"{player} removed {value} from the {set}.";
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
					message = $"{player} reset the {set} to default.";
					break;
				case actionGet:
					return TextCommandResult.Success($"Current {set}: {string.Join(", ", config)}");
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
					tool = "No tool specified nor held!";
					return false;
				}

				if (itemSlot.Itemstack.Item?.Tool is null)
				{
					tool = $"{itemSlot.Itemstack.GetName()} is not a tool!";
					return false;
				}
				
				type = itemSlot.Itemstack.Item.Code;
			}
			else if (sapi.World.GetItem(type).Tool is null)
			{
				tool = $"'{type}' is not a tool!";
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
					material = "No material specified nor tool held!";
					return false;
				}

				type = itemSlot.Itemstack.Item?.Variant["material"] ?? itemSlot.Itemstack.Item?.Variant["metal"] ?? null;
				if (type is null)
				{
					material = "Tool does not have a specified material type!";
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
					toolType = "No tool category specified nor tool held!";
					return false;
				}

				type = itemSlot.Itemstack.Item.Tool?.ToString();
				if (type is null)
				{
					toolType = $"{itemSlot.Itemstack.GetName()} is not a tool!";
					return false;
				}
			}
			// Requires that all tools are registered with the vanilla enum tool type
			else if (!Enum.TryParse<EnumTool>(type, true, out _))
			{
				toolType = $"{type} is not a valid tool type!";
				return false;
			}

			toolType = type.ToLower();
			return true;
		}
	}
}
