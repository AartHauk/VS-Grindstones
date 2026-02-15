using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Grindstones
{
	public class ItemGrindingwheel : Item, IContainedMeshSource
	{

		public override void OnBeforeRender (ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
		{
			Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.GetOrCreate(capi, "grindstones.grindingwheeltextures", () => new Dictionary<string, MultiTextureMeshRef>());
			string key = GetMeshCacheKey(itemstack);

			if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef meshref))
			{
				MeshData mesh = GenMesh(itemstack, capi.ItemTextureAtlas, null);
				meshref = capi.Render.UploadMultiTextureMesh(mesh);
				meshRefs[key] = meshref;
			}

			renderinfo.ModelRef = meshref;
			renderinfo.NormalShaded = true;

			base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
		}

		public virtual MeshData GetOrCreateMesh (ItemStack itemstack, ITextureAtlasAPI targetAtlas)
		{
			ICoreClientAPI capi = api as ICoreClientAPI;
			MeshData mesh = new MeshData();

			CompositeShape rcshape = this.Shape.Clone();

			Shape? shape = capi.Assets.TryGet(rcshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"))?.ToObject<Shape>();

			ShapeTextureSource texSource = new ShapeTextureSource(capi, shape, "Grindingwheel Texture Source");

			CompositeTexture stone = new CompositeTexture(new AssetLocation("game", $"block/stone/rock/{itemstack.Attributes.GetString("stoneType")}*"));
			CompositeTexture metal = new CompositeTexture(new AssetLocation("game", $"block/metal/ingot/{itemstack.Attributes.GetString("rodMetal")}*"));

			stone.Bake(capi.Assets);
			metal.Bake(capi.Assets);

			texSource.textures["stone"] = stone;
			texSource.textures["metal"] = metal;

			if (shape is null) return mesh;
			capi.Tesselator.TesselateShape("grindstones.grindingwheel", shape, out mesh, texSource);
			return mesh;
		}

		public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos blockPos)
		{
			return GetOrCreateMesh(itemstack, targetAtlas);
		}

		public virtual string GetMeshCacheKey(ItemStack itemstack)
		{
			return $"{itemstack.Collectible.Code}-{itemstack.Attributes.GetString("stoneType")}-{itemstack.Attributes.GetString("rodMetal")}";
		}

		public override string GetHeldItemName (ItemStack itemStack)
		{
			if (Code == null) return "Invalid block, id" + this.Id;

			string type = ItemClass.Name();
			StringBuilder sb = new StringBuilder();
			string stone = Lang.GetMatching($"game:rock-{itemStack.Attributes.GetString("stoneType", "andesite")}");
			string metal = Lang.GetMatching($"game:item-rod-{itemStack.Attributes.GetString("rodMetal", "copper")}");

			sb.Append(Lang.Get("{0} Grinding wheel with {1}", stone, metal));

			foreach (var bh in CollectibleBehaviors)
			{
				bh.GetHeldItemName(sb, itemStack);
			}

			return sb.ToString();
		}
	}
}
