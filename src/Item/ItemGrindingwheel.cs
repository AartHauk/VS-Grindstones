using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Grindstones
{
	public class ItemGrindingwheel : Item
	{
		public override string GetHeldItemName (ItemStack itemStack)
		{
			if (Code == null)
		{
			return "Invalid block, id " + Id;
		}

		string text = ItemClass.Name();
		StringBuilder stringBuilder = new StringBuilder();

		string stone = Lang.Get("game:rock-" + Variant["stone"]);
		string metal = Lang.Get("game:item-rod-" + Variant["metal"]);

		stringBuilder.Append(Lang.Get("{0} Grinding Wheel with {1}", stone, metal));
		CollectibleBehavior[] collectibleBehaviors = CollectibleBehaviors;
		for (int i = 0; i < collectibleBehaviors.Length; i++)
		{
			collectibleBehaviors[i].GetHeldItemName(stringBuilder, itemStack);
		}

		return stringBuilder.ToString();
		}
	}
}
