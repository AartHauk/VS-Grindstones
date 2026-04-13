using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;


#nullable disable

namespace Grindstones
{
	public class ModGrindstones : ModSystem
	{
		public static string ModID { get; private set; }
		public static ILogger Logger { get; private set; }
		public static GrindstonesConfigServer ConfigServer { get; private set; }

		private static Harmony Harmony;

		public override double ExecuteOrder () => 0.3;

		public ModGrindstones()
		{
			ConfigServer ??= new GrindstonesConfigServer();
		}

		public override void StartPre (ICoreAPI api)
		{
			base.StartPre(api);
			ModID ??= Mod.Info.ModID;
			Logger ??= Mod.Logger;

			IdentityKey.SetDomain(ModID);
		}

		public override void Start (ICoreAPI api)
		{
			Logger.Event("Start Called from {0}", api.Side);

			base.Start(api);

			Harmony = new Harmony(ModID);
			Harmony.PatchAll();

			api.RegisterItemClass(ModID + ".grindingwheel", typeof(ItemGrindingwheel));

			api.RegisterBlockClass(ModID + ".grindstone", typeof(BlockGrindstone));
			api.RegisterBlockEntityClass(ModID + ".begrindstone", typeof(BlockEntityGrindstone));

			
			api.Network.RegisterChannel(IdentityKey.NetworkChannel)
				.RegisterMessageType<UpdateConfig>();
		}

		#region Server
		private IServerNetworkChannel serverChannel;
		private ICoreServerAPI sapi;

		public override void StartServerSide(ICoreServerAPI api)
		{
			Logger.Event("StartServerSide Called.");
			base.StartServerSide(api);

			TryLoadServerConfig(api);

			sapi = api;
			serverChannel = api.Network.GetChannel(IdentityKey.NetworkChannel);

			new Commands(api, serverChannel).RegisterServerCommands(api);

			//CreateServerCommands(api);
		}

		internal const string ConfigFile = "GrindstonesConfig.json";

		private static void TryLoadServerConfig (ICoreAPI api)
		{
			Logger.Notification("Loading Config.");

			GrindstonesConfigServer serverConfig;
			try
			{
				serverConfig = api.LoadModConfig<GrindstonesConfigServer>(ConfigFile) ?? new GrindstonesConfigServer();

				if (serverConfig.ConfigVersion == 1)
				{
					Logger.Warning("Version 1 of config found, updating config.");
					#pragma warning disable  // Ignore obsolete warning
					int gain = serverConfig.DurabilityPointsRepairedPerPointLost;
					#pragma warning restore
					serverConfig.RatioMaxDurabilityLossToDurabilityGain = "1:" + gain;
					serverConfig.ConfigVersion = 2;
				}

				if (serverConfig.ConfigVersion == 2)
				{Logger.Warning("Version 2 of config found, updating config.");
					serverConfig.ConfigVersion = 3;
				}

				api.StoreModConfig(serverConfig, ConfigFile);
			}
			catch (Exception e)
			{
				Logger.Error("Could not load server config! Loading default settings instead.");
				Logger.Error(e);

				serverConfig = new GrindstonesConfigServer();
			}

			ConfigServer = serverConfig;

			api.World.Config.SetString(IdentityKey.Ratio, serverConfig.RatioMaxDurabilityLossToDurabilityGain);
			api.World.Config.SetBool(IdentityKey.Safe, serverConfig.SafeSharpening);
			api.World.Config.SetString(IdentityKey.Whitelist, string.Join(",", serverConfig.Whitelist));
			api.World.Config.SetString(IdentityKey.Blacklist, string.Join(",", serverConfig.Blacklist));
			api.World.Config.SetString(IdentityKey.DisallowedTools, string.Join(",", serverConfig.NotRepairableToolTypes));
			api.World.Config.SetString(IdentityKey.AllowedMaterials, string.Join(",", serverConfig.AllowedRepairableMaterials));
		}

