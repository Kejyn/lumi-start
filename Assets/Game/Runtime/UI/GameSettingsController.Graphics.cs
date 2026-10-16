using System;
using System.Collections.Generic;
using System.Linq;
using Lumivale.Core.Settings;
using UnityEngine;

namespace Lumivale.Start.Game
{
    public sealed partial class GameSettingsController
    {
        [SerializeField] private GameChoiceButton frameRateLimit;
        [SerializeField] private GameChoiceButton vSync;
        [SerializeField] private GameChoiceButton renderScale;
        [SerializeField] private GameChoiceButton antiAliasing;
        [SerializeField] private GameChoiceButton postProcessing;
        [SerializeField] private GameChoiceButton shadowQuality;
        [SerializeField] private GameChoiceButton shadowDistance;
        [SerializeField] private GameChoiceButton textureQuality;
        [SerializeField] private GameChoiceButton anisotropicFiltering;
        [SerializeField] private GameChoiceButton lodQuality;

        public GameChoiceButton FrameRateLimitButton => frameRateLimit;
        public GameChoiceButton VSyncButton => vSync;
        public GameChoiceButton RenderScaleButton => renderScale;
        public GameChoiceButton AntiAliasingButton => antiAliasing;
        public GameChoiceButton PostProcessingButton => postProcessing;
        public GameChoiceButton ShadowQualityButton => shadowQuality;
        public GameChoiceButton ShadowDistanceButton => shadowDistance;
        public GameChoiceButton TextureQualityButton => textureQuality;
        public GameChoiceButton AnisotropicFilteringButton => anisotropicFiltering;
        public GameChoiceButton LodQualityButton => lodQuality;

        private void WireGraphics(bool wire)
        {
            Choice(frameRateLimit, FrameRateChanged, wire);
            Choice(vSync, VSyncChanged, wire);
            Choice(renderScale, RenderScaleChanged, wire);
            Choice(antiAliasing, AntiAliasingChanged, wire);
            Choice(postProcessing, PostProcessingChanged, wire);
            Choice(shadowQuality, ShadowQualityChanged, wire);
            Choice(shadowDistance, ShadowDistanceChanged, wire);
            Choice(textureQuality, TextureQualityChanged, wire);
            Choice(anisotropicFiltering, AnisotropicChanged, wire);
            Choice(lodQuality, LodChanged, wire);
        }

        private static void Choice(GameChoiceButton button, Action<int> listener, bool wire)
        {
            if (button == null) return;
            if (wire && !button.enabled) return;
            if (wire) button.ChoiceRequested += listener;
            else button.ChoiceRequested -= listener;
        }

        private void FrameRateChanged(int delta)
        {
            if (draft == null || draft.VSync) return;
            int[] values = { 30, 60, 120, -1 };
            draft.FrameRateLimit = values[Wrap(Array.IndexOf(values, draft.FrameRateLimit) + delta, values.Length)];
            RefreshChoiceLabels();
        }

        private void VSyncChanged(int delta) { if (draft == null) return; draft.VSync = !draft.VSync; RefreshChoiceLabels(); }
        private void RenderScaleChanged(int delta)
        {
            if (draft == null) return;
            draft.RenderScale = Mathf.Clamp(Mathf.Round((draft.RenderScale + delta * .05f) * 20f) / 20f, .5f, 1f);
            RefreshChoiceLabels();
        }
        private List<GraphicsAntiAliasing> AvailableAntiAliasing()
        {
            return host.Settings.RenderingCapabilities.AntiAliasingModes.Where(mode => draft.PostProcessing ||
                (mode != GraphicsAntiAliasing.FXAA && mode != GraphicsAntiAliasing.SMAA)).ToList();
        }
        private void AntiAliasingChanged(int delta)
        {
            if (draft == null) return;
            var values = AvailableAntiAliasing();
            if (values.Count == 0) return;
            draft.AntiAliasing = values[Wrap(values.IndexOf(draft.AntiAliasing) + delta, values.Count)];
            RefreshChoiceLabels();
        }
        private void PostProcessingChanged(int delta)
        {
            if (draft == null) return;
            draft.PostProcessing = !draft.PostProcessing;
            if (!draft.PostProcessing && (draft.AntiAliasing == GraphicsAntiAliasing.FXAA || draft.AntiAliasing == GraphicsAntiAliasing.SMAA))
                draft.AntiAliasing = GraphicsAntiAliasing.Off;
            RefreshChoiceLabels();
        }
        private void ShadowQualityChanged(int delta) { if (draft == null) return; draft.ShadowQuality = (GraphicsShadowQuality)Wrap((int)draft.ShadowQuality + delta, 4); RefreshChoiceLabels(); }
        private void ShadowDistanceChanged(int delta) { if (draft == null || draft.ShadowQuality == GraphicsShadowQuality.Off) return; draft.ShadowDistance = Mathf.Clamp(draft.ShadowDistance + delta * 5f, 5f, 100f); RefreshChoiceLabels(); }
        private void TextureQualityChanged(int delta) { if (draft == null) return; draft.TextureQuality = (GraphicsQuality)Wrap((int)draft.TextureQuality + delta, 3); RefreshChoiceLabels(); }
        private void AnisotropicChanged(int delta) { if (draft == null) return; draft.AnisotropicFiltering = (AnisotropicFiltering)Wrap((int)draft.AnisotropicFiltering + delta, 3); RefreshChoiceLabels(); }
        private void LodChanged(int delta) { if (draft == null) return; draft.LodQuality = (GraphicsQuality)Wrap((int)draft.LodQuality + delta, 3); RefreshChoiceLabels(); }

