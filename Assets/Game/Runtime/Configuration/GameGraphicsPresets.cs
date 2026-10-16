using System;
using Lumivale.Core.Settings;
using UnityEngine;

namespace Lumivale.Start.Game
{
    public enum GameGraphicsPreset
    {
        Low,
        Medium,
        High,
        Custom
    }

    /// <summary>Starter rendering policy. Display, frame pacing, audio and language stay independent.</summary>
    public static class GameGraphicsPresets
    {
        public static SettingsValues Apply(SettingsValues values, GameGraphicsPreset preset,
            RenderingCapabilities capabilities = null)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (!Enum.IsDefined(typeof(GameGraphicsPreset), preset))
                throw new ArgumentOutOfRangeException(nameof(preset));
            SettingsValues result = values.Clone();
            if (preset == GameGraphicsPreset.Custom) return result;

            bool low = preset == GameGraphicsPreset.Low;
            bool high = preset == GameGraphicsPreset.High;
            if (capabilities == null || capabilities.SupportsRenderScale) result.RenderScale = low ? 0.75f : 1f;
            if (capabilities == null || capabilities.SupportsPostProcessing) result.PostProcessing = !low;
            GraphicsAntiAliasing aa = low ? GraphicsAntiAliasing.Off : high ? GraphicsAntiAliasing.SMAA : GraphicsAntiAliasing.FXAA;
            result.AntiAliasing = Supports(capabilities, aa) ? aa : GraphicsAntiAliasing.Off;
            if (capabilities == null || capabilities.SupportsShadows)
            {
                result.ShadowQuality = low ? GraphicsShadowQuality.Low : high ? GraphicsShadowQuality.High : GraphicsShadowQuality.Medium;
                result.ShadowDistance = low ? 20f : high ? 50f : 35f;
            }
            if (capabilities == null || capabilities.SupportsTextureQuality)
                result.TextureQuality = low ? GraphicsQuality.Low : GraphicsQuality.High;
            if (capabilities == null || capabilities.SupportsAnisotropicFiltering)
                result.AnisotropicFiltering = low ? AnisotropicFiltering.Disable : high ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            if (capabilities == null || capabilities.SupportsLod)
                result.LodQuality = low ? GraphicsQuality.Low : high ? GraphicsQuality.High : GraphicsQuality.Medium;
            return result;
        }

        public static GameGraphicsPreset Classify(SettingsValues values, RenderingCapabilities capabilities = null)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            foreach (GameGraphicsPreset preset in new[] { GameGraphicsPreset.Low, GameGraphicsPreset.Medium, GameGraphicsPreset.High })
            {
                SettingsValues expected = Apply(values, preset, capabilities);
                if (Mathf.Approximately(values.RenderScale, expected.RenderScale) &&
                    values.AntiAliasing == expected.AntiAliasing && values.PostProcessing == expected.PostProcessing &&
                    values.ShadowQuality == expected.ShadowQuality && Mathf.Approximately(values.ShadowDistance, expected.ShadowDistance) &&
                    values.TextureQuality == expected.TextureQuality && values.AnisotropicFiltering == expected.AnisotropicFiltering &&
                    values.LodQuality == expected.LodQuality)
                    return preset;
            }
            return GameGraphicsPreset.Custom;
        }

        private static bool Supports(RenderingCapabilities capabilities, GraphicsAntiAliasing value)
        {
            if (capabilities == null) return true;
            foreach (GraphicsAntiAliasing mode in capabilities.AntiAliasingModes)
                if (mode == value) return true;
            return false;
        }
    }
}
