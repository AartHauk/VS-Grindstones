using HarmonyLib;
using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