        private void RefreshGraphicsChoices()
        {
            if (draft == null) return;
            var support = host.Settings.RenderingCapabilities;
            SetGraphicsChoice("frameRateLimit", frameRateLimit, support.SupportsFrameRateLimit,
                "options.graphics.fps", !draft.VSync,
                draft.FrameRateLimit < 0 ? GameText.Text("options.value.unlimited") : draft.FrameRateLimit.ToString(),
                draft.VSync ? GameText.Text("options.value.vsync_suffix") : string.Empty);
            SetGraphicsChoice("vSync", vSync, support.SupportsVSync, "options.graphics.vsync", true, OnOff(draft.VSync));
            SetGraphicsChoice("renderScale", renderScale, support.SupportsRenderScale, "options.graphics.render_scale", true, draft.RenderScale * 100f);
            SetGraphicsChoice("antiAliasing", antiAliasing, support.AntiAliasingModes.Count > 1, "options.graphics.aa", true, AaLabel(draft.AntiAliasing));
            SetGraphicsChoice("postProcessing", postProcessing, support.SupportsPostProcessing, "options.graphics.post_processing", true, OnOff(draft.PostProcessing));
            SetGraphicsChoice("shadowQuality", shadowQuality, support.SupportsShadows, "options.graphics.shadows", true, QualityLabel(draft.ShadowQuality));
            SetGraphicsChoice("shadowDistance", shadowDistance, support.SupportsShadows, "options.graphics.shadow_distance", draft.ShadowQuality != GraphicsShadowQuality.Off,
                draft.ShadowDistance.ToString("0.###", UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale));
            SetGraphicsChoice("textureQuality", textureQuality, support.SupportsTextureQuality, "options.graphics.textures", true, QualityLabel(draft.TextureQuality));
            SetGraphicsChoice("anisotropicFiltering", anisotropicFiltering, support.SupportsAnisotropicFiltering, "options.graphics.anisotropic", true,
                AnisotropicLabel(draft.AnisotropicFiltering));
            SetGraphicsChoice("lodQuality", lodQuality, support.SupportsLod, "options.graphics.lod", true, QualityLabel(draft.LodQuality));
            RefreshGraphicsNavigation();
        }

        private void RefreshGraphicsNavigation()
        {
            var categories = quality != null ? quality.GetComponentInParent<GameGraphicsCategories>(true) : null;
            if (categories != null)
            {
                categories.RefreshNavigation();
                return;
            }
            var scroll = quality != null ? quality.GetComponentInParent<GameGraphicsScroll>(true) : null;
            if (scroll == null || !scroll.gameObject.activeInHierarchy) return;
            var rows = new[] { "resolution", "mode", "quality", "frameRateLimit", "vSync", "renderScale", "antiAliasing",
                "postProcessing", "shadowQuality", "shadowDistance", "textureQuality", "anisotropicFiltering", "lodQuality" }
                .Select(key => (UnityEngine.UI.Selectable)GetGraphicsDropdown(key) ?? GraphicsLegacyRow(key))
                .Where(row => row != null && row.IsActive() && row.IsInteractable()).ToArray();
            for (int i = 0; i < rows.Length; i++)
            {
                var nav = rows[i].navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? rows[i - 1] : scroll.Tab;
                nav.selectOnDown = i + 1 < rows.Length ? rows[i + 1] : back;
                rows[i].navigation = nav;
            }
            if (scroll.Tab != null)
            {
                var nav = scroll.Tab.navigation;
                nav.selectOnDown = rows.Length > 0 ? rows[0] : back;
                scroll.Tab.navigation = nav;
            }
            foreach (var button in new[] { apply, reset, back })
            {
                var nav = button.navigation;
                nav.selectOnUp = rows.Length > 0 ? rows[rows.Length - 1] : scroll.Tab;
                button.navigation = nav;
            }
        }

        private static string AaLabel(GraphicsAntiAliasing value) => value == GraphicsAntiAliasing.Off ?
            GameText.Text("options.value.off") : value.ToString().Replace("MSAA", "MSAA ");
        private static string QualityLabel<T>(T value) => GameText.Text("options.quality." + value);
        private static string OnOff(bool value) => GameText.Text(value ? "options.value.on" : "options.value.off");
        private void SetGraphicsChoice(string settingKey, GameChoiceButton button, bool available, string key,
            bool interactable = true, params object[] args)
        {
            if (button == null) return;
            if (RefreshGraphicsDropdown(settingKey, button, available, key, interactable, args)) return;
            button.gameObject.SetActive(available);
            button.interactable = interactable;
            Label(button, key, args);
        }
    }
}
