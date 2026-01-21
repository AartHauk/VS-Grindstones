using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Grindstones
{
	public class GrindstonesConfigServer
	{
		public int ConfigVersion = 2;

		public string RatioMaxDurabilityLossToDurabilityGain = "1:4";

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

		public HashSet<string> NotRepairableToolTypes = new HashSet<string>(){
			"Bow",
			"Sling",
			"Firearm",
			"Crossbow",
			"Shield"
		};

		public bool IsRepairableTool (string tool)
		{
			return !NotRepairableToolTypes.Contains(tool);
		}

		public HashSet<string> AllowedRepairableMaterials = new HashSet<string>(){
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
		};

		public bool IsRepairableMaterial (string material)
		{
			return AllowedRepairableMaterials.Contains(material);
		}

		[Obsolete("Version 1 config setting, use MaxDuabilityLoss and DurabilityGain instead.")]
		public int DurabilityPointsRepairedPerPointLost = 4;

		public bool ShouldSerializeDurabilityPointsRepairedPerPointLost () { return false; }
	}
}
