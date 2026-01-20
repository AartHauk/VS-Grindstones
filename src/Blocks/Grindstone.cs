using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;
using Vintagestory.API.Datastructures;

namespace Grindstones
{
	internal class Grindstone : Block
	{
		public override bool OnBlockInteractStart (IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			GrindstoneEntity gse = (GrindstoneEntity) world.BlockAccessor.GetBlockEntity(blockSel.Position);
			// null sanity check
			if (gse is null) return false;

			return gse.OnInteractStart(byPlayer, blockSel);
		}

		public override bool OnBlockInteractStep (float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			GrindstoneEntity gse = (GrindstoneEntity) world.BlockAccessor.GetBlockEntity(blockSel.Position);
			// null sanity check
			if (gse is null) return false;

			return gse.OnInteractStep(byPlayer, blockSel);
		}

		public override bool OnBlockInteractCancel (float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			GrindstoneEntity gse = (GrindstoneEntity) world.BlockAccessor.GetBlockEntity(blockSel.Position);
			// null sanity check
			if (gse is null) return false;

			return gse.OnInteractionStop(byPlayer, blockSel);
		}
	}
}
