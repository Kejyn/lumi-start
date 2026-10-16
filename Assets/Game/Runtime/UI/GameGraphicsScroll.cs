using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumivale.Start.Game
{
    /// <summary>Keeps selected graphics rows visible without moving the Options frame or footer.</summary>
    public sealed class GameGraphicsScroll : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private UnityEngine.UI.Button tab;
        private GameObject previous;
        public UnityEngine.UI.Button Tab => tab;
        public void Configure(UnityEngine.UI.ScrollRect scrollView, UnityEngine.UI.Button tabButton) { scroll = scrollView; tab = tabButton; }
        private void OnEnable() { previous = null; if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; } }
        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == previous) return;
            previous = selected;
            if (selected == null || scroll == null || scroll.content == null || scroll.viewport == null || !selected.transform.IsChildOf(scroll.content)) return;
            if (selected.GetComponentInParent<UnityEngine.UI.ScrollRect>() != scroll) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var viewport = scroll.viewport.rect;
            float hidden = scroll.content.rect.height - viewport.height;
            if (hidden <= 0f) return;
            float delta = bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0f;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - delta / hidden);
        }
    }
}
