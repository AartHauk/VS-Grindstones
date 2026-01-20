using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Grindstones
{
	public class InventoryGrindstone : InventoryGeneric
	{
		ItemSlot slot;
		BlockEntity container;

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
				slot = value;
			}
		}
		public override int Count => 1;

		public InventoryGrindstone (BlockEntity be, string invId, ICoreAPI api): base(1, invId, api)
		{
			container = be;
			slot = NewSlot(0);
		}

		public override void OnItemSlotModified (ItemSlot slot)
		{
			base.OnItemSlotModified(slot);
			container.MarkDirty(redrawOnClient: true);
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
