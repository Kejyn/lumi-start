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
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        private Canvas gameplayCanvas;
        private bool gameplayCanvasEnabled;
        private bool ownsCanvasGate;

        public void Configure(GameSceneController sceneNavigation, GameObject root, PromptReference[] references)
        {
            navigation = sceneNavigation;
            canvasRoot = root;
            prompts = references ?? Array.Empty<PromptReference>();
        }

        private void Awake() => GateGameplayCanvas();
        private void Start() { started = true; Bind(); }
        private void OnEnable() { if (started) Bind(); }

        private async void Bind()
        {
            int version = ++bindVersion;
            GateGameplayCanvas();
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
                LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
                ready = true;
                RefreshPrompts();
                RestoreGameplayCanvas();
            }
            catch (OperationCanceledException) { if (version == bindVersion) Release(); }
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
            if (!ready || host == null || !host.Input.IsInitialized || !GameText.IsReady) return;
            foreach (PromptReference prompt in prompts)
            {
                if (prompt == null || prompt.Label == null) continue;
                string template = host.Input.ActiveDeviceFamily == InputDeviceFamily.Gamepad
                    ? prompt.GamepadTemplate : prompt.KeyboardMouseTemplate;
                prompt.Label.text = Render(GameText.Text(template ?? string.Empty), host.Input);
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
                            string label = GetBindingLabel(action, i);
                            if (!parts.Contains(label)) parts.Add(label);
                        }
                    }
                    if (parts.Count != 0) return string.Join(" / ", parts);
                }
                else if (!binding.isPartOfComposite && MatchesFamily(binding.effectivePath, input.ActiveDeviceFamily))
                    return GetBindingLabel(action, i);
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

        /// <summary>Displays a physical binding without translating its internal path or identifier.</summary>
        public static string GetBindingLabel(InputAction action, int index)
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
                    case "leftstick": return GameText.Text("binding.control.left_stick");
                    case "rightstick": return GameText.Text("binding.control.right_stick");
                    case "dpad": return GameText.Text("binding.control.dpad");
                    case "leftstick/up": return GameText.Text("binding.control.left_stick_up");
                    case "leftstick/down": return GameText.Text("binding.control.left_stick_down");
                    case "leftstick/left": return GameText.Text("binding.control.left_stick_left");
                    case "leftstick/right": return GameText.Text("binding.control.left_stick_right");
                    case "leftstick/x": return GameText.Text("binding.control.left_stick_x");
                    case "leftstick/y": return GameText.Text("binding.control.left_stick_y");
                    case "rightstick/up": return GameText.Text("binding.control.right_stick_up");
                    case "rightstick/down": return GameText.Text("binding.control.right_stick_down");
                    case "rightstick/left": return GameText.Text("binding.control.right_stick_left");
                    case "rightstick/right": return GameText.Text("binding.control.right_stick_right");
                    case "rightstick/x": return GameText.Text("binding.control.right_stick_x");
                    case "rightstick/y": return GameText.Text("binding.control.right_stick_y");
                    case "dpad/up": return GameText.Text("binding.control.dpad_up");
                    case "dpad/down": return GameText.Text("binding.control.dpad_down");
                    case "dpad/left": return GameText.Text("binding.control.dpad_left");
                    case "dpad/right": return GameText.Text("binding.control.dpad_right");
                    case "dpad/x": return GameText.Text("binding.control.dpad_x");
                    case "dpad/y": return GameText.Text("binding.control.dpad_y");
                }
            }
            if (!string.IsNullOrEmpty(layout) && InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse"))
            {
                switch (name)
                {
                    case "leftbutton": return GameText.Text("binding.control.mouse_left");
                    case "rightbutton": return GameText.Text("binding.control.mouse_right");
                    case "middlebutton": return GameText.Text("binding.control.mouse_middle");
                    case "scroll": return GameText.Text("binding.control.mouse_wheel");
                }
            }
            if (!string.IsNullOrEmpty(layout) && InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard"))
            {
                switch (name)
                {
                    case "space": return GameText.Text("binding.control.space");
                    case "enter": return GameText.Text("binding.control.enter");
                    case "numpadenter": return GameText.Text("binding.control.numpad_enter");
                    case "escape": return GameText.Text("binding.control.escape");
                    case "backspace": return GameText.Text("binding.control.backspace");
                    case "tab": return GameText.Text("binding.control.tab");
                    case "uparrow": return GameText.Text("binding.control.arrow_up");
                    case "downarrow": return GameText.Text("binding.control.arrow_down");
                    case "leftarrow": return GameText.Text("binding.control.arrow_left");
                    case "rightarrow": return GameText.Text("binding.control.arrow_right");
                    case "leftshift": return GameText.Text("binding.control.shift_left");
                    case "rightshift": return GameText.Text("binding.control.shift_right");
                    case "leftctrl": return GameText.Text("binding.control.ctrl_left");
                    case "rightctrl": return GameText.Text("binding.control.ctrl_right");
                    case "leftalt": return GameText.Text("binding.control.alt_left");
                    case "rightalt": return GameText.Text("binding.control.alt_right");
                    case "home": return GameText.Text("binding.control.home");
                    case "end": return GameText.Text("binding.control.end");
                    case "pageup": return GameText.Text("binding.control.page_up");
                    case "pagedown": return GameText.Text("binding.control.page_down");
                    case "insert": return GameText.Text("binding.control.insert");
                    case "delete": return GameText.Text("binding.control.delete");
                    case "capslock": return GameText.Text("binding.control.caps_lock");
                    case "numlock": return GameText.Text("binding.control.num_lock");
                    case "printscreen": return GameText.Text("binding.control.print_screen");
                    case "scrolllock": return GameText.Text("binding.control.scroll_lock");
                    case "pause": return GameText.Text("binding.control.pause");
                    case "leftmeta": return GameText.Text("binding.control.meta_left");
                    case "rightmeta": return GameText.Text("binding.control.meta_right");
                    case "contextmenu": return GameText.Text("binding.control.context_menu");
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
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            if (host != null)
            {
                host.Input.ActiveDeviceChanged -= DeviceChanged;
                host.Input.BindingsChanged -= RefreshPrompts;
            }
            rememberedSelections.Clear();
            screens = Array.Empty<UiScreen>();
            host = null;
            RestoreGameplayCanvas();
        }

        private void GateGameplayCanvas()
        {
            if (ownsCanvasGate || navigation == null || !navigation.IsGameplay || canvasRoot == null) return;
            gameplayCanvas = canvasRoot.GetComponent<Canvas>();
            if (gameplayCanvas == null) return;
            gameplayCanvasEnabled = gameplayCanvas.enabled;
            ownsCanvasGate = true;
            gameplayCanvas.enabled = false;
        }

        private void RestoreGameplayCanvas()
        {
            if (!ownsCanvasGate) return;
            ownsCanvasGate = false;
            if (gameplayCanvas != null) gameplayCanvas.enabled = gameplayCanvasEnabled;
            gameplayCanvas = null;
        }

        private void LocaleChanged(Locale locale) => RefreshPrompts();

        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
