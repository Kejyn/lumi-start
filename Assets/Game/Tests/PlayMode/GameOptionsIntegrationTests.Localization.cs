using System.Collections;
using System.Linq;
using Lumivale.Core.SceneFlow;
using Lumivale.Start.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameOptionsIntegrationTests
    {
        private void AppliedLanguage(string code)
        {
            var values = host.Settings.Current;
            values.LanguageCode = code;
            host.Settings.Update(values);
            host.Localization.Preview(code);
        }

        private IEnumerator OpenLanguage()
        {
            yield return Wait(host.Localization.PreviewReady);
            yield return OpenOptions();
            yield return Click(Options().View.Tabs[4]);
            Assert.That(SettingsController().LanguageDropdown.gameObject.activeInHierarchy, Is.True);
            Assert.That(SettingsController().LanguageButton.enabled, Is.False,
                "The retained historical row must not compete with the native dropdown selectable.");
        }

        private IEnumerator ChooseLanguage(string code)
        {
            var dropdown = SettingsController().LanguageDropdown;
            string before = LocaleCode;
            string draftBefore = SettingsController().Draft.LanguageCode;
            yield return ClickLanguageControl(dropdown);
            Assert.That(dropdown.IsExpanded, Is.True);
            Assert.That(LocaleCode, Is.EqualTo(before), "Opening the list must not preview another language.");
            Assert.That(SettingsController().Draft.LanguageCode, Is.EqualTo(draftBefore));
            string autonym = code == "fr" ? "Français" : "English";
            var item = dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>(false)
                .Single(toggle => toggle.GetComponentInChildren<TMP_Text>().text == autonym);
            yield return ClickLanguageControl(item);
            yield return Wait(host.Localization.PreviewReady);
            yield return WaitLanguageDropdownClosed(dropdown);
            Assert.That(SettingsController().Draft.LanguageCode, Is.EqualTo(code));
            Assert.That(dropdown.captionText.text, Is.EqualTo(GameText.Text("options.interface.language", autonym)));
        }

        private static IEnumerator WaitLanguageDropdownClosed(TMP_Dropdown dropdown)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (dropdown.IsExpanded)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }

        private IEnumerator ClickLanguageControl(UnityEngine.UI.Selectable control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = control.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }

        private static string LocaleCode => LocalizationSettings.SelectedLocale.Identifier.Code;

        [UnityTest]
        public IEnumerator LocalizationKeepsApprovedMainMenuButtonWidthsAndFontSizes()
        {
            AppliedLanguage("en");
            yield return Wait(host.Localization.PreviewReady);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var buttons = new[] { Menu().Views.Primary, Menu().Views.Options, Menu().Views.CreditsButton, Menu().Views.Exit };
            var widths = buttons.Select(b => b.GetComponent<RectTransform>().rect.width).ToArray();
            var fontSizes = buttons.Select(b => b.GetComponentInChildren<TMP_Text>().fontSize).ToArray();
            yield return OpenLanguage();
            yield return ChooseLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            yield return Click(SettingsController().ApplyButton);
            yield return Click(SettingsController().BackButton);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.That(buttons[i].GetComponent<RectTransform>().rect.width, Is.EqualTo(widths[i]).Within(.01f), buttons[i].name);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                label.ForceMeshUpdate();
                Assert.That(label.fontSize, Is.EqualTo(fontSizes[i]));
                Assert.That(label.isTextOverflowing, Is.False, label.text);
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .01f),
                    "The entire single-line label must fit, including its margins: " + label.text);
            }
        }

        [UnityTest]
        public IEnumerator LocalizationRapidPreviewKeepsLastRequestAndShutdownDrainsNativeLoading()
        {
            AppliedLanguage("en");
            yield return Wait(host.Localization.PreviewReady);
            for (int i = 0; i < 25; i++) host.Localization.Preview(i % 2 == 0 ? "fr" : "en");
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            host.Localization.Preview("en");
            yield return Wait(host.ShutdownAsync());
            Assert.That(host.Localization.IsInitialized, Is.False);
            Object.Destroy(host.gameObject);
            yield return null;
            CreateHost();
            yield return Wait(host.Ready);
            Assert.That(host.Localization.IsInitialized, Is.True);
        }

        [UnityTest]
        public IEnumerator LocalizationFirstLaunchUsesDetectedLanguageWithoutWritingPreferences()
        {
            foreach (var systemLanguage in new[] { SystemLanguage.French, SystemLanguage.English, SystemLanguage.Russian })
            {
                string isolatedKey = key + ".first-launch." + systemLanguage;
                var settings = new Lumivale.Core.Settings.SettingsService(new Lumivale.Core.Settings.SettingsConfiguration(
                    new Lumivale.Core.Settings.SettingsValues { LanguageCode = GameLocalizationService.DetectLanguage(systemLanguage) },
                    isolatedKey, new[] { "en", "fr" }));
                var localization = new GameLocalizationService();
                var coordinator = new Lumivale.Core.Bootstrap.BootstrapCoordinator(
                    new Lumivale.Core.Bootstrap.ILumiService[] { settings, localization });
                yield return Wait(coordinator.InitializeAsync());
                Assert.That(LocaleCode, Is.EqualTo(systemLanguage == SystemLanguage.French ? "fr" : "en"));
                Assert.That(PlayerPrefs.HasKey(isolatedKey), Is.False);
                yield return Wait(coordinator.ShutdownAsync());
            }
        }

        [UnityTest]
        public IEnumerator LocalizationRepeatedDynamicBindingDoesNotDuplicateUpdateCallbacks()
        {
            AppliedLanguage("en");
            yield return Wait(host.Localization.PreviewReady);
            yield return OpenLanguage();
            var label = SettingsController().ApplyButton.GetComponentInChildren<TMP_Text>();
            for (int i = 0; i < 12; i++) GameText.Set(label, "options.apply");
            yield return null;
            yield return null;
            int updates = 0;
            UnityEngine.Events.UnityAction<string> observe = _ => updates++;
            var localizer = label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            localizer.OnUpdateString.AddListener(observe);
            try
            {
                host.Localization.Preview("fr");
                yield return Wait(host.Localization.PreviewReady);
                yield return null;
                yield return null;
                Assert.That(updates, Is.EqualTo(1));
                Assert.That(label.text, Is.EqualTo("Appliquer").IgnoreCase);
            }
            finally { localizer.OnUpdateString.RemoveListener(observe); }
        }

        [UnityTest]
        public IEnumerator LocalizationPreviewUpdatesOpenModalAndDiscardRestoresAppliedLanguage()
        {
            AppliedLanguage("en");
            int loads = host.CompletedLoads;
            yield return OpenLanguage();
            var choice = SettingsController().LanguageDropdown;
            yield return ChooseLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            Assert.That(host.Settings.Current.LanguageCode, Is.EqualTo("en"));
            Assert.That(Options().CurrentTab, Is.EqualTo(4));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(choice.gameObject));
            Assert.That(SettingsController().ApplyButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Appliquer").IgnoreCase);
            Assert.That(choice.captionText.text, Is.EqualTo(GameText.Text("options.interface.language", "Français")));
            yield return Click(SettingsController().BackButton);
            Assert.That(host.UI.IsOpen(Options().View.UnsavedConfirmation.Id), Is.True);
            host.Localization.Preview("en");
            yield return Wait(host.Localization.PreviewReady);
            yield return null;
            Assert.That(host.UI.IsOpen(Options().View.UnsavedConfirmation.Id), Is.True);
            Assert.That(Options().View.DiscardChanges.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("No").IgnoreCase);
            host.Localization.Preview("fr");
            yield return Wait(host.Localization.PreviewReady);
            yield return null;
            Assert.That(Options().View.DiscardChanges.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Non").IgnoreCase);
            yield return Click(Options().View.DiscardChanges);
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo("en"));
            Assert.That(host.CompletedLoads, Is.EqualTo(loads));
            Assert.That(PlayerPrefs.HasKey(key), Is.False, "Language preview and discard must not create a preference document.");
        }

        [UnityTest]
        public IEnumerator LocalizationApplyPersistsAcrossOptionsAndFreshBootstrap()
        {
            AppliedLanguage("en");
            yield return OpenLanguage();
            yield return ChooseLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            yield return Click(SettingsController().ApplyButton);
            Assert.That(host.Settings.Current.LanguageCode, Is.EqualTo("fr"));
            Assert.That(PlayerPrefs.GetString(key), Does.Contain("\"LanguageCode\":\"fr\""));
            yield return Click(SettingsController().BackButton);
            yield return OpenLanguage();
            Assert.That(SettingsController().Draft.LanguageCode, Is.EqualTo("fr"));
            yield return Click(SettingsController().BackButton);
            var previous = host.Localization;
            yield return Wait(host.ShutdownAsync());
            Object.Destroy(host.gameObject);
            yield return null;
            CreateHost();
            yield return Wait(host.Ready);
            RestrictDevices();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            yield return Press(Key.Space);
            Assert.That(host.Localization, Is.Not.SameAs(previous));
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            Assert.That(host.Settings.Current.LanguageCode, Is.EqualTo("fr"));
            Assert.That(Menu().Views.Primary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Jouer").IgnoreCase);
        }

        [UnityTest]
        public IEnumerator LocalizationResetPreviewsDetectedDefaultAndBackCanRestoreSavedChoice()
        {
            string fallback = host.Settings.Defaults.LanguageCode;
            string other = fallback == "en" ? "fr" : "en";
            AppliedLanguage(other);
            yield return OpenLanguage();
            yield return Click(SettingsController().ResetButton);
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo(fallback));
            Assert.That(host.Settings.Current.LanguageCode, Is.EqualTo(other));
            yield return Click(SettingsController().BackButton);
            Assert.That(host.UI.IsOpen(Options().View.UnsavedConfirmation.Id), Is.True);
            yield return Click(Options().View.DiscardChanges);
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo(other));
        }

        [UnityTest]
        public IEnumerator LocalizationKeyboardAndGamepadSwitchLanguageWithoutLosingFocus()
        {
            AppliedLanguage("en");
            yield return OpenLanguage();
            var choice = SettingsController().LanguageDropdown;
            EventSystem.current.SetSelectedGameObject(choice.gameObject);
            yield return Press(Key.Enter);
            Assert.That(choice.IsExpanded, Is.True);
            Assert.That(LocaleCode, Is.EqualTo("en"));
            yield return Press(Key.DownArrow);
            Assert.That(LocaleCode, Is.EqualTo("en"), "Navigating the list must not change language.");
            yield return Press(Key.Escape);
            yield return WaitLanguageDropdownClosed(choice);
            Assert.That(SettingsController().IsOpen, Is.True, "Cancel closes the list before the Options screen.");
            Assert.That(LocaleCode, Is.EqualTo("en"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(choice.gameObject));
            yield return Press(Key.Enter);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            yield return Wait(host.Localization.PreviewReady);
            yield return WaitLanguageDropdownClosed(choice);
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(choice.gameObject));
            yield return Pad(GamepadButton.South);
            Assert.That(choice.IsExpanded, Is.True);
            yield return Pad(GamepadButton.LeftShoulder);
            Assert.That(Options().CurrentTab, Is.EqualTo(4), "Tab shortcuts must not move behind the expanded list.");
            yield return Pad(GamepadButton.DpadUp);
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            yield return Pad(GamepadButton.East);
            yield return WaitLanguageDropdownClosed(choice);
            Assert.That(SettingsController().IsOpen, Is.True);
            Assert.That(LocaleCode, Is.EqualTo("fr"));
            yield return Pad(GamepadButton.South);
            yield return Pad(GamepadButton.DpadUp);
            yield return Pad(GamepadButton.South);
            yield return Wait(host.Localization.PreviewReady);
            yield return WaitLanguageDropdownClosed(choice);
            Assert.That(LocaleCode, Is.EqualTo("en"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(choice.gameObject));
            Assert.That(Options().CurrentTab, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator LocalizationSurvivesSceneNavigationAndRefreshesDynamicGameplayText()
        {
            AppliedLanguage("fr");
            int id = host.GetInstanceID();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            yield return Press(Key.C);
            Assert.That(Saves().Counter, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(t => t.text.Contains("Compteur")), Is.True, "A dynamic gameplay label must use the selected locale.");
            yield return Press(Key.Escape);
            yield return Click(SettingsController().PauseOptionsButton);
            yield return Click(Options().View.Tabs[4]);
            yield return ChooseLanguage("en");
            yield return Wait(host.Localization.PreviewReady);
            Assert.That(LocaleCode, Is.EqualTo("en"));
            yield return Click(SettingsController().ApplyButton);
            yield return Click(SettingsController().BackButton);
            Assert.That(host.Pause.IsPaused, Is.True);
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            Assert.That(host.GetInstanceID(), Is.EqualTo(id));
            Assert.That(LocaleCode, Is.EqualTo("en"));
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
        }
    }
}
