using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;


#nullable disable

namespace Grindstones
{
	public class BlockGrindstone : BlockContainer
	{
		private BlockSelection sel;

		// Returning false from this specific method does not sync with the server
		public override bool OnBlockInteractStart (IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			if (blockSel.Position == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);
			sel = blockSel;

			BlockEntityGrindstone beec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGrindstone;

			if (beec != null)
			{
				beec.OnInteractStart(world, byPlayer, blockSel);
			}

			return true;
		}

		public override bool OnBlockInteractStep (float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			if (blockSel.Position == null) return false;
			sel = blockSel;

			BlockEntityGrindstone beec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGrindstone;

			if (beec != null)
			{
				return beec.OnInteractStep(world, byPlayer, blockSel);
			}

			return true;
		}

		public override bool OnBlockInteractCancel (float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			BlockEntityGrindstone beec = world.BlockAccessor.GetBlockEntity<BlockEntityGrindstone>(sel.Position);

			if (beec != null)
			{
				return beec.OnInteractStop(byPlayer, sel);
			}

			return true;
		}

		public override ItemStack OnPickBlock (IWorldAccessor world, BlockPos pos)
		{
			string wood = this.Variant["wood"];
			ItemStack stack = new ItemStack(api.World.GetBlock(new AssetLocation("grindstones", "grindstone-" + wood + "-east")));
			return stack;
		}

		public override ItemStack[] GetDrops (IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			string wood = this.Variant["wood"];
			ItemStack stack = new ItemStack(api.World.GetBlock(new AssetLocation("grindstones", "grindstone-" + wood + "-east")));
			return [ stack ];
		}
	}
}
