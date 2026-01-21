using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Vintagestory.API.Util;

namespace Grindstones
{
	public class GrindstonesConfigServer
	{
		public int ConfigVersion = 2;

		public string RatioMaxDurabilityLossToDurabilityGain = "1:4";

		public bool SafeSharpening = false;

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
		public float DurabilityGain
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
			return !NotRepairableToolTypes.Contains(tool.ToLower());
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
			return AllowedRepairableMaterials.Contains(material.ToLower());
		}

		[Obsolete("Version 1 config setting, use MaxDuabilityLoss and DurabilityGain instead.")]
		public int DurabilityPointsRepairedPerPointLost = 4;

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
