using ImGuiNET;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using VSImGui;

namespace ModdingTools
{
    public class ImGuiShapeManager
    {
        private readonly Dictionary<string, ImGuiShape> mShapes = new();
        private readonly ICoreClientAPI mClientApi;
        public readonly ImGuiShapeRender mRenderer;

        public Vector4 PosAndSize;

        public ImGuiShapeManager(ICoreClientAPI api)
        {
            mClientApi = api;
            mRenderer = new(api);
            PosAndSize = new(0, 0, 0, 1);

            mClientApi.Event.RegisterRenderer(mRenderer, EnumRenderStage.Ortho);
        }

        public int Render(string shapePath, Selection selection)
        {
            if (!mShapes.ContainsKey(shapePath)) mShapes.Add(shapePath, new(mClientApi, shapePath));

            mRenderer.PosAndSize = PosAndSize;
            mRenderer.SlotToRender = selection.SlotSelection;

            return mRenderer.Texture;
        }
    }

    public class ImGuiShapeRender : IRenderer
    {
        public ItemSlot? SlotToRender { get; set; }
        public Vector4 PosAndSize;
        public Entity? EntityToRender { get; set; }
        public int Texture => mTexture;
        
        private ShaderProgram? mShaderProgram;
        private readonly ICoreClientAPI mClientApi;
        private int mTexture = -1;
        private int mFrameBuffer = -1;
        private int mDepthBuffer = -1;
        private int mPrevBuffer = -1;

        public double RenderOrder => 0.9;

        public int RenderRange => 0;

        public ImGuiShapeRender(ICoreClientAPI api)
        {
            mClientApi = api;

            api.Event.ReloadShader += LoadAnimatedItemShaders;
            LoadAnimatedItemShaders();
            SetUpTexture();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage) => Render(deltaTime);

        public void Render(float dt)
        {
            if (EntityToRender == null) return;

            //RenderItemstackToGui(SlotToRender, PosAndSize.X, PosAndSize.Y, PosAndSize.Z, PosAndSize.W, ColorUtil.WhiteArgb, dt);

            IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
            IShaderProgram prog = ShaderPrograms.Gui;

            prevProg?.Stop();
            prog.Use();

            BindTextureFrameBuffer();
            mClientApi.Render.RenderEntityToGui(dt, EntityToRender, PosAndSize.X, PosAndSize.Y, PosAndSize.Z, 0, 1, ColorUtil.WhiteArgb);
            UnbindTextureFrameBuffer();

            prog?.Stop();
            prevProg?.Use();
        }

