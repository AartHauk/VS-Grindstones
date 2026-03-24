using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

		//public static INetworkChannel modChannel;

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
				.RegisterMessageType<ConfigUpdated>();
		}

		IClientNetworkChannel clientChannel;
		ICoreClientAPI capi;
		public override void StartClientSide (ICoreClientAPI api)
		{
			Logger.Event("StartClientSide Called.");
			base.StartClientSide(api);

			GetServerSettings(api);

			capi = api;

			clientChannel = api.Network.GetChannel(ModID + ".NetworkChannel")
				.SetMessageHandler<ConfigUpdated>(OnConfigUpdated);
		}

		private void OnConfigUpdated (ConfigUpdated config)
		{
			GetServerSettings(capi);
		}

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

		public override void Dispose ()
		{
			Logger.Event("Dispose Called.");
			base.Dispose();
			harmony?.UnpatchAll(ModID);
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
			api.World.Config.SetString(ModID + ".ToolBlackList", string.Join(",", serverConfig.NotRepairableToolTypes));
			api.World.Config.SetString(ModID + ".MaterialWhitelist", string.Join(",", serverConfig.AllowedRepairableMaterials));
			api.World.Config.SetBool(ModID + ".Safe", serverConfig.SafeSharpening);
		}

		private void GetServerSettings(ICoreAPI api)
		{
			Logger.Event("Recieving config settings from server.");
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = api.World.Config.GetString(ModID + ".Ratio", ConfigServer.RatioMaxDurabilityLossToDurabilityGain);
			ConfigServer.NotRepairableToolTypes = [..api.World.Config.GetString(ModID + ".ToolBlackList", string.Join(",", ConfigServer.NotRepairableToolTypes)).Split(",")];
			ConfigServer.AllowedRepairableMaterials = [..api.World.Config.GetString(ModID + ".MaterialWhitelist", string.Join(",", ConfigServer.AllowedRepairableMaterials)).Split(",")];
			ConfigServer.SafeSharpening = api.World.Config.GetBool(ModID + ".Safe", ConfigServer.SafeSharpening);
		}

		// TODO Add the ability to change settings on the fly
		private void CreateServerCommands(ICoreAPI api)
		{
			//api.ChatCommands.Create("GTest")
			//	.WithDescription("Test Command Callback")
			//	.RequiresPrivilege(Privilege.controlserver)
			//	.HandleWith((args) =>
			//	{
			//		sapi.SendMessageToGroup(
			//			GlobalConstants.GeneralChatGroup,
			//			"Testing Network Calls...",
			//			EnumChatType.Notification
			//		);
			//		Logger.Debug("Sending test packet");
			//		serverChannel.BroadcastPacket(new ConfigUpdated());
			//		return TextCommandResult.Success();
			//	})
			//	.Validate();

			api.ChatCommands.Create("GConfig")
				.WithDescription("Change Grindstones mod config settings on the fly")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("Change the ratio of MaxLoss to Gain.")
					.WithArgs(new StringArgParser("ratio", true))
					.HandleWith(OnUpdateRatio)
					.EndSubCommand()
				.Validate();

			api.ChatCommands.Create("GSettings")
				.WithDescription("Gets the settings values for the Grindstones mod.")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("ratio")
					.WithDescription("View the currently set ratio of MaxLoss to Gain.")
					.HandleWith((args) =>
					{
						string message = "Current ratio: " + ConfigServer.RatioMaxDurabilityLossToDurabilityGain;
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
			string ratio = args.LastArg.ToString();
			string message = "Updating Grindstones repair ratio from " + previousratio + " to " + ratio + ".";

			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = ratio;
			sapi.World.Config.SetString(ModID + ".Ratio", ConfigServer.RatioMaxDurabilityLossToDurabilityGain);
			sapi.StoreModConfig<GrindstonesConfigServer>(ConfigServer, configFile);
			serverChannel.BroadcastPacket(new ConfigUpdated());

			sapi.SendMessageToGroup(
				GlobalConstants.InfoLogChatGroup,
				message,
				EnumChatType.Notification
			);
			Logger.Notification(message);

			return TextCommandResult.Success();
		}
	}

	[ProtoContract]
	public class ConfigUpdated {}
}
