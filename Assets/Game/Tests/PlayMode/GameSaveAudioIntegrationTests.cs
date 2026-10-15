using System.Collections;
using System.IO;
using System.Linq;
using Lumivale.Core.Audio;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.Persistence;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.Settings;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameMenuIntegrationTests
    {
        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator GameplaySaveUiRestoresMutatedCubeCounterAcrossThreeSlots()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            yield return Press(Key.Enter);
            yield return ReadyGameplay();
            var view = Gameplay();
            Vector3 initial = Navigation().PlayerPosition;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            for (int frame = 0; frame < 10; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Distance(initial, Navigation().PlayerPosition), Is.GreaterThan(.01f));
            yield return Press(Key.C);
            yield return Pad(GamepadButton.RightShoulder);
            Assert.That(view.Counter, Is.EqualTo(2));
            Vector3 savedPosition = Navigation().PlayerPosition;

            yield return Press(Key.Tab);
            yield return Wait(view.LastOperation);
            Assert.That(view.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            yield return Click(view.SaveButton);
            yield return Wait(view.LastOperation);
            Assert.That(view.LastStatus, Is.EqualTo(PersistenceStatus.Success));
            Assert.That(File.Exists(Path.Combine(host.Persistence.StorageDirectory, "slot-demo-1.json")), Is.True);
            yield return Click(view.IncreaseButton);
            Assert.That(view.Counter, Is.EqualTo(3));
            for (int slot = 2; slot <= 3; slot++)
            {
                EventSystem.current.SetSelectedGameObject(view.SlotButton.gameObject);
                yield return Pad(GamepadButton.DpadRight);
                yield return Wait(view.LastOperation);
                Assert.That(view.SlotId, Is.EqualTo("demo-" + slot));
                yield return Click(view.SaveButton);
                yield return Wait(view.LastOperation);
                Assert.That(view.LastStatus, Is.EqualTo(PersistenceStatus.Success));
            }
            var slots = host.Persistence.ListSlotsAsync();
            yield return Wait(slots);
            Assert.That(slots.Result.Data.Count, Is.EqualTo(3));
            Assert.That(Directory.GetFiles(host.Persistence.StorageDirectory, "*.json"), Has.Length.EqualTo(3));

            yield return Pad(GamepadButton.East);
            Assert.That(view.IsOpen, Is.False);
            Assert.That(host.Pause.IsPaused, Is.True);
            yield return Pad(GamepadButton.East);
            Assert.That(host.Pause.IsPaused, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
            for (int frame = 0; frame < 10; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Distance(savedPosition, Navigation().PlayerPosition), Is.GreaterThan(.01f));
            yield return Press(Key.C);
            Assert.That(view.Counter, Is.EqualTo(4));

            yield return Pad(GamepadButton.LeftShoulder);
            yield return Wait(view.LastOperation);
            EventSystem.current.SetSelectedGameObject(view.SlotButton.gameObject);
            yield return Pad(GamepadButton.DpadRight);
            yield return Wait(view.LastOperation);
            Assert.That(view.SlotId, Is.EqualTo("demo-1"));
            yield return Click(view.LoadButton);
            yield return Wait(view.LastOperation);
            Assert.That(view.LastStatus, Is.EqualTo(PersistenceStatus.Success));
            Assert.That(view.Counter, Is.EqualTo(2));
            Assert.That(Navigation().PlayerPosition, Is.EqualTo(savedPosition));
            Assert.That(host.Input.CurrentContext, Is.EqualTo(InputContext.Pause));
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }

    public sealed partial class GameOptionsIntegrationTests
    {
        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator AppliedVolumesStaySynchronizedAcrossScenesAndNativeSfx()
        {
            yield return OpenOptions();
            yield return Click(Options().View.Tabs[3]);
            SettingsValues original = host.Settings.Current;
            foreach (var slider in new[] { SettingsController().MasterSlider, SettingsController().MusicSlider,
                SettingsController().SfxSlider })
            {
                EventSystem.current.SetSelectedGameObject(slider.gameObject);
                yield return Pad(GamepadButton.DpadLeft);
            }
            SettingsValues expected = SettingsController().Draft;
            Assert.That(expected.MasterVolume, Is.LessThan(original.MasterVolume));
            Assert.That(expected.MusicVolume, Is.LessThan(original.MusicVolume));
            Assert.That(expected.SfxVolume, Is.LessThan(original.SfxVolume));
            Assert.That(host.Settings.Current.MasterVolume, Is.EqualTo(original.MasterVolume),
                "The options draft must not affect services before Apply.");
            yield return Click(SettingsController().ApplyButton);
            Assert.That(PlayerPrefs.HasKey(key), Is.True);
            Assert.That(SettingsController().UiSoundCount, Is.GreaterThan(0));
            AssertSavedAudioSynchronized(expected);
            yield return Click(SettingsController().BackButton);

            int hostId = host.GetInstanceID();
            var audio = host.Audio;
            var settings = host.Settings;
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            AssertSavedAudioSynchronized(expected);
            yield return Press(Key.F);
            yield return Press(Key.G);
            Assert.That(SettingsController().Sound2DCount, Is.EqualTo(1));
            Assert.That(SettingsController().Sound3DCount, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<LumiAudioRunner>().GetComponentsInChildren<AudioSource>()
                .Any(source => source.spatialBlend == 1f && source.clip == host.Gameplay3DSound && source.isPlaying), Is.True);
            yield return Pad(GamepadButton.West);
            yield return Pad(GamepadButton.North);
            Assert.That(SettingsController().Sound2DCount, Is.EqualTo(2));
            Assert.That(SettingsController().Sound3DCount, Is.EqualTo(2));
            yield return Press(Key.Escape);
            yield return Click(Navigation().ReturnButton);
            yield return Click(Navigation().ConfirmButton);
            yield return ReadyMenu();
            Assert.That(host.GetInstanceID(), Is.EqualTo(hostId));
            Assert.That(host.Audio, Is.SameAs(audio));
            Assert.That(host.Settings, Is.SameAs(settings));
            Assert.That(host.Audio.IsMusicPlaying, Is.True);
            Assert.That(host.Audio.CurrentMusic, Is.EqualTo(host.Music));
            Assert.That(Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            AssertSavedAudioSynchronized(expected);
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertSavedAudioSynchronized(SettingsValues expected)
        {
            var actual = host.Settings.Current;
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(host.Audio.MasterVolume, Is.EqualTo(actual.MasterVolume));
            Assert.That(host.Audio.MusicVolume, Is.EqualTo(actual.MusicVolume));
            Assert.That(host.Audio.SfxVolume, Is.EqualTo(actual.SfxVolume));
            Assert.That(host.Mixer.GetFloat("MasterVolume", out float master), Is.True);
            Assert.That(host.Mixer.GetFloat("MusicVolume", out float music), Is.True);
            Assert.That(host.Mixer.GetFloat("SfxVolume", out float sfx), Is.True);
            Assert.That(master, Is.EqualTo(AudioVolume.NormalizedToDecibels(actual.MasterVolume)).Within(.001f));
            Assert.That(music, Is.EqualTo(AudioVolume.NormalizedToDecibels(actual.MusicVolume)).Within(.001f));
            Assert.That(sfx, Is.EqualTo(AudioVolume.NormalizedToDecibels(actual.SfxVolume)).Within(.001f));
        }
    }
}