        public void RenderItemstackToGui(ItemSlot inSlot, double posX, double posY, double posZ, float size, int color, float dt, bool shading = true, bool origRotate = false, bool showStackSize = true)
        {
            /*Matrixf modelMat = new();
            ItemStack itemstack = inSlot.Itemstack;
            ItemRenderInfo renderInfo = mClientApi.Render.GetItemStackRenderInfo(inSlot, EnumItemRenderTarget.Gui);
            if (renderInfo.ModelRef == null)
            {
                return;
            }

            itemstack.Collectible.InGuiIdle(mClientApi.World, itemstack);
            ModelTransform transform = renderInfo.Transform;
            if (transform == null)
            {
                return;
            }

            bool flag = itemstack.Class == EnumItemClass.Block;
            bool flag2 = origRotate && renderInfo.Transform.Rotate;
            modelMat.Identity();
            modelMat.Translate((int)posX - ((itemstack.Class == EnumItemClass.Item) ? 3 : 0), (int)posY - ((itemstack.Class == EnumItemClass.Item) ? 1 : 0), (float)posZ);
            modelMat.Translate(transform.Origin.X + GuiElement.scaled(transform.Translation.X), transform.Origin.Y + GuiElement.scaled(transform.Translation.Y), (double)(transform.Origin.Z * size) + GuiElement.scaled(transform.Translation.Z));
            modelMat.Scale(size * transform.ScaleXYZ.X, size * transform.ScaleXYZ.Y, size * transform.ScaleXYZ.Z);
            modelMat.RotateXDeg(transform.Rotation.X + (flag ? 180f : 0f));
            modelMat.RotateYDeg(transform.Rotation.Y - (float)((!flag) ? 1 : (-1)) * (flag2 ? (dt / 50f) : 0f));
            modelMat.RotateZDeg(transform.Rotation.Z);
            modelMat.Translate(0f - transform.Origin.X, 0f - transform.Origin.Y, 0f - transform.Origin.Z);

            IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
            IShaderProgram prog;

            IRenderAPI rpi = mClientApi.Render;
            prevProg?.Stop();

            prog = mShaderProgram;
            prog.Use();
            prog.Uniform("alphaTest", renderInfo.AlphaTest);
            prog.UniformMatrix("modelViewMatrix", modelMat.Values);
            prog.Uniform("normalShaded", renderInfo.NormalShaded ? 1 : 0);
            prog.Uniform("overlayOpacity", 0);

            Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));
            int temperature = 0;
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(temperature);
            int extraGlow = GameMath.Clamp((temperature - 550) / 2, 0, 255);
            Vec4f rgbaGlowIn = new(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], extraGlow / 255f);
            prog.Uniform("extraGlow", extraGlow);
            prog.Uniform("rgbaAmbientIn", mClientApi.Ambient.BlendedAmbientColor);
            prog.Uniform("rgbaLightIn", lightRGBSVec4f);
            prog.Uniform("rgbaGlowIn", rgbaGlowIn);

            float[] tmpVals = new float[4];
            Vec4f outPos = new();
            float[] array = Mat4f.Create();
            Mat4f.RotateY(array, array, mClientApi.World.Player.Entity.SidedPos.Yaw);
            Mat4f.RotateX(array, array, mClientApi.World.Player.Entity.SidedPos.Pitch);
            Mat4f.Mul(array, array, modelMat.Values);
            tmpVals[0] = mClientApi.Render.ShaderUniforms.LightPosition3D.X;
            tmpVals[1] = mClientApi.Render.ShaderUniforms.LightPosition3D.Y;
            tmpVals[2] = mClientApi.Render.ShaderUniforms.LightPosition3D.Z;
            tmpVals[3] = 0f;
            Mat4f.MulWithVec4(array, tmpVals, outPos);
            prog.Uniform("lightPosition", new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize());
            prog.UniformMatrix("toShadowMapSpaceMatrixFar", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
            prog.UniformMatrix("toShadowMapSpaceMatrixNear", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
            prog.BindTexture2D("itemTex", renderInfo.TextureId, 0);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);

            Matrixf TransformationMatrices4x3 = new();
            TransformationMatrices4x3.Identity();
            prog.UniformMatrices4x3(
                "elementTransforms",
                GlobalConstants.MaxAnimatedElements,
                TransformationMatrices4x3.Values
            );

            BindTextureFrameBuffer();
            mClientApi.Render.RenderMesh(renderInfo.ModelRef);
            UnbindTextureFrameBuffer();

            prog?.Stop();
            prevProg?.Use();*/
        }

