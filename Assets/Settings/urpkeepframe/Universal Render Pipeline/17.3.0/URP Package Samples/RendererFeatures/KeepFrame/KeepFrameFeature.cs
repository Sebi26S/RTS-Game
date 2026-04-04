using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class KeepFrameFeature : ScriptableRendererFeature
{
    class CopyFramePass : ScriptableRenderPass
    {
        RTHandle m_Destination;

        public void Setup(RTHandle destination)
        {
            m_Destination = destination;
        }

#if URP_COMPATIBILITY_MODE
#pragma warning disable 618, 672
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType != CameraType.Game)
                return;

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get("CopyFramePass");
            Blit(cmd, source, m_Destination);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore 618, 672
#endif

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle destination = renderGraph.ImportTexture(m_Destination);

            if (!source.IsValid() || !destination.IsValid())
                return;

            RenderGraphUtils.BlitMaterialParameters para =
                new(source, destination, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
            renderGraph.AddBlitPass(para, "Copy Frame Pass");
        }
    }

    class DrawOldFramePass : ScriptableRenderPass
    {
        class PassData
        {
            public TextureHandle source;
            public Material material;
            public string name;
        }

        Material m_DrawOldFrameMaterial;
        RTHandle m_Handle;
        string m_TextureName;

        public void Setup(Material drawOldFrameMaterial, RTHandle handle, string textureName)
        {
            m_DrawOldFrameMaterial = drawOldFrameMaterial;
            m_TextureName = textureName;
            m_Handle = handle;
        }

        static void ExecutePass(RasterCommandBuffer cmd, RTHandle source, Material material)
        {
            if (material == null || source == null)
                return;

            Vector2 viewportScale = source.useScaling
                ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;

            Blitter.BlitTexture(cmd, source, viewportScale, material, 0);
        }

#if URP_COMPATIBILITY_MODE
#pragma warning disable 618, 672
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(nameof(DrawOldFramePass));
            cmd.SetGlobalTexture(m_TextureName, m_Handle);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            ExecutePass(CommandBufferHelpers.GetRasterCommandBuffer(cmd), source, m_DrawOldFrameMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore 618, 672
#endif

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle oldFrameTextureHandle = renderGraph.ImportTexture(m_Handle);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Old Frame Pass", out var passData);

            TextureHandle destination = resourceData.activeColorTexture;

            if (!oldFrameTextureHandle.IsValid() || !destination.IsValid())
                return;

            passData.material = m_DrawOldFrameMaterial;
            passData.source = oldFrameTextureHandle;
            passData.name = m_TextureName;

            builder.UseTexture(oldFrameTextureHandle, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalTexture(data.name, data.source);
                ExecutePass(context.cmd, data.source, data.material);
            });
        }
    }

    [Serializable]
    public class Settings
    {
        public Material displayMaterial;
        public string textureName;
    }

    CopyFramePass m_CopyFrame;
    DrawOldFramePass m_DrawOldFrame;
    RTHandle m_OldFrameHandle;

    public Settings settings = new Settings();

    private static bool pendingClear = true;
    private static bool subscribedToSceneLoaded = false;

    public override void Create()
    {
        m_CopyFrame = new CopyFramePass();
        m_CopyFrame.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        m_DrawOldFrame = new DrawOldFramePass();
        m_DrawOldFrame.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

        if (!subscribedToSceneLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            subscribedToSceneLoaded = true;
        }

        pendingClear = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pendingClear = true;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.depthBufferBits = 0;
        descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;

        var textureName = string.IsNullOrEmpty(settings.textureName) ? "_FrameCopyTex" : settings.textureName;
        RenderingUtils.ReAllocateHandleIfNeeded(
            ref m_OldFrameHandle,
            descriptor,
            FilterMode.Bilinear,
            TextureWrapMode.Clamp,
            name: textureName
        );

        if (pendingClear)
        {
            ClearOldFrame();
            pendingClear = false;
        }

        m_CopyFrame.Setup(m_OldFrameHandle);
        m_DrawOldFrame.Setup(settings.displayMaterial, m_OldFrameHandle, textureName);

        renderer.EnqueuePass(m_CopyFrame);
        renderer.EnqueuePass(m_DrawOldFrame);
    }

    private void ClearOldFrame()
    {
        if (m_OldFrameHandle == null || m_OldFrameHandle.rt == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = m_OldFrameHandle.rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    protected override void Dispose(bool disposing)
    {
        m_OldFrameHandle?.Release();
    }
}