using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PlayAreaFogFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material material;
        [Range(0f, 5f)] public float fogWidth = 3f;
        [FormerlySerializedAs("fogIntensity")]
        [Range(0f, 1f)] public float fogMaxOpacity = 1f;
        [Range(0f, 1f)] public float fogEdgeOpacity = 0.65f;
        [Range(0.1f, 4f)] public float fogRampPower = 1.6f;
        public Color fogColor = new Color(0.45f, 0.55f, 0.65f, 1f);
    }

    [SerializeField] private Settings settings = new Settings();

    private PlayAreaFogPass _pass;

    public override void Create()
    {
        _pass = new PlayAreaFogPass(settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings == null || settings.material == null)
            return;

        if (renderingData.cameraData.isPreviewCamera)
            return;
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
            return;
        var cam = renderingData.cameraData.camera;
        if (cam != null && cam.GetComponent<PlayAreaFogIgnore>() != null)
            return;

        renderer.EnqueuePass(_pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (settings == null || settings.material == null)
            return;
        if (renderingData.cameraData.isPreviewCamera)
            return;
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
            return;
        var cam = renderingData.cameraData.camera;
        if (cam != null && cam.GetComponent<PlayAreaFogIgnore>() != null)
            return;

        _pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        _pass.SetTarget(renderer.cameraColorTargetHandle);
    }

    private sealed class PlayAreaFogPass : ScriptableRenderPass
    {
        private static readonly int FogWidthId = Shader.PropertyToID("_FogWidth");
        private static readonly int FogMaxOpacityId = Shader.PropertyToID("_FogMaxOpacity");
        private static readonly int FogEdgeOpacityId = Shader.PropertyToID("_FogEdgeOpacity");
        private static readonly int FogRampPowerId = Shader.PropertyToID("_FogRampPower");
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly Vector4 FullscreenScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private readonly Settings _settings;
        private RTHandle _colorTarget;
        private RTHandle _copiedColor;
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();
        public PlayAreaFogPass(Settings settings)
        {
            _settings = settings;
            profilingSampler = new ProfilingSampler("PlayAreaFog");
        }

        public void SetTarget(RTHandle colorTarget)
        {
            _colorTarget = colorTarget;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, desc, name: "_PlayAreaFogCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings == null || _settings.material == null) return;
            if (_colorTarget == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("PlayAreaFog");

            using (new ProfilingScope(cmd, profilingSampler))
            {
                CoreUtils.SetRenderTarget(cmd, _copiedColor);
                Blitter.BlitTexture(cmd, _colorTarget, FullscreenScaleBias, 0f, false);

                CoreUtils.SetRenderTarget(cmd, _colorTarget);

                _settings.material.SetFloat(FogWidthId, Mathf.Max(0.001f, _settings.fogWidth));
                float maxOpacity = Mathf.Clamp01(_settings.fogMaxOpacity);
                float edgeOpacity = Mathf.Clamp01(_settings.fogEdgeOpacity);
                if (edgeOpacity > maxOpacity)
                    edgeOpacity = maxOpacity;
                _settings.material.SetFloat(FogMaxOpacityId, maxOpacity);
                _settings.material.SetFloat(FogEdgeOpacityId, edgeOpacity);
                _settings.material.SetFloat(FogRampPowerId, Mathf.Max(0.1f, _settings.fogRampPower));
                _settings.material.SetColor(FogColorId, _settings.fogColor);

                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, _copiedColor);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, FullscreenScaleBias);

                cmd.DrawProcedural(Matrix4x4.identity, _settings.material, 0, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
            _copiedColor?.Release();
            _copiedColor = null;
        }
    }
}
