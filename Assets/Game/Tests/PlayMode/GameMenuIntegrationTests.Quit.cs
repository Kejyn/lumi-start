using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lumivale.Core.Audio;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.UI;
using Lumivale.Start.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameMenuIntegrationTests
    {
        [UnityTest]
        public IEnumerator UiCorrectionsQuitKeepsCanvasHiddenThroughShutdownAndDeferredExit()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            var menu = Menu();
            var canvas = menu.Views.Boot.GetComponentInParent<Canvas>(true);

            yield return Click(menu.Views.Exit);
            yield return Click(menu.Views.QuitNo);
            Assert.That(canvas.enabled, Is.True, "No preserves the ordinary menu presentation.");
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.Views.Exit.gameObject));
            yield return Click(menu.Views.Exit);
            yield return Press(Key.Escape);
            Assert.That(canvas.enabled, Is.True, "Back preserves the ordinary menu presentation.");
            Assert.That(host.UI.ModalCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.Views.Exit.gameObject));

            int exitCallbacks = 0;
            int observedFrames = 0;
            bool hiddenThroughout = true;
            bool stoppedThroughout = true;
            bool noAudioThroughout = true;
            menu.QuitRequested = async () =>
            {
                exitCallbacks++;
                // Simulate a process exit that is deferred to later native frames. Never
                // invoke Application.Quit or the Editor stop bridge from this fixture.
                for (int frame = 0; frame < 3; frame++)
                {
                    await Awaitable.NextFrameAsync();
                    observedFrames++;
                    hiddenThroughout &= canvas != null && !canvas.enabled;
                    stoppedThroughout &= host.Coordinator.State == BootstrapState.Stopped;
                    noAudioThroughout &= UnityEngine.Object.FindObjectsByType<LumiAudioRunner>(
                        FindObjectsSortMode.None).Length == 0;
                }
            };

            yield return Click(menu.Views.Exit);
            yield return Click(menu.Views.QuitYes);
            yield return Wait(menu.LastOperation);
            yield return null;
            Assert.That(exitCallbacks, Is.EqualTo(1));
            Assert.That(observedFrames, Is.EqualTo(3));
            Assert.That(hiddenThroughout, Is.True,
                "Closing UiScreens must not reveal Canvas children such as GameplaySavesButton.");
            Assert.That(stoppedThroughout, Is.True);
            Assert.That(noAudioThroughout, Is.True);
            Assert.That(canvas.enabled, Is.False);
            Assert.That(menu.QuitConfirmed, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UiCorrectionsQuitFailureRestoresReadableConfirmationAfterCoordinatorStopped()
        {
            yield return Press(Key.Space);
            yield return ReadyMenu();
            var menu = Menu();
            var canvas = menu.Views.Boot.GetComponentInParent<Canvas>(true);
            int exitCallbacks = 0;
            menu.QuitRequested = () =>
            {
                exitCallbacks++;
                return Task.FromException(new InvalidOperationException("Fixture final exit failed."));
            };
            LogAssert.Expect(LogType.Exception,
                new Regex("InvalidOperationException: Fixture final exit failed\\."));

            yield return Click(menu.Views.Exit);
            yield return Click(menu.Views.QuitYes);
            yield return Wait(menu.LastOperation);
            yield return null;

            Assert.That(exitCallbacks, Is.EqualTo(1));
            Assert.That(host.Coordinator.State, Is.EqualTo(BootstrapState.Stopped));
            Assert.That(UnityEngine.Object.FindObjectsByType<LumiAudioRunner>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(canvas.enabled, Is.True, "A failed final exit must leave a readable diagnostic.");
            Assert.That(menu.Views.ConfirmQuit.CanvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(menu.Views.ConfirmQuit.CanvasGroup.interactable, Is.False,
                "A stopped service stack cannot offer live menu navigation.");
            Assert.That(menu.Views.ConfirmQuit.CanvasGroup.blocksRaycasts, Is.True);
            Assert.That(menu.Views.QuitStatus.text, Is.EqualTo(GameText.Text("menu.quit.failed")));
            Assert.That(menu.Views.ConfirmQuit.transform.GetSiblingIndex(),
                Is.EqualTo(menu.Views.ConfirmQuit.transform.parent.childCount - 1));
            var visibleScreens = canvas.GetComponentsInChildren<UiScreen>(true)
                .Where(screen => screen.CanvasGroup != null && screen.CanvasGroup.alpha > .001f)
                .Select(screen => screen.Id).ToArray();
            Assert.That(visibleScreens, Is.EqualTo(new[] { menu.Views.ConfirmQuit.Id }));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
