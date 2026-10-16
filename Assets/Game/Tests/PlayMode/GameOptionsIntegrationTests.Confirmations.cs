using System.Collections;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Persistence;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.UI;
using Lumivale.Start.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    public sealed partial class GameOptionsIntegrationTests
    {
        [UnityTest]
        public IEnumerator UiCorrectionsReturnConfirmationUsesHorizontalNoYesAndKeepsTheBootstrap()
        {
            AppliedLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            int bootstrapId = host.GetInstanceID();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            var navigation = Navigation();
            yield return Press(Key.Escape);
            Assert.That(host.Pause.IsPaused, Is.True);

            yield return Click(navigation.ReturnButton);
            Assert.That(host.UI.IsOpen("ConfirmReturn"), Is.True);
            AssertUiCorrectionHorizontalNoYes(navigation.CancelButton, navigation.ConfirmButton);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(navigation.CancelButton.gameObject));
            yield return Click(navigation.CancelButton);
            Assert.That(host.UI.IsOpen("ConfirmReturn"), Is.False);
            Assert.That(host.UI.IsOpen("Pause"), Is.True);
            Assert.That(host.Pause.IsPaused, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.GameplayPath));

            yield return Click(navigation.ReturnButton);
            AssertUiCorrectionHorizontalNoYes(navigation.CancelButton, navigation.ConfirmButton);
            yield return Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(navigation.ConfirmButton.gameObject));
            yield return Press(Key.Enter);
            yield return Wait(navigation.LastNavigation);
            yield return ReadyMenu();
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.MenuPath));
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            Assert.That(host.GetInstanceID(), Is.EqualTo(bootstrapId));
            Assert.That(LumiBootstrap.Instance, Is.SameAs(host));
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UiCorrectionsGameplayDeleteNoKeepsTheSaveAndYesDeletesIt()
        {
            AppliedLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            yield return SeedUiCorrectionSave();
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)));
            yield return ReadyGameplay();
            var saves = Saves();
            yield return Press(Key.Tab);
            yield return Wait(saves.LastOperation);
            Assert.That(saves.IsOpen, Is.True);
            Assert.That(saves.SlotId, Is.EqualTo("demo-1"));

            yield return Click(saves.DeleteButton);
            Assert.That(host.UI.IsOpen("DeleteSave"), Is.True);
            AssertUiCorrectionHorizontalNoYes(saves.CancelDeleteButton, saves.ConfirmDeleteButton);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(saves.CancelDeleteButton.gameObject));
            yield return Click(saves.CancelDeleteButton);
            Assert.That(host.UI.IsOpen("DeleteSave"), Is.False);
            Assert.That(saves.IsOpen, Is.True);
            Assert.That(host.Pause.IsPaused, Is.True);
            var retained = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
            yield return Wait(retained);
            Assert.That(retained.Result.Status, Is.EqualTo(PersistenceStatus.Success));
            Assert.That(retained.Result.Data.Counter, Is.EqualTo(5));
            Assert.That(retained.Result.Data.PositionY, Is.EqualTo(.5f));

            yield return Click(saves.DeleteButton);
            AssertUiCorrectionHorizontalNoYes(saves.CancelDeleteButton, saves.ConfirmDeleteButton);
            yield return Click(saves.ConfirmDeleteButton);
            yield return Wait(saves.LastOperation);
            Assert.That(host.UI.IsOpen("DeleteSave"), Is.False);
            Assert.That(saves.IsOpen, Is.True);
            Assert.That(saves.LastStatus, Is.EqualTo(PersistenceStatus.Success));
            var deleted = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
            yield return Wait(deleted);
            Assert.That(deleted.Result.Status, Is.EqualTo(PersistenceStatus.NotFound));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UiCorrectionsMenuDeleteNoKeepsTheSaveAndGamepadYesDeletesIt()
        {
            AppliedLanguage("fr");
            yield return Wait(host.Localization.PreviewReady);
            yield return SeedUiCorrectionSave();
            // The existing menu cached its initial empty slot list in SetUp. Recreate
            // the scene so the native Primary route observes this fixture-owned save.
            yield return Wait(host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.MenuPath)));
            yield return ReadyMenu();
            var menu = Menu();
            Assert.That(menu.HasSaves, Is.True);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo("MainMenu"));
            yield return Click(menu.Views.Primary);
            yield return Wait(menu.LastOperation);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.SaveSelection.Id));
            Assert.That(menu.SelectedSlotId, Is.EqualTo("demo-1"));

            yield return Click(menu.Views.Delete);
            Assert.That(host.UI.IsOpen(menu.Views.DeleteConfirmation.Id), Is.True);
            AssertUiCorrectionHorizontalNoYes(menu.Views.DeleteNo, menu.Views.DeleteYes);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.Views.DeleteNo.gameObject));
            yield return Click(menu.Views.DeleteNo);
            Assert.That(host.UI.IsOpen(menu.Views.DeleteConfirmation.Id), Is.False);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.SaveSelection.Id));
            var retained = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
            yield return Wait(retained);
            Assert.That(retained.Result.Status, Is.EqualTo(PersistenceStatus.Success));
            Assert.That(retained.Result.Data.Counter, Is.EqualTo(5));
            Assert.That(retained.Result.Data.PositionY, Is.EqualTo(.5f));

            yield return Click(menu.Views.Delete);
            AssertUiCorrectionHorizontalNoYes(menu.Views.DeleteNo, menu.Views.DeleteYes);
            yield return Pad(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.Views.DeleteYes.gameObject));
            yield return Pad(GamepadButton.South);
            yield return Wait(menu.LastOperation);
            Assert.That(host.UI.IsOpen(menu.Views.DeleteConfirmation.Id), Is.False);
            Assert.That(host.UI.CurrentScreenId, Is.EqualTo(menu.Views.SaveSelection.Id));
            var deleted = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
            yield return Wait(deleted);
            Assert.That(deleted.Result.Status, Is.EqualTo(PersistenceStatus.NotFound));
            Assert.That(menu.HasSaves, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator SeedUiCorrectionSave()
        {
            var saved = host.Persistence.SaveAsync("demo-1", new GameSaveData { Counter = 5, PositionY = .5f },
                GameSaveData.CreateSchema());
            yield return Wait(saved);
            Assert.That(saved.Result.Status, Is.EqualTo(PersistenceStatus.Success));
            var loaded = host.Persistence.LoadAsync("demo-1", GameSaveData.CreateSchema());
            yield return Wait(loaded);
            Assert.That(loaded.Result.Status, Is.EqualTo(PersistenceStatus.Success));
            Assert.That(loaded.Result.Data.Counter, Is.EqualTo(5));
            Assert.That(loaded.Result.Data.PositionY, Is.EqualTo(.5f));
        }

        private static void AssertUiCorrectionHorizontalNoYes(UnityEngine.UI.Button no, UnityEngine.UI.Button yes)
        {
            Assert.That(no, Is.Not.Null);
            Assert.That(yes, Is.Not.Null);
            Assert.That(no.transform.parent, Is.EqualTo(yes.transform.parent));
            Assert.That(no.transform.parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(no.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(yes.transform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(no.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Non").IgnoreCase);
            Assert.That(yes.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Oui").IgnoreCase);
            var screen = no.GetComponentInParent<UiScreen>();
            Assert.That(screen.FirstSelected, Is.EqualTo(no));
            Assert.That(no.navigation.selectOnRight, Is.EqualTo(yes));
            Assert.That(yes.navigation.selectOnLeft, Is.EqualTo(no));
            Canvas.ForceUpdateCanvases();
            var noRect = no.GetComponent<RectTransform>();
            var yesRect = yes.GetComponent<RectTransform>();
            Vector3 noCenter = noRect.TransformPoint(noRect.rect.center);
            Vector3 yesCenter = yesRect.TransformPoint(yesRect.rect.center);
            Assert.That(yesCenter.x, Is.GreaterThan(noCenter.x));
            Assert.That(yesCenter.y, Is.EqualTo(noCenter.y).Within(.01f));
            Assert.That(noRect.rect.width, Is.GreaterThan(0f));
            Assert.That(yesRect.rect.width, Is.GreaterThan(0f));
            var noCorners = new Vector3[4];
            var yesCorners = new Vector3[4];
            noRect.GetWorldCorners(noCorners);
            yesRect.GetWorldCorners(yesCorners);
            Assert.That(noCorners[2].x, Is.LessThanOrEqualTo(yesCorners[0].x + .01f),
                "The No/Yes buttons must be horizontal without overlapping.");
        }
    }
}
