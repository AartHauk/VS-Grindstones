using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

namespace Grindstones
{
	public class GrindstonesConfigServer
	{
		#region defaults
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
		public int MaxDurabilityLoss
		{
			get
			{
				int loss = 1;
				Int32.TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[0], out loss);
				return loss;
			}
		}

		[JsonIgnore]
		public int DurabilityGain
		{
			get
			{
				int gain = 4;
				Int32.TryParse(RatioMaxDurabilityLossToDurabilityGain.Split(":")[1], out gain);
				return gain;
			}
		}

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
			NotRepairableToolTypes = [..NotRepairableToolTypes.Select((str) =>
			{
				return str.ToLower();
			})];

			AllowedRepairableMaterials = [..AllowedRepairableMaterials.Select((str) =>
			{
				return str.ToLower();
			})];
		}
	}
}