		private void CreateServerCommands(ICoreAPI api)
		{
			CommandArgumentParsers parsers = api.ChatCommands.Parsers;

			api.ChatCommands.Create("GConfig")
				.WithAlias("GSettings")
				.WithDescription("Change Grindstones mod config settings on the fly")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("Change the ratio of MaxLoss to Gain.")
					.WithArgs([new WordArgParser("action", true, ["set", "get"]), new StringArgParser("ratio", false)])
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.BeginSubCommand("safety")
					.WithDescription("Change the state of the Safe Sharpening setting.")
					.WithArgs([new WordArgParser("action", true, ["set", "get"]), new BoolArgParser("safety", "enable", false)])
					.HandleWith(OnUpdateSafety)
					.EndSubCommand()
				.BeginSubCommand("whitelist")
					.WithDescription("Edit the current overriding whitelist.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove", "toDefault", "get"]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand("blacklist")
					.WithDescription("Edit the current overriding blacklist.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove", "toDefault", "get"]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand("allowedMaterials")
					.WithDescription("Edit the currently repairable materials.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove", "toDefault", "get"]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.BeginSubCommand("disallowedTools")
					.WithDescription("Edit the currently disallowed tool types.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove", "toDefault", "get"]), new StringArgParser("type", false)])
					.HandleWith(OnUpdateSet)
					.EndSubCommand()
				.Validate();

			 //api.ChatCommands.Create("GSettings")
				//.WithDescription("Gets the settings values for the Grindstones mod.")
				//.RequiresPrivilege(Privilege.controlserver)
				//.BeginSubCommand("ratio")
				//	.WithDescription("View the currently set ratio of MaxLoss to Gain.")
				//	.HandleWith((args) =>
				//	{
				//		string message = "Current ratio in config: " + ConfigServer.RatioMaxDurabilityLossToDurabilityGain
				//					  +"\nCurrent ratio in world : " + api.World.Config.GetAsString(IdentityKey.Ratio);
				//		sapi.SendMessage(
				//			args.Caller.Player,
				//			GlobalConstants.InfoLogChatGroup,
				//			message,
				//			EnumChatType.Notification
				//		);
				//		return TextCommandResult.Success();
				//	})
				//	.EndSubCommand()
				//.BeginSubCommand("whitelist")
				//	.WithDescription("View the current specific item whitelist.")
				//	.HandleWith((_) => TextCommandResult.Success($"Current whitelist in config: {string.Join(",", ConfigServer.Whitelist)}" +
				//	                                                $"\nCurrent whitelist in world: {api.World.Config.GetAsString(IdentityKey.Whitelist)}"))
				//	.EndSubCommand()
				//.BeginSubCommand("blacklist")
				//	.WithDescription("View the current specific item blacklist.")
				//	.HandleWith((_) => TextCommandResult.Success($"Current blacklist in config: {string.Join(",", ConfigServer.Blacklist)}" +
				//	                                                $"\nCurrent blacklist in world: {api.World.Config.GetAsString(IdentityKey.Blacklist)}"))
				//	.EndSubCommand()
				//.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string action = args[0] as string;

			if (action == "get")
			{
				return TextCommandResult.Success($"Current ratio: {ConfigServer.RatioMaxDurabilityLossToDurabilityGain}");
			}
			else if (action != "set")
			{
				return TextCommandResult.Error($"Unknown action \"{action}\"");
			}

			string oldRatio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = args[1] as string ?? oldRatio;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair ratio from " + oldRatio + " to " + ratio + ".";

			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = ratio;
			sapi.World.Config.SetString(IdentityKey.Ratio, ratio);
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Ratio = ratio,
			});

			Logger.Notification(message);
			return TextCommandResult.Success(message);
		}

		private TextCommandResult OnUpdateSafety(TextCommandCallingArgs args)
		{
			string action = args[0] as string;

			if (action == "get")
			{
				return TextCommandResult.Success($"Safe sharpening is currently: {(ConfigServer.SafeSharpening ? "enabled" : "disabled")}");
			}
			else if (action != "set")
			{
				return TextCommandResult.Error($"Unknown action \"{action}\"");
			}

			bool oldSafety = ConfigServer.SafeSharpening;
			bool safety = (bool) args[1];
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair safety from " + oldSafety + " to " + safety + ".";

			ConfigServer.SafeSharpening = safety;
			sapi.World.Config.SetBool(IdentityKey.Safe, safety);
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Safe = safety,
			});

			Logger.Notification(message);
			return TextCommandResult.Success(message);
		}

		private TextCommandResult OnUpdateSet(TextCommandCallingArgs args)
		{
			string setting = args.SubCmdCode;
			string action = (args[0] as string).ToLower();
			string type = (args[1] as string)?.ToLower();

			string player = args.Caller.Player.PlayerName;
			string set;
			string message;

			if (action != "toDefault" && action != "get")
			{
				ItemSlot currentHotbar = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

				switch (setting)
				{
					case "whitelist":
					case "blacklist":
						if (!validateTool(out type, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
					case "allowedmaterials":
						if (!validateMaterial(out type, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
					case "disallowedtools":
						if (!validateToolType(out type, currentHotbar, type)) return TextCommandResult.Error(type);
						break;
				}
			}

			ref HashSet<string> config = ref ConfigServer.Whitelist;
			switch (setting)
			{
				case "whitelist":
					set = "whitelist";
					break;
				case "blacklist":
					config = ref ConfigServer.Blacklist;
					set = "blacklist";
					break;
				case "allowedmaterials":
					config = ref ConfigServer.AllowedRepairableMaterials;
					set = "material whitelist";
					break;
				case "disallowedtools":
					config = ref ConfigServer.NotRepairableToolTypes;
					set = "tool blacklist";
					break;
				default:
					return TextCommandResult.Error($"Unknown config setting '{setting}'.");
			}
			
			switch (action)
			{
				case "add":
					config.Add(type);
					message = $"{player} added {type} to the {set}.";
					break;
				case "remove":
					config.Remove(type);
					message = $"{player} removed {type} from the {set}.";
					break;
				case "todefault":
					config = setting switch
					{
						"whitelist" => GrindstonesConfigServer.DefaultWhitelist.ToHashSet(),
						"blacklist" => GrindstonesConfigServer.DefaultBlacklist.ToHashSet(),
						"allowedmaterials" => GrindstonesConfigServer.DefaultAllowedMaterials.ToHashSet(),
						"disallowedtools" => GrindstonesConfigServer.DefaultDisallowedTools.ToHashSet(),
						_ => config
					};
					message = $"{player} reset the {set} to default.";
					break;
				case "get":
					message = $"Current {set}: {string.Join(", ", config)}";
					break;
				default:
					return TextCommandResult.Error($"Unknown action '{action}'.");
			}

			if (action != "get") {
				UpdateConfig update = new UpdateConfig();			
			
				switch (setting)
				{
					case "whitelist":
						update.Whitelist = config.ToArray();
						break;
					case "blacklist":
						update.Blacklist = config.ToArray();
						break;
					case "allowedmaterials":
						update.AllowedMaterials = config.ToArray();
						break;
					case "disallowedtools":
						update.DisallowedTools = config.ToArray();
						break;
				}

				sapi.StoreModConfig(ConfigServer, ConfigFile);
				serverChannel.BroadcastPacket(update);
			}
			
			Logger.Audit(message);
			return TextCommandResult.Success(message);
		}

		private bool validateTool (out string tool, ItemSlot itemSlot, string type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					tool = "No tool specified nor held!";
					return false;
				}

				if (itemSlot.Itemstack?.Item?.Tool is null)
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

		private bool validateMaterial (out string material, ItemSlot itemSlot, string type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					material = "No material specified nor tool held!";
					return false;
				}

				material = itemSlot.Itemstack?.Item?.Variant["material"] ?? itemSlot?.Itemstack?.Item?.Variant["metal"] ?? null;
				if (material is null)
				{
					material = "Tool does not have a specified material type!";
					return false;
				}
			}
			else // Maybe add materials from an ore dict someday
			{
				material = type;
			}

			material = material.ToLower();
			return true;
		}

		private bool validateToolType (out string toolType, ItemSlot itemSlot, string type = null)
		{
			if (type is null)
			{
				if (itemSlot.Empty)
				{
					toolType = "No tool category specified nor tool held!";
					return false;
				}

				if (itemSlot?.Itemstack.Item?.Tool is null)
				{
					toolType = $"{itemSlot.Itemstack.GetName()} is not a tool!";
					return false;
				}

				type = itemSlot.Itemstack.Item.Tool.ToString();
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
		#endregion

		#region Client
		IClientNetworkChannel clientChannel;
		ICoreClientAPI capi;
		public override void StartClientSide (ICoreClientAPI api)
		{
			Logger.Event("StartClientSide Called.");
			base.StartClientSide(api);

			GetServerSettings(api);

			capi = api;

			clientChannel = api.Network.GetChannel(IdentityKey.NetworkChannel)
				.SetMessageHandler<UpdateConfig>(OnConfigUpdated);

			// TODO Rework this command to be better for client
			api.ChatCommands.Create("GSettings")
				.WithDescription("Gets the settings values for the Grindstones mod.")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("cratio")
					.WithDescription("View the currently set ratio of MaxLoss to Gain.")
					.HandleWith((_) =>
					{
						string message = "Current ratio in config: " + ConfigServer.RatioMaxDurabilityLossToDurabilityGain
									  +"\nCurrent ratio in world : " + api.World.Config.GetAsString(IdentityKey.Ratio);
						capi.SendChatMessage(message);
						return TextCommandResult.Success();
					})
					.EndSubCommand()
				.Validate();
		}
		private void GetServerSettings(ICoreAPI api)
		{
			Logger.Event("Receiving config settings from server.");
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = api.World.Config.GetString(IdentityKey.Ratio, GrindstonesConfigServer.DefaultRepairRatio);
			ConfigServer.SafeSharpening = api.World.Config.GetBool(IdentityKey.Safe, GrindstonesConfigServer.DefaultSafeSharpening);
			ConfigServer.Whitelist = [..api.World.Config.GetString(IdentityKey.Whitelist, string.Join(",", GrindstonesConfigServer.DefaultWhitelist)).Split(",")];
			ConfigServer.Blacklist = [..api.World.Config.GetString(IdentityKey.Blacklist, string.Join(",", GrindstonesConfigServer.DefaultBlacklist)).Split(",")];
			ConfigServer.NotRepairableToolTypes = [..api.World.Config.GetString(IdentityKey.DisallowedTools, string.Join(",", GrindstonesConfigServer.DefaultDisallowedTools)).Split(",")];
			ConfigServer.AllowedRepairableMaterials = [..api.World.Config.GetString(IdentityKey.AllowedMaterials, string.Join(",",GrindstonesConfigServer.DefaultAllowedMaterials)).Split(",")];
		}

		private void OnConfigUpdated (UpdateConfig config)
		{
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = config.Ratio;
			ConfigServer.SafeSharpening = config.Safe;
			ConfigServer.Whitelist = [..config.Whitelist];
			ConfigServer.Blacklist = [..config.Blacklist];
			ConfigServer.NotRepairableToolTypes = [..config.DisallowedTools];
			ConfigServer.AllowedRepairableMaterials = [..config.AllowedMaterials];
		}
		#endregion

		public override void Dispose ()
		{
			Logger.Event("Dispose Called.");
			base.Dispose();
			Harmony?.UnpatchAll(ModID);
		}
	}

	[ProtoContract]
	public class UpdateConfig
	{
		[ProtoMember(1)] public string Ratio = ModGrindstones.ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
		[ProtoMember(2)] public bool Safe = ModGrindstones.ConfigServer.SafeSharpening;
		[ProtoMember(3)] public string[] Whitelist = ModGrindstones.ConfigServer.Whitelist.ToArray();
		[ProtoMember(4)] public string[] Blacklist = ModGrindstones.ConfigServer.Blacklist.ToArray();
		[ProtoMember(5)] public string[] DisallowedTools = ModGrindstones.ConfigServer.NotRepairableToolTypes.ToArray();
		[ProtoMember(6)] public string[] AllowedMaterials = ModGrindstones.ConfigServer.AllowedRepairableMaterials.ToArray();
	}

	public class IdentityKey(string key) : IComparable<IdentityKey>
	{
		#region World Config Keys
		
		public static readonly IdentityKey Ratio = new("Ratio");
		public static readonly IdentityKey Safe = new("Safe");
		public static readonly IdentityKey Whitelist = new("Whitelist");
		public static readonly IdentityKey Blacklist = new("Blacklist");
		public static readonly IdentityKey DisallowedTools = new("DisallowedTools");
		public static readonly IdentityKey AllowedMaterials = new("AllowedMaterials");
		
		#endregion
		#region Network Keys

		public static readonly IdentityKey NetworkChannel = new("NetworkChannel");

		#endregion
		
		private static string Domain = "grindstones";
		private readonly string key = key;

		public static implicit operator string (IdentityKey identityKey) => identityKey.ToString();
		
		internal static void SetDomain (string domain) => Domain = domain;
		
		public override string ToString() => $"{Domain}.{this.key}";

		public int CompareTo(IdentityKey identityKey)
		{
			return string.Compare($"{Domain}.{key}", $"{Domain}.{identityKey.key}", StringComparison.Ordinal);
		}
	}
}
