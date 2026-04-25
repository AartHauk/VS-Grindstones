using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using static System.Int32;

namespace Grindstones
{
	public class GrindstonesConfigServer
	{
		[JsonIgnore]
		public const string Unspecified = "unspecified";
		
		#region Defaults
		[JsonIgnore]
		public const string DefaultRepairRatio = "1:4";
		[JsonIgnore]
		public const bool DefaultSafeSharpening = false;
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultWhitelist = [];
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultBlacklist = [];
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultDisallowedTools = [
			"bow",
			"sling",
			"firearm",
			"crossbow",
			"shield"
		];
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultAllowedMaterials = [
			Unspecified,
			"copper",
			"tinbronze",
			"bismuthbronze",
			"blackbronze",
			"gold",
			"silver",
			"iron",
			"meteoriciron",
			"steel",
			"ornategold",
			"ornatesilver"
		];
		#endregion

		public int ConfigVersion = 3;
		[Obsolete("Version 1 config setting, use MaxDuabilityLoss and DurabilityGain instead.")]
		public int DurabilityPointsRepairedPerPointLost = 4;
		public string RatioMaxDurabilityLossToDurabilityGain = DefaultRepairRatio;
		public bool SafeSharpening = DefaultSafeSharpening;
		public HashSet<string> Whitelist = DefaultWhitelist.ToHashSet();
		public HashSet<string> Blacklist = DefaultBlacklist.ToHashSet();
		public HashSet<string> NotRepairableToolTypes = DefaultDisallowedTools.ToHashSet();
		public HashSet<string> AllowedRepairableMaterials = DefaultAllowedMaterials.ToHashSet();

		[JsonIgnore]
		public int MaxDurabilityLoss => TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[0], out int loss) ? loss : 1;

		[JsonIgnore]
		public int DurabilityGain => TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[1], out int gain) ? gain : 4;

		public bool IsWhitelisted(string tool)
		{
			return Whitelist.Contains(tool?.ToLower() ?? Unspecified);
		}

		public bool IsBlacklisted(string tool)
		{
			return Blacklist.Contains(tool?.ToLower() ?? Unspecified);
		}
		
		public bool IsRepairableTool (string tool)
		{
			return !NotRepairableToolTypes.Contains(tool?.ToLower() ?? Unspecified);
		}

		public bool IsRepairableMaterial (string material)
		{
			return AllowedRepairableMaterials.Contains(material?.ToLower() ?? Unspecified);
		}

		public bool ShouldSerializeDurabilityPointsRepairedPerPointLost () { return false; }

		[OnDeserialized]
		internal void OnDeserialized (StreamingContext context)
		{
			NotRepairableToolTypes = [..NotRepairableToolTypes.Select((str) => str.ToLower())];
			AllowedRepairableMaterials = [..AllowedRepairableMaterials.Select((str) => str.ToLower())];
		}
	}
}
