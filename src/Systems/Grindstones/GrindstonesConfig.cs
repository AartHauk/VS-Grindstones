using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using static System.Int32;

namespace Grindstones
{
	public class GrindstonesConfigServer
	{
		#region Defaults
		[JsonIgnore]
		public const string DefaultRepairRatio = "1:4";
		[JsonIgnore]
		public const bool DefaultSafeSharpening = false;
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultDisallowedTools = [
			"bow",
			"sling",
			"firearm",
			"crossbow","shield"
		];
		[JsonIgnore]
		public static readonly ImmutableHashSet<string> DefaultAllowedMaterials = [
			"unspecified",
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

		public int ConfigVersion = 2;
		[Obsolete("Version 1 config setting, use MaxDuabilityLoss and DurabilityGain instead.")]
		public int DurabilityPointsRepairedPerPointLost = 4;
		public string RatioMaxDurabilityLossToDurabilityGain = DefaultRepairRatio;
		public bool SafeSharpening = DefaultSafeSharpening;
		public HashSet<string> NotRepairableToolTypes = DefaultDisallowedTools.ToHashSet();
		public HashSet<string> AllowedRepairableMaterials = DefaultAllowedMaterials.ToHashSet();

		[JsonIgnore]
		public int MaxDurabilityLoss => TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[0], out var loss) ? loss : 1;

		[JsonIgnore]
		public int DurabilityGain => TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[1], out var gain) ? gain : 4;

		public bool IsRepairableTool (string tool)
		{
			return !NotRepairableToolTypes.Contains(tool?.ToLower() ?? "unspecified");
		}

		public bool IsRepairableMaterial (string material)
		{
			return AllowedRepairableMaterials.Contains(material?.ToLower() ?? "unspecified");
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
