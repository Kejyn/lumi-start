using System;
using Lumivale.Core.Settings;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lumivale.Start.GameTests
{
    public sealed class GameGraphicsPresetsTests
    {
        [TestCase(GameGraphicsPreset.Low)]
        [TestCase(GameGraphicsPreset.Medium)]
        [TestCase(GameGraphicsPreset.High)]
        public void PresetsPreserveDisplayFramePacingAudioAndTheSuppliedSnapshot(GameGraphicsPreset preset)
        {
            var original = new SettingsValues
            {
                Width = 1920, Height = 1080, DisplayMode = FullScreenMode.Windowed, QualityName = "Fixture",
                FrameRateLimit = 120, VSync = false, MasterVolume = 0.23f, MusicVolume = 0.37f,
                SfxVolume = 0.41f, LanguageCode = "fr", RenderScale = 0.9f
            };
            string before = JsonUtility.ToJson(original);
            SettingsValues result = GameGraphicsPresets.Apply(original, preset);
            Assert.That(GameGraphicsPresets.Classify(result), Is.EqualTo(preset));
            Assert.That(result.Width, Is.EqualTo(original.Width));
            Assert.That(result.Height, Is.EqualTo(original.Height));
            Assert.That(result.DisplayMode, Is.EqualTo(original.DisplayMode));
            Assert.That(result.QualityName, Is.EqualTo(original.QualityName));
            Assert.That(result.FrameRateLimit, Is.EqualTo(original.FrameRateLimit));
            Assert.That(result.VSync, Is.EqualTo(original.VSync));
            Assert.That(result.MasterVolume, Is.EqualTo(original.MasterVolume));
            Assert.That(result.MusicVolume, Is.EqualTo(original.MusicVolume));
            Assert.That(result.SfxVolume, Is.EqualTo(original.SfxVolume));
            Assert.That(result.LanguageCode, Is.EqualTo(original.LanguageCode));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
        }

        [Test]
        public void AnIndividualRenderingChangeSelectsCustomWhileDisplayChangesRemainIndependent()
        {
            SettingsValues values = GameGraphicsPresets.Apply(new SettingsValues(), GameGraphicsPreset.Medium);
            values.FrameRateLimit = 30;
            values.Width = 640;
            Assert.That(GameGraphicsPresets.Classify(values), Is.EqualTo(GameGraphicsPreset.Medium));
            values.ShadowDistance = 21f;
            Assert.That(GameGraphicsPresets.Classify(values), Is.EqualTo(GameGraphicsPreset.Custom));
        }

        [Test]
        public void LimitedCapabilitiesProduceARecognizablePresetWithoutUnsupportedAntialiasing()
        {
            var capabilities = new RenderingCapabilities(supportsRenderScale: true,
                antiAliasingModes: new[] { GraphicsAntiAliasing.Off });
            SettingsValues values = GameGraphicsPresets.Apply(new SettingsValues(), GameGraphicsPreset.High, capabilities);
            Assert.That(values.AntiAliasing, Is.EqualTo(GraphicsAntiAliasing.Off));
            // Identical supported settings have one canonical label: the first matching preset.
            Assert.That(GameGraphicsPresets.Classify(values, capabilities), Is.EqualTo(GameGraphicsPreset.Medium));
        }

        [TestCase(GraphicsAntiAliasing.FXAA)]
        [TestCase(GraphicsAntiAliasing.SMAA)]
        public void DisablingPostProcessingNeverLeavesAnIneffectivePostAntialiasingSelection(GraphicsAntiAliasing mode)
        {
            var backend = new GameGraphicsBackend();
            var values = new SettingsValues { AntiAliasing = mode, PostProcessing = false };
            SettingsValues validated = backend.ValidateRendering(values, null);
            Assert.That(validated.AntiAliasing, Is.EqualTo(GraphicsAntiAliasing.Off));
            Assert.That(values.AntiAliasing, Is.EqualTo(mode));
            backend.Release();
        }

        [Test]
        public void AnAdHocCameraNeverReceivesARuntimeComponentEvenWhenTheBackendIsReusedImmediately()
        {
            var cameraObject = new GameObject("Graphics camera without authored URP data", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            bool originalMsaa = camera.allowMSAA;
            var backend = new GameGraphicsBackend();
            try
            {
                GraphicsCapabilities display = backend.GetCapabilities();
                var values = new SettingsValues
                {
                    Width = display.CurrentResolution.Width, Height = display.CurrentResolution.Height,
                    DisplayMode = display.CurrentMode, QualityName = display.CurrentQualityName
                };
                for (int session = 0; session < 2; session++)
                {
                    backend.Apply(values);
                    Assert.That(cameraObject.GetComponent<UniversalAdditionalCameraData>(), Is.Null);
                    backend.Release();
                    Assert.That(cameraObject.GetComponent<UniversalAdditionalCameraData>(), Is.Null);
                    Assert.That(camera.allowMSAA, Is.EqualTo(originalMsaa));
                }
            }
            finally
            {
                backend.Release();
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void NativeRenderingUsesAnIndependentAssetAndReleaseRestoresQualityCameraAndFramePacing()
        {
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(source, Is.Not.Null, "The Starter test requires its configured URP asset.");
            string sourceBefore = JsonUtility.ToJson(source);
            RenderPipelineAsset originalOverride = QualitySettings.renderPipeline;
            int originalFps = Application.targetFrameRate;
            int originalVSync = QualitySettings.vSyncCount;
            int originalMips = QualitySettings.globalTextureMipmapLimit;
            float originalLod = QualitySettings.lodBias;
            AnisotropicFiltering originalAnisotropic = QualitySettings.anisotropicFiltering;
            var cameraObject = new GameObject("Graphics backend fixture", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var camera = cameraObject.GetComponent<Camera>();
            var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            bool postBefore = cameraData.renderPostProcessing;
            bool msaaBefore = camera.allowMSAA;
            var backend = new GameGraphicsBackend();
            try
            {
                GraphicsCapabilities display = backend.GetCapabilities();
                var values = new SettingsValues
                {
                    Width = display.CurrentResolution.Width, Height = display.CurrentResolution.Height,
                    DisplayMode = display.CurrentMode, QualityName = display.CurrentQualityName,
                    FrameRateLimit = 30, VSync = false
                };
                values = GameGraphicsPresets.Apply(values, GameGraphicsPreset.Low, backend.RenderingCapabilities);
                backend.Apply(values);
                var runtime = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Assert.That(runtime, Is.Not.SameAs(source));
                Assert.That(runtime.renderScale, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(runtime.shadowDistance, Is.EqualTo(20f));
                Assert.That(runtime.shadowCascadeCount, Is.EqualTo(1));
                Assert.That(runtime.mainLightShadowmapResolution, Is.EqualTo(512));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(2));
                Assert.That(QualitySettings.lodBias, Is.EqualTo(0.5f));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(Application.targetFrameRate, Is.EqualTo(30));
                Assert.That(cameraData.renderPostProcessing, Is.False);
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
                backend.Release();
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(originalOverride));
                Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(source));
                Assert.That(Application.targetFrameRate, Is.EqualTo(originalFps));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(originalVSync));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(originalMips));
                Assert.That(QualitySettings.lodBias, Is.EqualTo(originalLod));
                Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(originalAnisotropic));
                Assert.That(cameraData.renderPostProcessing, Is.EqualTo(postBefore));
                Assert.That(camera.allowMSAA, Is.EqualTo(msaaBefore));
                backend.Apply(values);
                backend.Release();
                backend.Release();
                Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(source));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
            }
            finally
            {
                backend.Release();
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
