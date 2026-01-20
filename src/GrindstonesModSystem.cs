using HarmonyLib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;

#nullable disable

namespace Grindstones
{
    public class GrindstonesModSystem : ModSystem
    {
		public static GrindstonesModSystem Instance { get; private set; }
		public static string ModID { get; private set; }
		public static ILogger Logger {  get; private set; }
		public static ServerConfig Config { get; private set; }

		// Called on server and client
		// Useful for registering block/entity classes on both sides

		private static Harmony harmony;

        // Force load after xskills for patching reasons
		public override double ExecuteOrder () => 0.3;

		public GrindstonesModSystem () : base() {
			if (Instance == null) Instance = this;
			if (Config == null) Config = new ServerConfig();
		}

		public override void StartPre (ICoreAPI api)
		{
			if (ModID == null) ModID = this.Mod.Info.ModID;
			if (Logger == null) Logger = this.Mod.Logger;
		}

		public override void Start(ICoreAPI api)
        {
			Logger.Notification("Start: " + api.Side);
			harmony = new Harmony(Mod.Info.ModID);
			harmony.PatchAll();

			api.RegisterBlockClass(Mod.Info.ModID + ".grindstone", typeof(BlockGrindstone));
			api.RegisterBlockEntityClass(Mod.Info.ModID + ".begrindstone", typeof(BlockEntityGrindstone));
        }

		public override void StartClientSide (ICoreClientAPI api)
		{
			base.StartClientSide(api);

			Logger.Notification("Loading config from server");
			Config.DurabilityPointsRepairedPerPointLost = api.World.Config.GetInt(Mod.Info.ModID + ".MaxDrain", Config.DurabilityPointsRepairedPerPointLost);
			Config.NotRepairableToolTypes = new List<string>(api.World.Config.GetString(Mod.Info.ModID + ".ToolBlackList").Split(","));
			Config.AllowedRepairableMaterials = new List<string>(api.World.Config.GetString(Mod.Info.ModID + ".MaterialWhitelist").Split(","));
		}

		public override void StartServerSide (ICoreServerAPI api)
		{
			base.StartServerSide(api);

			TryLoadServerConfig(api);
		}

		public override void Dispose ()
		{
            base.Dispose();
            harmony?.UnpatchAll(Mod.Info.ModID);
		}

		private static string configFile = "GrindstonesConfig.json";
		private void TryLoadServerConfig (ICoreAPI api) {
			Logger.Notification("Loading Config");
			ServerConfig serverConfig;
			try
			{
				serverConfig = api.LoadModConfig<ServerConfig>(configFile);

				if (serverConfig == null)
				{
					serverConfig = new ServerConfig();
				}

				api.StoreModConfig<ServerConfig>(serverConfig, configFile);
			}
			catch (Exception e) {
				Mod.Logger.Error("Could not load server config! Loading default settings instead.");
				Mod.Logger.Error(e);

				serverConfig = new ServerConfig();
			}

			Config = serverConfig;

			api.World.Config.SetInt(Mod.Info.ModID + ".MaxDrain", serverConfig.DurabilityPointsRepairedPerPointLost);
			api.World.Config.SetString(Mod.Info.ModID + ".ToolBlackList", string.Join(",", serverConfig.NotRepairableToolTypes));
			api.World.Config.SetString(Mod.Info.ModID + ".MaterialWhitelist", string.Join(",", serverConfig.AllowedRepairableMaterials));
		}
    }
}
