using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
					.WithDescription(Lang.Get("gconfig.desc.ratio"))
					.WithArgs([new WordArgParser("action", true, [..ratioActions]), new StringArgParser("ratio", false)])
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.BeginSubCommand("safety")
					.WithDescription(Lang.Get("gconfig.desc.safety"))
					.WithArgs([new WordArgParser("action", true, [..safteyActions]), new BoolArgParser("safety", "enable", false)])
					.HandleWith(OnUpdateSafety)
					.EndSubCommand()
				.BeginSubCommand(settingWhitelist)
					.WithDescription(Lang.Get("gconfig.desc.whitelist"))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingBlacklist)
					.WithDescription(Lang.Get("gconfig.desc.blacklist"))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingAllowedMaterials)
					.WithDescription(Lang.Get("gconfig.desc.allowedMaterials"))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand(settingDisallowedTools)
					.WithDescription(Lang.Get("gconfig.desc.disalloedTools"))
					.WithArgs([new WordArgParser("action", true, [..setActions]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string action = ((string) args[0]).ToLower();

			if (!ratioActions.Contains(action)) return TextCommandResult.Error(Lang.Get("gconfig.error.unknownaction", action));

			if (action == actionGet)
			{
				return TextCommandResult.Success(Lang.Get("gconfig.message.getratio", ConfigServer.RatioMaxDurabilityLossToDurabilityGain));
			}

			string oldRatio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = (action == actionToDefault) ? GrindstonesConfigServer.DefaultRepairRatio : args[1] as string ?? oldRatio;
			string player = args.Caller.Player.PlayerName;
			string message = Lang.Get("gconfig.message.ratioupdate", player, oldRatio, ratio);

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
			if (!safteyActions.Contains(action)) return TextCommandResult.Error(Lang.Get("gconfig.error.unknownaction", action));

			if (action == actionGet)
			{
				return TextCommandResult.Success(Lang.Get("gconfig.message.getsafety", Lang.Get(ConfigServer.SafeSharpening ? "enabled" : "disabled")));
			}

			bool oldSafety = ConfigServer.SafeSharpening;
			bool safety = (action == actionToDefault) ? GrindstonesConfigServer.DefaultSafeSharpening : (bool) args[1];
			string player = args.Caller.Player.PlayerName;
			string message = Lang.Get("gconfig.message.safetyupdate", player, oldSafety, safety);

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

			if (!setActions.Contains(action)) return TextCommandResult.Error(Lang.Get("gconfig.error.unknownaction", action));

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
				}
			}

			ref HashSet<string> config = ref ConfigServer.Whitelist;
			switch (setting)
			{
				case settingWhitelist:
					set = Lang.Get("whitelist");
					break;
				case settingBlacklist:
					config = ref ConfigServer.Blacklist;
					set = Lang.Get("blacklist");
					break;
				case settingAllowedMaterials:
					config = ref ConfigServer.AllowedRepairableMaterials;
					set = Lang.Get("material whitelist");
					break;
				case settingDisallowedTools:
					config = ref ConfigServer.NotRepairableToolTypes;
					set = Lang.Get("tool blacklist");
					break;
			}
			
			switch (action)
			{
				case actionAdd:
					config.Add(value);
					message = Lang.Get("gconfig.message.setadd", player, value, set);
					break;
				case actionRemove:
					config.Remove(value);
					message = Lang.Get("gconfig.message.setremove", player, value, set);
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
					message = Lang.Get("gconfig.message.setdefault", player, set);
					break;
				case actionGet:
					return TextCommandResult.Success(Lang.Get("gconfig.message.getset", set, string.Join(", ", config)));
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
					tool = Lang.Get("gconfig.error.notool");
					return false;
				}

				if (itemSlot.Itemstack.Item?.Tool is null)
				{
					tool = Lang.Get("gconfig.error.invalidtool", itemSlot.Itemstack.GetName());
					return false;
				}
				
				type = itemSlot.Itemstack.Item.Code;
			}
			else if (sapi.World.GetItem(type).Tool is null)
			{
				tool = Lang.Get("gconfig.error.invalidtool", type);
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
					material = Lang.Get("gconfig.error.nomaterial");
					return false;
				}

				type = itemSlot.Itemstack.Item?.Variant["material"] ?? itemSlot.Itemstack.Item?.Variant["metal"] ?? null;
				if (type is null)
				{
					material = Lang.Get("gconfig.error.unspecifiedmaterial", itemSlot.Itemstack.GetName());
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
					toolType = Lang.Get("gconfig.error.notooltype");
					return false;
				}

				type = itemSlot.Itemstack.Item.Tool?.ToString();
				if (type is null)
				{
					toolType = Lang.Get("gconfig.error.invalidtool", itemSlot.Itemstack.GetName());
					return false;
				}
			}
			// Requires that all tools are registered with the vanilla enum tool type
			else if (!Enum.TryParse<EnumTool>(type, true, out _))
			{
				toolType = Lang.Get("gconfig.error.invalidtooltype", type);
				return false;
			}

			toolType = type.ToLower();
			return true;
		}
	}
}
