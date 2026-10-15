using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Lumivale.Core.Audio;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.Persistence;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.Settings;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lumivale.Start.GameTests
{
    /// <summary>Each genuine Editor session has fresh product state, even when static/scene reload are disabled.</summary>
    public sealed class GamePlaySessionTests
    {
        private bool originalOptionsEnabled;
        private bool originalPaused;
        private EnterPlayModeOptions originalOptions;
        private bool originalPlayerBackground;
        private bool originalBackground;
        private float originalTime;
        private int originalQuality;
        private int originalWidth;
        private int originalHeight;
        private FullScreenMode originalMode;
        private string originalScene;
        private InputSettings.EditorInputBehaviorInPlayMode originalInputFocus;
        private InputSettings.BackgroundBehavior originalInputBackground;
        private Keyboard keyboard;
        private string root;
        private string gameId;
        private string preferenceKey;
        private string[] observedKeys;
        private bool[] originalKeyPresence;
        private string[] originalKeyValues;

        [SetUp]
        public void SetUp()
        {
            originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            originalPaused = EditorApplication.isPaused;
            originalOptions = EditorSettings.enterPlayModeOptions;
            originalPlayerBackground = PlayerSettings.runInBackground;
            originalBackground = Application.runInBackground;
            originalTime = Time.timeScale;
            originalQuality = QualitySettings.GetQualityLevel();
            originalWidth = Screen.width;
            originalHeight = Screen.height;
            originalMode = Screen.fullScreenMode;
            originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            originalInputFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalInputBackground = InputSystem.settings.backgroundBehavior;
            observedKeys = new[] { GameBootstrap.DefaultPreferenceKey,
                GameBootstrap.DefaultPreferenceKey + ".presentation.v1",
                GameBootstrap.DefaultPreferenceKey + ".bindings.v1" };
            originalKeyPresence = new bool[observedKeys.Length];
            originalKeyValues = new string[observedKeys.Length];
            for (int i = 0; i < observedKeys.Length; i++)
            {
                originalKeyPresence[i] = PlayerPrefs.HasKey(observedKeys[i]);
                originalKeyValues[i] = PlayerPrefs.GetString(observedKeys[i]);
            }
            string unique = Guid.NewGuid().ToString("N");
            root = Path.Combine(Path.GetTempPath(), "LumiProductSessions-" + unique);
            gameId = "polish-session-" + unique;
            preferenceKey = "com.lumivale.polish.session." + unique;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            PlayerSettings.runInBackground = true;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            EditorApplication.isPaused = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Exception shutdownFailure = null;
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPaused = false;
                    if (LumiBootstrap.Instance != null)
                    {
                        var stopping = LumiBootstrap.Instance.ShutdownAsync();
                        double deadline = EditorApplication.timeSinceStartup + 20d;
                        while (!stopping.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                        {
                            EditorApplication.QueuePlayerLoopUpdate();
                            yield return null;
                        }
                        if (!stopping.IsCompleted)
                            shutdownFailure = new TimeoutException("Fixture shutdown did not complete before exiting Play mode.");
                        else if (stopping.IsFaulted) shutdownFailure = stopping.Exception;
                        else if (stopping.IsCanceled)
                            shutdownFailure = new InvalidOperationException("Fixture shutdown was unexpectedly cancelled.");
                    }
                    // An assertion must not leave the Editor in Play mode or skip restoring owned state.
                    yield return new ExitPlayMode();
                }
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
                EditorSettings.enterPlayModeOptions = originalOptions;
                PlayerSettings.runInBackground = originalPlayerBackground;
                Application.runInBackground = originalBackground;
                Time.timeScale = originalTime;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalInputFocus;
                InputSystem.settings.backgroundBehavior = originalInputBackground;
                PlayerPrefs.DeleteKey(preferenceKey);
                PlayerPrefs.DeleteKey(preferenceKey + ".presentation.v1");
                PlayerPrefs.DeleteKey(preferenceKey + ".bindings.v1");
                PlayerPrefs.Save();
                QualitySettings.SetQualityLevel(originalQuality, true);
                Screen.SetResolution(originalWidth, originalHeight, originalMode);
                EditorApplication.isPaused = originalPaused;
                if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(originalScene))
                    EditorSceneManager.OpenScene(originalScene);
                if (Directory.Exists(root)) Directory.Delete(root, true);
                AssertUserPreferencesUnchanged();
            }
            if (shutdownFailure != null) throw shutdownFailure;
        }

        [UnityTest]
        public IEnumerator FreshBootReturnsOnEachSessionWithoutDomainReload() =>
            Sessions(EnterPlayModeOptions.DisableDomainReload);

        [UnityTest]
        public IEnumerator FreshBootReturnsOnEachSessionWithoutDomainOrSceneReload() =>
            Sessions(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload);

        private IEnumerator Sessions(EnterPlayModeOptions options)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = options;
            InputService previousInput = null;
            PersistenceService previousPersistence = null;
            Lumivale.Core.Settings.SettingsService previousSettings = null;
            AudioService previousAudio = null;
            var saved = new GameSaveData { Counter = 7, PositionX = 1.25f, PositionY = .5f, PositionZ = -2f };
            for (int pass = 0; pass < 2; pass++)
            {
                EditorSceneManager.OpenScene(GameScenePaths.MenuPath);
                var configured = Object.FindFirstObjectByType<GameBootstrap>();
                Assert.That(configured, Is.Not.Null);
                configured.ConfigurePersistence(gameId, root);
                var serialized = new SerializedObject(configured);
                serialized.FindProperty("preferenceKey").stringValue = preferenceKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                yield return new EnterPlayMode(expectDomainReload: false);
                EditorApplication.isPaused = false;
                Application.runInBackground = true;
                var host = LumiBootstrap.Instance as GameBootstrap;
                Assert.That(host, Is.Not.Null);
                yield return Wait(host.Ready, "Bootstrap Ready");
                keyboard = InputSystem.AddDevice<Keyboard>("Product Session Keyboard");
                host.Input.RuntimeAsset.devices = new InputDevice[] { keyboard };
                yield return ReadyMenu();
                var menu = Object.FindFirstObjectByType<GameMenuController>();
                var session = GameSessionState.GetOrCreate(host);
                Assert.That(host.Input, Is.Not.SameAs(previousInput));
                Assert.That(host.Persistence, Is.Not.SameAs(previousPersistence));
                Assert.That(host.Settings, Is.Not.SameAs(previousSettings));
                Assert.That(host.Audio, Is.Not.SameAs(previousAudio));
                Assert.That(session.BootDismissed, Is.False);
                Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.Boot.Id));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null,
                    "The Start screen contains a text prompt and has no selectable button.");
                Assert.That(host.Persistence.Configuration.RootPath, Is.EqualTo(Path.GetFullPath(root)));
                Assert.That(host.Persistence.Configuration.GameId, Is.EqualTo(gameId));
                Assert.That(host.Settings.StorageKey, Is.EqualTo(preferenceKey));
                Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(host.Audio.IsMusicPlaying, Is.True, "Session " + pass + ": music must play after Ready.");
                if (pass == 0)
                {
                    var values = host.Settings.Current;
                    values.MasterVolume = .42f;
                    values.MusicVolume = .31f;
                    values.SfxVolume = .63f;
                    host.Settings.Update(values);
                    host.Settings.Save();
                    var save = host.Persistence.SaveAsync("demo-1", saved, GameSaveData.CreateSchema());
                    yield return Wait(save);
                    Assert.That(save.Result.IsSuccess, Is.True, save.Result.Message);
                }
                else
                {
                    Assert.That(host.Settings.LastLoadStatus, Is.EqualTo(SettingsLoadStatus.Loaded));
                    var load = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
                    yield return Wait(load);
                    Assert.That(load.Result.IsSuccess, Is.True, load.Result.Message);
                    Assert.That(JsonUtility.ToJson(load.Result.Data), Is.EqualTo(JsonUtility.ToJson(saved)),
                        "A new service must release and reacquire the same owned directory across Play sessions.");
                }
                Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(.42f));
                Assert.That(host.Settings.Current.MusicVolume, Is.EqualTo(.31f));
                Assert.That(host.Settings.Current.SfxVolume, Is.EqualTo(.63f));
                AssertAudioSynchronized(host);
                yield return Press(Key.Space);
                yield return ReadyMenu();
                Assert.That(session.BootDismissed, Is.True, "Session " + pass + ": Space must dismiss Start.");
                Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
                Assert.That(host.UI.ModalCount, Is.Zero);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.Views.Primary.gameObject));
                yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)),
                    "Menu to gameplay scene load");
                yield return ReadyGameplay();
                var gameplay = Object.FindFirstObjectByType<GameSaveController>();
                Assert.That(gameplay.Counter, Is.Zero);
                yield return Press(Key.C);
                Assert.That(gameplay.Counter, Is.EqualTo(1), "A single action has exactly one live gameplay callback.");
                yield return Press(Key.Escape);
                Assert.That(host.Pause.IsPaused, Is.True, "Session " + pass + ": Escape must open Pause after gameplay input.");
                Assert.That(Time.timeScale, Is.Zero);
                yield return Press(Key.C);
                Assert.That(gameplay.Counter, Is.EqualTo(1), "Gameplay remains inactive while paused.");
                yield return Press(Key.Escape);
                Assert.That(host.Pause.IsPaused, Is.False);
                yield return Press(Key.C);
                Assert.That(gameplay.Counter, Is.EqualTo(2));
                Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                AssertAudioSynchronized(host);
                AssertUserPreferencesUnchanged();
                previousInput = host.Input;
                previousPersistence = host.Persistence;
                previousSettings = host.Settings;
                previousAudio = host.Audio;
                yield return Wait(host.ShutdownAsync(), "Controlled session shutdown");
                yield return PlayerFrames(1);
                Assert.That(previousInput.IsInitialized, Is.False);
                Assert.That(previousPersistence.IsInitialized, Is.False);
                Assert.That(previousSettings.IsInitialized, Is.False);
                Assert.That(previousAudio.IsInitialized, Is.False);
                Assert.That(Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Is.Empty);
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                yield return new ExitPlayMode();
                AssertUserPreferencesUnchanged();
            }
        }

        private void AssertUserPreferencesUnchanged()
        {
            for (int i = 0; i < observedKeys.Length; i++)
            {
                Assert.That(PlayerPrefs.HasKey(observedKeys[i]), Is.EqualTo(originalKeyPresence[i]), observedKeys[i]);
                Assert.That(PlayerPrefs.GetString(observedKeys[i]), Is.EqualTo(originalKeyValues[i]), observedKeys[i]);
            }
        }

        private static void AssertAudioSynchronized(GameBootstrap host)
        {
            var values = host.Settings.Current;
            Assert.That(host.Audio.MasterVolume, Is.EqualTo(values.MasterVolume));
            Assert.That(host.Audio.MusicVolume, Is.EqualTo(values.MusicVolume));
            Assert.That(host.Audio.SfxVolume, Is.EqualTo(values.SfxVolume));
            Assert.That(host.Mixer.GetFloat("MasterVolume", out float master), Is.True);
            Assert.That(host.Mixer.GetFloat("MusicVolume", out float music), Is.True);
            Assert.That(host.Mixer.GetFloat("SfxVolume", out float sfx), Is.True);
            Assert.That(master, Is.EqualTo(AudioVolume.NormalizedToDecibels(values.MasterVolume)).Within(.001f));
            Assert.That(music, Is.EqualTo(AudioVolume.NormalizedToDecibels(values.MusicVolume)).Within(.001f));
            Assert.That(sfx, Is.EqualTo(AudioVolume.NormalizedToDecibels(values.SfxVolume)).Within(.001f));
        }

        private static IEnumerator ReadyGameplay()
        {
            double deadline = EditorApplication.timeSinceStartup + 20d;
            while (Object.FindFirstObjectByType<GameSaveController>() == null ||
                !Object.FindFirstObjectByType<GameSaveController>().IsReady ||
                Object.FindFirstObjectByType<GameSceneController>() == null ||
                !Object.FindFirstObjectByType<GameSceneController>().IsReady ||
                Object.FindFirstObjectByType<GameplaySnapshotAdapter>() == null ||
                !Object.FindFirstObjectByType<GameplaySnapshotAdapter>().IsReady)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Gameplay did not become ready.");
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            yield return PlayerFrames(2);
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return PlayerFrames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return PlayerFrames(2);
        }

        private static IEnumerator PlayerFrames(int count)
        {
            int first = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + 20d;
            while (Time.frameCount - first < count)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                    "Waiting for genuine Player frames while the Editor test runner continues to tick.");
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
        }

        private static IEnumerator ReadyMenu()
        {
            double deadline = EditorApplication.timeSinceStartup + 20d;
            GameMenuController menu;
            do
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                menu = Object.FindFirstObjectByType<GameMenuController>();
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Product startup did not become ready.");
            }
            while (menu == null || !menu.IsReady || menu.IsBusy);
            yield return PlayerFrames(2);
        }

        private static IEnumerator Wait(Task task, string operation = "Product lifecycle work")
        {
            double deadline = EditorApplication.timeSinceStartup + 20d;
            while (!task.IsCompleted)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                    operation + " timed out at native frame " + Time.frameCount + ".");
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }
}
