using System;
using System.Net;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Grindstones
{
	public class GrindstoneRenderer : IRenderer
	{
		private ICoreClientAPI api;
		private BlockPos pos;

		public MeshRef wheelMeshRef;

		public Vec3f wheelPos = new Vec3f(0, 0, 0);
		public Vec3f wheelRotRad = new Vec3f(0, 0, 0);

		Matrixf ModelMat = new Matrixf();
		float blockRotation;

		long updatedTotalMs;

		bool Animate;

		public GrindstoneRenderer(BlockPos pos, ICoreClientAPI capi, float blockRot)
		{
			this.pos = pos;
			this.api = capi;
			this.blockRotation = blockRot;
			this.Animate = false;
		}

		public double RenderOrder
		{
			get { return 0.5; }
		}

		public int RenderRange
		{
			get { return 24; }
		}

		float lastAngle = 0f;

		public void OnRenderFrame (float deltaTime, EnumRenderStage stage)
		{
			if (wheelMeshRef is null) return;

			long ellapsedMs = api.InWorldEllapsedMilliseconds;

			IRenderAPI rpi = api.Render;
			IClientWorldAccessor worldAccessor = api.World;
			Vec3d camPos = worldAccessor.Player.Entity.CameraPos;
			Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

			rpi.GlDisableCullFace();
			IStandardShaderProgram prog = rpi.StandardShader;
			prog.Use();
			prog.ExtraGlow = 0;
			prog.RgbaAmbientIn = rpi.AmbientColor;
			prog.RgbaFogIn = rpi.FogColor;
			prog.FogMinIn = rpi.FogMin;
			prog.FogDensityIn = rpi.FogDensity;
			prog.RgbaTint = ColorUtil.WhiteArgbVec;
			prog.RgbaLightIn = lightrgbs;

			prog.DontWarpVertices = 0;
			prog.AddRenderFlags = 0;
			prog.ExtraGodray = 0;
			prog.NormalShaded = 1;

			rpi.BindTexture2d(api.ItemTextureAtlas.AtlasTextures[0].TextureId);

			float origx = -0.5f;
			float origy = -0.25f;
			float origz = -0.5f;

			wheelPos.X = 0;
			wheelPos.Y = (13f - 3.5f) / 16;
			wheelPos.Z = 0;



			if (Animate) wheelRotRad.X = lastAngle - deltaTime * 2 * GameMath.PI;
			else wheelRotRad.X = lastAngle;
			wheelRotRad.Y = 0;
			wheelRotRad.Z = 0;

			lastAngle = wheelRotRad.X;

			prog.NormalShaded = 0;
			prog.ModelMatrix = ModelMat
				.Identity()
				.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
				.Translate(-origx, -origy, -origz)
				.RotateYDeg(blockRotation)
				.Translate(wheelPos)
				.Rotate(wheelRotRad)
				.Scale(1f, 1f, 1f)
				.Translate(origx, origy, origz)
				.Values
			;
			rpi.RenderMesh(wheelMeshRef);

			prog.Stop();
		}

		internal void UpdateMeshes (MeshData wheelMesh, bool animate)
		{
			Animate = animate;

			wheelMeshRef?.Dispose();
			wheelMeshRef = null;

			if (wheelMesh is not null)
			{
				wheelMeshRef = api.Render.UploadMesh(wheelMesh);
			}

			updatedTotalMs = api.InWorldEllapsedMilliseconds;
		}

		public void Dispose ()
		{ 
			api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
			api.Event.UnregisterRenderer (this, EnumRenderStage.AfterFinalComposition);
			wheelMeshRef?.Dispose();
		}
	}
}