        public void RenderItemstackToGuiOld(ItemSlot inSlot, double posX, double posY, double posZ, float size, int color, float dt, bool shading = true, bool origRotate = false, bool showStackSize = true)
        {
            /*ShaderProgramGui shader = ShaderPrograms.Gui;
            Matrixf modelMat = new();

            IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
            IShaderProgram prog = ShaderPrograms.Gui;

            prevProg?.Stop();
            prog.Use();


            ItemStack itemstack = inSlot.Itemstack;
            ItemRenderInfo itemStackRenderInfo = mClientApi.Render.GetItemStackRenderInfo(inSlot, EnumItemRenderTarget.Gui);
            if (itemStackRenderInfo.ModelRef == null)
            {
                return;
            }

            itemstack.Collectible.InGuiIdle(mClientApi.World, itemstack);
            ModelTransform transform = itemStackRenderInfo.Transform;
            if (transform == null)
            {
                return;
            }

            bool flag = itemstack.Class == EnumItemClass.Block;
            bool flag2 = origRotate && itemStackRenderInfo.Transform.Rotate;
            modelMat.Identity();
            modelMat.Translate((int)posX - ((itemstack.Class == EnumItemClass.Item) ? 3 : 0), (int)posY - ((itemstack.Class == EnumItemClass.Item) ? 1 : 0), (float)posZ);
            modelMat.Translate(transform.Origin.X + GuiElement.scaled(transform.Translation.X), transform.Origin.Y + GuiElement.scaled(transform.Translation.Y), (double)(transform.Origin.Z * size) + GuiElement.scaled(transform.Translation.Z));
            modelMat.Scale(size * transform.ScaleXYZ.X, size * transform.ScaleXYZ.Y, size * transform.ScaleXYZ.Z);
            modelMat.RotateXDeg(transform.Rotation.X + (flag ? 180f : 0f));
            modelMat.RotateYDeg(transform.Rotation.Y - (float)((!flag) ? 1 : (-1)) * (flag2 ? ((float)*//*game.Platform.EllapsedMs*//*0 / 50f) : 0f));
            modelMat.RotateZDeg(transform.Rotation.Z);
            modelMat.Translate(0f - transform.Origin.X, 0f - transform.Origin.Y, 0f - transform.Origin.Z);
            int num = (int)itemstack.Collectible.GetTemperature(mClientApi.World, itemstack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
            float[] array = ColorUtil.ToRGBAFloats(color);
            int num2 = GameMath.Clamp((num - 550) / 2, 0, 255);
            bool flag3 = itemstack.Attributes.HasAttribute("temperature");
            shader.NormalShaded = (itemStackRenderInfo.NormalShaded ? 1 : 0);
            shader.RgbaIn = new Vec4f(array[0], array[1], array[2], array[3]);
            shader.ExtraGlow = num2;
            shader.TempGlowMode = (flag3 ? 1 : 0);
            shader.RgbaGlowIn = (flag3 ? new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], num2 / 255f) : new Vec4f(1f, 1f, 1f, num2 / 255f));
            shader.ApplyColor = (itemStackRenderInfo.ApplyColor ? 1 : 0);
            shader.Tex2d2D = itemStackRenderInfo.TextureId;
            shader.AlphaTest = itemStackRenderInfo.AlphaTest;
            shader.OverlayOpacity = itemStackRenderInfo.OverlayOpacity;
            if (itemStackRenderInfo.OverlayTexture != null && itemStackRenderInfo.OverlayOpacity > 0f)
            {
                shader.Tex2dOverlay2D = itemStackRenderInfo.OverlayTexture.TextureId;
                shader.OverlayTextureSize = new Vec2f(itemStackRenderInfo.OverlayTexture.Width, itemStackRenderInfo.OverlayTexture.Height);
                shader.BaseTextureSize = new Vec2f(itemStackRenderInfo.TextureSize.Width, itemStackRenderInfo.TextureSize.Height);
                TextureAtlasPosition textureAtlasPosition = InventoryItemRenderer.GetTextureAtlasPosition(mClientApi.World as ClientMain, itemstack);
                shader.BaseUvOrigin = new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1);
            }

            shader.ModelMatrix = modelMat.Values;
            shader.ProjectionMatrix = (mClientApi.World as ClientMain)?.CurrentProjectionMatrix;
            shader.ModelViewMatrix = modelMat.ReverseMul((mClientApi.World as ClientMain)?.CurrentModelViewMatrix).Values;
            shader.ApplyModelMat = 1;
            
            *//*if (game.api.eventapi.itemStackRenderersByTarget[(int)itemstack.Collectible.ItemClass][0].TryGetValue(itemstack.Collectible.Id, out var value))
            {
                value(inSlot, itemStackRenderInfo, modelMat, posX, posY, posZ, size, color, origRotate, showStackSize);
                shader.ApplyModelMat = 0;
                shader.NormalShaded = 0;
                shader.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
                shader.AlphaTest = 0f;
                return;
            }*//*

            shader.DamageEffect = itemStackRenderInfo.DamageEffect;

            BindTextureFrameBuffer();

            mClientApi.Render.RenderMesh(itemStackRenderInfo.ModelRef);

            UnbindTextureFrameBuffer();

            shader.ApplyModelMat = 0;
            shader.NormalShaded = 0;
            shader.TempGlowMode = 0;
            shader.DamageEffect = 0f;
            
            *//*LoadedTexture value2 = null;
            if (itemstack.StackSize != 1 && showStackSize)
            {
                float num3 = size / (float)GuiElement.scaled(25.600000381469727);
                string key = itemstack.StackSize + "-" + (int)(num3 * 100f);
                if (!StackSizeTextures.TryGetValue(key, out value2))
                {
                    value2 = (StackSizeTextures[key] = GenStackSizeTexture(itemstack.StackSize, num3));
                }
            }

            if (value2 != null)
            {
                float num4 = size / (float)GuiElement.scaled(25.600000381469727);
                game.Platform.GlToggleBlend(on: true, EnumBlendMode.PremultipliedAlpha);
                game.Render2DLoadedTexture(value2, (int)(posX + (double)size + 1.0 - value2.Width), (int)(posY + (double)num4 * GuiElement.scaled(3.0) - GuiElement.scaled(4.0)), (int)posZ + 100);
                game.Platform.GlToggleBlend(on: true);
            }*//*

            shader.AlphaTest = 0f;
            shader.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);

            prog?.Stop();
            prevProg?.Use();*/
        }

