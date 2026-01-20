using System.Collections.Generic;

namespace Grindstones
{
	public class ServerConfig
	{
		public int ConfigVersion = 1;
		public int DurabilityPointsRepairedPerPointLost = 4;

		public List<string> NotRepairableToolTypes = new List<string>(){
			"Bow",
			"Sling",
			"Firearm",
			"Crossbow",
			"Shield"
		};

		public List<string> AllowedRepairableMaterials = new List<string>(){
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
	}
}
