using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;

namespace Grindstones
{
	[HarmonyPatch(typeof(CollectibleObject))]
	public class CollectibleObjectPatch
	{
		[HarmonyPostfix]
		[HarmonyPatch("GetMaxDurability")]
		public static void Postfix (ref int __result, ItemStack itemstack) {
			float maxDuarbility = (int) (itemstack?.Attributes.TryGetInt("maxDurability") ?? __result);
			__result = (int) maxDuarbility;
		}
	}
}
