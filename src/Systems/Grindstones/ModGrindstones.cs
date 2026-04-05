using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Server;


#nullable disable

namespace Grindstones
{
	public class ModGrindstones : ModSystem
	{
		public static ModGrindstones Instance { get; private set; }
		public static string ModID;
		public static ILogger Logger;
		public static GrindstonesConfigServer ConfigServer;

		private static Harmony harmony;

		public override double ExecuteOrder () => 0.3;

		public ModGrindstones() :base()
		{
			if (Instance is null) Instance = this;
			if (ConfigServer is null) ConfigServer = new GrindstonesConfigServer();
		}

		public override void StartPre (ICoreAPI api)
		{
			base.StartPre(api);
			if (ModID is null) ModID = Mod.Info.ModID;
			if (Logger is null) Logger = Mod.Logger;
		}

		public override void Start (ICoreAPI api)
		{
			Logger.Event("Start Called from {0}", api.Side);

			base.Start(api);

			harmony = new Harmony(ModID);
			harmony.PatchAll();

			api.RegisterItemClass(ModID + ".grindingwheel", typeof(ItemGrindingwheel));

			api.RegisterBlockClass(ModID + ".grindstone", typeof(BlockGrindstone));
			api.RegisterBlockEntityClass(ModID + ".begrindstone", typeof(BlockEntityGrindstone));

			
			api.Network.RegisterChannel(ModID + ".NetworkChannel")
				.RegisterMessageType<UpdateConfig>();
		}

		#region Server
		IServerNetworkChannel serverChannel;
		ICoreServerAPI sapi;
		public override void StartServerSide (ICoreServerAPI api)
		{
			Logger.Event("StartServerSide Called.");
			base.StartServerSide(api);

			TryLoadServerConfig(api);
	
			sapi = api;
			serverChannel = api.Network.GetChannel(ModID + ".NetworkChannel");

			CreateServerCommands(api);
		}

		private readonly string configFile = "GrindstonesConfig.json";

		private void TryLoadServerConfig (ICoreAPI api)
		{
			Logger.Notification("Loading Config.");

			GrindstonesConfigServer serverConfig;
			try
			{
				serverConfig = api.LoadModConfig<GrindstonesConfigServer>(configFile);

				if (serverConfig is null)
				{
					serverConfig = new GrindstonesConfigServer();
				}

				if (serverConfig.ConfigVersion == 1)
				{
					Logger.Warning("Version 1 of confing found, updating config.");
					#pragma warning disable  // Ignore obsolete warning
					int gain = serverConfig.DurabilityPointsRepairedPerPointLost;
					#pragma warning restore
					serverConfig.RatioMaxDurabilityLossToDurabilityGain = "1:" + gain;
					serverConfig.ConfigVersion = 2;
				}

				api.StoreModConfig<GrindstonesConfigServer>(serverConfig, configFile);
			}
			catch (Exception e)
			{
				Logger.Error("Could not load server config! Loading default settings instead.");
				Logger.Error(e);

				serverConfig = new GrindstonesConfigServer();
			}

			ConfigServer = serverConfig;

			api.World.Config.SetString(ModID + ".Ratio", serverConfig.RatioMaxDurabilityLossToDurabilityGain);
			api.World.Config.SetBool(ModID + ".Safe", serverConfig.SafeSharpening);
			api.World.Config.SetString(ModID + ".ToolBlackList", string.Join(",", serverConfig.NotRepairableToolTypes));
			api.World.Config.SetString(ModID + ".MaterialWhitelist", string.Join(",", serverConfig.AllowedRepairableMaterials));
		}

