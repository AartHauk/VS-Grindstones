using System;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace Grindstones
{
	public class BlockEntityGrindstone : BlockEntityContainer
	{
		internal InventoryGrindstone inventory;
		public override InventoryBase Inventory => inventory;
		public override string InventoryClassName => ModGrindstones.ModID + ".begrindstone";

		MeshData wheelMesh;

		GrindstoneRenderer renderer;
		public bool IsSharpening = false;
		ILoadedSound sharpeningSound;

		BlockEntityAnimationUtil animUtil
		{
			get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
		}

		public BlockEntityGrindstone()
		{
			inventory = new InventoryGrindstone(this, InventoryClassName + "-0", null);
			inventory.SlotModified += Inv_SlotModified;
		}

		private void Inv_SlotModified(int t1)
		{
			if (Api.Side == EnumAppSide.Client)
			{
				updateMeshesAndRenderer(Api as ICoreClientAPI);
			}
		}

		public override void Initialize (ICoreAPI api)
		{
			base.Initialize(api);

			inventory.LateInitialize(InventoryClassName + "-" + Pos, api);

			if (api.Side == EnumAppSide.Client)
			{
				(api as ICoreClientAPI).Event.RegisterRenderer(renderer = new GrindstoneRenderer(Pos, api as  ICoreClientAPI, getRotation()), EnumRenderStage.Opaque, ModGrindstones.ModID + ".grindstonerenderer");
				updateMeshesAndRenderer(api as ICoreClientAPI);

				if (sharpeningSound is null)
				{
					sharpeningSound = (api.World as IClientWorldAccessor).LoadSound(new SoundParams()
					{
						Location = new AssetLocation("grindstones:sounds/sharpening.ogg"),
						ShouldLoop = true,
						Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
						DisposeOnFinish = false,
						Volume = 1f
					});
				}
			}

			StopWheel();
		}

		// This interaction cannont return false at first or else the interaction is not synced with the server
		public void OnInteractStart (IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

			// Make sure the player is holding modifier key before trying any inventory interactions
			if (!byPlayer.Entity.Controls.ShiftKey) return;

			// Is the player's hands empty
			if (slot.Empty)
			{
				if (HasWheel)
				{

					ItemStack stack = inventory[0].TakeOutWhole();
					inventory[0].MarkDirty();

					if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
					{
						world.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1, 0.5));
					}

					Api.Logger.Audit("{0} Took 1x{1} from Grindstone at {2}.",
						byPlayer.PlayerName,
						stack.Collectible.Code,
						Pos
					);

					wheelMesh = null;
					StopWheel();
					MarkDirty(true);
				}
				return;
			}
			
			if (slot?.Itemstack?.Collectible == null) return;

			// Is the player holding a grindstone
			if (slot.Itemstack.Collectible.Code.FirstCodePart().StartsWith("grindingwheel"))
			{
				if (!HasWheel)
				{
					var moved = slot.TryPutInto(world, inventory[0], 1);

					Api.Logger.Audit("{0} Put 1x{1} from Grindstone at {2}.",
						byPlayer.PlayerName,
						inventory[0].Itemstack.Collectible.Code,
						Pos
					);
				}
			}
		}

		public bool OnInteractStep (IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{

			// Make sure there is a grinding wheel before trying anything
			if (inventory[0].Empty)
			{
				if (IsSharpening)
				{
					StopWheel();
					MarkDirty(true);
				}
				return false;
			}

			ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

			// Check if player is holding anything
			if (activeSlot.Empty)
			{
				if (IsSharpening)
				{
					StopWheel();
					MarkDirty(true);
				}
				return true;
			}

			ItemStack heldItemStack = activeSlot.Itemstack;
			Item heldItem = heldItemStack.Item;

			// Check if item can be repaired
			if (!isRepariable(heldItem))
			{
				if (IsSharpening)
				{
					StopWheel();
					MarkDirty(true);
				}
				return true;
			}

			int maxDurabilityLoss = ModGrindstones.ConfigServer.MaxDurabilityLoss;
			int durabilityGain = ModGrindstones.ConfigServer.DurabilityGain;

			int starterMax = heldItemStack.Collectible.GetMaxDurability(heldItemStack);

			int currentDurability = heldItemStack.Collectible.GetRemainingDurability(heldItemStack);
			int currentMaxDurability = heldItemStack.Attributes.GetInt("maxDurability", starterMax);

			// Stop repairing, item is already at max durability
			if (currentDurability >= currentMaxDurability)
			{
				if (IsSharpening)
				{
					StopWheel();
					MarkDirty(true);
				}
				return true;
			}

			// Start animation becase we are doing work
			if (!IsSharpening)
			{
				StartWheel();
				MarkDirty(true);
			}

			int nextDurability = currentDurability + durabilityGain;
			int nextMaxDurability = currentMaxDurability - maxDurabilityLoss;

			// Do not go above max durability
			if (nextDurability > nextMaxDurability)
			{
				nextDurability = nextMaxDurability;
			}

			heldItemStack.Item.DamageItem(world, byPlayer.Entity, activeSlot, currentDurability - nextDurability);
			heldItemStack.Attributes.SetInt("maxDurability", nextMaxDurability);

			return true;
		}

		public bool OnInteractStop (IPlayer byPlayer, BlockSelection blockSel)
		{
			StopWheel();
			MarkDirty(true);
			return true;
		}

		#region Wheel start/stop

		long startLoadingMs;

		void StartWheel ()
		{
			IsSharpening = true;

			if (Api.Side != EnumAppSide.Client) return;

			startLoadingMs = Api.World.ElapsedMilliseconds;

			updateMeshesAndRenderer(Api as ICoreClientAPI);

			if (sharpeningSound?.IsFadingOut == true || sharpeningSound?.IsPlaying == false)
			{
				sharpeningSound.SetPitchOffset(randomStd(dev: 0.1f));
				sharpeningSound?.Start();
				sharpeningSound?.FadeIn(0.25f, null);
			}

			animUtil.StartAnimation(new AnimationMetaData()
			{
				Animation = "grindstones.grindingwheelworking",
				Code = "grindstones.grindingwheelworking",
				AnimationSpeed = 1f,
				EaseOutSpeed = 1f,
				EaseInSpeed = 1f
			});
		}

		void StopWheel ()
		{
			IsSharpening = false;

			if (Api.Side != EnumAppSide.Client) return;

			updateMeshesAndRenderer(Api as ICoreClientAPI);

			sharpeningSound?.FadeOutAndStop(0.25f);

			animUtil.StopAnimation("grindstones.grindingwheelworking");
		}

		#endregion

		#region mesh stuff

		private void updateMeshesAndRenderer(ICoreClientAPI capi)
		{
			if (HasWheel)
			{
				if (wheelMesh == null) wheelMesh = getOrCreateMesh(capi, "grindstones:grindingwheel" + inventory[0].Itemstack.Collectible.LastCodePart() + "Mesh", (cp) => createWheelMesh(cp));
			}
			else
			{
				wheelMesh = null;
			}

			renderer.UpdateMeshes(wheelMesh, IsSharpening);
		}

		private MeshData createWheelMesh (ICoreClientAPI cp)
		{
			cp.Tesselator.TesselateItem(inventory[0].Itemstack.Item, out MeshData wheelMesh);
			return wheelMesh;
		}

		int getRotation()
		{
			Block block = Api.World.BlockAccessor.GetBlock(Pos);

			int rot = 0;
			switch (block.LastCodePart())
			{
				case "north": rot = 0; break;
				case "east": rot = 270; break;
				case "south": rot = 180; break;
				case "west": rot = 90; break;
			}

			return rot;
		}

		MeshData getOrCreateMesh (ICoreClientAPI capi, string code, CreateMeshDelegate onCreate)
		{
			if (!Api.ObjectCache.TryGetValue(code, out object obj))
			{
				MeshData mesh = onCreate(capi);
				Api.ObjectCache[code] = mesh;
				return mesh;
			}
			else
			{
				return (MeshData) obj;
			}
		}

		#endregion

		public override void FromTreeAttributes (ITreeAttribute tree, IWorldAccessor worldForResolving)
		{
			base.FromTreeAttributes(tree, worldForResolving);

			IsSharpening = tree.GetBool("issharpening", false);

			if (worldForResolving.Side == EnumAppSide.Client && this.Api != null)
			{
				if (IsSharpening && inventory[0]?.Itemstack != null) StartWheel();
				else StopWheel();

				MarkDirty(true);
			}
		}

		public override void ToTreeAttributes (ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);

			tree.SetBool("issharpening", IsSharpening);
		}

		public bool HasWheel
		{
			get { return !inventory[0].Empty; }
		}

		public override void OnBlockUnloaded ()
		{
			base.OnBlockUnloaded();
			renderer?.Dispose();
			sharpeningSound?.Stop();
			sharpeningSound?.Dispose();

		}

		public override void OnBlockRemoved ()
		{
			base.OnBlockRemoved();
			renderer?.Dispose();
			sharpeningSound?.Stop();
			sharpeningSound?.Dispose();
		}

		public override void GetBlockInfo (IPlayer forPlayer, StringBuilder dsc)
		{
			string wheel = inventory[0].GetStackName() ?? Lang.Get("none");

			dsc.AppendLine(Lang.Get("Wheel: {0}", wheel));
		}

		public override bool OnTesselation (ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			if (animUtil?.animator == null)
			{
				animUtil?.InitializeAnimator(ModGrindstones.ModID + ".grindstone", null, null, new Vec3f(0, getRotation(), 0));
			}
			return base.OnTesselation(mesher, tessThreadTesselator);
		}

		internal static bool isRepariable (Item item)
		{
			// Ensure this is an item
			if (item is null) return false;

			// Ensure item is a tool
			if (item.Tool is null) return false;

			// Check if allowed tool type
			if (!ModGrindstones.ConfigServer.IsRepairableTool(item.Tool?.ToString() ?? "")) return false;

			// Check if allowed metal type
			if (!ModGrindstones.ConfigServer.IsRepairableMaterial(item.Variant["material"] ?? item.Variant["metal"])) return false;

			return true;
		}

		internal static Random rand = new Random();
		internal static float randomStd(float mean = 0, float dev = 1)
		{
			double u1 = 1 - rand.NextDouble();
			double u2 = 1 - rand.NextDouble();
			double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
			return (float) (mean + dev * randStdNormal);
		}
	}
}
