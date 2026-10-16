using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.SceneFlow;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    /// <summary>Native Input System/EventSystem and real PlayerPrefs; all data belongs to this fixture.</summary>
    public sealed partial class GameOptionsIntegrationTests
    {
        private const string ProductInputPath = "Assets/Game/Input/GameControls.inputactions";
        private GameBootstrap host;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private string root;
        private string key;
        private float originalTime;
        private bool originalBackground;
        private int originalQuality;
        private int originalWidth, originalHeight;
        private FullScreenMode originalMode;
        private InputSettings.EditorInputBehaviorInPlayMode originalFocus;
        private InputSettings.BackgroundBehavior originalInputBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTime = Time.timeScale;
            originalBackground = Application.runInBackground;
            originalQuality = QualitySettings.GetQualityLevel();
            originalWidth = Screen.width; originalHeight = Screen.height; originalMode = Screen.fullScreenMode;
            originalFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalInputBackground = InputSystem.settings.backgroundBehavior;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (LumiBootstrap.Instance != null)
            {
                var previous = LumiBootstrap.Instance;
                yield return Wait(previous.ShutdownAsync());
                UnityEngine.Object.Destroy(previous.gameObject);
                yield return null;
            }
            string unique = Guid.NewGuid().ToString("N");
            key = "lumivale.product.options.test." + unique;
            root = Path.Combine(Path.GetTempPath(), "LumiProductOptions-" + unique);
            keyboard = InputSystem.AddDevice<Keyboard>("Product Options Virtual Keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Product Options Virtual Mouse");
            gamepad = InputSystem.AddDevice<Gamepad>("Product Options Virtual Gamepad");
            CreateHost();
            yield return Wait(host.Ready);
            // Keep unrelated presentation assertions independent of the developer OS language.
            AppliedLanguage("en");
            yield return Wait(host.Localization.PreviewReady);
            RestrictDevices();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            yield return Press(Key.Space);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null)
            {
                yield return Wait(host.ShutdownAsync());
                UnityEngine.Object.Destroy(host.gameObject);
                yield return null;
            }
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (key != null)
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(key + GamePreferences.PresentationSuffix);
                PlayerPrefs.DeleteKey(key + GamePreferences.BindingsSuffix);
                PlayerPrefs.Save();
            }
            if (root != null && Directory.Exists(root)) Directory.Delete(root, true);
            Time.timeScale = originalTime;
            Application.runInBackground = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalFocus;
            InputSystem.settings.backgroundBehavior = originalInputBackground;
            QualitySettings.SetQualityLevel(originalQuality, true);
            Screen.SetResolution(originalWidth, originalHeight, originalMode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveTabsRespondToMouseAndVirtualGamepadAndPreserveMenuBindings()
        {
            string reserved = ReservedBindings();
            yield return OpenOptions();
            var options = Options();
            for (int tab = 0; tab < 5; tab++)
            {
                yield return Click(options.View.Tabs[tab]);
                Assert.That(options.CurrentTab, Is.EqualTo(tab));
                for (int panel = 0; panel < 5; panel++)
                    Assert.That(options.View.Panels[panel].activeSelf, Is.EqualTo(panel == tab));
            }
            EventSystem.current.SetSelectedGameObject(options.View.Tabs[1].gameObject);
            yield return Pad(GamepadButton.South);
            Assert.That(options.CurrentTab, Is.EqualTo(1));
            Assert.That(options.View.BindingRows.All(row => row.ActionPath.StartsWith("Gameplay/", StringComparison.Ordinal)), Is.True);
            Assert.That(SettingsController().LanguageButton.gameObject.activeSelf, Is.True);
            Assert.That(SettingsController().LanguageButton.transform.IsChildOf(options.View.Panels[4].transform), Is.True);
            yield return Pad(GamepadButton.East);
            Assert.That(SettingsController().IsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(SettingsController().MenuOptionsButton.gameObject));
            Assert.That(ReservedBindings(), Is.EqualTo(reserved));
        }

        [UnityTest]
        public IEnumerator KeyboardRebindBackDiscardsAndApplySurvivesFreshBootstrap()
        {
            yield return OpenControls();
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row);
            yield return Press(Key.E);
            yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/e"));
            yield return Click(SettingsController().BackButton);
            yield return Click(Options().View.DiscardChanges);
            yield return null;
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/c"));
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.False);

            yield return OpenControls();
            row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row);
            yield return Press(Key.E);
            yield return FinishCapture();
            yield return Click(SettingsController().ApplyButton);
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.True);
            yield return Click(SettingsController().BackButton);
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            yield return Press(Key.C);
            Assert.That(Saves().Counter, Is.Zero);
            yield return Press(Key.E);
            Assert.That(Saves().Counter, Is.EqualTo(1));

            var previousInput = host.Input;
            yield return Wait(host.ShutdownAsync());
            UnityEngine.Object.Destroy(host.gameObject);
            yield return null;
            CreateHost(); yield return Wait(host.Ready); RestrictDevices();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            Assert.That(host.Input, Is.Not.SameAs(previousInput));
            yield return Press(Key.E);
            Assert.That(Saves().Counter, Is.EqualTo(1));
