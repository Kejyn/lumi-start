using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.Settings;
using Lumivale.Start.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameOptionsIntegrationTests
    {
        [UnityTest]
        public IEnumerator GraphicsPresetsApplyToRuntimeClonesAndManualChoicesBecomeCustom()
        {
            var assets = CaptureGraphicsSources();
            yield return OpenGraphics();
            foreach (GameGraphicsPreset preset in new[] { GameGraphicsPreset.Low, GameGraphicsPreset.Medium, GameGraphicsPreset.High })
            {
                yield return SelectGraphicsPreset(preset);
                Assert.That(GameGraphicsPresets.Classify(SettingsController().Draft, host.Settings.RenderingCapabilities), Is.EqualTo(preset));
                yield return Click(SettingsController().ApplyButton);
                yield return null;
                AssertGraphicsEngine(host.Settings.Current);
                Assert.That(GameGraphicsPresets.Classify(host.Settings.Current, host.Settings.RenderingCapabilities), Is.EqualTo(preset));
                AssertGraphicsSourcesUnchanged(assets);
            }

            var applied = host.Settings.Current;
            yield return GraphicsKey(SettingsController().RenderScaleButton, Key.LeftArrow);
            Assert.That(SettingsController().Draft.RenderScale, Is.LessThan(applied.RenderScale));
            Assert.That(GameGraphicsPresets.Classify(SettingsController().Draft, host.Settings.RenderingCapabilities), Is.EqualTo(GameGraphicsPreset.Custom));
            Assert.That(SettingsController().GetGraphicsDropdown("quality").captionText.text,
                Does.Contain(GameText.Text("options.quality.Custom")));
            Assert.That(Options().HasUnsavedChanges, Is.True);
            Assert.That(host.Settings.Current.ContentEquals(applied), Is.True);
            AssertGraphicsEngine(applied);
            AssertGraphicsSourcesUnchanged(assets);
        }

        [UnityTest]
        public IEnumerator GraphicsFramePacingAppliesVSyncPolicyAndSurvivesFreshBootstrap()
        {
            var assets = CaptureGraphicsSources();
            yield return OpenGraphics();
            if (SettingsController().Draft.VSync)
                yield return GraphicsKey(SettingsController().VSyncButton, Key.RightArrow);
            Assert.That(SettingsController().GetGraphicsDropdown("frameRateLimit").interactable, Is.True);
            foreach (int limit in new[] { 120, -1, 60, 120 })
            {
                yield return SelectFrameRate(limit);
                yield return Click(SettingsController().ApplyButton);
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(Application.targetFrameRate, Is.EqualTo(limit));
            }

            yield return GraphicsMouse(SettingsController().VSyncButton);
            Assert.That(SettingsController().Draft.VSync, Is.True);
            Assert.That(SettingsController().GetGraphicsDropdown("frameRateLimit").interactable, Is.False);
            yield return Click(SettingsController().ApplyButton);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            Assert.That(host.Settings.Current.FrameRateLimit, Is.EqualTo(120), "VSync must retain the user's inactive FPS limit.");

            yield return GraphicsKey(SettingsController().VSyncButton, Key.RightArrow);
            yield return SelectFrameRate(30);
            yield return Click(SettingsController().ApplyButton);
            var saved = host.Settings.Current;
            string document = PlayerPrefs.GetString(key);
            Assert.That(document, Is.Not.Empty);
            yield return Click(SettingsController().BackButton);

            var previous = host;
            yield return Wait(host.ShutdownAsync());
            UnityEngine.Object.Destroy(host.gameObject);
            yield return null;
            CreateHost();
            yield return Wait(host.Ready);
            RestrictDevices();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            yield return Press(Key.Space);
            Assert.That(host, Is.Not.SameAs(previous));
            Assert.That(host.Settings.Current.ContentEquals(saved), Is.True);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(document));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            AssertGraphicsEngine(saved);
            AssertGraphicsSourcesUnchanged(assets);
        }

        [UnityTest]
        public IEnumerator GraphicsAntiAliasingUsesItsNativeCameraOrPipelineAndPostProcessingFiltersChoices()
        {
            var assets = CaptureGraphicsSources();
            yield return OpenGraphics();
            if (!SettingsController().Draft.PostProcessing)
                yield return GraphicsMouse(SettingsController().PostProcessingButton);
            var supported = host.Settings.RenderingCapabilities.AntiAliasingModes.ToArray();
            Assert.That(supported, Does.Contain(GraphicsAntiAliasing.FXAA));
            Assert.That(supported, Does.Contain(GraphicsAntiAliasing.SMAA));
            foreach (GraphicsAntiAliasing mode in supported)
            {
                yield return SelectAntiAliasing(mode);
                yield return Click(SettingsController().ApplyButton);
                yield return null;
                AssertGraphicsEngine(host.Settings.Current);
                Assert.That(host.Settings.Current.AntiAliasing, Is.EqualTo(mode));
            }

            yield return SelectAntiAliasing(GraphicsAntiAliasing.SMAA);
            yield return GraphicsMouse(SettingsController().PostProcessingButton);
            Assert.That(SettingsController().Draft.PostProcessing, Is.False);
            Assert.That(SettingsController().Draft.AntiAliasing, Is.EqualTo(GraphicsAntiAliasing.Off));
            for (int attempt = 0; attempt < supported.Length + 1; attempt++)
            {
                yield return GraphicsPad("antiAliasing", 1);
                Assert.That(SettingsController().Draft.AntiAliasing, Is.Not.EqualTo(GraphicsAntiAliasing.FXAA));
                Assert.That(SettingsController().Draft.AntiAliasing, Is.Not.EqualTo(GraphicsAntiAliasing.SMAA));
                Assert.That(supported, Does.Contain(SettingsController().Draft.AntiAliasing));
            }
            yield return Click(SettingsController().ApplyButton);
            AssertGraphicsEngine(host.Settings.Current);
            AssertGraphicsSourcesUnchanged(assets);
        }

        [UnityTest]
        public IEnumerator GraphicsResetRemainsADraftAndBackConfirmationDiscardsWithoutChangingEngineOrStorage()
        {
            var assets = CaptureGraphicsSources();
            yield return OpenGraphics();
            yield return SelectGraphicsPreset(GameGraphicsPreset.Low);
            yield return Click(SettingsController().ApplyButton);
            var applied = host.Settings.Current;
            string document = PlayerPrefs.GetString(key);

            yield return SelectGraphicsPreset(GameGraphicsPreset.High);
            yield return Click(SettingsController().ResetButton);
            Assert.That(SettingsController().Draft.ContentEquals(host.Settings.Defaults), Is.True);
            Assert.That(host.Settings.Current.ContentEquals(applied), Is.True);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(document));
            Assert.That(Options().HasUnsavedChanges, Is.True);
            AssertGraphicsEngine(applied);
            yield return Click(SettingsController().BackButton);
            Assert.That(host.UI.IsOpen(Options().View.UnsavedConfirmation.Id), Is.True);
            yield return Click(Options().View.DiscardChanges);
            Assert.That(Options().IsOpen, Is.False);
            Assert.That(host.Settings.Current.ContentEquals(applied), Is.True);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(document));
            AssertGraphicsEngine(applied);
            yield return OpenGraphics();
            Assert.That(SettingsController().Draft.ContentEquals(applied), Is.True);
            Assert.That(Options().HasUnsavedChanges, Is.False);
            AssertGraphicsSourcesUnchanged(assets);
        }

        [UnityTest]
        public IEnumerator GraphicsLastRowStaysVisibleForKeyboardMouseAndXboxWithoutMovingTheFooter()
        {
            yield return OpenGraphics();
            var controller = SettingsController();
            var footer = new[] { controller.BackButton, controller.ResetButton, controller.ApplyButton };
            var before = footer.Select(button => GraphicsCorners(button.GetComponent<RectTransform>())).ToArray();
            var categories = GraphicsCategories();
            categories.SelectCategory(0);
            yield return null; yield return null;
            Assert.That(categories.CurrentCategory, Is.EqualTo(0));
            Assert.That(categories.Panels.All(panel => panel.activeInHierarchy), Is.True);
            var last = controller.GetGraphicsDropdown("lodQuality");
            var scroll = last.GetComponentInParent<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(last.transform.IsChildOf(scroll.content), Is.True);
            Assert.That(footer.All(button => !button.transform.IsChildOf(scroll.content)), Is.True);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(controller.GetGraphicsDropdown("mode").gameObject));
            for (int attempt = 0; attempt < ConsistentGraphicsKeys.Length && EventSystem.current.currentSelectedGameObject != last.gameObject; attempt++)
            {
                if (attempt % 2 == 0) yield return Press(Key.DownArrow);
                else yield return Pad(GamepadButton.DpadDown);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(last.gameObject));
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, last.transform);
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 2f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 2f));

            GraphicsQuality initial = controller.Draft.LodQuality;
            yield return GraphicsKey(controller.LodQualityButton, Key.RightArrow);
            Assert.That(controller.Draft.LodQuality, Is.Not.EqualTo(initial));
            GraphicsQuality keyboardChoice = controller.Draft.LodQuality;
            yield return GraphicsPad("lodQuality", 1);
            Assert.That(controller.Draft.LodQuality, Is.Not.EqualTo(keyboardChoice));
            GraphicsQuality padChoice = controller.Draft.LodQuality;
            yield return GraphicsMouse(controller.LodQualityButton);
            Assert.That(controller.Draft.LodQuality, Is.Not.EqualTo(padChoice));
            for (int button = 0; button < footer.Length; button++)
            {
                var after = GraphicsCorners(footer[button].GetComponent<RectTransform>());
                for (int corner = 0; corner < 4; corner++)
                    Assert.That(Vector3.Distance(after[corner], before[button][corner]), Is.LessThan(0.1f));
            }
            Assert.That(last.navigation.selectOnDown, Is.EqualTo(controller.BackButton));
            yield return Click(controller.BackButton);
            if (host.UI.IsOpen(Options().View.UnsavedConfirmation.Id))
                yield return Click(Options().View.DiscardChanges);
            Assert.That(Options().IsOpen, Is.False);
        }

        private IEnumerator OpenGraphics()
        {
            yield return OpenOptions();
            yield return Click(Options().View.Tabs[2]);
            Assert.That(Options().CurrentTab, Is.EqualTo(2));
            yield return ShowGraphicsCategory("quality");
            Assert.That(SettingsController().GetGraphicsDropdown("renderScale").gameObject.activeInHierarchy, Is.True);
        }

        private IEnumerator GraphicsKey(GameChoiceButton button, Key direction)
        {
            string key = GraphicsKeyFor(button);
            yield return ShowGraphicsCategory(key);
            var dropdown = SettingsController().GetGraphicsDropdown(key);
            int target = NextGraphicsIndex(dropdown, key, direction == Key.LeftArrow ? -1 : 1);
            yield return GraphicsChooseByNavigation(key, target, gamepadInput: false);
        }

        private IEnumerator GraphicsMouse(GameChoiceButton button)
        {
            string key = GraphicsKeyFor(button);
            yield return ShowGraphicsCategory(key);
            var dropdown = SettingsController().GetGraphicsDropdown(key);
            int target = NextGraphicsIndex(dropdown, key, 1);
            Assert.That(dropdown.IsActive() && dropdown.IsInteractable(), Is.True);
            var before = SettingsController().Draft;
            yield return ClickLanguageControl(dropdown);
            Assert.That(dropdown.IsExpanded, Is.True);
            Assert.That(SettingsController().Draft.ContentEquals(before), Is.True, "Opening a selector does not change its draft.");
            string label = dropdown.options[target].text;
            var item = dropdown.GetComponentsInChildren<Toggle>(false)
                .Single(toggle => toggle.GetComponentInChildren<TMP_Text>().text == label);
            Assert.That(item.interactable, Is.True);
            yield return ClickLanguageControl(item);
            yield return WaitLanguageDropdownClosed(dropdown);
        }

        private IEnumerator GraphicsPad(string key, int direction)
        {
            yield return ShowGraphicsCategory(key);
            var dropdown = SettingsController().GetGraphicsDropdown(key);
            int target = NextGraphicsIndex(dropdown, key, direction);
            yield return GraphicsChooseByNavigation(key, target, gamepadInput: true);
        }

        private IEnumerator GraphicsChooseByNavigation(string key, int target, bool gamepadInput)
        {
            yield return ShowGraphicsCategory(key);
            var dropdown = SettingsController().GetGraphicsDropdown(key);
            Assert.That(dropdown != null && dropdown.IsActive() && dropdown.IsInteractable(), Is.True, key);
            var before = SettingsController().Draft;
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            yield return null;
            if (gamepadInput) yield return Pad(GamepadButton.South);
            else yield return Press(Key.Enter);
            Assert.That(dropdown.IsExpanded, Is.True);
            Assert.That(SettingsController().Draft.ContentEquals(before), Is.True, "Opening a selector does not change its draft.");
            var items = dropdown.GetComponentsInChildren<Toggle>(false);
            Assert.That(items.Length, Is.EqualTo(dropdown.options.Count));
            Assert.That(items[target].interactable, Is.True, "The derived Custom value must remain read-only.");
            int current = Array.FindIndex(items, item => item.gameObject == EventSystem.current.currentSelectedGameObject);
            Assert.That(current, Is.GreaterThanOrEqualTo(0), "The native list owns its focused Toggle.");
            while (current != target)
            {
                int previous = current;
                if (gamepadInput) yield return Pad(current < target ? GamepadButton.DpadDown : GamepadButton.DpadUp);
                else yield return Press(current < target ? Key.DownArrow : Key.UpArrow);
                current = Array.FindIndex(items, item => item.gameObject == EventSystem.current.currentSelectedGameObject);
                Assert.That(current, Is.GreaterThanOrEqualTo(0));
                Assert.That(current, Is.Not.EqualTo(previous), "Native list navigation must advance toward the requested item.");
                Assert.That(SettingsController().Draft.ContentEquals(before), Is.True, "Navigation alone does not choose a value.");
            }
            if (gamepadInput) yield return Pad(GamepadButton.South);
            else yield return Press(Key.Enter);
            yield return WaitLanguageDropdownClosed(dropdown);
            Assert.That(dropdown.value, Is.EqualTo(target));
        }

        private static int NextGraphicsIndex(TMP_Dropdown dropdown, string key, int direction)
        {
            Assert.That(dropdown, Is.Not.Null, key);
            int index = dropdown.value;
            for (int attempt = 0; attempt < dropdown.options.Count; attempt++)
            {
                index = (index + direction + dropdown.options.Count) % dropdown.options.Count;
                if (key != "quality" || dropdown.options[index].text != GameText.Text("options.quality.Custom")) return index;
            }
            Assert.Fail("No selectable graphics option exists for " + key);
            return 0;
        }

        private static GameGraphicsCategories GraphicsCategories() =>
            SettingsController().GetGraphicsDropdown("quality").GetComponentInParent<GameGraphicsCategories>(true);

        private static IEnumerator ShowGraphicsCategory(string key)
        {
            var categories = GraphicsCategories();
            Assert.That(categories, Is.Not.Null);
            int category = key == "mode" || key == "resolution" || key == "vSync" || key == "frameRateLimit" ? 0 :
                key == "quality" || key == "renderScale" || key == "antiAliasing" || key == "postProcessing" ? 1 : 2;
            categories.SelectCategory(category);
            EventSystem.current.SetSelectedGameObject(SettingsController().GetGraphicsDropdown(key).gameObject);
            yield return null; yield return null;
        }

        private static string GraphicsKeyFor(GameChoiceButton button)
        {
            var controller = SettingsController();
            if (button == controller.ResolutionButton) return "resolution";
            if (button == controller.ModeButton) return "mode";
            if (button == controller.QualityButton) return "quality";
            if (button == controller.FrameRateLimitButton) return "frameRateLimit";
            if (button == controller.VSyncButton) return "vSync";
            if (button == controller.RenderScaleButton) return "renderScale";
            if (button == controller.AntiAliasingButton) return "antiAliasing";
            if (button == controller.PostProcessingButton) return "postProcessing";
            if (button == controller.ShadowQualityButton) return "shadowQuality";
            if (button == controller.ShadowDistanceButton) return "shadowDistance";
            if (button == controller.TextureQualityButton) return "textureQuality";
            if (button == controller.AnisotropicFilteringButton) return "anisotropicFiltering";
            if (button == controller.LodQualityButton) return "lodQuality";
            throw new ArgumentException("The graphics fixture received an unknown row.", nameof(button));
        }

        private IEnumerator SelectGraphicsPreset(GameGraphicsPreset preset)
        {
            for (int attempt = 0; attempt < 4 &&
                GameGraphicsPresets.Classify(SettingsController().Draft, host.Settings.RenderingCapabilities) != preset; attempt++)
                yield return GraphicsKey(SettingsController().QualityButton, Key.RightArrow);
            Assert.That(GameGraphicsPresets.Classify(SettingsController().Draft, host.Settings.RenderingCapabilities), Is.EqualTo(preset));
        }

        private IEnumerator SelectFrameRate(int limit)
        {
            for (int attempt = 0; attempt < 4 && SettingsController().Draft.FrameRateLimit != limit; attempt++)
            {
                yield return GraphicsPad("frameRateLimit", 1);
            }
            Assert.That(SettingsController().Draft.FrameRateLimit, Is.EqualTo(limit));
        }

        private IEnumerator SelectAntiAliasing(GraphicsAntiAliasing mode)
        {
            for (int attempt = 0; attempt < 7 && SettingsController().Draft.AntiAliasing != mode; attempt++)
                yield return GraphicsKey(SettingsController().AntiAliasingButton, Key.RightArrow);
            Assert.That(SettingsController().Draft.AntiAliasing, Is.EqualTo(mode));
        }

        private static Vector3[] GraphicsCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        private static Dictionary<UniversalRenderPipelineAsset, string> CaptureGraphicsSources()
        {
            var result = new Dictionary<UniversalRenderPipelineAsset, string>();
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                result.Add(asset, UnityEditor.EditorJsonUtility.ToJson(asset));
            }
