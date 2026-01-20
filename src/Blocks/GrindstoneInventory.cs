using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Grindstones
{
	internal class GrindstoneInventory : InventoryDisplayed
	{
		ItemSlot slot;
		public override ItemSlot this[int slotId] 
		{
			get 
			{
				if (slotId != 0) throw new ArgumentOutOfRangeException(nameof(slotId));
				return slot;
			}
			set
			{
				if (slotId != 0) throw new ArgumentOutOfRangeException(nameof(slotId));
				if (value == null) throw new ArgumentNullException(nameof(value));
				slot = value;
			} 
		}

		public override int Count
		{
			get { return 1; }
		}

		public GrindstoneInventory (BlockEntity be, string invId, ICoreAPI api) : base(be, 1, invId, api) 
		{
			slot = NewSlot(0);
		}

		public override void FromTreeAttributes (ITreeAttribute tree)
		{
			slot = SlotsFromTreeAttributes(tree)[0];
		}

		public override void ToTreeAttributes (ITreeAttribute tree)
		{
			SlotsToTreeAttributes(new ItemSlot[1] { slot }, tree);
		}
	}
}
