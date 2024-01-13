using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace ModdingTools.Render;

public class TestItemGuiRenderer : IRenderer
{
    public double RenderOrder => 0.8;
    public int RenderRange => 0;

    public TestShape? mShape;
    public ItemSlot? mSlot;
    public float[] mModelMat = new float[16] { 2.0f, 0.04f, 0, 0, 0.04f, 1.99f, 0.14f, 0, 0, -0.14f, 2, 0, 1.04f, -0.64f, -1.65f, 1 };

    private readonly ICoreClientAPI mClientApi;
    private ShaderProgram? mShaderProgram;
    

    public TestItemGuiRenderer(ICoreClientAPI api)
    {
        mClientApi = api;
        mClientApi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        mClientApi.Event.ReloadShader += LoadAnimatedItemShaders;

        LoadAnimatedItemShaders();
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
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (mSlot == null) return;
        
        Matrixf modelMat = new();
        modelMat.Values = mModelMat;
        ItemRenderInfo itemStackRenderInfo = mClientApi.Render.GetItemStackRenderInfo(mSlot, EnumItemRenderTarget.HandFp);

        using (new RenderedTexture(mClientApi))
        {
            RenderHandFp(mSlot, itemStackRenderInfo, modelMat);
        }
    }
    private void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat)
    {
        IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
        IShaderProgram prog;

        IRenderAPI rpi = mClientApi.Render;
        prevProg?.Stop();

        Debug.Assert(mShaderProgram != null);
        prog = ShaderPrograms.Helditem;
        prog.Use();
        prog.Uniform("alphaTest", renderInfo.AlphaTest);
        prog.UniformMatrix("modelViewMatrix", modelMat.Values);
        prog.Uniform("normalShaded", renderInfo.NormalShaded ? 1 : 0);
        prog.Uniform("overlayOpacity", renderInfo.OverlayOpacity);

        if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0f)
        {
            prog.Uniform("tex2dOverlay", renderInfo.OverlayTexture.TextureId);
            prog.Uniform("overlayTextureSize", new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height));
            prog.Uniform("baseTextureSize", new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height));
            TextureAtlasPosition textureAtlasPosition = mClientApi.Render.GetTextureAtlasPosition(inSlot.Itemstack);
            prog.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
        }

        Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));
        int num16 = (int)inSlot.Itemstack.Collectible.GetTemperature(mClientApi.World, inSlot.Itemstack);
        float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num16);
        int num17 = GameMath.Clamp((num16 - 550) / 2, 0, 255);
        Vec4f rgbaGlowIn = new(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num17 / 255f);
        prog.Uniform("extraGlow", num17);
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

        /*prog.UniformMatrices4x3(
            "elementTransforms",
            GlobalConstants.MaxAnimatedElements,
            mShape?.Animator?.TransformationMatrices4x3
        );*/

        mClientApi.Render.RenderMesh(mShape?.CurrentMeshRef);

        prog.Stop();
        prevProg?.Use();
    }
    public void Dispose()
    {

    }
}

public class TestShape : ITexPositionSource
{
    public AnimatorBase? Animator { get; set; }
    public Shape? CurrentShape { get; private set; }
    public MeshRef? CurrentMeshRef { get; private set; }
    public ITextureAtlasAPI? CurrentAtlas { get; private set; }
    public string CacheKey { get; private set; }

    private readonly ICoreClientAPI mClientApi;

    public TestShape(ICoreClientAPI api, string shapePath)
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

public sealed class RenderedTexture : IDisposable
{
    static public int Texture => mTexture;
    public static void UpdateSize(ICoreClientAPI api) => SetUpTexture(api);

    static private int mTexture = -1;
    static private int mFrameBuffer = -1;
    static private int mPrevBuffer = -1;
    static private int mDepthBuffer = -1;
    static private bool mSetUp = false;

    public RenderedTexture(ICoreClientAPI api)
    {
        CreateTexture(api);
        BindTextureFrameBuffer(api);
    }
    private static void CreateTexture(ICoreClientAPI api)
    {
        if (mSetUp) return;
        mSetUp = true;

        mFrameBuffer = GL.GenFramebuffer();
        mTexture = GL.GenTexture();
        mDepthBuffer = GL.GenRenderbuffer();

        SetUpTexture(api);
    }
    private static void SetUpTexture(ICoreClientAPI api)
    {
        CreateTexture(api);

        int height = api.Render.FrameHeight;
        int width = api.Render.FrameWidth;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, mFrameBuffer);

        GL.BindTexture(TextureTarget.Texture2D, mTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, mDepthBuffer);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, mDepthBuffer);

        GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, mTexture, 0);
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    private static void BindTextureFrameBuffer(ICoreClientAPI api)
    {
        mPrevBuffer = GL.GetInteger(GetPName.DrawFramebufferBinding);
        int height = api.Render.FrameHeight;
        int width = api.Render.FrameWidth;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, mFrameBuffer);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.Viewport(0, 0, width, height);
    }
    private static void UnbindTextureFrameBuffer()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, mPrevBuffer);
    }

    public void Dispose()
    {
        UnbindTextureFrameBuffer();
    }
}
