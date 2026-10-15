using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.SceneFlow;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameMenuIntegrationTests
    {
        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator NativeSceneLoadingRejectsOverlapAndRecoversAfterAnUnavailableScene()
        {
            int hostId = host.GetInstanceID();
            var progress = new List<float>();
            var failures = new List<SceneLoadErrorCode>();
            int completions = 0;
            Action<SceneLoadProgress> onProgress = value => progress.Add(value.Progress);
            Action<SceneLoadFailure> onFailure = value => failures.Add(value.ErrorCode);
            Action<SceneLoadResult> onCompleted = _ => completions++;
            host.Scenes.ProgressChanged += onProgress;
            host.Scenes.LoadFailed += onFailure;
            host.Scenes.LoadCompleted += onCompleted;
            try
            {
                var first = host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath));
                var overlap = host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath));
                yield return WaitExpectedFailure(overlap);
                Assert.That(overlap.Exception.GetBaseException(), Is.TypeOf<SceneFlowException>());
                Assert.That(((SceneFlowException)overlap.Exception.GetBaseException()).ErrorCode,
                    Is.EqualTo(SceneLoadErrorCode.Busy));
                Assert.That(host.Scenes.IsLoading, Is.True, "The rejected request must not release the native load's gate.");
                yield return Wait(first);
                yield return ReadyGameplay();
                Assert.That(progress, Is.Not.Empty);
                Assert.That(progress[progress.Count - 1], Is.EqualTo(1f));
                for (int i = 1; i < progress.Count; i++)
                    Assert.That(progress[i], Is.GreaterThanOrEqualTo(progress[i - 1]));

                var unavailable = host.Scenes.LoadAsync(new SceneLoadRequest(
                    "Assets/Game/Scenes/UnavailableForLifecycleTest.unity"));
                yield return WaitExpectedFailure(unavailable);
                Assert.That(unavailable.Exception.GetBaseException(), Is.TypeOf<SceneFlowException>());
                Assert.That(((SceneFlowException)unavailable.Exception.GetBaseException()).ErrorCode,
                    Is.EqualTo(SceneLoadErrorCode.SceneUnavailable));
                Assert.That(host.Scenes.IsLoading, Is.False);
                Assert.That(host.Input.IsSuspended, Is.False);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.GameplayPath));

                yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
                yield return ReadyMenu();
                Assert.That(completions, Is.EqualTo(2));
                CollectionAssert.AreEqual(new[] { SceneLoadErrorCode.Busy, SceneLoadErrorCode.SceneUnavailable }, failures);
                Assert.That(host.Input.CurrentContext, Is.EqualTo(InputContext.UI));
                Assert.That(host.Input.IsSuspended, Is.False);
                Assert.That(host.GetInstanceID(), Is.EqualTo(hostId));
                Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
            finally
            {
                host.Scenes.ProgressChanged -= onProgress;
                host.Scenes.LoadFailed -= onFailure;
                host.Scenes.LoadCompleted -= onCompleted;
            }
        }

        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator CancelledNativeLoadActivatesMenuAndRestoresPausedInputBeforeAnotherLoad()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            using (var cancellation = new CancellationTokenSource())
            {
                bool observedNativeProgress = false;
                Action<SceneLoadProgress> cancelAfterStart = value =>
                {
                    if (value.Progress > 0f && value.Progress < 1f)
                    {
                        observedNativeProgress = true;
                        cancellation.Cancel();
                    }
                };
                host.Scenes.ProgressChanged += cancelAfterStart;
                try
                {
                    var loading = host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath), cancellation.Token);
                    yield return WaitExpectedFailure(loading, allowCancellation: true);
                    Assert.That(observedNativeProgress, Is.True, "Cancel only after Unity's operation has started.");
                    Assert.That(loading.IsCanceled, Is.True);
                    yield return ReadyMenu();
                    Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
                    Assert.That(host.Scenes.IsLoading, Is.False);
                    Assert.That(host.Input.IsSuspended, Is.False);
                    Assert.That(host.Input.CurrentContext, Is.EqualTo(InputContext.UI));
                    Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
                    Assert.That(host.Pause.IsPaused, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                }
                finally
                {
                    host.Scenes.ProgressChanged -= cancelAfterStart;
                }
            }

            // Use native menu input after cancellation, rather than bypassing its subscriptions.
            yield return Press(Key.Enter);
            yield return ReadyGameplay();
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(host.UI.ModalCount, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator SceneControllerReenableOwnsOnePauseCallbackAndShutdownLeavesItInert()
        {
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            var controller = Navigation();
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);

            controller.enabled = false;
            Assert.That(controller.IsReady, Is.False);
            Assert.That(host.Pause.IsPaused, Is.False);
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.False, "A disabled controller must have no pause callback.");

            controller.enabled = true;
            yield return ReadyGameplay();
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(host.UI.ModalCount, Is.EqualTo(1), "Reactivation must not duplicate the pause registration.");
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);
            yield return Wait(host.ShutdownAsync());
            Assert.That(host.Coordinator.State, Is.EqualTo(BootstrapState.Stopped));
            Assert.That(controller.IsReady, Is.False);
            Assert.That(host.Input.IsInitialized, Is.False);
            Assert.That(host.Pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Vector3 stoppedPosition = controller.PlayerPosition;
            yield return Press(Key.D);
            yield return Press(Key.Escape);
            yield return Press(Key.Enter);
            Assert.That(controller.PlayerPosition, Is.EqualTo(stoppedPosition));
            Assert.That(host.Pause.IsPaused, Is.False);
            controller.enabled = false;
            controller.enabled = true;
            yield return null;
            yield return null;
            Assert.That(controller.IsReady, Is.False, "Completed Ready must not restart bindings on a stopped host.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitExpectedFailure(Task task, bool allowCancellation = false)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "A failing native scene operation timed out.");
                yield return null;
            }
            Assert.That(task.IsFaulted || (allowCancellation && task.IsCanceled), Is.True,
                "The operation must fail or be explicitly cancelled.");
            if (task.IsFaulted) _ = task.Exception;
        }
    }
}
