using System;
using System.Collections.Generic;
using System.Linq;
using Lumivale.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Lumivale.Start.Game
{
    public sealed partial class GameSettingsController
    {
        [Serializable]
        private sealed class GraphicsDropdownBinding
        {
            public string Key;
            public TMP_Dropdown Dropdown;
            [NonSerialized] public UnityAction<int> Listener;
            [NonSerialized] public List<GraphicsDropdownOption> Options;
        }

        private sealed class GraphicsDropdownOption
        {
            public object Value;
            public string Label;
            public bool Selectable;
            public GraphicsDropdownOption(object value, string label, bool selectable = true)
            { Value = value; Label = label; Selectable = selectable; }
        }

        [SerializeField] private GraphicsDropdownBinding[] graphicsDropdowns = Array.Empty<GraphicsDropdownBinding>();

        public bool IsAnyDropdownOpen => IsLanguageDropdownOpen ||
            graphicsDropdowns.Any(binding => binding?.Dropdown != null && binding.Dropdown.IsExpanded);

        public TMP_Dropdown GetGraphicsDropdown(string key)
        {
            string canonical = GraphicsDropdownKey(key);
            return graphicsDropdowns.FirstOrDefault(binding => binding != null && binding.Key == canonical)?.Dropdown;
        }

        /// <summary>Associates a native selector with its stable setting key without changing the draft.</summary>
        public void ConfigureGraphicsDropdown(string key, TMP_Dropdown dropdown)
        {
            string canonical = GraphicsDropdownKey(key);
            var legacy = GraphicsLegacyRow(canonical);
            if (legacy == null) throw new ArgumentException("Unknown graphics dropdown key: " + key, nameof(key));
            var binding = graphicsDropdowns.FirstOrDefault(candidate => candidate != null && candidate.Key == canonical);
            if (binding == null)
            {
                binding = new GraphicsDropdownBinding { Key = canonical };
                var bindings = new List<GraphicsDropdownBinding>(graphicsDropdowns) { binding };
                graphicsDropdowns = bindings.ToArray();
            }
            if (binding.Dropdown != null && binding.Listener != null)
                binding.Dropdown.onValueChanged.RemoveListener(binding.Listener);
            binding.Dropdown = dropdown;
            if (dropdown != null) legacy.enabled = false;
            if (ready) WireGraphicsDropdown(binding, true);
        }

        public bool CloseDropdowns(bool restoreFocus = true)
        {
            bool closed = CloseLanguageDropdown(restoreFocus);
            foreach (var binding in graphicsDropdowns)
                if (binding?.Dropdown != null) closed |= HideDropdown(binding.Dropdown, restoreFocus);
            return closed;
        }

        private static bool HideDropdown(TMP_Dropdown dropdown, bool restoreFocus)
        {
            if (dropdown == null || !dropdown.IsExpanded) return false;
            var selected = EventSystem.current?.currentSelectedGameObject;
            dropdown.Hide();
            if (!restoreFocus && EventSystem.current != null)
            {
                if (selected != null && selected.transform.IsChildOf(dropdown.transform)) selected = null;
                EventSystem.current.SetSelectedGameObject(selected);
            }
            return true;
        }

        private static string GraphicsDropdownKey(string key)
        {
            switch (key)
            {
                case "display": return "mode";
                case "preset": return "quality";
                case "fps": return "frameRateLimit";
                case "vsync": return "vSync";
                case "aa": return "antiAliasing";
                case "anisotropic": return "anisotropicFiltering";
                case "lod": return "lodQuality";
                default: return key;
            }
        }

        private GameChoiceButton GraphicsLegacyRow(string key)
        {
            switch (key)
            {
                case "resolution": return resolution;
                case "mode": return mode;
                case "quality": return quality;
                case "frameRateLimit": return frameRateLimit;
                case "vSync": return vSync;
                case "renderScale": return renderScale;
                case "antiAliasing": return antiAliasing;
                case "postProcessing": return postProcessing;
                case "shadowQuality": return shadowQuality;
                case "shadowDistance": return shadowDistance;
                case "textureQuality": return textureQuality;
                case "anisotropicFiltering": return anisotropicFiltering;
                case "lodQuality": return lodQuality;
                default: return null;
            }
        }

        private void WireGraphicsDropdowns(bool wire)
        {
            foreach (var binding in graphicsDropdowns) WireGraphicsDropdown(binding, wire);
        }

        private void WireGraphicsDropdown(GraphicsDropdownBinding binding, bool wire)
        {
            if (binding?.Dropdown == null) return;
            if (binding.Listener != null) binding.Dropdown.onValueChanged.RemoveListener(binding.Listener);
            if (!wire) return;
            if (binding.Listener == null) binding.Listener = index => GraphicsDropdownSelected(binding, index);
            binding.Dropdown.onValueChanged.AddListener(binding.Listener);
        }

        private void GraphicsDropdownSelected(GraphicsDropdownBinding binding, int index)
        {
            if (!CanInteract || !IsOpen || draft == null || binding.Options == null ||
                index < 0 || index >= binding.Options.Count || !binding.Dropdown.IsInteractable()) return;
            var option = binding.Options[index];
            if (!option.Selectable) { RefreshChoiceLabels(); return; }
            switch (binding.Key)
            {
                case "resolution":
                    var selectedResolution = (DisplayResolution)option.Value;
                    draft.Width = selectedResolution.Width; draft.Height = selectedResolution.Height; break;
                case "mode": draft.DisplayMode = (FullScreenMode)option.Value; break;
                case "quality": draft = GameGraphicsPresets.Apply(draft, (GameGraphicsPreset)option.Value,
                    host.Settings.RenderingCapabilities); break;
                case "frameRateLimit": draft.FrameRateLimit = (int)option.Value; break;
                case "vSync": draft.VSync = (bool)option.Value; break;
                case "renderScale": draft.RenderScale = (float)option.Value; break;
                case "antiAliasing": draft.AntiAliasing = (GraphicsAntiAliasing)option.Value; break;
                case "postProcessing":
                    draft.PostProcessing = (bool)option.Value;
                    if (!draft.PostProcessing && (draft.AntiAliasing == GraphicsAntiAliasing.FXAA ||
                        draft.AntiAliasing == GraphicsAntiAliasing.SMAA)) draft.AntiAliasing = GraphicsAntiAliasing.Off;
                    break;
                case "shadowQuality": draft.ShadowQuality = (GraphicsShadowQuality)option.Value; break;
                case "shadowDistance": draft.ShadowDistance = (float)option.Value; break;
                case "textureQuality": draft.TextureQuality = (GraphicsQuality)option.Value; break;
                case "anisotropicFiltering": draft.AnisotropicFiltering = (AnisotropicFiltering)option.Value; break;
                case "lodQuality": draft.LodQuality = (GraphicsQuality)option.Value; break;
            }
            RefreshChoiceLabels();
        }

        private bool RefreshGraphicsDropdown(string key, GameChoiceButton row, bool available,
            string captionKey, bool interactable, params object[] captionArguments)
        {
            var binding = graphicsDropdowns.FirstOrDefault(candidate => candidate != null && candidate.Key == key);
            if (binding?.Dropdown == null) return false;
            row.gameObject.SetActive(available);
            row.interactable = interactable;
            binding.Dropdown.interactable = interactable;
            if (!available) return true;
            binding.Options = GraphicsOptions(key);
            object selected = GraphicsValue(key);
            int index = binding.Options.FindIndex(option => Equals(option.Value, selected));
            var labels = binding.Options.Select(option => option.Label).ToList();
            if (!binding.Dropdown.options.Select(option => option.text).SequenceEqual(labels))
            {
                binding.Dropdown.ClearOptions();
                binding.Dropdown.AddOptions(labels);
            }
            binding.Dropdown.SetValueWithoutNotify(Math.Max(0, index));
            if (binding.Dropdown.IsExpanded)
            {
                var visibleOptions = binding.Dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>(false);
                if (visibleOptions.Length == labels.Count)
                    for (int optionIndex = 0; optionIndex < visibleOptions.Length; optionIndex++)
                    {
                        var text = visibleOptions[optionIndex].GetComponentInChildren<TMP_Text>();
                        if (text != null) text.text = labels[optionIndex];
                    }
            }
            // RefreshShownValue supplies only the chosen option. The official localizer owns
            // the full caption so all selectors use the same label/value presentation.
            GameText.Set(binding.Dropdown.captionText, captionKey, captionArguments);
            return true;
        }

        private object GraphicsValue(string key)
        {
            switch (key)
            {
                case "resolution": return new DisplayResolution(draft.Width, draft.Height);
                case "mode": return draft.DisplayMode;
                case "quality": return GameGraphicsPresets.Classify(draft, host.Settings.RenderingCapabilities);
                case "frameRateLimit": return draft.FrameRateLimit;
                case "vSync": return draft.VSync;
                case "renderScale": return draft.RenderScale;
                case "antiAliasing": return draft.AntiAliasing;
                case "postProcessing": return draft.PostProcessing;
                case "shadowQuality": return draft.ShadowQuality;
                case "shadowDistance": return draft.ShadowDistance;
                case "textureQuality": return draft.TextureQuality;
                case "anisotropicFiltering": return draft.AnisotropicFiltering;
                case "lodQuality": return draft.LodQuality;
                default: throw new ArgumentException("Unknown graphics dropdown key: " + key);
            }
        }

        private List<GraphicsDropdownOption> GraphicsOptions(string key)
        {
            var options = new List<GraphicsDropdownOption>();
            switch (key)
            {
                case "resolution":
                    foreach (var value in host.Settings.Capabilities.Resolutions)
                        options.Add(new GraphicsDropdownOption(value, value.Width + " x " + value.Height));
                    break;
                case "mode":
                    foreach (var value in host.Settings.Capabilities.Modes)
                        options.Add(new GraphicsDropdownOption(value, GameText.Text("options.display." + value)));
                    break;
                case "quality":
                    foreach (GameGraphicsPreset value in new[] { GameGraphicsPreset.Low, GameGraphicsPreset.Medium, GameGraphicsPreset.High })
                        options.Add(new GraphicsDropdownOption(value, QualityLabel(value)));
                    if (GameGraphicsPresets.Classify(draft, host.Settings.RenderingCapabilities) == GameGraphicsPreset.Custom)
                        options.Add(new GraphicsDropdownOption(GameGraphicsPreset.Custom, QualityLabel(GameGraphicsPreset.Custom), false));
                    break;
                case "frameRateLimit":
                    foreach (int value in new[] { 30, 60, 120, -1 })
                        options.Add(new GraphicsDropdownOption(value, value < 0 ? GameText.Text("options.value.unlimited") : value.ToString()));
                    break;
                case "vSync": case "postProcessing":
                    options.Add(new GraphicsDropdownOption(false, OnOff(false)));
                    options.Add(new GraphicsDropdownOption(true, OnOff(true)));
                    break;
                case "renderScale":
                    var scales = Enumerable.Range(10, 11).Select(index => index / 20f).ToList();
                    if (!scales.Contains(draft.RenderScale)) scales.Add(draft.RenderScale);
                    foreach (float value in scales.OrderBy(value => value))
                        options.Add(new GraphicsDropdownOption(value, GameText.Text("options.value.percentage", value * 100f)));
                    break;
                case "antiAliasing":
                    foreach (var value in AvailableAntiAliasing()) options.Add(new GraphicsDropdownOption(value, AaLabel(value)));
                    break;
                case "shadowQuality":
                    foreach (GraphicsShadowQuality value in Enum.GetValues(typeof(GraphicsShadowQuality)))
                        options.Add(new GraphicsDropdownOption(value, QualityLabel(value)));
                    break;
                case "shadowDistance":
                    var distances = Enumerable.Range(1, 20).Select(index => index * 5f).ToList();
                    if (!distances.Contains(draft.ShadowDistance)) distances.Add(draft.ShadowDistance);
                    foreach (float value in distances.OrderBy(value => value))
                        options.Add(new GraphicsDropdownOption(value, GameText.Text("options.value.distance", value)));
                    break;
                case "textureQuality": case "lodQuality":
                    foreach (GraphicsQuality value in Enum.GetValues(typeof(GraphicsQuality)))
                        options.Add(new GraphicsDropdownOption(value, QualityLabel(value)));
                    break;
                case "anisotropicFiltering":
                    foreach (AnisotropicFiltering value in Enum.GetValues(typeof(AnisotropicFiltering)))
                        options.Add(new GraphicsDropdownOption(value, AnisotropicLabel(value)));
                    break;
            }
            return options;
        }

        private static string AnisotropicLabel(AnisotropicFiltering value) => GameText.Text(
            value == AnisotropicFiltering.Disable ? "options.value.off" :
            value == AnisotropicFiltering.Enable ? "options.value.per_texture" : "options.value.forced_on");

        private void LateUpdate()
        {
            // TMP_Dropdown exposes a native list of Toggles rather than per-option flags.
            // Mark the derived Custom entry read-only before the next input frame, without
            // substituting a custom dropdown implementation or changing the preference.
            foreach (var binding in graphicsDropdowns)
            {
                if (binding?.Dropdown == null || !binding.Dropdown.IsExpanded || binding.Options == null ||
                    binding.Options.All(option => option.Selectable)) continue;
                var toggles = binding.Dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>(false);
                if (toggles.Length != binding.Options.Count) continue;
                for (int index = 0; index < toggles.Length; index++)
                    toggles[index].interactable = binding.Options[index].Selectable;
                var selectable = toggles.Where(toggle => toggle.interactable).ToArray();
                for (int index = 0; index < selectable.Length; index++)
                {
                    var navigation = selectable[index].navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                    navigation.selectOnUp = index > 0 ? selectable[index - 1] : null;
                    navigation.selectOnDown = index + 1 < selectable.Length ? selectable[index + 1] : null;
                    navigation.selectOnLeft = navigation.selectOnUp;
                    navigation.selectOnRight = navigation.selectOnDown;
                    selectable[index].navigation = navigation;
                }
                if (EventSystem.current == null) continue;
                var selected = toggles.FirstOrDefault(toggle => toggle.gameObject == EventSystem.current.currentSelectedGameObject);
                if (selected != null && !selected.interactable)
                {
                    var first = selectable.FirstOrDefault();
                    if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
                }
            }
        }
    }
}