        public int Render(ImGuiShape model, Selection selection, Matrixf modelMat, int textureId, bool normalShaded = true)
        {
            /*ItemRenderInfo renderInfo = mClientApi.Render.GetItemStackRenderInfo(selection.SlotSelection.Itemstack, EnumItemRenderTarget.HandFp);

            IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
            IShaderProgram prog;

            IRenderAPI rpi = mClientApi.Render;
            prevProg?.Stop();

            prog = mShaderProgram;
            prog.Use();
            prog.Uniform("alphaTest", 0.005f); // @TODO RenderAlphaTest from CollectibleObject
            prog.UniformMatrix("modelViewMatrix", modelMat.Values);
            prog.Uniform("normalShaded", normalShaded ? 1 : 0);
            prog.Uniform("overlayOpacity", 0); // renderInfo.OverlayOpacity); // @TODO for overlay

            *//*if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0f)
            {
                prog.Uniform("tex2dOverlay", renderInfo.OverlayTexture.TextureId);
                prog.Uniform("overlayTextureSize", new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height));
                prog.Uniform("baseTextureSize", new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height));
                TextureAtlasPosition textureAtlasPosition = mClientApi.Render.GetTextureAtlasPosition(inSlot.Itemstack);
                prog.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
            }*//* // Overlay

            Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));
            int num16 = 0; // (int)inSlot.Itemstack.Collectible.GetTemperature(mClientApi.World, inSlot.Itemstack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num16);
            int extraGlow = GameMath.Clamp((num16 - 550) / 2, 0, 255);
            Vec4f rgbaGlowIn = new(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], extraGlow / 255f);
            prog.Uniform("extraGlow", extraGlow);
            prog.Uniform("rgbaAmbientIn", mClientApi.Ambient.BlendedAmbientColor);
            prog.Uniform("rgbaLightIn", lightRGBSVec4f);
            prog.Uniform("rgbaGlowIn", rgbaGlowIn);

            float[] tmpVals = new float[4];
            Vec4f outPos = new();
            float[] array = Mat4f.Create();
            Mat4f.RotateY(array, array, mClientApi.World.Player.Entity.SidedPos.Yaw);
            Mat4f.RotateX(array, array, mClientApi.World.Player.Entity.SidedPos.Pitch);
            Mat4f.Mul(array, array, modelMat.Values);
            tmpVals[0] = mClientApi.Render.ShaderUniforms.LightPosition3D.X;
            tmpVals[1] = mClientApi.Render.ShaderUniforms.LightPosition3D.Y;
            tmpVals[2] = mClientApi.Render.ShaderUniforms.LightPosition3D.Z;
            tmpVals[3] = 0f;
            Mat4f.MulWithVec4(array, tmpVals, outPos);
            prog.Uniform("lightPosition", new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize());
            prog.UniformMatrix("toShadowMapSpaceMatrixFar", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
            prog.UniformMatrix("toShadowMapSpaceMatrixNear", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
            prog.BindTexture2D("itemTex", renderInfo.TextureId, 0);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);

            prog.UniformMatrices4x3(
                "elementTransforms",
                GlobalConstants.MaxAnimatedElements,
                model.Animator?.TransformationMatrices4x3
            );

            BindTextureFrameBuffer();
            mClientApi.Render.RenderMesh(model.CurrentMeshRef);
            UnbindTextureFrameBuffer();

            prog?.Stop();
            prevProg?.Use();*/

            return mTexture;
        }

