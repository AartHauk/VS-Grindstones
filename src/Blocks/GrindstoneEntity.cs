using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace Grindstones
{
	// TODO Figure out how to get the animation from the item in the slot

	internal class GrindstoneEntity : BlockEntityDisplay
	{
		static AssetLocation wheelAsset = new AssetLocation("grindstones:shapes/item/grindingwheel.json");
		public BlockFacing Facing { get; protected set; } = BlockFacing.NORTH;

		public override string InventoryClassName => GrindstonesModSystem.Instance.Mod.Info.ModID + ".GrindstoneInventory";
		public override InventoryBase Inventory => inventory;

		internal Matrixf mat = new Matrixf();

		float rotateY = 0f;
		string Wood = "oak";
		GrindstoneInventory inventory;
		Shape wheelShape;

		private BlockEntityAnimationUtil animUtil {
			get {
				// inventory[0].Itemstack.Get

				return (GetBehavior<BEBehaviorAnimatable>())?.animUtil;
			}
		}

		public GrindstoneEntity ()
		{
			inventory = new GrindstoneInventory(this, InventoryClassName + "-0", null);
			inventory.SlotModified += Inv_SlotModified;
		}

		private void Inv_SlotModified(int t1)
		{
			updateMeshes();
		}

		public override void Initialize (ICoreAPI api)
		{
			Facing = BlockFacing.FromCode(Block.Variant["side"]);
			if (Facing == null) Facing = BlockFacing.NORTH;

			Wood = Block.Variant["wood"];
			if (Wood == null) Wood = "oak";

			switch (Facing.Index)
			{
				case 0: // North
					rotateY = 0;
					break;
				case 1: // East
					rotateY = 270;
					break;
				case 2: // South
					rotateY = 180;
					break;
				case 3: // West
					rotateY = 90;
					break;
				default:
					break;
			}

			mat.Translate(0.5f, 0.5f, 0.5f);
			mat.RotateYDeg(rotateY);
			mat.Translate(-0.5f, -0.5f, -0.5f);

			base.Initialize(api);

			inventory.LateInitialize(InventoryClassName + "-" + Pos, api);

			if (Api.World.Side != EnumAppSide.Client) return;
			ICoreClientAPI capi = (ICoreClientAPI) Api;
			if (capi is null) return;

			wheelShape = Shape.TryGet(capi, wheelAsset);
			if (wheelShape == null)
			{
				GrindstonesModSystem.Instance.Mod.Logger.Error("Shape for block {0} not found or errored, was supposed to be at {1}. Block animations not loaded!", "Grinding Wheel", wheelAsset);
			}
			InitAnimator();
		}

		public /*override*/ bool OnTesselationInternal (ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			if (animUtil?.animator == null)
			{

			}

			base.OnTesselation(mesher, tessThreadTesselator);

			ICoreClientAPI capi = (ICoreClientAPI) Api;
			if (capi is null) return false;

			//MeshData meshWheel = ObjectCacheUtil.GetOrCreate(capi, "grindstonewheelmesh-" + rotateY, () =>
			//{
			//	Shape shapeWheel = Shape.TryGet(capi, "grindstones:shapes/item/grindingwheel.json");
			//	capi.Tesselator.TesselateShape(Block, shapeWheel, out MeshData mesh, new Vec3f(0, rotateY, 0));

			//	return mesh;
			//});

			MeshData meshBase = ObjectCacheUtil.GetOrCreate(capi, "grindstonebasemesh-" + Wood + "-" + rotateY, () => {
				Shape shapeBase = Shape.TryGet(capi, "grindstones:shapes/block/treadlegrindstone.json");
				capi.Tesselator.TesselateShape(Block, shapeBase, out MeshData mesh, new Vec3f(0, rotateY, 0));

				return mesh;
			});

			//mesher.AddMeshData(meshWheel);
			mesher.AddMeshData(meshBase);

			for (int i = 0; i < Behaviors.Count; i++)
			{
				Behaviors[i].OnTesselation(mesher, tessThreadTesselator);
			}

			return true;
		}

		// Remember TRS (Transfrom -> Rotate -> Scale)
		protected override float[][] genTransformationMatrices () {
			float[][] tfMatrices = new float[1][];

			Matrixf mat = new Matrixf();
			mat.Translate(0, (13 - 3.5) / 16, 0);

			mat.Translate(0.5f, 0.25, 0.5f);
			mat.RotateYDeg(rotateY);
			mat.Translate(-0.5, -0.25, -0.5);

			tfMatrices[0] = mat.Values;
			return tfMatrices;
		}

		public override void ToTreeAttributes (ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);

			tree.SetBool("on", On);
		}

		public override void FromTreeAttributes (ITreeAttribute tree, IWorldAccessor worldForResolving)
		{
			base.FromTreeAttributes(tree, worldForResolving);

			On = tree.GetBool("on");

			if (On) Activate();
			else Deactivate();

			RedrawAfterReceivingTreeAttributes(worldForResolving);
		}

		private void InitAnimator ()
		{
			if (Api.World.Side == EnumAppSide.Client)
			{
				animUtil.InitializeAnimator(GrindstonesModSystem.Instance.Mod.Info.ModID + ".grindstoneAnimator");
			}
		}

		bool On = false;
		public bool OnInteractStart(IPlayer byPlayer, BlockSelection blockSel)
		{
			ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

			// Make sure the player is holding modifier key before trying any inventory interactions
			if (!byPlayer.Entity.Controls.ShiftKey) return true;

			// Is the player's hands empty
			if (activeSlot.Empty)
			{
				return removeGrindingWheel(byPlayer);
			}

			ItemStack heldItemStack = activeSlot.Itemstack;
			Item heldItem = heldItemStack.Item;

			// Is the player holding a grindstone
			if (isGrindingWheel(heldItem))
			{
				return addGrindingWheel(byPlayer, heldItemStack);
			}

			return true;
		}

		public bool OnInteractStep (IPlayer byPlayer, BlockSelection blockSel) {

			// Make sure there is a grinding wheel before trying anything
			if (inventory[0].Empty)
			{
				if (On) Deactivate();
				return false;
			}

			ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

			// Check if player is holding anything
			if (activeSlot.Empty)
			{
				if (On) Deactivate();
				return true;
			}

			ItemStack heldItemStack = activeSlot.Itemstack;
			Item heldItem = heldItemStack.Item;

			// Check if item can be repaired
			if (!isRepariable(heldItem))
			{
				if (On) Deactivate();
				return true;
			}

			float maxDurabiltyDrain = 1.0f / GrindstonesModSystem.Config.DurabilityPointsRepairedPerPointLost;

			int starterMax = heldItemStack.Collectible.GetMaxDurability(heldItemStack);

			int durability = heldItemStack.Collectible.GetRemainingDurability(heldItemStack);
			float maxDurability = heldItemStack.Attributes.GetFloat("maxDurability", starterMax);

			// Do not go above max durability
			if (durability >= (int) (maxDurability - maxDurabiltyDrain))
			{
				if (On) Deactivate();
				return true;
			}

			if (!On) Activate();

			durability += 1;
			maxDurability -= maxDurabiltyDrain;

			heldItemStack.Collectible.SetDurability(heldItemStack, durability);
			heldItemStack.Attributes.SetFloat("maxDurability", maxDurability);

			return true;
		}

		public bool OnInteractionStop (IPlayer byPlayer, BlockSelection blockSel)
		{
			if (On) Deactivate();
			return true;
		}

		private bool addGrindingWheel (IPlayer byPlayer, ItemStack itemStack)
		{
			// Ensure there is space for the wheel
			if (!inventory[0].Empty) return true;


			byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(Api.World, inventory[0], 1);
			MarkDirty(true);
			return false;
		}

		private bool removeGrindingWheel (IPlayer byPlayer)
		{
			// Is there a wheel
			if (inventory[0].Empty) return true;

			byPlayer.InventoryManager.TryGiveItemstack(inventory[0].TakeOutWhole());
			MarkDirty(true);
			return false;
		}

		public static bool isGrindingWheel (Item item)
		{
			return item.Code.FirstPathPart().StartsWith("grindingwheel");
		}

		public static bool isRepariable(Item item)
		{
			// Ensure this is an item
			if (item is null) return false;

			// Ensure item is a tool
			if (item.Tool is null) return false;

			// Check if allowed tool type
			if (GrindstonesModSystem.Config.NotRepairableToolTypes.Contains(item.Tool?.ToString() ?? "")) return false;

			// Check if allowed metal type
			if (!GrindstonesModSystem.Config.AllowedRepairableMaterials.Contains(item.Variant["material"] ?? item.Variant["metal"])) return false;

			return true;
		}

		private void Activate ()
		{
			GrindstonesModSystem.Instance.Mod.Logger.Event("Starting grindstone animation");
			if (inventory[0].Empty || Api is null) return;

			On = true;
			AnimationMetaData animMeta = new AnimationMetaData() { Animation = "grindstones.grindingwheelworking", Code = "grindstones.grindingwheelworking", EaseInSpeed = 1, EaseOutSpeed = 2, AnimationSpeed = 1f };
			bool? ret = animUtil?.StartAnimation(animMeta);

			if (ret != true)
			{
				GrindstonesModSystem.Instance.Mod.Logger.Error("Animation failed to start with animUtil {0} and contains key? \"{1}\" = {2}", animUtil, animMeta.Code, animUtil?.activeAnimationsByAnimCode.ContainsKey(animMeta.Code) ?? false);
			}

			MarkDirty(true);
		}

		private void Deactivate ()
		{
			GrindstonesModSystem.Instance.Mod.Logger.Event("Stopping grindstone animation");
			animUtil?.StopAnimation("grindstones.grindingwheelworking");

			On = false;

			MarkDirty(true);
		}
	}
}
