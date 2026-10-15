using System;
using Lumivale.Core.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Lumivale.Start.Game
{
    /// <summary>Starter binding presentation; stable binding ids target the Input runtime clone.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "ProductRebindRow")]
    public sealed class GameRebindRow : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private string actionPath;
        [SerializeField] private string bindingId;
        [SerializeField] private string controlsPath;
        [SerializeField] private UnityEngine.UI.Button rebindButton;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private TextMeshProUGUI bindingLabel;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        private GameObject lastSelection;
        private string bindingDisplay;
        private UnityEngine.UI.Outline captureOutline;
        private Color idleOutlineColor;
        private Vector2 idleOutlineDistance;
        private bool idleOutlineEnabled;

        public string ActionPath => actionPath;
        public Guid BindingId => Guid.Parse(bindingId);
        public string ControlsPath => controlsPath;
        public UnityEngine.UI.Button Button => rebindButton;
        public bool IsCapturing { get; private set; }
        public event Action<GameRebindRow> RebindRequested;

        public void Configure(string path, Guid id, string matchingControls,
            UnityEngine.UI.Button button, TextMeshProUGUI actionText,
            TextMeshProUGUI bindingText, UnityEngine.UI.ScrollRect scrollView)
        {
            SetCapture(false);
            if (rebindButton != null) rebindButton.onClick.RemoveListener(RequestRebind);
            actionPath = path;
            bindingId = id.ToString();
            controlsPath = matchingControls;
            rebindButton = button;
            actionLabel = actionText;
            bindingLabel = bindingText;
            scroll = scrollView;
            captureOutline = null;
            bindingDisplay = bindingText != null ? bindingText.text : null;
            if (isActiveAndEnabled && rebindButton != null) rebindButton.onClick.AddListener(RequestRebind);
        }

        public void Refresh(InputService input)
        {
            if (input == null || !input.IsInitialized || bindingLabel == null) return;
            var action = input.GetAction(actionPath);
            for (int index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (binding.id != BindingId) continue;
                bindingDisplay = string.IsNullOrEmpty(binding.effectivePath) ? "Unbound" : action.GetBindingDisplayString(index);
                if (string.IsNullOrEmpty(bindingDisplay)) bindingDisplay = binding.effectivePath;
                RefreshBindingText();
                if (actionLabel != null)
                {
                    string device = controlsPath.StartsWith("<Gamepad>", StringComparison.Ordinal) ? "Gamepad" :
                        controlsPath.StartsWith("<Mouse>", StringComparison.Ordinal) ? "Mouse" : "Keyboard";
                    actionLabel.text = action.name + (binding.isPartOfComposite ? " / " + binding.name : "") + "  ·  " + device;
                }
                return;
            }
            bindingDisplay = "Binding unavailable";
            RefreshBindingText();
            if (rebindButton != null) rebindButton.interactable = false;
        }

        /// <summary>Marks only this cell while InputService owns the interactive capture.</summary>
        public void SetCapture(bool capturing)
        {
            if (IsCapturing == capturing) return;
            if (capturing)
            {
                if (bindingDisplay == null && bindingLabel != null) bindingDisplay = bindingLabel.text;
                captureOutline = rebindButton != null ? rebindButton.GetComponent<UnityEngine.UI.Outline>() : null;
                if (captureOutline != null)
                {
                    idleOutlineColor = captureOutline.effectColor;
                    idleOutlineDistance = captureOutline.effectDistance;
                    idleOutlineEnabled = captureOutline.enabled;
                    captureOutline.effectColor = Color.white;
                    captureOutline.effectDistance = new Vector2(2f, -2f);
                    captureOutline.enabled = true;
                }
            }
            else if (captureOutline != null)
            {
                captureOutline.effectColor = idleOutlineColor;
                captureOutline.effectDistance = idleOutlineDistance;
                captureOutline.enabled = idleOutlineEnabled;
            }
            IsCapturing = capturing;
            RefreshBindingText();
        }

        private void RefreshBindingText()
        {
            if (bindingLabel != null && (IsCapturing || bindingDisplay != null))
                bindingLabel.text = IsCapturing ? "Listening\u2026" : bindingDisplay;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (scroll == null || scroll.viewport == null || scroll.content == null || !scroll.vertical) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
            Rect viewport = scroll.viewport.rect;
            float hiddenHeight = scroll.content.rect.height - viewport.height;
            if (hiddenHeight <= 0f) return;
            float delta = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
                bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0f;
            if (Mathf.Abs(delta) < 0.01f) return;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - delta / hiddenHeight);
        }

        // A row may wrap the selectable button rather than live on the button itself.
        // Selection events do not bubble to parents; observe owned child selection once.
        private void LateUpdate()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelection) return;
            lastSelection = selected;
            if (selected != null && selected.transform.IsChildOf(transform)) OnSelect(null);
        }

        private void OnEnable()
        {
            if (rebindButton != null) rebindButton.onClick.AddListener(RequestRebind);
        }

        private void OnDisable()
        {
            SetCapture(false);
            lastSelection = null;
            if (rebindButton != null) rebindButton.onClick.RemoveListener(RequestRebind);
        }

        private void RequestRebind() => RebindRequested?.Invoke(this);
    }
}
