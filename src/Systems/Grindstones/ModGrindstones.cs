using HarmonyLib;
using ProtoBuf;
using System;
using System.Linq;
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

			CreateServerCommands(api);
		}

		private const string ConfigFile = "GrindstonesConfig.json";

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

		// TODO Add the ability to change settings on the fly
		// TODO Create helper class/method to generate commands
		private void CreateServerCommands(ICoreAPI api)
		{
			api.ChatCommands.Create("GConfig")
				.WithDescription("Change Grindstones mod config settings on the fly")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("Change the ratio of MaxLoss to Gain.")
					.WithArgs(new StringArgParser("ratio", true))
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.BeginSubCommand("safety")
					.WithDescription("Change the state of the Safe Sharpening setting.")
					.WithArgs(new BoolArgParser("safety", "safety", true))
					.HandleWith(OnUpdateSafety)
					.EndSubCommand()
				.BeginSubCommand("whitelist")
					.WithDescription("Add/Remove item from Grindstone whitelist.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove"]), new StringArgParser("item", false)])
					.HandleWith(OnUpdateWhitelist)
					.EndSubCommand()
				.BeginSubCommand("blacklist")
					.WithDescription("Add/Remove item from Grindstone blacklist.")
					.WithArgs([new WordArgParser("action", true, ["add", "remove"]), new StringArgParser("item", false)])
					.HandleWith(OnUpdateBlacklist)
					.EndSubCommand()
				.Validate();

			api.ChatCommands.Create("GSettings")
				.WithDescription("Gets the settings values for the Grindstones mod.")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("View the currently set ratio of MaxLoss to Gain.")
					.HandleWith((args) =>
					{
						string message = "Current ratio in config: " + ConfigServer.RatioMaxDurabilityLossToDurabilityGain
									  +"\nCurrent ratio in world : " + api.World.Config.GetAsString(IdentityKey.Ratio);
						sapi.SendMessage(
							args.Caller.Player,
							GlobalConstants.InfoLogChatGroup,
							message,
							EnumChatType.Notification
						);
						return TextCommandResult.Success();
					})
					.EndSubCommand()
				.BeginSubCommand("whitelist")
					.WithDescription("View the current specific item whitelist.")
					.HandleWith((_) => TextCommandResult.Success($"Current whitelist in config: {string.Join(",", ConfigServer.Whitelist)}" +
					                                                $"\nCurrent whitelist in world: {api.World.Config.GetAsString(IdentityKey.Whitelist)}"))
					.EndSubCommand()
				.BeginSubCommand("blacklist")
					.WithDescription("View the current specific item blacklist.")
					.HandleWith((_) => TextCommandResult.Success($"Current blacklist in config: {string.Join(",", ConfigServer.Blacklist)}" +
					                                                $"\nCurrent blacklist in world: {api.World.Config.GetAsString(IdentityKey.Blacklist)}"))
					.EndSubCommand()
				.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string oldRatio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = args.LastArg as string ?? oldRatio;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair ratio from " + oldRatio + " to " + ratio + ".";

			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = ratio;
			sapi.World.Config.SetString(IdentityKey.Ratio, ratio);
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Ratio = ratio,
			});

			sapi.SendMessageToGroup(
				GlobalConstants.InfoLogChatGroup,
				message,
				EnumChatType.Notification
			);
			Logger.Notification(message);

			return TextCommandResult.Success();
		}

		private TextCommandResult OnUpdateSafety(TextCommandCallingArgs args)
		{
			bool oldSafety = ConfigServer.SafeSharpening;
			bool safety = (bool) args.LastArg;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair safety from " + oldSafety + " to " + safety + ".";

			ConfigServer.SafeSharpening = safety;
			sapi.World.Config.SetBool(IdentityKey.Safe, safety);
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig()
			{
				Safe = safety,
			});

			sapi.SendMessageToGroup(
				GlobalConstants.InfoLogChatGroup,
				message,
				EnumChatType.Notification
			);
			Logger.Notification(message);

			return TextCommandResult.Success();
		}
		
		// TODO Combine similar functions into one
		private TextCommandResult OnUpdateWhitelist(TextCommandCallingArgs args)
		{
			string action = args[0] as string;
			string tool = args[1] as string;

			if (tool is null)
			{
				ItemSlot itemSlot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;
				if (itemSlot.Empty)
				{
					return TextCommandResult.Error("No tool specified or held!");
				}

				if (itemSlot.Itemstack.Item.Tool is null)
				{
					return TextCommandResult.Error($"{itemSlot.Itemstack.Item.Code} is not a tool!");
				}
				
				tool = itemSlot.Itemstack.Item.Code;
			}
			else if (sapi.World.GetItem(tool).Tool is null)
			{
				return TextCommandResult.Error($"'{tool}' is not a tool!");
			}

			string message;
			
			switch (action)
			{
				case "add":
					ConfigServer.Whitelist.Add(tool);
					message = $"{args.Caller.Player.PlayerName} added '{tool}' to whitelist.";
					break;
				case "remove":
					ConfigServer.Whitelist.Remove(tool);
					message = $"{args.Caller.Player.PlayerName} removed '{tool}' from whitelist.";
					break;
				default:
					return TextCommandResult.Error($"Unknown action '{action}'.");
			}
			
			string[] whitelist = ConfigServer.Whitelist.ToArray();
			sapi.World.Config.SetString(IdentityKey.Whitelist, string.Join(",", whitelist));
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig(){
				Whitelist = whitelist
			});
			
			Logger.Notification(message);
			
			return TextCommandResult.Success($"'{tool}' was successfully {(action == "add" ? "added to" : "removed from")} whitelist.");
		}
		
		private TextCommandResult OnUpdateBlacklist(TextCommandCallingArgs args)
		{
			string action = args[0] as string;
			string tool = args[1] as string;

			if (tool is null)
			{
				ItemSlot itemSlot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;
				if (itemSlot.Empty)
				{
					return TextCommandResult.Error("No tool specified or held!");
				}

				if (itemSlot.Itemstack.Item.Tool is null)
				{
					return TextCommandResult.Error($"{itemSlot.Itemstack.Item.Code} is not a tool!");
				}
				
				tool = itemSlot.Itemstack.Item.Code;
			}
			else if (sapi.World.GetItem(tool).Tool is null)
			{
				return TextCommandResult.Error($"'{tool}' is not a tool!");
			}

			string message;
			
			switch (action)
			{
				case "add":
					ConfigServer.Blacklist.Add(tool);
					message = $"{args.Caller.Player.PlayerName} added '{tool}' to blacklist.";
					break;
				case "remove":
					ConfigServer.Blacklist.Remove(tool);
					message = $"{args.Caller.Player.PlayerName} removed '{tool}' from blacklist.";
					break;
				default:
					return TextCommandResult.Error($"Unknown action '{action}'.");
			}
			
			string[] blacklist = ConfigServer.Blacklist.ToArray();
			sapi.World.Config.SetString(IdentityKey.Blacklist, string.Join(",", blacklist));
			sapi.StoreModConfig(ConfigServer, ConfigFile);
			serverChannel.BroadcastPacket(new UpdateConfig(){
				Blacklist = blacklist
			});
			
			Logger.Notification(message);
			
			return TextCommandResult.Success($"'{tool}' was successfully {(action == "add" ? "added to" : "removed from")} blacklist.");
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

		public static readonly IdentityKey Ratio = new IdentityKey("Ratio");
		public static readonly IdentityKey Safe = new IdentityKey("Safe");
		public static readonly IdentityKey Whitelist = new IdentityKey("Whitelist");
		public static readonly IdentityKey Blacklist = new IdentityKey("Blacklist");
		public static readonly IdentityKey DisallowedTools = new IdentityKey("DisallowedTools");
		public static readonly IdentityKey AllowedMaterials = new IdentityKey("AllowedMaterials");

		#endregion

		#region Network Keys

		public static readonly IdentityKey NetworkChannel = new IdentityKey("NetworkChannel");

		#endregion
		

		private static string Domain = "grindstones";
		private readonly string key = key;
		public static implicit operator string(IdentityKey identityKey) => $"{Domain}.{identityKey.key}";
		
		public static void SetDomain (string domain) =>  Domain = domain;
		
		public int CompareTo(IdentityKey identityKey)
		{
			return string.Compare($"{Domain}.{key}", $"{Domain}.{identityKey.key}", StringComparison.Ordinal);
		}
	}
}
