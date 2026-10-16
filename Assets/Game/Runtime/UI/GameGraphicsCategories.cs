using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumivale.Start.Game
{
    /// <summary>Stacked Graphics sections sharing the existing scroll area and footer.</summary>
    [DisallowMultipleComponent]
    public sealed class GameGraphicsCategories : MonoBehaviour
    {
        [SerializeField] private GameSettingsController controller;
        [SerializeField] private UnityEngine.UI.Button graphicsTab;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private UnityEngine.UI.Button[] categoryTabs = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] private GameObject[] categoryPanels = Array.Empty<GameObject>();
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private UnityEngine.UI.Button reset;
        [SerializeField] private UnityEngine.UI.Button apply;
        private bool navigationDirty = true;
        private static readonly string[][] Keys =
        {
            new[] { "mode", "resolution", "vSync", "frameRateLimit" },
            new[] { "quality", "renderScale", "antiAliasing", "postProcessing" },
            new[] { "shadowQuality", "shadowDistance", "textureQuality", "anisotropicFiltering", "lodQuality" }
        };

        public int CurrentCategory { get; private set; }
        public IReadOnlyList<UnityEngine.UI.Button> Tabs => categoryTabs;
        public IReadOnlyList<GameObject> Panels => categoryPanels;

        public void Configure(GameSettingsController settings, UnityEngine.UI.Button parentTab,
            UnityEngine.UI.ScrollRect scrollView, UnityEngine.UI.Button[] tabs, GameObject[] panels,
            UnityEngine.UI.Button backButton, UnityEngine.UI.Button resetButton, UnityEngine.UI.Button applyButton)
        {
            controller = settings;
            graphicsTab = parentTab;
            scroll = scrollView;
            categoryTabs = tabs;
            categoryPanels = panels;
            back = backButton;
            reset = resetButton;
            apply = applyButton;
            ShowSections();
        }

        public void SelectCategory(int index)
        {
            if (index < 0 || index >= Keys.Length || categoryPanels.Length != Keys.Length) return;
            if (controller != null) controller.CloseDropdowns(false);
            CurrentCategory = index;
            ShowSections();
            foreach (string key in Keys[index])
            {
                var dropdown = controller != null ? controller.GetGraphicsDropdown(key) : null;
                if (dropdown == null || !dropdown.IsActive() || !dropdown.IsInteractable()) continue;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
                break;
            }
        }

        private void ShowSections()
        {
            if (categoryPanels == null || categoryPanels.Length != Keys.Length) return;
            for (int i = 0; i < categoryPanels.Length; i++)
                if (categoryPanels[i] != null) categoryPanels[i].SetActive(true);
            foreach (var tab in categoryTabs)
                if (tab != null) tab.gameObject.SetActive(false);
            controller?.RefreshDropdownCaptions();
            navigationDirty = true;
            RefreshNavigation();
        }

        public void RefreshNavigation()
        {
            // A value change can enable another row while the native popup is still
            // fading out. Retain that refresh until the dropdown has really closed.
            navigationDirty = true;
            if (!isActiveAndEnabled || controller == null || !controller.IsReady || !controller.IsOpen ||
                controller.IsAnyDropdownOpen) return;
            var active = new List<TMP_Dropdown>();
            foreach (var section in Keys)
                foreach (string key in section)
                {
                    var dropdown = controller.GetGraphicsDropdown(key);
                    if (dropdown != null && dropdown.IsActive() && dropdown.IsInteractable()) active.Add(dropdown);
                }
            for (int i = 0; i < active.Count; i++)
            {
                var nav = active[i].navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? (UnityEngine.UI.Selectable)active[i - 1] : graphicsTab;
                nav.selectOnDown = i + 1 < active.Count ? (UnityEngine.UI.Selectable)active[i + 1] : back;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                active[i].navigation = nav;
            }
            if (graphicsTab != null)
            {
                var nav = graphicsTab.navigation;
                nav.selectOnDown = active.Count > 0 ? (UnityEngine.UI.Selectable)active[0] : back;
                graphicsTab.navigation = nav;
            }
            foreach (var footer in new[] { back, reset, apply })
                if (footer != null)
                {
                    var nav = footer.navigation;
                    nav.selectOnUp = active.Count > 0 ? (UnityEngine.UI.Selectable)active[active.Count - 1] : graphicsTab;
                    footer.navigation = nav;
                }
            navigationDirty = false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            ShowSections();
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void LateUpdate()
        {
            var first = controller != null ? controller.GetGraphicsDropdown("mode") : null;
            if (navigationDirty || (graphicsTab != null && first != null && first.IsActive() && first.IsInteractable() &&
                graphicsTab.navigation.selectOnDown != first)) RefreshNavigation();
        }
    }
}