		// TODO Add the ability to change settings on the fly
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
					.WithDescription("Change the state of the Safe Shapening setting.")
					.WithArgs(new BoolArgParser("safety", "safety", true))
					.HandleWith(OnUpdateSafety)
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
									  +"\nCurrent ratio in world : " + api.World.Config.GetAsString(ModID + ".Ratio");
						sapi.SendMessage(
							args.Caller.Player,
							GlobalConstants.InfoLogChatGroup,
							message,
							EnumChatType.Notification
						);
						return TextCommandResult.Success();
					})
					.EndSubCommand()
				.Validate();
		}

		private TextCommandResult OnUpdateRatio(TextCommandCallingArgs args)
		{
			string previousratio = ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
			string ratio = args.LastArg as string;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair ratio from " + previousratio + " to " + ratio + ".";

			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = ratio;
			sapi.World.Config.SetString(ModID + ".Ratio", ratio);
			sapi.StoreModConfig<GrindstonesConfigServer>(ConfigServer, configFile);
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
			bool previoussafety = ConfigServer.SafeSharpening;
			bool safety = (bool) args.LastArg;
			string message = args.Caller.Player.PlayerName + " updated Grindstones repair saftey from " + previoussafety + " to " + safety + ".";

			ConfigServer.SafeSharpening = safety;
			sapi.World.Config.SetBool(ModID + ".Safe", safety);
			sapi.StoreModConfig<GrindstonesConfigServer>(ConfigServer, configFile);
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

			clientChannel = api.Network.GetChannel(ModID + ".NetworkChannel")
				.SetMessageHandler<UpdateConfig>(OnConfigUpdated);

			// TODO Rework this command to be better for client
			api.ChatCommands.Create("GSettings")
				.WithDescription("Gets the settings values for the Grindstones mod.")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("cratio")
					.WithDescription("View the currently set ratio of MaxLoss to Gain.")
					.HandleWith((args) =>
					{
						string message = "Current ratio in config: " + ConfigServer.RatioMaxDurabilityLossToDurabilityGain
									  +"\nCurrent ratio in world : " + api.World.Config.GetAsString(ModID + ".Ratio");
						capi.SendChatMessage(message);
						return TextCommandResult.Success();
					})
					.EndSubCommand()
				.Validate();
		}
		private void GetServerSettings(ICoreAPI api)
		{
			Logger.Event("Recieving config settings from server.");
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = api.World.Config.GetString(ModID + ".Ratio", GrindstonesConfigServer.DefaultRepairRatio);
			ConfigServer.SafeSharpening = api.World.Config.GetBool(ModID + ".Safe", GrindstonesConfigServer.DefaultSafeSharpening);
			ConfigServer.NotRepairableToolTypes = [..api.World.Config.GetString(ModID + ".ToolBlackList", string.Join(",", GrindstonesConfigServer.DefaultDisallowedTools)).Split(",")];
			ConfigServer.AllowedRepairableMaterials = [..api.World.Config.GetString(ModID + ".MaterialWhitelist", string.Join(",",GrindstonesConfigServer.DefaultAllowedMaterials)).Split(",")];
		}

		private void OnConfigUpdated (UpdateConfig config)
		{
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = config.Ratio;
			ConfigServer.SafeSharpening = config.Safe;
			ConfigServer.NotRepairableToolTypes = [..config.DisallowedTools];
			ConfigServer.AllowedRepairableMaterials = [..config.AllowedMaterials];
		}
		#endregion

		public override void Dispose ()
		{
			Logger.Event("Dispose Called.");
			base.Dispose();
			harmony?.UnpatchAll(ModID);
		}
	}

	[ProtoContract]
	public class UpdateConfig
	{
		[ProtoMember(1)]
		public string Ratio = ModGrindstones.ConfigServer.RatioMaxDurabilityLossToDurabilityGain;

		[ProtoMember(2)]
		public bool Safe = ModGrindstones.ConfigServer.SafeSharpening;

		[ProtoMember(3)]
		public string[] DisallowedTools = ModGrindstones.ConfigServer.NotRepairableToolTypes.ToArray();
		
		[ProtoMember(4)]
		public string[] AllowedMaterials = ModGrindstones.ConfigServer.AllowedRepairableMaterials.ToArray();
	}
}
