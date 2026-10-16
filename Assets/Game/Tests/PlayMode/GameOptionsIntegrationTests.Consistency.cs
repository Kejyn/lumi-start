using System.Collections;
using System.Linq;
using Lumivale.Start.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameOptionsIntegrationTests
    {
        private static readonly string[] ConsistentGraphicsKeys = {
            "mode", "resolution", "vSync", "frameRateLimit", "quality", "renderScale", "antiAliasing",
            "postProcessing", "shadowQuality", "shadowDistance", "textureQuality", "anisotropicFiltering", "lodQuality"
        };

        [UnityTest, Category("OptionsConsistencyUI")]
        public IEnumerator OptionsConsistencyGraphicsGroupsAndExplicitSelectionKeepTheFooterFixed()
        {
            yield return OpenGraphics();
            var settings = SettingsController();
            var groups = Object.FindFirstObjectByType<GameGraphicsCategories>();
            Assert.That(groups, Is.Not.Null);
            Assert.That(groups.Tabs, Is.Empty, "Graphics sections do not expose clickable category tabs.");
            Assert.That(groups.Panels.Count, Is.EqualTo(3));
            var footer = new[] { settings.BackButton, settings.ResetButton, settings.ApplyButton };
            var corners = footer.Select(b => GraphicsCorners(b.GetComponent<RectTransform>())).ToArray();
            foreach (string id in ConsistentGraphicsKeys)
            {
                var control = settings.GetGraphicsDropdown(id);
                Assert.That(control, Is.Not.Null, id);
                Assert.That(control.options.Count, Is.GreaterThan(0), id);
                Assert.That(control.GetComponentInParent<GameChoiceButton>(true).enabled, Is.False, id);
            }
            var sectionKeys = new[] { "options.graphics.category.display", "options.graphics.category.image", "options.graphics.category.details" };
            for (int category = 0; category < sectionKeys.Length; category++)
            {
                var panel = groups.Panels[category];
                Assert.That(panel.activeInHierarchy, Is.True, sectionKeys[category]);
                Assert.That(panel.GetComponentsInChildren<TMP_Text>(false).Any(text =>
                    text.GetComponentInParent<TMP_Dropdown>() == null && text.text == GameText.Text(sectionKeys[category])),
                    Is.True, "Each visible section retains its localized title: " + sectionKeys[category]);
                groups.SelectCategory(category);
                yield return null; yield return null;
                Assert.That(groups.CurrentCategory, Is.EqualTo(category));
                int rows = ConsistentGraphicsKeys.Count(id => settings.GetGraphicsDropdown(id).gameObject.activeInHierarchy);
                Assert.That(rows, Is.EqualTo(13), "Moving focus between sections must leave every section visible.");
            }
            var dropdown = settings.GetGraphicsDropdown("renderScale");
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            yield return null; yield return null;
            var applied = host.Settings.Current;
            var draftBefore = settings.Draft;
            int selected = dropdown.value;
            yield return ClickLanguageControl(dropdown);
            Assert.That(dropdown.IsExpanded, Is.True);
            Assert.That(settings.Draft.ContentEquals(draftBefore), Is.True,
                "Opening a graphics list must not edit the draft.");
            int next = selected == 0 ? 1 : 0;
            yield return ChooseConsistentItem(dropdown, next);
            Assert.That(settings.Draft.RenderScale, Is.Not.EqualTo(draftBefore.RenderScale));
            Assert.That(host.Settings.Current.ContentEquals(applied), Is.True);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            for (int b = 0; b < footer.Length; b++)
                for (int c = 0; c < 4; c++)
                    Assert.That(Vector3.Distance(GraphicsCorners(footer[b].GetComponent<RectTransform>())[c], corners[b][c]), Is.LessThan(.1f));
            yield return Click(settings.BackButton);
            yield return Click(Options().View.DiscardChanges);
            Assert.That(host.Settings.Current.ContentEquals(applied), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("OptionsConsistencyUI")]
        public IEnumerator OptionsConsistencyDropdownCancelAndNavigationDoNotChangeDraftOrTabs()
        {
            yield return OpenGraphics();
            var settings = SettingsController();
            var dropdown = settings.GetGraphicsDropdown("vSync");
            var before = settings.Draft;
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            yield return Pad(GamepadButton.South);
            Assert.That(dropdown.IsExpanded, Is.True);
            yield return Pad(GamepadButton.RightShoulder);
            Assert.That(Options().CurrentTab, Is.EqualTo(2));
            yield return Pad(GamepadButton.DpadDown);
            Assert.That(settings.Draft.ContentEquals(before), Is.True);
            yield return Pad(GamepadButton.East);
            yield return WaitLanguageDropdownClosed(dropdown);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));
            Assert.That(settings.Draft.ContentEquals(before), Is.True);
            yield return Press(Key.Enter);
            Assert.That(dropdown.IsExpanded, Is.True);
            yield return Press(Key.DownArrow);
            Assert.That(settings.Draft.ContentEquals(before), Is.True);
            yield return Press(Key.Escape);
            yield return WaitLanguageDropdownClosed(dropdown);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(Options().HasUnsavedChanges, Is.False);
            yield return ClickLanguageControl(dropdown);
            yield return ChooseConsistentItem(dropdown, 0);
            Assert.That(settings.Draft.VSync, Is.False);
            var fps = settings.GetGraphicsDropdown("frameRateLimit");
            Assert.That(fps.IsInteractable(), Is.True);
            Assert.That(dropdown.navigation.selectOnDown, Is.EqualTo(fps),
                "VSync's popup must refresh keyboard navigation after enabling FPS.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("OptionsConsistencyUI")]
        public IEnumerator OptionsConsistencyLanguageAlignmentAudioSlidersAndSingleFeedbackStayCoherent()
        {
            yield return OpenLanguage();
            var settings = SettingsController();
            var language = settings.LanguageDropdown;
            var graphics = settings.GetGraphicsDropdown("resolution");
            Assert.That(language.captionText.text, Is.EqualTo(GameText.Text("options.interface.language", "English")));
            Assert.That(language.captionText.alignment, Is.EqualTo(graphics.captionText.alignment));
            Assert.That(language.captionText.rectTransform.anchorMin, Is.EqualTo(graphics.captionText.rectTransform.anchorMin));
            Assert.That(language.captionText.rectTransform.anchorMax, Is.EqualTo(graphics.captionText.rectTransform.anchorMax));
            Assert.That(language.captionText.rectTransform.offsetMin, Is.EqualTo(graphics.captionText.rectTransform.offsetMin));
            Assert.That(language.captionText.rectTransform.offsetMax, Is.EqualTo(graphics.captionText.rectTransform.offsetMax));
            yield return ChooseLanguage("fr");
            Assert.That(language.captionText.text, Is.EqualTo(GameText.Text("options.interface.language", "Français")));

            yield return Click(Options().View.Tabs[3]);
            var reference = Options().View.MovementSensitivity;
            foreach (var slider in new[] { settings.MasterSlider, settings.MusicSlider, settings.SfxSlider })
            {
                Assert.That(slider.handleRect.sizeDelta, Is.EqualTo(reference.handleRect.sizeDelta));
                Assert.That(slider.handleRect.anchorMin.y, Is.EqualTo(reference.handleRect.anchorMin.y));
                Assert.That(slider.handleRect.anchorMax.y, Is.EqualTo(reference.handleRect.anchorMax.y));
                Assert.That(slider.transform.Find("Background").GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(reference.transform.Find("Background").GetComponent<UnityEngine.UI.Image>().color));
                Assert.That(slider.transform.Find("Background").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(reference.transform.Find("Background").GetComponent<RectTransform>().sizeDelta));
                Assert.That(slider.fillRect.gameObject.activeInHierarchy, Is.False,
                    "The historical thick Audio fill is retained but hidden.");
                Assert.That(slider.minValue, Is.Zero);
                Assert.That(slider.maxValue, Is.EqualTo(1f));
                Assert.That(slider.direction, Is.EqualTo(reference.direction));
            }
            float original = settings.Draft.MasterVolume;
            EventSystem.current.SetSelectedGameObject(settings.MasterSlider.gameObject);
            yield return Press(Key.LeftArrow);
            Assert.That(settings.Draft.MasterVolume, Is.LessThan(original));
            Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(original));
            yield return Click(Options().View.Tabs[1]);
            yield return Click(Options().View.ResetBindings);
            Assert.That(Options().View.BindingStatus.gameObject.activeSelf, Is.False,
                "The duplicate status above Reset bindings stays hidden.");
            Assert.That(settings.StatusMessage, Is.EqualTo(GameText.Text("options.controls.defaults")));
            yield return Click(settings.BackButton);
            yield return Click(Options().View.DiscardChanges);
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(original));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("OptionsConsistencyUI")]
        public IEnumerator OptionsConsistencyLongDropdownKeepsFocusedItemsVisibleWithoutMovingOuterPage()
        {
            yield return OpenGraphics();
            yield return ShowGraphicsCategory("shadowDistance");
            var dropdown = SettingsController().GetGraphicsDropdown("shadowDistance");
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            yield return null; yield return null;
            var outer = dropdown.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            float outerPosition = outer.verticalNormalizedPosition;
            var before = SettingsController().Draft;
            yield return Pad(GamepadButton.South);
            Assert.That(dropdown.IsExpanded, Is.True);
            var inner = dropdown.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(false).Single();
            var items = dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>(false);
            Assert.That(items.Length, Is.GreaterThan(5));
            for (int step = 0; step < items.Length; step++) yield return Pad(GamepadButton.DpadUp);
            for (int step = 0; step < items.Length; step++)
            {
                Canvas.ForceUpdateCanvases();
                var selected = EventSystem.current.currentSelectedGameObject.transform;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(inner.viewport, selected);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(inner.viewport.rect.yMin - 2f));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(inner.viewport.rect.yMax + 2f));
                Assert.That(SettingsController().Draft.ContentEquals(before), Is.True);
                Assert.That(outer.verticalNormalizedPosition, Is.EqualTo(outerPosition).Within(.001f));
                if (step + 1 < items.Length) yield return Pad(GamepadButton.DpadDown);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(items[items.Length - 1].gameObject));
            yield return Pad(GamepadButton.South);
            yield return WaitLanguageDropdownClosed(dropdown);
            Assert.That(SettingsController().Draft.ShadowDistance, Is.EqualTo(100f));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator ChooseConsistentItem(TMP_Dropdown dropdown, int index)
        {
            string label = dropdown.options[index].text;
            var item = dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>(false)
                .Single(toggle => toggle.GetComponentInChildren<TMP_Text>().text == label);
            yield return ClickLanguageControl(item);
            yield return WaitLanguageDropdownClosed(dropdown);
        }
    }
}