        public void DrawImGuiWindow()
        {
            ImGui.Begin("Test texture");
            if (mTexture != -1)
            {
                float windowWidth = ImGui.GetWindowWidth();
                float windowHeight = ImGui.GetWindowHeight();

                ImGui.GetWindowDrawList().AddImage(
                    mTexture,
                    ImGui.GetCursorScreenPos(),
                    ImGui.GetCursorScreenPos() + new System.Numerics.Vector2(windowWidth, windowHeight),
                    new System.Numerics.Vector2(0, 1),
                    new System.Numerics.Vector2(1, 0)
                );
            }
            ImGui.Text($"mFrameBuffer: {mFrameBuffer}");
            ImGui.Text($"mTexture: {mTexture}");
            ImGui.End();
        }

        private bool LoadAnimatedItemShaders()
        {
            mShaderProgram = mClientApi.Shader.NewShaderProgram() as ShaderProgram;

            if (mShaderProgram == null) return false;

            mShaderProgram.AssetDomain = mClientApi.ModLoader.GetModSystem<ModdingToolsSystem>().Mod.Info.ModID;
            mClientApi.Shader.RegisterFileShaderProgram("moddingtoolsshapeeditor", mShaderProgram);
            mShaderProgram.Compile();

            return true;
        }
        private void SetUpTexture()
        {
            int height = mClientApi.Render.FrameHeight;
            int width = mClientApi.Render.FrameWidth;

            mFrameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, mFrameBuffer);

            mTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, mTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            mDepthBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, mDepthBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, mDepthBuffer);

            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, mTexture, 0);
            GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
        }

        private void BindTextureFrameBuffer()
        {
            mPrevBuffer = GL.GetInteger(GetPName.DrawFramebufferBinding);
            int height = mClientApi.Render.FrameHeight;
            int width = mClientApi.Render.FrameWidth;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, mFrameBuffer);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, width, height);
        }
        private void UnbindTextureFrameBuffer()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, mPrevBuffer);
        }
        
        public void Dispose()
        {
        }
    }

    public class ImGuiShape : ITexPositionSource
    {
        public AnimatorBase? Animator { get; set; }
        public Shape? CurrentShape { get; private set; }
        public MeshRef? CurrentMeshRef { get; private set; }
        public ITextureAtlasAPI? CurrentAtlas { get; private set; }
        public string CacheKey { get; private set; }

        private readonly ICoreClientAPI mClientApi;

        public ImGuiShape(ICoreClientAPI api, string shapePath)
        {
            mClientApi = api;
            CacheKey = $"shapeEditorCollectibleMeshes-{shapePath.GetHashCode()}";

            CurrentAtlas = mClientApi.ItemTextureAtlas;

            AssetLocation shapeLocation = new(shapePath);
            shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            CurrentShape = Shape.TryGet(api, shapeLocation); // @TODO null check
            CurrentShape.ResolveReferences(api.Logger, CacheKey);
            MeshData meshData = InitializeMeshData(CacheKey, CurrentShape, this);
            CurrentMeshRef = InitializeMeshRef(meshData);
            Animator = GetAnimator(mClientApi, CacheKey, CurrentShape);
        }

        private MeshData InitializeMeshData(string cacheDictKey, Shape shape, ITexPositionSource texSource)
        {
            if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            shape.ResolveReferences(mClientApi.World.Logger, cacheDictKey);
            CacheInvTransforms(shape.Elements);
            shape.ResolveAndLoadJoints();

            mClientApi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out MeshData meshdata, texSource, null);

            return meshdata;
        }
        private MeshRef InitializeMeshRef(MeshData meshData)
        {
            MeshRef? meshRef = null;

            if (RuntimeEnv.MainThreadId == Environment.CurrentManagedThreadId)
            {
                meshRef = mClientApi.Render.UploadMesh(meshData);
            }
            else
            {
                mClientApi.Event.EnqueueMainThreadTask(() =>
                {
                    meshRef = mClientApi.Render.UploadMesh(meshData);
                }, "uploadmesh");
            }

            Debug.Assert(meshRef != null);
            return meshRef;
        }
        private static void CacheInvTransforms(ShapeElement[] elements)
        {
            if (elements == null) return;

            for (int i = 0; i < elements.Length; i++)
            {
                elements[i].CacheInverseTransformMatrix();
                CacheInvTransforms(elements[i].Children);
            }
        }
        private AnimatorBase? GetAnimator(ICoreClientAPI capi, string cacheDictKey, Shape blockShape)
        {
            if (blockShape == null)
            {
                return null;
            }

            Dictionary<string, AnimCacheEntry>? animCache;
            capi.ObjectCache.TryGetValue("coAnimCache", out object? animCacheObj);
            animCache = animCacheObj as Dictionary<string, AnimCacheEntry>;
            if (animCache == null)
            {
                capi.ObjectCache["coAnimCache"] = animCache = new Dictionary<string, AnimCacheEntry>();
            }

            AnimatorBase animator;

            if (animCache.TryGetValue(cacheDictKey, out AnimCacheEntry? cacheObj))
            {
                animator = capi.Side == EnumAppSide.Client ?
                    new ClientAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById) :
                    new ServerAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById)
                ;
            }
            else
            {
                for (int i = 0; blockShape.Animations != null && i < blockShape.Animations.Length; i++)
                {
                    blockShape.Animations[i].GenerateAllFrames(blockShape.Elements, blockShape.JointsById);
                }

                animator = capi.Side == EnumAppSide.Client ?
                    new ClientAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById) :
                    new ServerAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById)
                ;

                animCache[cacheDictKey] = new AnimCacheEntry()
                {
                    Animations = blockShape.Animations,
                    RootElems = (animator as ClientAnimator)?.rootElements,
                    RootPoses = (animator as ClientAnimator)?.RootPoses
                };
            }

            return animator;
        }


        public Size2i? AtlasSize => CurrentAtlas?.Size;
        public virtual TextureAtlasPosition? this[string textureCode]
        {
            get
            {
                AssetLocation? texturePath = null;
                CurrentShape?.Textures.TryGetValue(textureCode, out texturePath);

                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return GetOrCreateTexPos(texturePath);
            }
        }
        private TextureAtlasPosition? GetOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = CurrentAtlas[texturePath];

            if (texpos == null)
            {
                IAsset texAsset = mClientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    CurrentAtlas.GetOrInsertTexture(texturePath, out _, out texpos);
                }
                else
                {
                    mClientApi.World.Logger.Warning($"Bullseye.CollectibleBehaviorAnimatable: texture {texturePath}, not no such texture found.");
                }
            }

            return texpos;
        }
    }
}
