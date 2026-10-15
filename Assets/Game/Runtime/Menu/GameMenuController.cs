using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Persistence;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.UI;
using Lumivale.Start.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lumivale.Start.Game
{
    /// <summary>Starter presentation and three-slot navigation, using the existing public Core services.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "ProductMenuController")]
    public sealed class GameMenuController : MonoBehaviour
    {
        [Serializable]
        public sealed class ViewReferences
        {
            public UiScreen Boot, SaveSelection, Credits, ConfirmQuit, DeleteConfirmation;
            public Button BootContinue, Primary, Options, CreditsButton, Exit;
            public Button[] Slots;
            public Button Load, NewGame, Delete, SavesBack, CreditsBack;
            public Button QuitYes, QuitNo, DeleteYes, DeleteNo;
            public TMP_Text PrimaryLabel;
            public TMP_Text[] SlotLabels;
            public TMP_Text SaveStatus, DeleteMessage, StartPrompt, QuitStatus;
            public RectTransform CreditsRoll;
        }

        [SerializeField] private GameSceneController navigation;
        [SerializeField] private ViewReferences views = new ViewReferences();
        [SerializeField, Min(1f), Tooltip("Credits scroll speed in Canvas units per unscaled second.")]
        private float creditsSpeed = 34f;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly SaveSchema<GameSaveData> schema = GameSaveData.CreateSchema();
        private readonly SaveSlotInfo[] slots = new SaveSlotInfo[3];
        private Action<bool>[] slotListeners;
        private GameSlotActivation[] slotActivations;
        private GameBootstrap host;
        private GameSessionState session;
        private CancellationTokenSource operationCancellation;
        private int bindVersion;
        private int selectedIndex;
        private int pointerSlotIndex = -1;
        private bool started;
        private bool ready;
        private bool busy;
        private bool pendingBack;
        private bool pendingSlotAction;
        private bool pendingContinue;
        private int continueRequestedFrame;
        private bool pendingRefresh;
        private string pendingDeleteSlot;
        private Vector2 creditsOrigin;
        private float creditsTravel = 1350f;
        private float startPromptTime;
        private Canvas presentationCanvas;

        public ViewReferences Views => views;
        public bool IsReady => ready && host != null && host.IsReady &&
            host.Persistence.IsInitialized && navigation != null && navigation.IsReady;
        public bool IsBusy => busy;
        public bool HasSaves { get; private set; }
        public string SelectedSlotId => "demo-" + (selectedIndex + 1);
        public IReadOnlyList<SaveSlotInfo> CurrentSlotStatuses => Array.AsReadOnly(slots);
        public Task LastOperation { get; private set; } = Task.CompletedTask;
        public PersistenceStatus LastStatus { get; private set; }
        public bool QuitConfirmed { get; private set; }
        public bool IsSelectedSlotEmpty => slots[selectedIndex]?.Status == PersistenceStatus.NotFound;

        /// <summary>Tests may replace only the final process exit; Bootstrap shutdown is still awaited.</summary>
        public Func<Task> QuitRequested { get; set; }

        /// <summary>An Editor-only bridge ends Play Mode after the ordinary runtime cleanup.</summary>
        public event Action EditorQuitRequested;

        private bool CanInteract => IsReady && isActiveAndEnabled &&
            !host.Input.IsSuspended && !host.Scenes.IsLoading && !QuitConfirmed;
        private bool IsSelectionOpen => IsReady && host.UI.IsOpen(views.SaveSelection.Id);

        public void Configure(GameSceneController nav, ViewReferences references)
        {
            navigation = nav;
            views = references ?? throw new ArgumentNullException(nameof(references));
        }

        private void Awake()
        {
            // The scene stores this Canvas disabled too. Keep backgrounds and other
            // nodes outside UiScreen hidden until the session's first screen is chosen.
            presentationCanvas = views.Boot != null ? views.Boot.GetComponentInParent<Canvas>(true) : null;
            if (presentationCanvas != null) presentationCanvas.enabled = false;
        }

        private void Start() { started = true; Bind(); }
        private void OnEnable() { if (started) Bind(); }

        private async void Bind()
        {
            int version = ++bindVersion;
            try
            {
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null || !host.HasPersistence)
                    throw new InvalidOperationException("The product menu requires the configured Starter Bootstrap.");
                await host.Ready;
                while (IsCurrent(version) && host.IsReady && navigation != null && !navigation.IsReady)
                    await Awaitable.NextFrameAsync();
                if (!IsCurrent(version) || !host.IsReady) return;
                ValidateReferences();
                session = GameSessionState.GetOrCreate(host);
                selectedIndex = session.SelectedSlotId[session.SelectedSlotId.Length - 1] - '1';
                startPromptTime = 0f;
                creditsOrigin = views.CreditsRoll != null ? views.CreditsRoll.anchoredPosition : Vector2.zero;
                foreach (UiScreen screen in OwnedScreens()) subscriptions.Add(host.UI.Register(screen));
                subscriptions.Add(host.Input.Subscribe("UI/Cancel", _ =>
                {
                    // The ordinary navigation owns modal cancellation. This presenter owns only its roots.
                    if (CanInteract && host.UI.ModalCount == 0 &&
                        (host.UI.CurrentScreenId == views.Credits.Id || IsSelectionOpen)) pendingBack = true;
                }));
                subscriptions.Add(host.Input.Subscribe("Common/SaveSlotAction", _ =>
                { if (CanInteract && IsSelectionOpen && host.UI.ModalCount == 0) pendingSlotAction = true; }));
                subscriptions.Add(host.Input.Subscribe("UI/StartContinue", _ =>
                {
                    if (!CanInteract || busy || !host.UI.IsOpen(views.Boot.Id) || pendingContinue) return;
                    pendingContinue = true;
                    continueRequestedFrame = Time.frameCount;
                }));
                host.UI.ScreenClosed += ScreenClosed;
                Wire(true);
                RefreshControls();
                // Selecting the first screen does not depend on disk discovery or a
                // Scene Flow input lease. Never render the bare background while waiting.
                host.UI.Open(session.BootDismissed ? "MainMenu" : views.Boot.Id, rememberHistory: false);
                if (presentationCanvas != null) presentationCanvas.enabled = true;
                // Scene Flow suspends input while activating this scene. Its post-load frame releases the lease;
                // defer our first disk operation until then so an ordinary return cannot skip slot discovery.
                while (IsCurrent(version) && host.IsReady && (host.Input.IsSuspended || host.Scenes.IsLoading))
                    await Awaitable.NextFrameAsync();
                if (!IsCurrent(version) || !host.IsReady) return;
                ready = true;
                LastOperation = RefreshSlotsAsync();
                await LastOperation;
                if (!IsCurrent(version) || !host.IsReady) return;
            }
            catch (OperationCanceledException) { if (version == bindVersion) Release(); }
            catch (Exception exception)
            {
                if (version == bindVersion) { Release(); Debug.LogException(exception, this); }
            }
        }

        private bool IsCurrent(int version) => this != null && isActiveAndEnabled && version == bindVersion;

        private void ValidateReferences()
        {
            if (navigation == null || !navigation.IsReady || views == null || views.PrimaryLabel == null ||
                views.SaveStatus == null || views.DeleteMessage == null || views.StartPrompt == null ||
                views.Slots == null || views.Slots.Length != 3 ||
                views.SlotLabels == null || views.SlotLabels.Length != 3)
                throw new InvalidOperationException("Configure the product menu's navigation, labels and three slots.");
            foreach (UiScreen screen in OwnedScreens())
                if (screen == null) throw new InvalidOperationException("A product menu screen reference is missing.");
            foreach (Button button in AllButtons())
                if (button == null) throw new InvalidOperationException("A product menu button reference is missing.");
            foreach (Button button in views.Slots)
                if (button.GetComponent<GameSlotActivation>() == null)
                    throw new InvalidOperationException("Each save slot requires a GameSlotActivation component.");
            foreach (TMP_Text label in views.SlotLabels)
                if (label == null) throw new InvalidOperationException("A product slot label reference is missing.");
        }

        private UiScreen[] OwnedScreens() => new[]
        { views.Boot, views.SaveSelection, views.Credits, views.ConfirmQuit, views.DeleteConfirmation };

        private Button[] AllButtons() => new[]
        {
            views.Primary, views.Options, views.CreditsButton, views.Exit,
            views.Slots[0], views.Slots[1], views.Slots[2], views.NewGame, views.Delete,
            views.SavesBack, views.CreditsBack, views.QuitYes, views.QuitNo, views.DeleteYes, views.DeleteNo
        };

        private void Update()
        {
            bool back = pendingBack, slotAction = pendingSlotAction;
            pendingBack = pendingSlotAction = false;
            // Space/A also participates in normal uGUI Submit. Let that input frame finish on
            // the non-selectable Start screen before exposing the Main Menu's first button.
            if (pendingContinue && Time.frameCount > continueRequestedFrame)
            {
                pendingContinue = false;
                ContinueBoot();
            }
            if (CanInteract)
            {
                if (IsSelectionOpen && !busy && host.UI.ModalCount == 0 && EventSystem.current != null)
                    for (int i = 0; i < views.Slots.Length; i++)
                        if (EventSystem.current.currentSelectedGameObject == views.Slots[i].gameObject && selectedIndex != i)
                            SelectSlot(i);
                if (back && host.UI.ModalCount == 0) BackToMain();
                if (slotAction && IsSelectionOpen && host.UI.ModalCount == 0)
                {
                    if (IsSelectedSlotEmpty) StartSelectedNewGame();
                    else RequestDelete();
                }
                if (pendingRefresh && !busy)
                {
                    pendingRefresh = false;
                    LastOperation = RefreshSlotsAsync();
                }
            }
            if (IsReady && views.StartPrompt != null && host.UI.IsOpen(views.Boot.Id))
            {
                startPromptTime += Time.unscaledDeltaTime;
                float wave = (1f - Mathf.Cos(startPromptTime * (Mathf.PI * 2f / 2.8f))) * 0.5f;
                Color color = views.StartPrompt.color;
                color.a = Mathf.Lerp(0.12f, 1f, wave);
                views.StartPrompt.color = color;
            }
            if (IsReady && views.CreditsRoll != null && host.UI.IsOpen(views.Credits.Id))
            {
                Vector2 position = views.CreditsRoll.anchoredPosition;
                position.y += Mathf.Max(1f, creditsSpeed) * Time.unscaledDeltaTime;
                if (position.y > creditsOrigin.y + creditsTravel) position = creditsOrigin;
                views.CreditsRoll.anchoredPosition = position;
            }
        }

        private void ContinueBoot()
        {
            if (!CanInteract || busy || session.BootDismissed || !host.UI.IsOpen(views.Boot.Id)) return;
            session.BootDismissed = true;
            host.UI.Open("MainMenu", rememberHistory: false);
            pendingRefresh = true;
        }

        private void Primary()
        {
            if (!CanInteract || busy || host.UI.CurrentScreenId != "MainMenu") return;
            if (HasSaves) OpenSelection();
            else Run(StartFirstEmptyGame);
        }

        private void OpenSelection()
        {
            if (!CanInteract || busy) return;
            pointerSlotIndex = -1;
            host.UI.Open(views.SaveSelection.Id);
            FocusSelectedSlot();
            Run(RefreshSlots);
        }

        private void ShowCredits()
        {
            if (!CanInteract || busy) return;
            if (views.CreditsRoll != null) views.CreditsRoll.anchoredPosition = creditsOrigin;
            host.UI.Open(views.Credits.Id);
            if (views.CreditsRoll != null)
            {
                Canvas.ForceUpdateCanvases();
                var text = views.CreditsRoll.GetComponent<TMP_Text>();
                var viewport = views.CreditsRoll.parent as RectTransform;
                // The roll starts just below its bottom-anchored viewport. Its final
                // line must cross that viewport before the next roll can begin.
                creditsTravel = (viewport != null ? viewport.rect.height : 0f) +
                    (text != null ? text.preferredHeight : views.CreditsRoll.rect.height) + 16f;
            }
        }

        private void RequestQuit()
        {
            if (!CanInteract || busy || host.UI.CurrentScreenId != "MainMenu") return;
            if (views.QuitStatus != null) views.QuitStatus.text = string.Empty;
            host.UI.OpenModal(views.ConfirmQuit.Id);
        }

        private void CancelQuit()
        {
            if (CanInteract && host.UI.IsOpen(views.ConfirmQuit.Id)) host.UI.Close(views.ConfirmQuit.Id);
        }

        private void ConfirmQuit()
        {
            if (!CanInteract || busy || !host.UI.IsOpen(views.ConfirmQuit.Id)) return;
            LastOperation = QuitAsync();
        }

        private async Task QuitAsync()
        {
            busy = true;
            RefreshControls();
            try
            {
                try { await host.ShutdownAsync(); }
                catch (Exception exception)
                {
                    // The coordinator has attempted all service stops. A failed service must
                    // not leave a deliberately exiting application stranded with dead input.
                    ReportQuitFailure("Cleanup reported an error: " + exception.Message, exception);
                }
                QuitConfirmed = true;
                if (QuitRequested != null) await QuitRequested();
                else if (Application.isEditor)
                {
                    if (EditorQuitRequested == null)
                        throw new InvalidOperationException("The Editor Play Mode exit bridge is not installed.");
                    EditorQuitRequested.Invoke();
                }
                else Application.Quit();
            }
            catch (Exception exception)
            {
                ReportQuitFailure("Unable to exit: " + exception.Message, exception);
            }
            finally { if (this != null) { busy = false; RefreshControls(); } }
        }

        private void ReportQuitFailure(string message, Exception exception)
        {
            if (this != null && views.QuitStatus != null)
            {
                views.QuitStatus.text = message;
                // UI shutdown has already hidden its registered screens. Preserve a visible
                // diagnostic when the final exit callback itself fails.
                CanvasGroup group = views.ConfirmQuit.CanvasGroup;
                if (group != null) group.alpha = 1f;
            }
            Debug.LogException(exception, this);
        }

        private void SelectSlot(int index)
        {
            if (!CanInteract || busy || !IsSelectionOpen || host.UI.ModalCount != 0) return;
            if (selectedIndex != index) pointerSlotIndex = -1;
            selectedIndex = index;
            session.SelectSlot(SelectedSlotId);
            RefreshSlotLabels();
            RefreshControls();
        }

        private void ActivateSlot(int index, bool pointer)
        {
            if (!CanInteract || busy || !IsSelectionOpen || host.UI.ModalCount != 0) return;
            bool secondPointerClick = pointerSlotIndex == index && selectedIndex == index;
            SelectSlot(index);
            if (pointer)
            {
                pointerSlotIndex = index;
                if (!secondPointerClick) return;
            }
            else pointerSlotIndex = -1;
            // Unknown/corrupt/incompatible slots go through Load too, so Persistence supplies
            // the diagnostic. Only a confirmed NotFound is eligible for a new game.
            if (slots[index] != null && !IsSelectedSlotEmpty) LoadSelected();
        }

        private void FocusSelectedSlot()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(views.Slots[selectedIndex].gameObject);
        }

        private void LoadSelected()
        {
            if (!CanInteract || busy || !IsSelectionOpen || host.UI.ModalCount != 0) return;
            pointerSlotIndex = -1;
            Run(LoadGame);
        }

        private void StartSelectedNewGame()
        {
            if (CanInteract && !busy && IsSelectionOpen && host.UI.ModalCount == 0) Run(StartEmptyGame);
        }

        private void RequestDelete()
        {
            if (!CanInteract || busy || !IsSelectionOpen || host.UI.ModalCount != 0 ||
                slots[selectedIndex] == null || IsSelectedSlotEmpty) return;
            pendingDeleteSlot = SelectedSlotId;
            views.DeleteMessage.text = "Delete slot " + (selectedIndex + 1) + "?\nThis cannot be undone.";
            host.UI.OpenModal(views.DeleteConfirmation.Id);
        }

        private void CancelDelete()
        {
            if (CanInteract && host.UI.IsOpen(views.DeleteConfirmation.Id)) host.UI.Close(views.DeleteConfirmation.Id);
        }

        private void ConfirmDelete()
        {
            if (!CanInteract || busy || !host.UI.IsOpen(views.DeleteConfirmation.Id) || pendingDeleteSlot == null) return;
            string slotId = pendingDeleteSlot;
            host.UI.Close(views.DeleteConfirmation.Id);
            Run(token => DeleteSlot(slotId, token));
        }

        private void BackToMain()
        {
            if (!CanInteract || host.UI.ModalCount != 0) return;
            if (host.UI.IsOpen(views.SaveSelection.Id)) host.UI.Close(views.SaveSelection.Id);
            else if (host.UI.IsOpen(views.Credits.Id)) host.UI.Close(views.Credits.Id);
            else return;
            pendingRefresh = true;
        }

        public Task RefreshSlotsAsync()
        {
            if (!IsReady || busy) return LastOperation;
            Run(RefreshSlots);
            return LastOperation;
        }

        private void Run(Func<CancellationToken, Task> operation)
        {
            if (!CanInteract || busy) return;
            operationCancellation?.Dispose();
            operationCancellation = new CancellationTokenSource();
            LastOperation = Execute(operation, operationCancellation.Token, bindVersion);
        }

        private async Task Execute(Func<CancellationToken, Task> operation, CancellationToken token, int version)
        {
            GameObject selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            busy = true;
            RefreshControls();
            try { await operation(token); }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (IsCurrent(version) && !token.IsCancellationRequested)
                {
                    LastStatus = PersistenceStatus.StorageError;
                    views.SaveStatus.text = "Save access failed: " + exception.Message;
                }
            }
            finally
            {
                if (IsCurrent(version))
                {
                    busy = false;
                    RefreshControls();
                    if (host != null && host.IsReady && EventSystem.current != null &&
                        EventSystem.current.currentSelectedGameObject == null)
                    {
                        var old = selection != null ? selection.GetComponent<Selectable>() : null;
                        if (old != null && old.IsActive() && old.IsInteractable())
                            EventSystem.current.SetSelectedGameObject(selection);
                        else if (IsSelectionOpen) EventSystem.current.SetSelectedGameObject(views.Slots[selectedIndex].gameObject);
                        else if (host.UI.CurrentScreenId == "MainMenu") EventSystem.current.SetSelectedGameObject(views.Primary.gameObject);
                    }
                }
            }
        }

        private async Task RefreshSlots(CancellationToken token)
        {
            pointerSlotIndex = -1;
            var result = await host.Persistence.ListSlotsAsync(token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent(bindVersion) || !host.IsReady) return;
            LastStatus = result.Status;
            for (int i = 0; i < 3; i++)
            {
                string slotId = "demo-" + (i + 1);
                slots[i] = new SaveSlotInfo(slotId, result.IsSuccess ? PersistenceStatus.NotFound : result.Status,
                    message: result.Message);
                if (result.IsSuccess)
                    foreach (SaveSlotInfo info in result.Data)
                        if (info.SlotId == slotId)
                        {
                            bool incompatible = info.Status == PersistenceStatus.Success && info.Metadata != null &&
                                (info.Metadata.ModelId != schema.ModelId || info.Metadata.DataVersion != schema.Version);
                            slots[i] = incompatible ? new SaveSlotInfo(slotId, PersistenceStatus.Incompatible,
                                info.Metadata, info.RecoveredFromBackup, "This save uses another model or version.") : info;
                            break;
                        }
            }
            // Unknown storage is never presented as empty, which would invite a destructive new-game assumption.
            HasSaves = Array.Exists(slots, info => info.Status != PersistenceStatus.NotFound);
            views.PrimaryLabel.text = HasSaves ? "Continue game" : "New game";
            views.SaveStatus.text = result.IsSuccess ? string.Empty :
                result.Status + ": " + result.Message;
            RefreshSlotLabels();
            RefreshControls();
        }

        private void RefreshSlotLabels()
        {
            for (int i = 0; i < 3; i++)
            {
                SaveSlotInfo info = slots[i];
                var card = views.Slots[i].GetComponent<GameSaveSlotCard>();
                if (card != null)
                {
                    card.Present(i + 1, info);
                    card.SetSelected(selectedIndex == i);
                    continue;
                }
                string label = info == null ? "Checking" : info.Status == PersistenceStatus.NotFound ? "Empty" :
                    info.Status == PersistenceStatus.Success ? (info.RecoveredFromBackup ? "Recoverable save" : "Saved game") :
                    info.Status.ToString();
                string date = info?.Metadata != null ? "\n" + info.Metadata.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "";
                views.SlotLabels[i].text = (selectedIndex == i ? "> " : "") + "Slot " + (i + 1) + "  /  " + label + date;
            }
        }

        private async Task StartFirstEmptyGame(CancellationToken token)
        {
            await RefreshSlots(token);
            for (int i = 0; i < 3; i++)
                if (slots[i].Status == PersistenceStatus.NotFound)
                {
                    selectedIndex = i;
                    await StartEmptyGame(token);
                    return;
                }
            token.ThrowIfCancellationRequested();
            host.UI.Open(views.SaveSelection.Id);
            FocusSelectedSlot();
            views.SaveStatus.text = "No empty slot is available. Load a save, or explicitly delete a selected slot.";
        }

        private async Task StartEmptyGame(CancellationToken token)
        {
            string slotId = SelectedSlotId;
            var exists = await host.Persistence.ExistsAsync(slotId, token);
            token.ThrowIfCancellationRequested();
            LastStatus = exists.Status;
            if (exists.Status != PersistenceStatus.NotFound)
            {
                await RefreshSlots(token);
                LastStatus = exists.IsSuccess ? PersistenceStatus.InvalidData : exists.Status;
                views.SaveStatus.text = "A new game needs an empty slot. Existing saves are never replaced automatically.\n" +
                    (exists.IsSuccess ? "The selected slot is occupied." : exists.Status + ": " + exists.Message);
                if (host.UI.CurrentScreenId == "MainMenu")
                {
                    host.UI.Open(views.SaveSelection.Id);
                    FocusSelectedSlot();
                }
                return;
            }
            session.PrepareNewGame(slotId);
            await NavigateToGameplay();
        }

        private async Task LoadGame(CancellationToken token)
        {
            string slotId = SelectedSlotId;
            var result = await host.Persistence.LoadAsync(slotId, schema, token);
            token.ThrowIfCancellationRequested();
            if (!IsSelectionOpen) return;
            LastStatus = result.Status;
            if (!result.IsSuccess)
            {
                views.SaveStatus.text = result.Status + ": " + result.Message;
                return;
            }
            session.PrepareLoadedGame(slotId, result.Data);
            await NavigateToGameplay();
        }

        private async Task NavigateToGameplay()
        {
            // Closing the views cancels disk work, but cannot cancel this already prepared transition.
            try { await host.Scenes.LoadAsync(new SceneLoadRequest(GameScenePaths.GameplayPath)); }
            catch
            {
                // Unity can activate a destination after cancellation. Its adapter must still receive the DTO.
                if (SceneManager.GetActiveScene().path != GameScenePaths.GameplayPath) session.ClearPendingSnapshot();
                throw;
            }
        }

        private async Task DeleteSlot(string slotId, CancellationToken token)
        {
            var result = await host.Persistence.DeleteAsync(slotId, token);
            token.ThrowIfCancellationRequested();
            LastStatus = result.Status;
            if (!IsSelectionOpen) return;
            await RefreshSlots(token);
            LastStatus = result.Status;
            views.SaveStatus.text = result.IsSuccess ? "Slot deleted. A new game can now use this empty slot." :
                result.Status + ": " + result.Message;
            // Keep the confirmed slot selected even if deleting a different generation changed
            // the visible contextual button and caused EventSystem to clear its selection.
            FocusSelectedSlot();
        }

        private void RefreshControls()
        {
            bool enabled = ready && !busy && host != null && host.IsReady && !QuitConfirmed;
            views.Primary.interactable = enabled;
            views.Options.interactable = enabled;
            views.CreditsButton.interactable = enabled;
            views.Exit.interactable = enabled;
            foreach (Button slot in views.Slots) slot.interactable = enabled;
            SaveSlotInfo selected = slots[selectedIndex];
            bool empty = selected != null && selected.Status == PersistenceStatus.NotFound;
            if (views.Load != null) views.Load.gameObject.SetActive(false);
            views.NewGame.gameObject.SetActive(empty);
            views.Delete.gameObject.SetActive(selected != null && !empty);
            views.NewGame.interactable = enabled && empty;
            views.Delete.interactable = enabled && selected != null && !empty;
            views.DeleteYes.interactable = enabled;
            views.QuitYes.interactable = enabled;
            Button action = empty ? views.NewGame : selected != null ? views.Delete : null;
            Navigation slotNavigation = views.Slots[2].navigation;
            slotNavigation.selectOnDown = action != null ? action : views.SavesBack;
            views.Slots[2].navigation = slotNavigation;
            Navigation newGameNavigation = views.NewGame.navigation;
            newGameNavigation.selectOnUp = views.Slots[2];
            newGameNavigation.selectOnLeft = views.SavesBack;
            views.NewGame.navigation = newGameNavigation;
            Navigation deleteNavigation = views.Delete.navigation;
            deleteNavigation.selectOnUp = views.Slots[2];
            deleteNavigation.selectOnLeft = views.SavesBack;
            views.Delete.navigation = deleteNavigation;
            Navigation backNavigation = views.SavesBack.navigation;
            backNavigation.selectOnRight = action;
            backNavigation.selectOnUp = views.Slots[2];
            views.SavesBack.navigation = backNavigation;
        }

        private void ScreenClosed(string id)
        {
            if (id == views.SaveSelection.Id || id == "MainMenu") operationCancellation?.Cancel();
            if (id == views.SaveSelection.Id)
            {
                pointerSlotIndex = -1;
                pendingRefresh = true;
            }
            if (id == views.DeleteConfirmation.Id) pendingDeleteSlot = null;
        }

        private void Wire(bool wire)
        {
            ButtonListener(views.Primary, Primary, wire);
            ButtonListener(views.CreditsButton, ShowCredits, wire);
            ButtonListener(views.Exit, RequestQuit, wire);
            ButtonListener(views.NewGame, StartSelectedNewGame, wire);
            ButtonListener(views.Delete, RequestDelete, wire);
            ButtonListener(views.SavesBack, BackToMain, wire);
            ButtonListener(views.CreditsBack, BackToMain, wire);
            ButtonListener(views.QuitYes, ConfirmQuit, wire);
            ButtonListener(views.QuitNo, CancelQuit, wire);
            ButtonListener(views.DeleteYes, ConfirmDelete, wire);
            ButtonListener(views.DeleteNo, CancelDelete, wire);
            if (wire)
            {
                slotListeners = new Action<bool>[3];
                slotActivations = new GameSlotActivation[3];
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    slotListeners[i] = pointer => ActivateSlot(index, pointer);
                    slotActivations[i] = views.Slots[i].GetComponent<GameSlotActivation>();
                    slotActivations[i].Activated += slotListeners[i];
                }
            }
            else if (slotListeners != null)
            {
                for (int i = 0; i < 3; i++)
                    if (slotActivations[i] != null)
                        slotActivations[i].Activated -= slotListeners[i];
                slotListeners = null;
                slotActivations = null;
            }
        }

        private static void ButtonListener(Button button, UnityEngine.Events.UnityAction action, bool wire)
        {
            if (button == null) return;
            if (wire) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }

        private void OnDisable() { bindVersion++; Release(); }
        private void OnDestroy() { bindVersion++; Release(); }

        private void Release()
        {
            if (presentationCanvas != null) presentationCanvas.enabled = false;
            ready = false;
            pendingBack = pendingSlotAction = pendingContinue = pendingRefresh = false;
            busy = false;
            pointerSlotIndex = -1;
            pendingDeleteSlot = null;
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            operationCancellation = null;
            if (views != null) Wire(false);
            if (host != null && host.Coordinator != null) host.UI.ScreenClosed -= ScreenClosed;
            for (int i = subscriptions.Count - 1; i >= 0; i--) subscriptions[i].Dispose();
            subscriptions.Clear();
        }
    }
}
