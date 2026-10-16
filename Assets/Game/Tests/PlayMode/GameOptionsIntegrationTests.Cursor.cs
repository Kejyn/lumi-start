using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.SceneFlow;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameOptionsIntegrationTests
    {
        [UnityTest]
        public IEnumerator OptionsConsistencyCursorTracksVirtualGamepadKeyboardAndMouseWithoutChangingLockState()
        {
            CursorLockMode originalLock = Cursor.lockState;
            var observed = new List<InputDeviceFamily>();
            host.Input.ActiveDeviceChanged += observed.Add;
            try
            {
                Assert.That(host.Cursor.IsInitialized, Is.True);
                Assert.That(Cursor.visible, Is.True);
                for (int repetition = 0; repetition < 3; repetition++)
                {
                    yield return Pad(GamepadButton.North);
                    Assert.That(host.Input.ActiveDeviceFamily, Is.EqualTo(InputDeviceFamily.Gamepad));
                    Assert.That(Cursor.visible, Is.False);

                    // A synthetic position refresh alone must not reclaim the cursor.
                    InputSystem.QueueDeltaStateEvent(mouse.position, new Vector2(100f + repetition, 80f));
                    yield return null; yield return null;
                    Assert.That(host.Input.ActiveDeviceFamily, Is.EqualTo(InputDeviceFamily.Gamepad));
                    Assert.That(Cursor.visible, Is.False);

                    yield return Press(Key.Z);
                    Assert.That(host.Input.ActiveDeviceFamily, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
                    Assert.That(Cursor.visible, Is.True);
                    yield return Pad(GamepadButton.North);
                    Assert.That(Cursor.visible, Is.False);
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = new Vector2(120f + repetition, 80f), delta = new Vector2(4f, 0f)
                    });
                    yield return null; yield return null;
                    Assert.That(host.Input.ActiveDeviceFamily, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
                    Assert.That(Cursor.visible, Is.True);
                    Assert.That(Cursor.lockState, Is.EqualTo(originalLock));
                }
                Assert.That(observed.Count, Is.EqualTo(12), "Releases and position-only samples do not duplicate switches.");
                Assert.That(PlayerPrefs.HasKey(key), Is.False, "Cursor presentation does not write settings.");
            }
            finally { host.Input.ActiveDeviceChanged -= observed.Add; }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OptionsConsistencyCursorStaysHiddenAcrossGameplayAndMenuWithTheSameService()
        {
            var cursor = host.Cursor;
            var input = host.Input;
            int bootstrap = host.GetInstanceID();
            CursorLockMode originalLock = Cursor.lockState;
            yield return Pad(GamepadButton.North);
            Assert.That(Cursor.visible, Is.False);
            var revealed = new List<int>();
            void ObservePresentation()
            {
                if (host != null && host.IsReady && host.Input.ActiveDeviceFamily == InputDeviceFamily.Gamepad && Cursor.visible)
                    revealed.Add(Time.frameCount);
            }

            Canvas.willRenderCanvases += ObservePresentation;
            try
            {
                yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
                yield return ReadyGameplay();
                Assert.That(Cursor.visible, Is.False);
                Assert.That(host.Cursor, Is.SameAs(cursor));
                yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
                yield return ReadyMenu();
                Assert.That(Cursor.visible, Is.False);
                Assert.That(host.Input.ActiveDeviceFamily, Is.EqualTo(InputDeviceFamily.Gamepad));
                Assert.That(host.Cursor, Is.SameAs(cursor));
                Assert.That(host.Input, Is.SameAs(input));
                Assert.That(host.GetInstanceID(), Is.EqualTo(bootstrap));
                Assert.That(revealed, Is.Empty, "Scene view disposal must not restore the session cursor mid-transition.");
                Assert.That(Cursor.lockState, Is.EqualTo(originalLock));
                yield return Press(Key.Z);
                Assert.That(Cursor.visible, Is.True, "Keyboard still reclaims the cursor after both scene transitions.");
                yield return Pad(GamepadButton.North);
                Assert.That(Cursor.visible, Is.False);
            }
            finally { Canvas.willRenderCanvases -= ObservePresentation; }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OptionsConsistencyCursorShutdownRestoresBothSnapshotsAndRemovesItsInputSubscription()
        {
            // Release the composed policy first so this isolated lifecycle check can capture
            // both native visibility states while the fixture's real Input remains active.
            yield return Wait(host.Cursor.ShutdownAsync(CancellationToken.None));
            bool originalVisibility = Cursor.visible;
            CursorLockMode originalLock = Cursor.lockState;
            GameCursorService cursor = null;
            try
            {
                foreach (bool captured in new[] { false, true })
                {
                    yield return Press(Key.Z);
                    Cursor.visible = captured;
                    cursor = new GameCursorService();
                    yield return Wait(cursor.InitializeAsync(host.Coordinator.Context, CancellationToken.None));
                    Assert.That(Cursor.visible, Is.True, "The initial keyboard/mouse family is applied immediately.");
                    // Repeated initialization must retain the first snapshot and one subscription.
                    yield return Wait(cursor.InitializeAsync(host.Coordinator.Context, CancellationToken.None));
                    yield return Pad(GamepadButton.North);
                    Assert.That(Cursor.visible, Is.False);
                    yield return Wait(cursor.ShutdownAsync(new CancellationToken(canceled: true)));
                    Assert.That(cursor.IsInitialized, Is.False);
                    Assert.That(Cursor.visible, Is.EqualTo(captured), "Cleanup restores the captured Unity state even with cancellation.");

                    Cursor.visible = false;
                    yield return Press(Key.Z);
                    Assert.That(Cursor.visible, Is.False, "A stopped policy must not react to live Input callbacks.");
                    Cursor.visible = true;
                    yield return Pad(GamepadButton.North);
                    Assert.That(Cursor.visible, Is.True);
                    yield return Wait(cursor.ShutdownAsync(CancellationToken.None));
                    Assert.That(Cursor.visible, Is.True, "Repeated shutdown must not restore an obsolete snapshot again.");
                    Assert.That(Cursor.lockState, Is.EqualTo(originalLock));
                }
            }
            finally
            {
                if (cursor != null) cursor.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
                Cursor.visible = originalVisibility;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OptionsConsistencyCursorBootstrapShutdownRestoresItsCapturedVisibility()
        {
            yield return Wait(host.ShutdownAsync());
            bool originalVisibility = Cursor.visible;
            CursorLockMode originalLock = Cursor.lockState;
            UnityEngine.Object.Destroy(host.gameObject);
            yield return null;
            try
            {
                // A fresh Bootstrap captures the restored Editor state before any service
                // owns it. Keeping that baseline also makes fixture cleanup safe on failure.
                Cursor.visible = originalVisibility;
                CreateHost();
                yield return Wait(host.Ready);
                RestrictDevices();
                yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
                yield return ReadyMenu();
                Assert.That(host.Cursor.IsInitialized, Is.True);
                Assert.That(Cursor.visible, Is.True);
                yield return Pad(GamepadButton.North);
                Assert.That(Cursor.visible, Is.False);
                yield return Press(Key.Z);
                Assert.That(Cursor.visible, Is.True);
                yield return Wait(host.ShutdownAsync());
                Assert.That(host.Coordinator.State, Is.EqualTo(BootstrapState.Stopped));
                Assert.That(host.Cursor.IsInitialized, Is.False);
                Assert.That(Cursor.visible, Is.EqualTo(originalVisibility));
                Cursor.visible = !originalVisibility;
                yield return Wait(host.ShutdownAsync());
                Assert.That(Cursor.visible, Is.EqualTo(!originalVisibility), "A repeated Bootstrap stop cannot change cursor ownership twice.");
                Assert.That(Cursor.lockState, Is.EqualTo(originalLock));
            }
            finally { Cursor.visible = originalVisibility; }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