#if UNITY_EDITOR
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProductInputPath);
            Assert.That(source.FindAction("Gameplay/IncrementCounter").bindings.All(binding => binding.overridePath == null), Is.True);
#endif
        }

        [UnityTest]
        public IEnumerator VirtualGamepadRebindAndCancelRestoreFocusWithoutResumingGameplay()
        {
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            yield return Press(Key.Escape);
            yield return Click(SettingsController().PauseOptionsButton);
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            yield return Click(Options().View.Tabs[1]);
            var row = Row("Gameplay/IncrementCounter", "<Gamepad>/*");
            yield return StartCapture(row);
            yield return Pad(GamepadButton.East);
            yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo("<Gamepad>/rightShoulder"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(row.Button.gameObject));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(host.Pause.IsPaused, Is.True);
            yield return StartCapture(row);
            yield return Pad(GamepadButton.Select);
            yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo("<Gamepad>/select"));
            yield return Click(SettingsController().ApplyButton);
            yield return Pad(GamepadButton.East);
            yield return Pad(GamepadButton.East);
            Assert.That(host.Pause.IsPaused, Is.False);
            yield return Pad(GamepadButton.Select);
            Assert.That(Saves().Counter, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MouseRebindingChangesTheRealGameplaySoundAction()
        {
            yield return OpenControls();
            var row = Row("Gameplay/Sfx2D", "<Mouse>/*");
            yield return StartCapture(row);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Back));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo("<Mouse>/backButton"));
            yield return Click(SettingsController().ApplyButton);
            yield return Click(SettingsController().BackButton);
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            int before = SettingsController().Sound2DCount;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Back));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null; yield return null;
            Assert.That(SettingsController().Sound2DCount, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator BindingDefaultsArePreviewedAndBackRestoresTheSavedOverride()
        {
            yield return OpenControls();
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row); yield return Press(Key.E); yield return FinishCapture();
            yield return Click(SettingsController().ApplyButton);
            string saved = PlayerPrefs.GetString(key + GamePreferences.BindingsSuffix);
            yield return Click(Options().View.ResetBindings);
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/c"));
            Assert.That(PlayerPrefs.GetString(key + GamePreferences.BindingsSuffix), Is.EqualTo(saved));
            yield return Click(SettingsController().BackButton);
            yield return Click(Options().View.DiscardChanges);
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/e"));
        }

        [UnityTest]
        public IEnumerator InterfacePreviewDiscardsAndAppliedGameplayPreferencesHaveRealEffects()
        {
            yield return OpenOptions();
            var options = Options();
            Vector2 originalReference = options.View.Scaler.referenceResolution;
            yield return Click(options.View.Tabs[4]);
            EventSystem.current.SetSelectedGameObject(options.View.InterfaceScale.gameObject);
            yield return Press(Key.RightArrow);
            Assert.That(options.Draft.InterfaceScale, Is.GreaterThan(1f));
            Assert.That(options.View.Scaler.referenceResolution.x, Is.LessThan(originalReference.x));
            EventSystem.current.SetSelectedGameObject(options.View.ShowHints.gameObject);
            yield return Press(Key.Enter);
            Assert.That(options.Draft.ShowHints, Is.False);
            Assert.That(options.View.Hints.Where(hint => hint != null).All(hint => !hint.activeSelf), Is.True);
            yield return Click(SettingsController().BackButton);
            yield return Click(Options().View.DiscardChanges);
            Assert.That(options.View.Scaler.referenceResolution, Is.EqualTo(originalReference));
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.PresentationSuffix), Is.False);

            yield return OpenOptions();
            EventSystem.current.SetSelectedGameObject(options.View.MovementSensitivity.gameObject);
            yield return Pad(GamepadButton.DpadRight);
            Assert.That(options.Draft.MovementSensitivity, Is.GreaterThan(1f));
            EventSystem.current.SetSelectedGameObject(options.View.InvertY.gameObject);
            yield return Press(Key.Enter);
            Assert.That(options.Draft.InvertY, Is.True);
            yield return Click(SettingsController().ApplyButton);
            Assert.That(new GamePreferences(key).Current.InvertY, Is.True);
            Assert.That(new GamePreferences(key).Current.MovementSensitivity, Is.GreaterThan(1f));
            yield return Click(SettingsController().BackButton);
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            Vector3 before = Navigation().PlayerPosition;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int frame = 0; frame < 8; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(Navigation().PlayerPosition.z, Is.LessThan(before.z - .01f));
        }

        [UnityTest]
        public IEnumerator ClosingOptionsDuringCaptureReleasesLeaseAndRestoresBindingSnapshot()
        {
            yield return OpenControls();
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row);
            host.UI.Close("Options");
            yield return FinishCapture();
            Assert.That(host.Input.IsSuspended, Is.False);
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/c"));
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<Lumivale.Core.Audio.LumiAudioRunner>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReservedStartCaptureRollsBackWithoutClosingPausedOptions()
        {
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay(); yield return Press(Key.Escape);
            yield return Click(SettingsController().PauseOptionsButton); yield return null;
            yield return Click(Options().View.Tabs[1]);
            var row = Row("Gameplay/IncrementCounter", "<Gamepad>/*");
            string previous = BindingPath(row);
            yield return StartCapture(row); yield return Pad(GamepadButton.Start); yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo(previous));
            Assert.That(SettingsController().StatusMessage, Is.EqualTo(GameText.Text("options.controls.reserved")));
            Assert.That(Options().IsOpen, Is.True);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(host.UI.ModalCount, Is.EqualTo(2));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(row.Button.gameObject));
            Assert.That(Saves().Counter, Is.Zero);
            yield return Click(SettingsController().ApplyButton);
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.False);
        }

        [UnityTest]
        public IEnumerator ReservedTabCaptureRollsBackAndTheSaveShortcutStillWorksAfterResume()
        {
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay(); yield return Press(Key.Escape);
            yield return Click(SettingsController().PauseOptionsButton); yield return null;
            yield return Click(Options().View.Tabs[1]);
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row); yield return Press(Key.Tab); yield return FinishCapture();
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/c"));
            Assert.That(SettingsController().StatusMessage, Is.EqualTo(GameText.Text("options.controls.reserved")));
            Assert.That(Options().IsOpen, Is.True);
            Assert.That(host.UI.ModalCount, Is.EqualTo(2));
            Assert.That(Saves().IsOpen, Is.False);
            yield return Click(SettingsController().BackButton); yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.False);
            yield return Press(Key.C); Assert.That(Saves().Counter, Is.EqualTo(1));
            yield return Press(Key.Tab); yield return Wait(Saves().LastOperation);
            Assert.That(Saves().IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator StoredTabCollisionIsPreservedAndTheCurrentOverrideIsUnchanged()
        {
            var action = host.Input.GetAction("Gameplay/IncrementCounter");
            int index = KeyboardCounterIndex();
            action.ApplyBindingOverride(index, "<Keyboard>/tab");
            string original = BindingDocument(host.Input.ExportBindingOverrides());
            action.ApplyBindingOverride(index, "<Keyboard>/e");
            PlayerPrefs.SetString(key + GamePreferences.BindingsSuffix, original);
            var store = new GamePreferences(key); store.LoadBindingsOnce(host.Input);
            Assert.That(store.BindingDiagnostic, Does.Contain("reserved"));
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/e"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoredStartAliasCollisionIsRejectedEvenWithoutAConnectedGamepad()
        {
            var action = host.Input.GetAction("Gameplay/IncrementCounter");
            int index = Enumerable.Range(0, action.bindings.Count)
                .First(binding => action.bindings[binding].path == "<Gamepad>/rightShoulder");
            action.ApplyBindingOverride(index, "<Gamepad>/startButton");
            string original = BindingDocument(host.Input.ExportBindingOverrides());
            host.Input.ResetBindingOverrides();
            InputSystem.RemoveDevice(gamepad);
            host.Input.RuntimeAsset.devices = new InputDevice[] { keyboard, mouse };
            PlayerPrefs.SetString(key + GamePreferences.BindingsSuffix, original);
            var store = new GamePreferences(key); store.LoadBindingsOnce(host.Input);
            Assert.That(store.BindingDiagnostic, Does.Contain("reserved"));
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Gamepad>/rightShoulder"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoredUnknownBindingIdDoesNotChangeExistingOverridesOrThePreferenceDocument()
        {
            var action = host.Input.GetAction("Gameplay/IncrementCounter");
            int index = KeyboardCounterIndex();
            action.ApplyBindingOverride(index, "<Keyboard>/e");
            string export = host.Input.ExportBindingOverrides();
            export = export.Replace(action.bindings[index].id.ToString(), Guid.NewGuid().ToString());
            string original = BindingDocument(export);
            PlayerPrefs.SetString(key + GamePreferences.BindingsSuffix, original);
            var store = new GamePreferences(key); store.LoadBindingsOnce(host.Input);
            Assert.That(store.BindingDiagnostic, Is.Not.Empty);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/e"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoredMalformedOverridesDoNotChangeAnExistingRuntimeBinding()
        {
            var action = host.Input.GetAction("Gameplay/IncrementCounter");
            int index = KeyboardCounterIndex();
            action.ApplyBindingOverride(index, "<Keyboard>/e");
            string original = BindingDocument("{truncated");
            PlayerPrefs.SetString(key + GamePreferences.BindingsSuffix, original);
            var store = new GamePreferences(key); store.LoadBindingsOnce(host.Input);
            Assert.That(store.BindingDiagnostic, Is.Not.Empty);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/e"));
            Assert.That(PlayerPrefs.GetString(store.BindingsKey), Is.EqualTo(original));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitDefaultsApplyRepairsAFutureBindingDocumentEvenWhenRuntimeAlreadyUsesDefaults()
        {
            string future = "{\"Version\":2,\"Overrides\":\"future data must remain intact until an explicit reset and Apply\"}";
            string bindingKey = key + GamePreferences.BindingsSuffix;
            PlayerPrefs.SetString(bindingKey, future);
            PlayerPrefs.Save();
            yield return Wait(host.ShutdownAsync());
            UnityEngine.Object.Destroy(host.gameObject); yield return null;
            CreateHost(); yield return Wait(host.Ready); RestrictDevices();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu(); yield return Press(Key.Space);
            Assert.That(GameSessionState.GetOrCreate(host).Preferences.BindingDiagnostic, Is.Not.Empty);
            Assert.That(host.Input.ExportBindingOverrides(), Is.Empty);
            Assert.That(PlayerPrefs.GetString(bindingKey), Is.EqualTo(future));
            yield return OpenControls();
            yield return Click(Options().View.ResetBindings);
            Assert.That(host.Input.ExportBindingOverrides(), Is.Empty);
            Assert.That(PlayerPrefs.GetString(bindingKey), Is.EqualTo(future), "Reset remains a draft until Apply.");
            yield return Click(SettingsController().ApplyButton);
            Assert.That(PlayerPrefs.GetString(bindingKey), Is.Not.EqualTo(future));
            var freshStore = new GamePreferences(key); freshStore.LoadBindingsOnce(host.Input);
            Assert.That(freshStore.BindingDiagnostic, Is.Null);
            Assert.That(host.Input.ExportBindingOverrides(), Is.Empty);
            yield return Click(SettingsController().BackButton);
            Assert.That(host.Input.ExportBindingOverrides(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator UxOptionsBackConfirmsDirtyPreviewAndNoRestoresAllDraftValues()
        {
            yield return OpenControls();
            var options = Options();
            var initialSettings = host.Settings.Current;
            Vector2 initialScale = options.View.Scaler.referenceResolution;
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row); yield return Press(Key.E); yield return FinishCapture();
            yield return Click(options.View.Tabs[3]);
            EventSystem.current.SetSelectedGameObject(SettingsController().MasterSlider.gameObject);
            yield return Pad(GamepadButton.DpadLeft);
            yield return Click(options.View.Tabs[4]);
            EventSystem.current.SetSelectedGameObject(options.View.InterfaceScale.gameObject);
            yield return Press(Key.RightArrow);
            Assert.That(options.HasUnsavedChanges, Is.True);
            yield return Press(Key.Escape);
            Assert.That(options.IsOpen, Is.True);
            Assert.That(host.UI.IsOpen(options.View.UnsavedConfirmation.Id), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.View.DiscardChanges.gameObject));
            yield return Click(options.View.DiscardChanges);
            Assert.That(options.IsOpen, Is.False);
            Assert.That(options.View.Scaler.referenceResolution, Is.EqualTo(initialScale));
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/c"));
            Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(initialSettings.MasterVolume));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.PresentationSuffix), Is.False);
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(SettingsController().MenuOptionsButton.gameObject));
        }

        [UnityTest]
        public IEnumerator UxOptionsYesAppliesAndSavesSettingsPresentationAndBindings()
        {
            yield return OpenControls();
            var options = Options();
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row); yield return Press(Key.E); yield return FinishCapture();
            yield return Click(options.View.Tabs[0]);
            EventSystem.current.SetSelectedGameObject(options.View.InvertY.gameObject);
            yield return Press(Key.Enter);
            yield return Click(options.View.Tabs[3]);
            EventSystem.current.SetSelectedGameObject(SettingsController().MasterSlider.gameObject);
            yield return Pad(GamepadButton.DpadLeft);
            float chosenVolume = SettingsController().Draft.MasterVolume;
            yield return Click(SettingsController().BackButton);
            Assert.That(host.UI.IsOpen(options.View.UnsavedConfirmation.Id), Is.True);
            yield return Click(options.View.SaveChanges);
            Assert.That(options.IsOpen, Is.False);
            Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(chosenVolume));
            Assert.That(new GamePreferences(key).Current.InvertY, Is.True);
            Assert.That(PlayerPrefs.HasKey(key), Is.True);
            Assert.That(PlayerPrefs.HasKey(key + GamePreferences.BindingsSuffix), Is.True);
            Assert.That(BindingPath(row), Is.EqualTo("<Keyboard>/e"));
            yield return OpenOptions();
            Assert.That(options.HasUnsavedChanges, Is.False);
            yield return Pad(GamepadButton.East);
            Assert.That(options.IsOpen, Is.False);
            Assert.That(host.UI.IsOpen(options.View.UnsavedConfirmation.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator UxOptionsEquivalentDraftAndTabActionsDoNotPromptOrOpenSaves()
        {
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay(); yield return Press(Key.Escape);
            yield return Click(SettingsController().PauseOptionsButton); yield return null;
            var options = Options();
            yield return Press(Key.E);
            Assert.That(options.CurrentTab, Is.EqualTo(1));
            yield return Pad(GamepadButton.LeftShoulder);
            Assert.That(options.CurrentTab, Is.EqualTo(0));
            Assert.That(Saves().IsOpen, Is.False);
            yield return Pad(GamepadButton.RightShoulder);
            Assert.That(options.CurrentTab, Is.EqualTo(1));
            yield return Press(Key.A);
            Assert.That(options.CurrentTab, Is.EqualTo(0));
            Assert.That(options.IsOpen, Is.True);
            EventSystem.current.SetSelectedGameObject(options.View.InvertY.gameObject);
            yield return Press(Key.Enter); yield return Press(Key.Enter);
            yield return Click(options.View.Tabs[1]);
            var row = Row("Gameplay/IncrementCounter", "<Keyboard>/*");
            yield return StartCapture(row); yield return Press(Key.C); yield return FinishCapture();
            Assert.That(host.Input.ExportBindingOverrides(), Is.Not.Empty, "An explicit default override has equivalent effective values.");
            Assert.That(options.HasUnsavedChanges, Is.False);
            yield return Pad(GamepadButton.East);
            Assert.That(options.IsOpen, Is.False);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(host.UI.IsOpen(options.View.UnsavedConfirmation.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator UxOptionsFailedApplyStaysOpenAndNeverReportsSaved()
        {
            yield return OpenOptions();
            var options = Options();
            Vector2 initialScale = options.View.Scaler.referenceResolution;
            yield return Click(options.View.Tabs[4]);
            EventSystem.current.SetSelectedGameObject(options.View.InterfaceScale.gameObject);
            yield return Press(Key.RightArrow);
            yield return Press(Key.Escape);
            var previousApply = SettingsController().DraftApplyOverride;
            SettingsController().DraftApplyOverride = _ => throw new IOException("Controlled preference save failure.");
            try
            {
                yield return Click(options.View.SaveChanges);
                Assert.That(options.IsOpen, Is.True);
                Assert.That(host.UI.IsOpen(options.View.UnsavedConfirmation.Id), Is.True);
                Assert.That(options.View.ConfirmationStatus.text, Is.EqualTo(GameText.Text("options.status.apply_failed")));
                Assert.That(SettingsController().StatusMessage, Is.Not.EqualTo(GameText.Text("options.status.applied")));
                Assert.That(options.HasUnsavedChanges, Is.True);
                Assert.That(PlayerPrefs.HasKey(key + GamePreferences.PresentationSuffix), Is.False);
            }
            finally { SettingsController().DraftApplyOverride = previousApply; }
            yield return Click(options.View.DiscardChanges);
            Assert.That(options.IsOpen, Is.False);
            Assert.That(options.View.Scaler.referenceResolution, Is.EqualTo(initialScale));
        }

        private int KeyboardCounterIndex()
        {
            var action = host.Input.GetAction("Gameplay/IncrementCounter");
            return Enumerable.Range(0, action.bindings.Count)
                .First(index => action.bindings[index].path == "<Keyboard>/c");
        }

        private static string BindingDocument(string overrides) => "{\"Version\":1,\"Overrides\":\"" +
            overrides.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";

        private void CreateHost()
        {
#if UNITY_EDITOR
            var go = new GameObject("Product Options Isolated Bootstrap"); go.SetActive(false);
            host = go.AddComponent<GameBootstrap>();
            host.Configure(UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProductInputPath));
            const string audio = "Assets/Game/Audio/";
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(audio + "LumiDemo.mixer");
            host.ConfigureSettingsAudio(mixer, mixer.FindMatchingGroups("Master").Single(group => group.name == "Master"),
                mixer.FindMatchingGroups("Music").Single(group => group.name == "Music"),
                mixer.FindMatchingGroups("SFX").Single(group => group.name == "SFX"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "DemoMusic.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "UiClick.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "Gameplay2D.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "Gameplay3D.wav"), key);
            host.ConfigurePersistence("product-options", root);
            go.SetActive(true);
#else
            Assert.Ignore("The asset-backed Starter fixture executes in the Unity Editor.");
#endif
        }

        private void RestrictDevices() => host.Input.RuntimeAsset.devices = new InputDevice[] { keyboard, mouse, gamepad };
        private static GameOptionsController Options() => UnityEngine.Object.FindFirstObjectByType<GameOptionsController>();
        private static GameMenuController Menu() => UnityEngine.Object.FindFirstObjectByType<GameMenuController>();
        private static GameSettingsController SettingsController() => UnityEngine.Object.FindFirstObjectByType<GameSettingsController>();
        private static GameSaveController Saves() => UnityEngine.Object.FindFirstObjectByType<GameSaveController>();
        private static GameSceneController Navigation() => UnityEngine.Object.FindFirstObjectByType<GameSceneController>();

        private IEnumerator ReadyMenu()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (Options() == null || !Options().IsReady || Menu() == null || !Menu().IsReady)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return Wait(Menu().LastOperation); yield return null; yield return null;
        }

        private static IEnumerator ReadyGameplay()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (Options() == null || !Options().IsReady || Saves() == null || !Saves().IsReady)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return null; yield return null;
        }

        private IEnumerator OpenOptions()
        {
            yield return Click(SettingsController().MenuOptionsButton); yield return null;
            Assert.That(Options().IsOpen, Is.True);
        }

        private IEnumerator OpenControls()
        {
            yield return OpenOptions(); yield return Click(Options().View.Tabs[1]);
        }

        private static GameRebindRow Row(string actionPath, string controlPath) => Options().View.BindingRows.First(row =>
            row.ActionPath == actionPath && row.ControlsPath == controlPath);

        private string BindingPath(GameRebindRow row) => host.Input.GetAction(row.ActionPath).bindings
            .First(binding => binding.id == row.BindingId).effectivePath;

        private string ReservedBindings() => string.Join("|", host.Input.RuntimeAsset.actionMaps
            .Where(map => map.name != "Gameplay").SelectMany(map => map.bindings)
            .Select(binding => binding.id + ":" + binding.effectivePath));

        private IEnumerator StartCapture(GameRebindRow row)
        {
            EventSystem.current.SetSelectedGameObject(row.Button.gameObject);
            yield return null; yield return null;
            yield return Click(row.Button);
            float deadline = Time.realtimeSinceStartup + 2;
            while (!host.Input.IsRebinding)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }

        private IEnumerator FinishCapture()
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (host.Input.IsRebinding)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return null; yield return null;
        }

        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!task.IsCompleted)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            if (task.IsFaulted) throw task.Exception;
            Assert.That(task.IsCanceled, Is.False);
        }

        private IEnumerator Press(Key keyValue)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keyValue)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }

        private IEnumerator Pad(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
        }

        private IEnumerator Click(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }
    }
}
