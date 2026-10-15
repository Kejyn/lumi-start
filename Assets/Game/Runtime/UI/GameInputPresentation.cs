using System;
using System.Collections.Generic;
using System.Text;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.UI;
using Lumivale.Start.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumivale.Start.Game
{
    /// <summary>Starter-owned text prompts and controller focus; bindings remain owned by Input.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "ProductInputPresentation")]
    public sealed class GameInputPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class PromptReference
        {
            public TMP_Text Label;
            [TextArea] public string KeyboardMouseTemplate;
            [TextArea] public string GamepadTemplate;
        }

        [SerializeField] private GameSceneController navigation;
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private PromptReference[] prompts = Array.Empty<PromptReference>();
        private readonly Dictionary<UiScreen, GameObject> rememberedSelections = new Dictionary<UiScreen, GameObject>();
        private GameBootstrap host;
        private UiScreen[] screens = Array.Empty<UiScreen>();
        private bool started;
        private bool ready;
        private int bindVersion;

        public void Configure(GameSceneController sceneNavigation, GameObject root, PromptReference[] references)
        {
            navigation = sceneNavigation;
            canvasRoot = root;
            prompts = references ?? Array.Empty<PromptReference>();
        }

        private void Start() { started = true; Bind(); }
        private void OnEnable() { if (started) Bind(); }

        private async void Bind()
        {
            int version = ++bindVersion;
            try
            {
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null || navigation == null || canvasRoot == null)
                    throw new InvalidOperationException("Configure the product input presentation with its navigation and Canvas.");
                await host.Ready;
                while (IsCurrent(version) && host.IsReady && !navigation.IsReady)
                    await Awaitable.NextFrameAsync();
                if (!IsCurrent(version) || !host.IsReady) return;
                screens = canvasRoot.GetComponentsInChildren<UiScreen>(true);
                host.Input.ActiveDeviceChanged += DeviceChanged;
                host.Input.BindingsChanged += RefreshPrompts;
                ready = true;
                RefreshPrompts();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (version == bindVersion) { Release(); Debug.LogException(exception, this); }
            }
        }

        private bool IsCurrent(int version) => this != null && isActiveAndEnabled && version == bindVersion;

        private void DeviceChanged(InputDeviceFamily family)
        {
            RefreshPrompts();
            if (family == InputDeviceFamily.Gamepad) RestoreControllerFocus();
        }

        private void RefreshPrompts()
        {
            if (!ready || host == null || !host.Input.IsInitialized) return;
            foreach (PromptReference prompt in prompts)
            {
                if (prompt == null || prompt.Label == null) continue;
                string template = host.Input.ActiveDeviceFamily == InputDeviceFamily.Gamepad
                    ? prompt.GamepadTemplate : prompt.KeyboardMouseTemplate;
                prompt.Label.text = Render(template ?? string.Empty, host.Input);
            }
        }

        private static string Render(string template, InputService input)
        {
            var result = new StringBuilder(template.Length);
            int position = 0;
            while (position < template.Length)
            {
                int start = template.IndexOf('{', position);
                if (start < 0) { result.Append(template, position, template.Length - position); break; }
                int end = template.IndexOf('}', start + 1);
                if (end < 0) { result.Append(template, position, template.Length - position); break; }
                result.Append(template, position, start - position);
                result.Append(GetActionLabel(input, template.Substring(start + 1, end - start - 1)));
                position = end + 1;
            }
            return result.ToString();
        }

        /// <summary>Displays the first effective binding for the active device family, including overrides.</summary>
        public static string GetActionLabel(InputService input, string actionPath)
        {
            if (input == null || !input.IsInitialized || string.IsNullOrEmpty(actionPath)) return "—";
            InputAction action = input.RuntimeAsset.FindAction(actionPath, false);
            if (action == null) return "—";
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite)
                {
                    var parts = new List<string>();
                    while (i + 1 < action.bindings.Count && action.bindings[i + 1].isPartOfComposite)
                    {
                        i++;
                        if (MatchesFamily(action.bindings[i].effectivePath, input.ActiveDeviceFamily))
                        {
                            string label = BindingLabel(action, i);
                            if (!parts.Contains(label)) parts.Add(label);
                        }
                    }
                    if (parts.Count != 0) return string.Join(" / ", parts);
                }
                else if (!binding.isPartOfComposite && MatchesFamily(binding.effectivePath, input.ActiveDeviceFamily))
                    return BindingLabel(action, i);
            }
            return "—";
        }

        private static bool MatchesFamily(string path, InputDeviceFamily family)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string layout = InputControlPath.TryGetDeviceLayout(path);
            if (string.IsNullOrEmpty(layout)) return false;
            if (family == InputDeviceFamily.Gamepad)
                return InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad");
            return InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard") ||
                InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse");
        }

        private static string BindingLabel(InputAction action, int index)
        {
            string display = action.GetBindingDisplayString(index, out string layout, out string control,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            string name = (control ?? string.Empty).ToLowerInvariant();
            if (!string.IsNullOrEmpty(layout) && InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad"))
            {
                // Minimal Xbox-style text for the corresponding effective controls; no image assets.
                switch (name)
                {
                    case "buttonsouth": return "A";
                    case "buttoneast": return "B";
                    case "buttonwest": return "X";
                    case "buttonnorth": return "Y";
                    case "leftshoulder": return "LB";
                    case "rightshoulder": return "RB";
                    case "lefttrigger": return "LT";
                    case "righttrigger": return "RT";
                    case "start": case "startbutton": return "START";
                    case "select": return "BACK";
                    case "leftstick": return "L-STICK";
                    case "rightstick": return "R-STICK";
                    case "dpad": return "D-PAD";
                }
            }
            if (!string.IsNullOrEmpty(layout) && InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse"))
            {
                switch (name)
                {
                    case "leftbutton": return "LMB";
                    case "rightbutton": return "RMB";
                    case "middlebutton": return "MMB";
                    case "scroll": return "WHEEL";
                }
            }
            return string.IsNullOrWhiteSpace(display) ? "—" : display;
        }

        private UiScreen ActiveScreen()
        {
            if (!ready || host == null || !host.IsReady) return null;
            foreach (UiScreen screen in screens)
                if (screen != null && screen.CanvasGroup != null && screen.CanvasGroup.interactable &&
                    host.UI.IsOpen(screen.Id)) return screen;
            return null;
        }

        private static bool CanSelect(GameObject candidate, UiScreen screen)
        {
            if (candidate == null || screen == null || !candidate.transform.IsChildOf(screen.transform)) return false;
            Selectable selectable = candidate.GetComponent<Selectable>();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }

        private void RestoreControllerFocus()
        {
            if (!ready || host == null || !host.IsReady || host.Input.IsSuspended || host.Input.IsRebinding ||
                EventSystem.current == null) return;
            UiScreen active = ActiveScreen();
            if (active == null || CanSelect(EventSystem.current.currentSelectedGameObject, active)) return;
            rememberedSelections.TryGetValue(active, out GameObject candidate);
            if (!CanSelect(candidate, active)) candidate = active.FirstSelected != null ? active.FirstSelected.gameObject : null;
            if (CanSelect(candidate, active)) EventSystem.current.SetSelectedGameObject(candidate);
        }

        private void LateUpdate()
        {
            if (EventSystem.current == null) return;
            UiScreen active = ActiveScreen();
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (CanSelect(selected, active)) rememberedSelections[active] = selected;
        }

        private void Release()
        {
            ++bindVersion;
            ready = false;
            if (host != null)
            {
                host.Input.ActiveDeviceChanged -= DeviceChanged;
                host.Input.BindingsChanged -= RefreshPrompts;
            }
            rememberedSelections.Clear();
            screens = Array.Empty<UiScreen>();
            host = null;
        }

        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
