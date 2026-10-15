using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lumivale.Core.Audio;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Persistence;
using Lumivale.Core.SceneFlow;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lumivale.Start.GameTests
{
    /// <summary>Product views, real file storage and native device/EventSystem interaction in an owned namespace.</summary>
    public sealed partial class GameMenuIntegrationTests
    {
        private GameBootstrap host;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private string root;
        private string preferenceKey;
        private float originalTime;
        private bool originalBackground;
        private InputSettings.EditorInputBehaviorInPlayMode originalFocus;
        private InputSettings.BackgroundBehavior originalInputBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTime = Time.timeScale;
            originalBackground = Application.runInBackground;
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
            root = Path.Combine(Path.GetTempPath(), "LumiProductTests-" + unique);
            preferenceKey = "com.lumivale.polish.test." + unique;
            keyboard = InputSystem.AddDevice<Keyboard>("Product Virtual Keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Product Virtual Mouse");
            gamepad = InputSystem.AddDevice<Gamepad>("Product Virtual Gamepad");
            CreateHost();
            yield return Wait(host.Ready);
            host.Input.RuntimeAsset.devices = new InputDevice[] { keyboard, mouse, gamepad };
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            Assert.That(host.Persistence.Configuration.RootPath, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(host.Settings.StorageKey, Is.EqualTo(preferenceKey));
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.Boot.Id));
        }

        private void CreateHost()
        {
#if UNITY_EDITOR
            var go = new GameObject("Product Isolated Bootstrap");
            go.SetActive(false);
            host = go.AddComponent<GameBootstrap>();
            host.Configure(UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Game/Input/GameControls.inputactions"));
            const string audio = "Assets/Game/Audio/";
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(audio + "LumiDemo.mixer");
            host.ConfigureSettingsAudio(mixer, mixer.FindMatchingGroups("Master").Single(g => g.name == "Master"),
                mixer.FindMatchingGroups("Music").Single(g => g.name == "Music"),
                mixer.FindMatchingGroups("SFX").Single(g => g.name == "SFX"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "DemoMusic.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "UiClick.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "Gameplay2D.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "Gameplay3D.wav"), preferenceKey);
            host.ConfigurePersistence("polish-tests", root);
            go.SetActive(true);
#else
            Assert.Fail("This asset fixture is intended for native Editor PlayMode execution.");
#endif
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
            if (preferenceKey != null)
            {
                PlayerPrefs.DeleteKey(preferenceKey);
                PlayerPrefs.DeleteKey(preferenceKey + ".presentation.v1");
                PlayerPrefs.DeleteKey(preferenceKey + ".bindings.v1");
                PlayerPrefs.Save();
            }
            if (root != null && Directory.Exists(root)) Directory.Delete(root, true);
            Time.timeScale = originalTime;
            Application.runInBackground = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalFocus;
            InputSystem.settings.backgroundBehavior = originalInputBackground;
        }

        [UnityTest]
        public IEnumerator FinalUiMenuCanvasOnlyRendersTheChosenSessionScreenDuringSceneActivation()
        {
            foreach (bool dismissed in new[] { false, true })
            {
                GameSessionState.GetOrCreate(host).BootDismissed = dismissed;
                int previousMenu = Menu().GetInstanceID();
                string expected = dismissed ? "MainMenu" : Menu().Views.Boot.Id;
                int presentations = 0;
                var unexpected = new List<string>();
                void InspectPresentation()
                {
                    var menu = Menu();
                    if (menu == null || menu.GetInstanceID() == previousMenu) return;
                    var canvas = menu.Views.Boot.GetComponentInParent<Canvas>(true);
                    if (canvas == null || !canvas.isActiveAndEnabled) return;
                    presentations++;
                    var visible = canvas.GetComponentsInChildren<Lumivale.Core.UI.UiScreen>(true)
                        .Where(screen => screen.gameObject.activeInHierarchy && screen.CanvasGroup.alpha > .001f)
                        .Select(screen => screen.Id).ToArray();
                    if (host.UI.CurrentScreenId != expected || visible.Length != 1 || visible[0] != expected)
                        unexpected.Add("Frame " + Time.frameCount + ": " + string.Join(", ", visible));
                }

                Canvas.willRenderCanvases += InspectPresentation;
                try
                {
                    yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
                    yield return ReadyMenu();
                }
                finally { Canvas.willRenderCanvases -= InspectPresentation; }

                Assert.That(presentations, Is.GreaterThan(0), "Observe the native Canvas presentation callback.");
                Assert.That(unexpected, Is.Empty, "Only the session's selected root may be visible: " + string.Join("; ", unexpected));
                Assert.That(Menu().Views.Boot.GetComponentInParent<Canvas>(true).enabled, Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator KeyboardBootNewGameAndMenuLoadPreserveOneBootstrapAndTheExistingDto()
        {
            int hostId = host.GetInstanceID();
            var runtimeInput = host.Input.RuntimeAsset;
            yield return Press(Key.Space);
            yield return ReadyMenu();
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            Assert.That(Menu().Views.PrimaryLabel.text, Is.EqualTo("New game"));
            yield return Press(Key.Enter);
            yield return ReadyGameplay();
            Assert.That(Gameplay().Counter, Is.Zero);
            Assert.That(Gameplay().SlotId, Is.EqualTo("demo-1"));
            Assert.That(Directory.GetFiles(host.Persistence.StorageDirectory, "*.json"), Is.Empty,
                "New Game does not automatically create or replace a save.");

            var saved = new GameSaveData { Counter = 17, PositionX = 2.25f, PositionY = .5f, PositionZ = -3f };
            yield return Seed("demo-1", saved);
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);
            yield return Click(Navigation().ReturnButton);
            yield return Click(Navigation().ConfirmButton);
            yield return ReadyMenu();
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"), "Boot is shown once per launch, not on scene return.");
            Assert.That(Menu().HasSaves, Is.True);
            Assert.That(Menu().Views.PrimaryLabel.text, Is.EqualTo("Continue game"));
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            StringAssert.Contains("Saved game", Menu().Views.SlotLabels[0].text);
            Assert.That(Menu().CurrentSlotStatuses[0].Metadata.ModelId, Is.EqualTo("starter-cube"));
            Assert.That(Menu().CurrentSlotStatuses[0].Metadata.DataVersion, Is.EqualTo(1));
            yield return Click(Menu().Views.Slots[0]);
            yield return Click(Menu().Views.Slots[0]);
            yield return ReadyGameplay();
            Assert.That(Gameplay().Counter, Is.EqualTo(saved.Counter));
            Assert.That(Navigation().PlayerPosition, Is.EqualTo(new Vector3(saved.PositionX, saved.PositionY, saved.PositionZ)));
            Assert.That(host.GetInstanceID(), Is.EqualTo(hostId));
            Assert.That(host.Input.RuntimeAsset, Is.SameAs(runtimeInput));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(host.SourceAsset.actionMaps.All(map => !map.enabled), Is.True);
        }

        [UnityTest]
        public IEnumerator GamepadSlotShortcutAndConfirmedDeletionRefreshTheContinueLabelAndFocus()
        {
            yield return Pad(GamepadButton.South);
            yield return ReadyMenu();
            yield return Seed("demo-1", new GameSaveData { Counter = 1, PositionY = .5f });
            yield return Seed("demo-2", new GameSaveData { Counter = 2, PositionY = .5f });
            yield return Wait(Menu().RefreshSlotsAsync());
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            yield return Pad(GamepadButton.DpadDown);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-2"), "Native focus chooses the highlighted slot.");
            byte[] occupied = File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json"));
            yield return Pad(GamepadButton.West);
            Assert.That(host.UI.IsOpen(Menu().Views.DeleteConfirmation.Id), Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
            CollectionAssert.AreEqual(occupied, File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")));
            yield return Pad(GamepadButton.East);
            yield return Pad(GamepadButton.DpadDown);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-3"));
            yield return Pad(GamepadButton.West);
            yield return ReadyGameplay();
            Assert.That(Gameplay().SlotId, Is.EqualTo("demo-3"));
            Assert.That(Gameplay().Counter, Is.Zero);
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-3.json")), Is.False);
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            yield return Pad(GamepadButton.DpadUp);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-2"));
            yield return Click(Menu().Views.Delete);
            Assert.That(host.UI.IsOpen(Menu().Views.DeleteConfirmation.Id), Is.True);
            yield return Pad(GamepadButton.East);
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Delete.gameObject));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.True);
            for (int slot = 1; slot >= 0; slot--)
            {
                if (Menu().SelectedSlotId != "demo-" + (slot + 1)) yield return Pad(GamepadButton.DpadUp);
                yield return Click(Menu().Views.Delete);
                yield return Click(Menu().Views.DeleteYes);
                yield return Wait(Menu().LastOperation);
                Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-" + (slot + 1) + ".json")), Is.False);
            }
            Assert.That(Menu().HasSaves, Is.False);
            Assert.That(Menu().Views.PrimaryLabel.text, Is.EqualTo("New game"));
            yield return Press(Key.Escape);
            yield return ReadyMenu();
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Primary.gameObject));
        }

        [UnityTest]
        public IEnumerator CreditsEnterFromBottomOnEveryOpenAndCrossTheViewportBeforeRepeating()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            var menu = Menu();
            var roll = menu.Views.CreditsRoll;
            var viewport = (RectTransform)roll.parent;
            Time.timeScale = 0f;
            for (int opening = 0; opening < 2; opening++)
            {
                float entryDistance = float.NaN;
                float frameAllowance = 0f;
                void ObserveEntry()
                {
                    if (!float.IsNaN(entryDistance) || !host.UI.IsOpen(menu.Views.Credits.Id)) return;
                    Vector3 top = roll.TransformPoint(new Vector3(0f, roll.rect.yMax, 0f));
                    entryDistance = viewport.InverseTransformPoint(top).y - viewport.rect.yMin;
                    frameAllowance = Mathf.Max(10f, 34f * Time.unscaledDeltaTime * 2f);
                }

                Canvas.willRenderCanvases += ObserveEntry;
                try { yield return Click(menu.Views.CreditsButton); }
                finally { Canvas.willRenderCanvases -= ObserveEntry; }

                Assert.That(float.IsNaN(entryDistance), Is.False, "Observe the first native presentation of Credits.");
                Assert.That(entryDistance, Is.InRange(-1f, frameAllowance),
                    "The top of the roll enters at the viewport bottom, including when Credits reopens.");
                Assert.That(roll.anchorMin.y, Is.Zero);
                Assert.That(roll.anchorMax.y, Is.Zero);
                Assert.That(roll.pivot.y, Is.EqualTo(1f));

                float before = roll.anchoredPosition.y;
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(roll.anchoredPosition.y, Is.GreaterThan(before), "Credits use unscaled time.");

                // Put the roll past the old text-only repeat threshold without waiting
                // through the entire credits. It still has a viewport to travel across.
                float textHeight = roll.GetComponent<TMPro.TMP_Text>().preferredHeight;
                roll.anchoredPosition = new Vector2(roll.anchoredPosition.x, textHeight + 24f);
                yield return null;
                Assert.That(roll.anchoredPosition.y, Is.GreaterThan(textHeight),
                    "A bottom-entering roll cannot repeat before its last line clears the viewport top.");
                yield return Press(Key.Escape);
                Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SpaceBootCreditsUseUnscaledTimeAndQuitRestoresFocusBeforeAwaitedShutdown()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            yield return Click(Menu().Views.CreditsButton);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.Credits.Id));
            Time.timeScale = 0f;
            float before = Menu().Views.CreditsRoll.anchoredPosition.y;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(Menu().Views.CreditsRoll.anchoredPosition.y, Is.GreaterThan(before));
            yield return Pad(GamepadButton.East);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            float closed = Menu().Views.CreditsRoll.anchoredPosition.y;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Menu().Views.CreditsRoll.anchoredPosition.y, Is.EqualTo(closed));
            Time.timeScale = 1f;
            yield return Click(Menu().Views.Exit);
            Assert.That(host.UI.IsOpen(Menu().Views.ConfirmQuit.Id), Is.True);
            yield return Pad(GamepadButton.East);
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Exit.gameObject));
            int exits = 0;
            var menu = Menu();
            menu.QuitRequested = () => { exits++; return Task.CompletedTask; };
            yield return Click(menu.Views.Exit);
            yield return Click(menu.Views.QuitYes);
            yield return Wait(menu.LastOperation);
            yield return null;
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(menu.QuitConfirmed, Is.True);
            Assert.That(host.Coordinator.State, Is.EqualTo(BootstrapState.Stopped));
            Assert.That(host.Persistence.IsInitialized, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CorruptAndFutureVersionSlotsStayVisibleAndCannotBeUsedAsEmptyGames()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            string corruptPath = Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json");
            File.WriteAllText(corruptPath, "{fixture-corrupt");
            var future = host.Persistence.SaveAsync("demo-2", new GameSaveData { Counter = 9, PositionY = .5f },
                new SaveSchema<GameSaveData>("starter-cube", 2));
            yield return Wait(future);
            Assert.That(future.Result.IsSuccess, Is.True);
            byte[] futureBytes = File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json"));
            yield return Wait(Menu().RefreshSlotsAsync());
            Assert.That(Menu().HasSaves, Is.True);
            Assert.That(Menu().CurrentSlotStatuses[0].Status, Is.EqualTo(PersistenceStatus.Corrupt));
            Assert.That(Menu().CurrentSlotStatuses[1].Status, Is.EqualTo(PersistenceStatus.Incompatible));
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            StringAssert.Contains("Corrupt", Menu().Views.SlotLabels[0].text);
            StringAssert.Contains("Incompatible", Menu().Views.SlotLabels[1].text);
            yield return Click(Menu().Views.Slots[0]);
            yield return Click(Menu().Views.Slots[0]);
            yield return Wait(Menu().LastOperation);
            Assert.That(Menu().LastStatus, Is.EqualTo(PersistenceStatus.Corrupt));
            Assert.That(Menu().Views.NewGame.gameObject.activeSelf, Is.False);
            Assert.That(Menu().Views.Delete.gameObject.activeSelf, Is.True);
            Assert.That(File.ReadAllText(corruptPath), Is.EqualTo("{fixture-corrupt"));
            yield return Click(Menu().Views.Slots[1]);
            yield return Click(Menu().Views.Slots[1]);
            yield return Wait(Menu().LastOperation);
            Assert.That(Menu().LastStatus, Is.EqualTo(PersistenceStatus.Incompatible));
            CollectionAssert.AreEqual(futureBytes, File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")));
            Assert.That(host.UI.IsOpen(Menu().Views.SaveSelection.Id), Is.True);
        }

        [UnityTest]
        public IEnumerator MenuLoadsValidatedBackupAndGameplayStillProtectsTheCorruptPrimary()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            yield return Seed("demo-1", new GameSaveData { Counter = 11, PositionX = -2f, PositionY = .5f });
            yield return Seed("demo-1", new GameSaveData { Counter = 12, PositionX = 3f, PositionY = .5f });
            string path = Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json");
            File.WriteAllText(path, "{fixture-interrupted");
            yield return Wait(Menu().RefreshSlotsAsync());
            Assert.That(Menu().CurrentSlotStatuses[0].RecoveredFromBackup, Is.True);
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            StringAssert.Contains("Recoverable", Menu().Views.SlotLabels[0].text);
            yield return Click(Menu().Views.Slots[0]);
            yield return Click(Menu().Views.Slots[0]);
            yield return ReadyGameplay();
            Assert.That(Gameplay().Counter, Is.EqualTo(11));
            Assert.That(Navigation().PlayerPosition, Is.EqualTo(new Vector3(-2f, .5f, 0f)));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{fixture-interrupted"));
            yield return Press(Key.Tab);
            yield return Wait(Gameplay().LastOperation);
            yield return Click(Gameplay().SaveButton);
            yield return Wait(Gameplay().LastOperation);
            Assert.That(Gameplay().LastStatus, Is.EqualTo(PersistenceStatus.Corrupt));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{fixture-interrupted"));
        }

        [UnityTest]
        public IEnumerator ReenablingProductPresenterReleasesRegistrationsAndDoesNotReplayBoot()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            var menu = Menu();
            menu.enabled = false;
            Assert.That(menu.IsReady, Is.False);
            menu.enabled = true;
            yield return ReadyMenu();
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            yield return Click(menu.Views.CreditsButton);
            yield return Press(Key.Escape);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UiPolishTwoMouseClicksLoadSameSlotWithoutADoubleClickTimeout()
        {
            yield return Press(Key.Space);
            yield return Seed("demo-1", new GameSaveData { Counter = 11, PositionY = .5f });
            var expected = new GameSaveData { Counter = 29, PositionX = 4f, PositionY = .5f, PositionZ = -2f };
            yield return Seed("demo-2", expected);
            yield return OpenSaveSelection();
            int bootstrap = host.GetInstanceID();
            int completed = 0;
            Action<SceneLoadResult> onCompleted = _ => completed++;
            host.Scenes.LoadCompleted += onCompleted;
            try
            {
                yield return Click(Menu().Views.Slots[1]);
                Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-2"));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
                Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id));
                Assert.That(completed, Is.Zero, "The first pointer click only selects the save slot.");
                yield return new WaitForSecondsRealtime(.75f);
                yield return Click(Menu().Views.Slots[1]);
                yield return ReadyGameplay();
                Assert.That(Gameplay().SlotId, Is.EqualTo("demo-2"));
                Assert.That(Gameplay().Counter, Is.EqualTo(expected.Counter));
                Assert.That(Navigation().PlayerPosition, Is.EqualTo(new Vector3(4f, .5f, -2f)));
                Assert.That(host.GetInstanceID(), Is.EqualTo(bootstrap));
                Assert.That(completed, Is.EqualTo(1));
            }
            finally { host.Scenes.LoadCompleted -= onCompleted; }
        }

        [UnityTest]
        public IEnumerator UiPolishMouseTrackingResetsOnSelectionReopenAndRefreshWhileEmptySlotsStaySelected()
        {
            yield return Press(Key.Space);
            yield return Seed("demo-1", new GameSaveData { Counter = 11, PositionY = .5f });
            yield return Seed("demo-2", new GameSaveData { Counter = 12, PositionY = .5f });
            yield return OpenSaveSelection();
            yield return Click(Menu().Views.Slots[0]);
            yield return Hover(Menu().Views.Slots[1]);
            yield return Hover(Menu().Views.Slots[0]);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath),
                "Hovering never activates a save.");

            yield return Click(Menu().Views.Slots[1]);
            yield return Click(Menu().Views.Slots[0]);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-1"));
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id),
                "Returning to a previously clicked slot starts a fresh pointer sequence.");
            yield return Pad(GamepadButton.DpadDown);
            yield return Pad(GamepadButton.DpadUp);
            yield return Click(Menu().Views.Slots[0]);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id),
                "Selecting another slot by navigation also resets pointer tracking.");

            yield return Press(Key.Escape);
            yield return OpenSaveSelection();
            yield return Click(Menu().Views.Slots[0]);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id),
                "Closing and reopening Save Selection resets pointer tracking.");
            yield return Wait(Menu().RefreshSlotsAsync());
            yield return Click(Menu().Views.Slots[0]);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id),
                "Refreshing the slot list resets pointer tracking.");

            yield return Click(Menu().Views.Slots[2]);
            yield return Click(Menu().Views.Slots[2]);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-3"));
            Assert.That(Menu().Views.NewGame.gameObject.activeSelf, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-3.json")), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UiPolishSubmitStillLoadsImmediatelyAndSimultaneousValidationLoadsOnlyOnce()
        {
            yield return Pad(GamepadButton.South);
            yield return Seed("demo-1", new GameSaveData { Counter = 31, PositionX = -1f, PositionY = .5f });
            for (int validation = 0; validation < 3; validation++)
            {
                yield return OpenSaveSelection();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Slots[0].gameObject));
                int completed = 0;
                Action<SceneLoadResult> onCompleted = _ => completed++;
                host.Scenes.LoadCompleted += onCompleted;
                try
                {
                    if (validation == 0) yield return Press(Key.Enter);
                    else if (validation == 1) yield return Pad(GamepadButton.South);
                    else yield return SubmitAndClickTogether(Menu().Views.Slots[0]);
                    yield return ReadyGameplay();
                    Assert.That(Gameplay().Counter, Is.EqualTo(31));
                    Assert.That(Gameplay().SlotId, Is.EqualTo("demo-1"));
                    Assert.That(completed, Is.EqualTo(1), "Concurrent submit/click must not start a second scene load.");
                    Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                    LogAssert.NoUnexpectedReceived();
                }
                finally { host.Scenes.LoadCompleted -= onCompleted; }
                if (validation < 2)
                {
                    yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
                    yield return ReadyMenu();
                }
            }
        }

        [UnityTest]
        public IEnumerator UxSaveEmptySlotSubmitOnlySelectsAndGamepadXStartsWithoutAutosave()
        {
            yield return Press(Key.Space);
            yield return Seed("demo-1", new GameSaveData { Counter = 8, PositionY = .5f });
            yield return OpenSaveSelection();
            yield return Click(Menu().Views.Slots[1]);
            yield return Press(Key.Enter);
            yield return Pad(GamepadButton.South);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-2"));
            Assert.That(Menu().Views.NewGame.gameObject.activeSelf, Is.True);
            Assert.That(Menu().Views.Delete.gameObject.activeSelf, Is.False);
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.False);
            yield return Pad(GamepadButton.West);
            yield return ReadyGameplay();
            Assert.That(Gameplay().SlotId, Is.EqualTo("demo-2"));
            Assert.That(Gameplay().Counter, Is.Zero);
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.False,
                "Choosing a future save slot does not create an implicit save.");
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json")), Is.True);
        }

        [UnityTest]
        public IEnumerator UxSaveOccupiedXAndKeyboardActionConfirmDeleteThenRefreshTheSameSlotAndFocus()
        {
            yield return Press(Key.Space);
            yield return Seed("demo-1", new GameSaveData { Counter = 41, PositionY = .5f });
            yield return Seed("demo-2", new GameSaveData { Counter = 42, PositionY = .5f });
            byte[] untouched = File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json"));
            yield return OpenSaveSelection();
            yield return Pad(GamepadButton.DpadDown);
            Assert.That(Menu().SelectedSlotId, Is.EqualTo("demo-2"));
            Assert.That(Menu().Views.NewGame.gameObject.activeSelf, Is.False);
            Assert.That(Menu().Views.Delete.gameObject.activeSelf, Is.True);
            yield return Pad(GamepadButton.West);
            Assert.That(host.UI.IsOpen(Menu().Views.DeleteConfirmation.Id), Is.True);
            yield return Pad(GamepadButton.East);
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Slots[1].gameObject));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.True);
            yield return Press(Key.N);
            Assert.That(host.UI.IsOpen(Menu().Views.DeleteConfirmation.Id), Is.True);
            yield return Click(Menu().Views.DeleteYes);
            yield return Wait(Menu().LastOperation);
            yield return null;
            Assert.That(Menu().CurrentSlotStatuses[1].Status, Is.EqualTo(PersistenceStatus.NotFound));
            Assert.That(Menu().Views.NewGame.gameObject.activeSelf, Is.True);
            Assert.That(Menu().Views.Delete.gameObject.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Menu().Views.Slots[1].gameObject));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.False);
            CollectionAssert.AreEqual(untouched, File.ReadAllBytes(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json")));
            yield return Click(Menu().Views.NewGame);
            yield return ReadyGameplay();
            Assert.That(Gameplay().SlotId, Is.EqualTo("demo-2"));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-2.json")), Is.False);
        }

        [UnityTest]
        public IEnumerator UxSaveStartPromptUsesSpaceOrGamepadAAndFadesWithUnscaledTime()
        {
            var menu = Menu();
            Assert.That(menu.Views.StartPrompt.text, Is.EqualTo("Press Space to continue"));
            Assert.That(menu.Views.Boot.FirstSelected, Is.Null);
            yield return Press(Key.Enter);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .5f) }
                .WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null; yield return null;
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.Boot.Id), "Enter and mouse clicks do not validate Start.");
            Time.timeScale = 0f;
            float minAlpha = menu.Views.StartPrompt.color.a;
            float maxAlpha = minAlpha;
            float until = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < until)
            {
                minAlpha = Mathf.Min(minAlpha, menu.Views.StartPrompt.color.a);
                maxAlpha = Mathf.Max(maxAlpha, menu.Views.StartPrompt.color.a);
                yield return null;
            }
            Assert.That(maxAlpha - minAlpha, Is.GreaterThan(.02f), "The text must fade even while scaled time is stopped.");
            yield return Pad(GamepadButton.West);
            Assert.That(menu.Views.StartPrompt.text, Is.EqualTo("Press A to continue"));
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.Boot.Id));
            yield return Pad(GamepadButton.South);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            Assert.That(GameSessionState.GetOrCreate(host).BootDismissed, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath),
                "The same A press must not also submit New game in the destination menu.");
            Time.timeScale = 1f;
        }

        private IEnumerator OpenSaveSelection()
        {
            yield return Wait(Menu().RefreshSlotsAsync());
            yield return Click(Menu().Views.Primary);
            yield return Wait(Menu().LastOperation);
            yield return null;
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(Menu().Views.SaveSelection.Id));
        }

        private IEnumerator SubmitAndClickTogether(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null;
        }

        private IEnumerator Seed(string slotId, GameSaveData data)
        {
            var task = host.Persistence.SaveAsync(slotId, data, GameSaveData.CreateSchema());
            yield return Wait(task);
            Assert.That(task.Result.IsSuccess, Is.True, task.Result.Message);
        }

        private static GameMenuController Menu() => UnityEngine.Object.FindFirstObjectByType<GameMenuController>();
        private static GameSceneController Navigation() => UnityEngine.Object.FindFirstObjectByType<GameSceneController>();
        private static GameSaveController Gameplay() => UnityEngine.Object.FindFirstObjectByType<GameSaveController>();

        private IEnumerator ReadyMenu()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().path != GameScenePaths.MenuPath || Menu() == null ||
                !Menu().IsReady || Menu().IsBusy || host.Scenes.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The product menu did not become interactive.");
                yield return null;
            }
            yield return null;
            yield return null;
        }

        private IEnumerator ReadyGameplay()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().path != GameScenePaths.GameplayPath || Gameplay() == null ||
                !Gameplay().IsReady || Navigation() == null || !Navigation().IsReady || host.Scenes.IsLoading ||
                UnityEngine.Object.FindFirstObjectByType<GameplaySnapshotAdapter>() == null ||
                !UnityEngine.Object.FindFirstObjectByType<GameplaySnapshotAdapter>().IsReady)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The product gameplay did not become interactive.");
                yield return null;
            }
            yield return null;
            yield return null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        private IEnumerator Pad(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null;
        }

        private IEnumerator Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.IsInteractable(), Is.True, "The fixture must click an actual interactive button: " + button.name);
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }

        private IEnumerator Hover(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }

        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "An actual storage/lifecycle operation timed out.");
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }
}
