using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>Fits the direct buttons of one vertical group to its longest visible label.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.VerticalLayoutGroup))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "GameButtonGroup")]
    public sealed class GameButtonGroup : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Total horizontal space around the label, in Canvas units.")]
        private float horizontalPadding = 48f;
        [SerializeField, Min(0f)] private float minimumWidth = 120f;
        [SerializeField, Min(0f), Tooltip("Zero uses the available width of this group.")]
        private float maximumWidth;

        private readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
        private UnityEngine.UI.VerticalLayoutGroup layout;
        private RectTransform groupRect;
        private bool dirty = true;

        public void Configure(float padding = 48f, float minimum = 120f, float maximum = 0f)
        {
            horizontalPadding = Mathf.Max(0f, padding);
            minimumWidth = Mathf.Max(0f, minimum);
            maximumWidth = Mathf.Max(0f, maximum);
            dirty = true;
            RecalculateWidths();
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
            dirty = true;
            RecalculateWidths();
        }

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);

        private void OnValidate()
        {
            horizontalPadding = Mathf.Max(0f, horizontalPadding);
            minimumWidth = Mathf.Max(0f, minimumWidth);
            maximumWidth = Mathf.Max(0f, maximumWidth);
            dirty = true;
        }

        private void OnRectTransformDimensionsChange() => dirty = true;
        private void OnTransformChildrenChanged() => dirty = true;

        private void LateUpdate()
        {
            if (dirty) RecalculateWidths();
        }

        private void TextChanged(Object changed)
        {
            if (!(changed is TMP_Text label)) return;
            var button = label.GetComponentInParent<UnityEngine.UI.Button>();
            if (button != null && button.transform.parent == transform) dirty = true;
        }

        /// <summary>Updates LayoutElements; the VerticalLayoutGroup remains responsible for positioning.</summary>
        public void RecalculateWidths()
        {
            dirty = false;
            if (layout == null) layout = GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (groupRect == null) groupRect = transform as RectTransform;
            if (layout == null || groupRect == null || !layout.enabled) return;

            float available = Mathf.Max(0f, groupRect.rect.width - layout.padding.horizontal);
            if (maximumWidth > 0f) available = Mathf.Min(available, maximumWidth);
            if (available <= 0f) { dirty = true; return; }

            buttons.Clear();
            float longest = 0f;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var button = child.GetComponent<UnityEngine.UI.Button>();
                if (button == null) continue;
                var element = child.GetComponent<UnityEngine.UI.LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                buttons.Add(button);

                foreach (TMP_Text label in child.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!IsVisibleLabel(label, child)) continue;
                    // TMP preferredWidth already includes its own positive margins.
                    Vector4 margin = label.margin;
                    // Locale changes must not resize the approved frames. Non-localized groups
                    // keep their existing content fitting; translated groups use the design locale.
                    string referenceText = GameText.ReferenceLayoutText(label);
                    float preferred = referenceText == null ? label.preferredWidth : label.GetPreferredValues(referenceText).x;
                    float width = preferred - Mathf.Max(0f, margin.x) - Mathf.Max(0f, margin.z);
                    longest = Mathf.Max(longest, width);
                }
            }
            if (buttons.Count == 0) return;

            float common = Mathf.Min(available, Mathf.Ceil(Mathf.Max(minimumWidth, longest + horizontalPadding)));
            bool changed = !layout.childControlWidth || layout.childForceExpandWidth;
            if (!layout.childControlWidth) layout.childControlWidth = true;
            if (layout.childForceExpandWidth) layout.childForceExpandWidth = false;
            foreach (UnityEngine.UI.Button button in buttons)
            {
                var element = button.GetComponent<UnityEngine.UI.LayoutElement>();
                if (element == null) element = button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                if (!Mathf.Approximately(element.preferredWidth, common))
                { element.preferredWidth = common; changed = true; }
                if (!Mathf.Approximately(element.minWidth, common))
                { element.minWidth = common; changed = true; }
                if (!Mathf.Approximately(element.flexibleWidth, 0f))
                { element.flexibleWidth = 0f; changed = true; }
            }
            if (changed) UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(groupRect);
        }

        private static bool IsVisibleLabel(TMP_Text label, Transform button)
        {
            if (!label.enabled || string.IsNullOrWhiteSpace(label.text)) return false;
            for (Transform current = label.transform; current != button.parent; current = current.parent)
                if (current == null || !current.gameObject.activeSelf) return false;
            return true;
        }
    }
}
