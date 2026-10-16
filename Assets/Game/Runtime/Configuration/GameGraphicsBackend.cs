using System;
using System.Collections.Generic;
using Lumivale.Core.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lumivale.Start.Game
{
    /// <summary>
    /// Project URP adapter used by the existing Settings service. Only runtime clones are mutated;
    /// Release restores the touched native quality settings and camera state for repeated sessions.
    /// Cameras must author UniversalAdditionalCameraData to opt into post-processing and FXAA/SMAA;
    /// this adapter never adds or removes components during rendering.
    /// </summary>
    public sealed class GameGraphicsBackend : IRenderingSettingsBackend
    {
        private sealed class QualitySnapshot
        {
            public RenderPipelineAsset Pipeline;
            public int MipmapLimit;
            public AnisotropicFiltering Anisotropic;
            public float LodBias;
            public int VSync;
            public UniversalRenderPipelineAsset Runtime;
        }

        private sealed class CameraSnapshot
        {
            public Camera Camera;
            public UniversalAdditionalCameraData Data;
            public bool AllowMsaa;
            public bool PostProcessing;
            public AntialiasingMode AntiAliasing;
            public AntialiasingQuality AntiAliasingQuality;
        }

        private readonly UnityGraphicsSettingsBackend display = new UnityGraphicsSettingsBackend();
        private readonly Dictionary<int, QualitySnapshot> qualitySnapshots = new Dictionary<int, QualitySnapshot>();
        private readonly Dictionary<Camera, CameraSnapshot> cameraSnapshots = new Dictionary<Camera, CameraSnapshot>();
        private bool ownsSession;
        private int originalQuality;
        private int originalFrameRate;
        private DisplayResolution originalResolution;
        private FullScreenMode originalDisplayMode;
        private SettingsValues applied;

        public GraphicsCapabilities GetCapabilities() => display.GetCapabilities();

        public RenderingCapabilities RenderingCapabilities
        {
            get
            {
                UniversalRenderPipelineAsset source = ActiveSource();
                bool hasUrp = source != null;
                bool hasPost = hasUrp && HasPostProcessing(source);
                var modes = new List<GraphicsAntiAliasing> { GraphicsAntiAliasing.Off };
                if (hasPost)
                {
                    modes.Add(GraphicsAntiAliasing.FXAA);
                    modes.Add(GraphicsAntiAliasing.SMAA);
                }
                if (hasUrp && SupportsMsaa(source))
                {
                    AddMsaaIfSupported(modes, source, 2, GraphicsAntiAliasing.MSAA2x);
                    AddMsaaIfSupported(modes, source, 4, GraphicsAntiAliasing.MSAA4x);
                    AddMsaaIfSupported(modes, source, 8, GraphicsAntiAliasing.MSAA8x);
                }
                bool desktop = IsDesktop(Application.platform);
                bool mobile = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
                bool web = Application.platform == RuntimePlatform.WebGLPlayer;
                return new RenderingCapabilities(supportsFrameRateLimit: desktop || mobile || web,
                    supportsVSync: desktop, supportsRenderScale: hasUrp,
                    supportsPostProcessing: hasPost,
                    supportsShadows: hasUrp && SystemInfo.supportsShadows && (source.supportsMainLightShadows || source.supportsAdditionalLightShadows),
                    supportsTextureQuality: true, supportsAnisotropicFiltering: true, supportsLod: true,
                    antiAliasingModes: modes);
            }
        }

        public SettingsValues ValidateRendering(SettingsValues values, SettingsValues fallback)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            SettingsValues result = values.Clone();
            SettingsValues defaults = fallback ?? new SettingsValues();
            RenderingCapabilities capabilities = RenderingCapabilities;
            result.FrameRateLimit = IsFrameRate(result.FrameRateLimit) ? result.FrameRateLimit :
                IsFrameRate(defaults.FrameRateLimit) ? defaults.FrameRateLimit : 60;
            if (!capabilities.SupportsFrameRateLimit) result.FrameRateLimit = -1;
            if (!capabilities.SupportsVSync) result.VSync = false;
            result.RenderScale = IsFinite(result.RenderScale) ? Mathf.Clamp(result.RenderScale, 0.5f, 1f) :
                IsFinite(defaults.RenderScale) ? Mathf.Clamp(defaults.RenderScale, 0.5f, 1f) : 1f;
            if (!capabilities.SupportsRenderScale) result.RenderScale = 1f;
            if (!capabilities.SupportsPostProcessing) result.PostProcessing = false;
            if (!Contains(capabilities.AntiAliasingModes, result.AntiAliasing)) result.AntiAliasing = GraphicsAntiAliasing.Off;
            if (!result.PostProcessing && (result.AntiAliasing == GraphicsAntiAliasing.FXAA || result.AntiAliasing == GraphicsAntiAliasing.SMAA))
                result.AntiAliasing = GraphicsAntiAliasing.Off;
            result.ShadowQuality = Enum.IsDefined(typeof(GraphicsShadowQuality), result.ShadowQuality) ?
                result.ShadowQuality : defaults.ShadowQuality;
            if (!Enum.IsDefined(typeof(GraphicsShadowQuality), result.ShadowQuality) || !capabilities.SupportsShadows)
                result.ShadowQuality = GraphicsShadowQuality.Off;
            result.ShadowDistance = IsFinite(result.ShadowDistance) ? Mathf.Clamp(result.ShadowDistance, 0f, 200f) :
                IsFinite(defaults.ShadowDistance) ? Mathf.Clamp(defaults.ShadowDistance, 0f, 200f) : 35f;
            result.TextureQuality = ValidQuality(result.TextureQuality, defaults.TextureQuality);
            result.LodQuality = ValidQuality(result.LodQuality, defaults.LodQuality);
            if (!Enum.IsDefined(typeof(AnisotropicFiltering), result.AnisotropicFiltering))
                result.AnisotropicFiltering = Enum.IsDefined(typeof(AnisotropicFiltering), defaults.AnisotropicFiltering) ?
                    defaults.AnisotropicFiltering : AnisotropicFiltering.Enable;
            return result;
        }

        public void Apply(DisplayResolution resolution, FullScreenMode mode, string qualityName)
        {
            BeginSession();
            int index = Array.IndexOf(QualitySettings.names, qualityName);
            if (index < 0) throw new InvalidOperationException($"Quality profile '{qualityName}' is no longer available.");
            if (QualitySettings.GetQualityLevel() != index) QualitySettings.SetQualityLevel(index, true);
            CaptureQuality();
            if (Screen.width != resolution.Width || Screen.height != resolution.Height || Screen.fullScreenMode != mode)
                Screen.SetResolution(resolution.Width, resolution.Height, mode);
        }

        public void Apply(SettingsValues values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            Apply(new DisplayResolution(values.Width, values.Height), values.DisplayMode, values.QualityName);
            SettingsValues validated = ValidateRendering(values, null);
            QualitySnapshot quality = CaptureQuality();
            UniversalRenderPipelineAsset source = ActiveSource();
            if (source != null)
            {
                if (quality.Runtime == null)
                {
                    quality.Runtime = Object.Instantiate(source);
                    quality.Runtime.name = source.name + " (Runtime Graphics)";
                    quality.Runtime.hideFlags = HideFlags.DontSave;
                }
                UniversalRenderPipelineAsset runtime = quality.Runtime;
                runtime.renderScale = validated.RenderScale;
                runtime.msaaSampleCount = MsaaSamples(validated.AntiAliasing);
                runtime.shadowDistance = validated.ShadowQuality == GraphicsShadowQuality.Off ? 0f : validated.ShadowDistance;
                if (validated.ShadowQuality != GraphicsShadowQuality.Off)
                {
                    runtime.mainLightShadowmapResolution = validated.ShadowQuality == GraphicsShadowQuality.Low ? 512 :
                        validated.ShadowQuality == GraphicsShadowQuality.Medium ? 1024 : 2048;
                    runtime.additionalLightsShadowmapResolution = runtime.mainLightShadowmapResolution;
                    runtime.shadowCascadeCount = validated.ShadowQuality == GraphicsShadowQuality.Low ? 1 :
                        validated.ShadowQuality == GraphicsShadowQuality.Medium ? 2 : 4;
                }
                if (QualitySettings.renderPipeline != runtime) QualitySettings.renderPipeline = runtime;
            }
            QualitySettings.vSyncCount = validated.VSync ? 1 : 0;
            Application.targetFrameRate = validated.VSync ? -1 : validated.FrameRateLimit;
            QualitySettings.globalTextureMipmapLimit = validated.TextureQuality == GraphicsQuality.Low ? 2 :
                validated.TextureQuality == GraphicsQuality.Medium ? 1 : 0;
            QualitySettings.anisotropicFiltering = validated.AnisotropicFiltering;
            QualitySettings.lodBias = validated.LodQuality == GraphicsQuality.Low ? 0.5f :
                validated.LodQuality == GraphicsQuality.Medium ? 1f : 2f;
            applied = validated.Clone();
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                ApplyCamera(camera);
        }

        public void Release()
        {
            if (!ownsSession) return;
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            applied = null;
            foreach (CameraSnapshot snapshot in cameraSnapshots.Values)
            {
                if (snapshot.Camera != null) snapshot.Camera.allowMSAA = snapshot.AllowMsaa;
                if (snapshot.Data == null) continue;
                snapshot.Data.renderPostProcessing = snapshot.PostProcessing;
                snapshot.Data.antialiasing = snapshot.AntiAliasing;
                snapshot.Data.antialiasingQuality = snapshot.AntiAliasingQuality;
            }
            cameraSnapshots.Clear();
            foreach (KeyValuePair<int, QualitySnapshot> entry in qualitySnapshots)
            {
                if (QualitySettings.GetQualityLevel() != entry.Key) QualitySettings.SetQualityLevel(entry.Key, true);
                QualitySnapshot snapshot = entry.Value;
                if (QualitySettings.renderPipeline == snapshot.Runtime) QualitySettings.renderPipeline = snapshot.Pipeline;
                QualitySettings.globalTextureMipmapLimit = snapshot.MipmapLimit;
                QualitySettings.anisotropicFiltering = snapshot.Anisotropic;
                QualitySettings.lodBias = snapshot.LodBias;
                QualitySettings.vSyncCount = snapshot.VSync;
            }
            if (QualitySettings.GetQualityLevel() != originalQuality) QualitySettings.SetQualityLevel(originalQuality, true);
            Application.targetFrameRate = originalFrameRate;
            if (Screen.width != originalResolution.Width || Screen.height != originalResolution.Height || Screen.fullScreenMode != originalDisplayMode)
                Screen.SetResolution(originalResolution.Width, originalResolution.Height, originalDisplayMode);
            foreach (QualitySnapshot snapshot in qualitySnapshots.Values)
                if (snapshot.Runtime != null) DestroyOwned(snapshot.Runtime);
            qualitySnapshots.Clear();
            ownsSession = false;
        }

        private void BeginSession()
        {
            if (ownsSession) return;
            originalQuality = QualitySettings.GetQualityLevel();
            originalFrameRate = Application.targetFrameRate;
            originalResolution = new DisplayResolution(Math.Max(1, Screen.width), Math.Max(1, Screen.height));
            originalDisplayMode = Screen.fullScreenMode;
            CaptureQuality();
            ownsSession = true;
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private QualitySnapshot CaptureQuality()
        {
            int index = QualitySettings.GetQualityLevel();
            if (!qualitySnapshots.TryGetValue(index, out QualitySnapshot snapshot))
            {
                snapshot = new QualitySnapshot
                {
                    Pipeline = QualitySettings.renderPipeline,
                    MipmapLimit = QualitySettings.globalTextureMipmapLimit,
                    Anisotropic = QualitySettings.anisotropicFiltering,
                    LodBias = QualitySettings.lodBias,
                    VSync = QualitySettings.vSyncCount
                };
                qualitySnapshots.Add(index, snapshot);
            }
            return snapshot;
        }

        private UniversalRenderPipelineAsset ActiveSource()
        {
            int index = QualitySettings.GetQualityLevel();
            if (qualitySnapshots.TryGetValue(index, out QualitySnapshot snapshot) &&
                snapshot.Runtime != null && QualitySettings.renderPipeline == snapshot.Runtime)
                return (snapshot.Pipeline != null ? snapshot.Pipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera) => ApplyCamera(camera);

        private void SceneUnloaded(Scene scene) => PruneDestroyedCameras();

        private void PruneDestroyedCameras()
        {
            // Destroyed Unity wrappers otherwise remain strongly referenced across scene navigation.
            var removed = new List<Camera>();
            foreach (Camera camera in cameraSnapshots.Keys)
                if (camera == null) removed.Add(camera);
            foreach (Camera camera in removed) cameraSnapshots.Remove(camera);
        }

        private void ApplyCamera(Camera camera)
        {
            if (applied == null || camera == null || camera.cameraType != CameraType.Game || !camera.gameObject.scene.IsValid() || ActiveSource() == null) return;
            if (!cameraSnapshots.TryGetValue(camera, out CameraSnapshot snapshot))
            {
                PruneDestroyedCameras();
                camera.TryGetComponent(out UniversalAdditionalCameraData data);
                snapshot = new CameraSnapshot
                {
                    Camera = camera, Data = data, AllowMsaa = camera.allowMSAA,
                    PostProcessing = data != null && data.renderPostProcessing,
                    AntiAliasing = data != null ? data.antialiasing : AntialiasingMode.None,
                    AntiAliasingQuality = data != null ? data.antialiasingQuality : AntialiasingQuality.High
                };
                cameraSnapshots.Add(camera, snapshot);
            }
            camera.allowMSAA = MsaaSamples(applied.AntiAliasing) > 1;
            if (snapshot.Data == null) return;
            snapshot.Data.renderPostProcessing = applied.PostProcessing;
            snapshot.Data.antialiasing = applied.AntiAliasing == GraphicsAntiAliasing.FXAA ? AntialiasingMode.FastApproximateAntialiasing :
                applied.AntiAliasing == GraphicsAntiAliasing.SMAA ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            snapshot.Data.antialiasingQuality = AntialiasingQuality.High;
        }

        private static bool HasPostProcessing(UniversalRenderPipelineAsset source)
        {
            foreach (ScriptableRendererData data in source.rendererDataList)
                if (!(data is UniversalRendererData renderer) || renderer.postProcessData == null) return false;
            return source.rendererDataList.Length > 0;
        }

        private static bool SupportsMsaa(UniversalRenderPipelineAsset source)
        {
            if (SystemInfo.supportsMultisampledTextures == 0 ||
                (source.supportsCameraOpaqueTexture && !SystemInfo.supportsStoreAndResolveAction && Application.isMobilePlatform)) return false;
            foreach (ScriptableRendererData data in source.rendererDataList)
                if (!(data is UniversalRendererData renderer) ||
                    (renderer.renderingMode != RenderingMode.Forward && renderer.renderingMode != RenderingMode.ForwardPlus)) return false;
            return source.rendererDataList.Length > 0;
        }

        private static void AddMsaaIfSupported(List<GraphicsAntiAliasing> modes, UniversalRenderPipelineAsset source,
            int samples, GraphicsAntiAliasing mode)
        {
            var descriptor = new RenderTextureDescriptor(64, 64,
                source.supportsHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default, 24) { msaaSamples = samples };
            if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) == samples) modes.Add(mode);
        }

        private static int MsaaSamples(GraphicsAntiAliasing mode) => mode == GraphicsAntiAliasing.MSAA2x ? 2 :
            mode == GraphicsAntiAliasing.MSAA4x ? 4 : mode == GraphicsAntiAliasing.MSAA8x ? 8 : 1;

        private static bool IsFrameRate(int value) => value == -1 || value == 30 || value == 60 || value == 120;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static GraphicsQuality ValidQuality(GraphicsQuality value, GraphicsQuality fallback) =>
            Enum.IsDefined(typeof(GraphicsQuality), value) ? value :
            Enum.IsDefined(typeof(GraphicsQuality), fallback) ? fallback : GraphicsQuality.High;

        private static bool Contains(IReadOnlyList<GraphicsAntiAliasing> choices, GraphicsAntiAliasing value)
        {
            foreach (GraphicsAntiAliasing choice in choices) if (choice == value) return true;
            return false;
        }

        private static bool IsDesktop(RuntimePlatform platform) => platform == RuntimePlatform.WindowsEditor ||
            platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.OSXEditor ||
            platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.LinuxEditor || platform == RuntimePlatform.LinuxPlayer;

        private static void DestroyOwned(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