#endif
            Assert.That(result, Is.Not.Empty);
            return result;
        }

        private static void AssertGraphicsSourcesUnchanged(Dictionary<UniversalRenderPipelineAsset, string> assets)
        {
#if UNITY_EDITOR
            foreach (var asset in assets)
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(asset.Key), Is.EqualTo(asset.Value), asset.Key.name);
            var active = GraphicsSettings.currentRenderPipeline;
            Assert.That(active, Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(active), Is.Empty, "The active pipeline must be an owned runtime instance.");
            Assert.That((active.hideFlags & HideFlags.DontSave) != 0, Is.True);
#endif
        }

        private static void AssertGraphicsEngine(SettingsValues expected)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(pipeline.renderScale, Is.EqualTo(expected.RenderScale).Within(0.001f));
            int samples = expected.AntiAliasing == GraphicsAntiAliasing.MSAA2x ? 2 :
                expected.AntiAliasing == GraphicsAntiAliasing.MSAA4x ? 4 :
                expected.AntiAliasing == GraphicsAntiAliasing.MSAA8x ? 8 : 1;
            Assert.That(pipeline.msaaSampleCount, Is.EqualTo(samples));
            Assert.That(pipeline.shadowDistance, Is.EqualTo(expected.ShadowQuality == GraphicsShadowQuality.Off ? 0f : expected.ShadowDistance).Within(0.001f));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(expected.VSync ? 1 : 0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(expected.VSync ? -1 : expected.FrameRateLimit));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(expected.TextureQuality == GraphicsQuality.Low ? 2 :
                expected.TextureQuality == GraphicsQuality.Medium ? 1 : 0));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(expected.AnisotropicFiltering));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(expected.LodQuality == GraphicsQuality.Low ? 0.5f :
                expected.LodQuality == GraphicsQuality.Medium ? 1f : 2f).Within(0.001f));
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(camera => camera.cameraType == CameraType.Game && camera.gameObject.scene.IsValid()).ToArray();
            Assert.That(cameras, Is.Not.Empty);
            foreach (var camera in cameras)
            {
                Assert.That(camera.TryGetComponent(out UniversalAdditionalCameraData data), Is.True);
                Assert.That(camera.allowMSAA, Is.EqualTo(samples > 1));
                Assert.That(data.renderPostProcessing, Is.EqualTo(expected.PostProcessing));
                Assert.That(data.antialiasing, Is.EqualTo(expected.AntiAliasing == GraphicsAntiAliasing.FXAA ?
                    AntialiasingMode.FastApproximateAntialiasing : expected.AntiAliasing == GraphicsAntiAliasing.SMAA ?
                    AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None));
            }
        }
    }
}
