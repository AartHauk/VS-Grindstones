using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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

			api.RegisterBlockClass(ModID + ".grindstone", typeof(BlockGrindstone));
			api.RegisterBlockEntityClass(ModID + ".begrindstone", typeof(BlockEntityGrindstone));
		}

		public override void StartClientSide (ICoreClientAPI api)
		{
			Logger.Event("StartClientSide Called.");
			base.StartClientSide(api);

			GetServerSettings(api);
		}
		public override void StartServerSide (ICoreServerAPI api)
		{
			Logger.Event("StartServerSide Called.");
			base.StartServerSide(api);

			TryLoadServerConfig(api);
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
		}

		private void GetServerSettings(ICoreAPI api)
		{
			Logger.Event("Recieving config settings from server.");
			ConfigServer.RatioMaxDurabilityLossToDurabilityGain = api.World.Config.GetString(ModID + ".Ratio", ConfigServer.RatioMaxDurabilityLossToDurabilityGain);
			ConfigServer.NotRepairableToolTypes = [..api.World.Config.GetString(ModID + ".ToolBlackList", string.Join(",", ConfigServer.NotRepairableToolTypes)).Split(",")];
			ConfigServer.AllowedRepairableMaterials = [..api.World.Config.GetString(ModID + ".MaterialWhitelist", string.Join(",", ConfigServer.AllowedRepairableMaterials)).Split(",")];
		}

	}
}
