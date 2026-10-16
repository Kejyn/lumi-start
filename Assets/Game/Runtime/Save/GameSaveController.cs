using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Persistence;
using Lumivale.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Lumivale.Start.Game
{
    /// <summary>Project-owned capture, validation and application of demo state, with explicit saves.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase4DemoController")]
    public sealed class GameSaveController : MonoBehaviour
    {
        [SerializeField] private GameSceneController navigation;
        [SerializeField] private Transform player;
        [SerializeField] private UiScreen savesScreen;
        [SerializeField] private UiScreen deleteScreen;
        [SerializeField] private UnityEngine.UI.Button openButton;
        [SerializeField] private UnityEngine.UI.Button pauseButton;
        [SerializeField] private GameChoiceButton slotButton;
        [SerializeField] private UnityEngine.UI.Button increaseButton;
        [SerializeField] private UnityEngine.UI.Button decreaseButton;
        [SerializeField] private UnityEngine.UI.Button saveButton;
        [SerializeField] private UnityEngine.UI.Button loadButton;
        [SerializeField] private UnityEngine.UI.Button deleteButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private UnityEngine.UI.Button cancelButton;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI deleteText;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly SaveSchema<GameSaveData> schema = GameSaveData.CreateSchema();
        private GameBootstrap host;
        private CancellationTokenSource viewCancellation;
        private int bindVersion;
        private int slotIndex;
        private bool started;
        private bool ready;
        private bool pendingOpen;
        private bool busy;
        private GameObject operationSelection;
        private string pendingDeleteSlot;
        private List<SaveSlotInfo> listedSlots;
        private bool showingSlots;

        public int Counter { get; private set; }
        public string SlotId => "demo-" + (slotIndex + 1);
        public bool IsReady => ready && host != null && host.IsReady && host.HasPersistence;
        public bool IsOpen => IsReady && host.UI.IsOpen("Saves");
        public bool IsBusy => busy;
        public Task LastOperation { get; private set; } = Task.CompletedTask;
        public PersistenceStatus LastStatus { get; private set; }
        public string StatusMessage => statusText != null ? statusText.text : "";
        public GameChoiceButton SlotButton => slotButton;
        public UnityEngine.UI.Button SaveButton => saveButton;
        public UnityEngine.UI.Button LoadButton => loadButton;
        public UnityEngine.UI.Button DeleteButton => deleteButton;
        public UnityEngine.UI.Button IncreaseButton => increaseButton;
        public UnityEngine.UI.Button DecreaseButton => decreaseButton;
        public UnityEngine.UI.Button ConfirmDeleteButton => confirmButton;
        public UnityEngine.UI.Button CancelDeleteButton => cancelButton;
        public UnityEngine.UI.Button BackButton => backButton;
        public UnityEngine.UI.Button OpenButton => openButton;
        public UnityEngine.UI.Button PauseButton => pauseButton;

        /// <summary>Selects an existing project slot without reading, writing or deleting its data.</summary>
        public void ConfigureSelectedSlot(string slotId)
        {
            if (slotId != "demo-1" && slotId != "demo-2" && slotId != "demo-3")
                throw new ArgumentException("The Starter uses demo-1, demo-2 and demo-3.", nameof(slotId));
            slotIndex = slotId[slotId.Length - 1] - '1';
            RefreshState();
        }

        /// <summary>Applies a validated menu-loaded snapshot; storage remains owned by Persistence.</summary>
        public void ApplySnapshot(GameSaveData data, string slotId)
        {
            if (!IsReady || busy || player == null)
                throw new InvalidOperationException("Wait for the gameplay save view before applying its state.");
            schema.Validate(data);
            ConfigureSelectedSlot(slotId);
            player.position = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
            Counter = data.Counter;
            RefreshState();
        }

        public void Configure(GameSceneController nav, Transform cube, UiScreen saves, UiScreen confirm,
            UnityEngine.UI.Button open, UnityEngine.UI.Button pause, GameChoiceButton slot,
            UnityEngine.UI.Button increase, UnityEngine.UI.Button decrease, UnityEngine.UI.Button save,
            UnityEngine.UI.Button load, UnityEngine.UI.Button delete, UnityEngine.UI.Button back,
            UnityEngine.UI.Button yes, UnityEngine.UI.Button no, TextMeshProUGUI state,
            TextMeshProUGUI status, TextMeshProUGUI confirmation)
        {
            navigation = nav; player = cube; savesScreen = saves; deleteScreen = confirm;
            openButton = open; pauseButton = pause; slotButton = slot; increaseButton = increase;
            decreaseButton = decrease; saveButton = save; loadButton = load; deleteButton = delete;
            backButton = back; confirmButton = yes; cancelButton = no;
            stateText = state; statusText = status; deleteText = confirmation;
        }

        private void Start() { started = true; Bind(); }
        private void OnEnable() { if (started) Bind(); }
        private async void Bind()
        {
            int version = ++bindVersion;
            try
            {
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null || !host.HasPersistence || !navigation.IsGameplay) return;
                await host.Ready;
                if (this == null || !isActiveAndEnabled || version != bindVersion || host == null || !host.IsReady) return;
                subscriptions.Add(host.UI.Register(savesScreen));
                subscriptions.Add(host.UI.Register(deleteScreen));
                if (host.Input.RuntimeAsset.FindAction("Common/OpenSaves") != null)
                    subscriptions.Add(host.Input.Subscribe("Common/OpenSaves", context =>
                    {
                        // Gameplay deliberately leaves the UI map inactive. The game owns this
                        // pointer shortcut; defer opening until native action callbacks finish.
                        if (context.control.device is UnityEngine.InputSystem.Mouse pointer &&
                            context.control.name == "leftButton" && !RectTransformUtility.RectangleContainsScreenPoint(
                                openButton.GetComponent<RectTransform>(), pointer.position.ReadValue())) return;
                        pendingOpen = true;
                    }));
                if (host.Input.RuntimeAsset.FindAction("Gameplay/IncrementCounter") != null)
                    subscriptions.Add(host.Input.Subscribe("Gameplay/IncrementCounter", _ =>
                    { if (IsReady && !host.Pause.IsPaused && !host.Input.IsSuspended) Increase(); }));
                host.UI.ScreenClosed += ScreenClosed;
                LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
                Wire(true);
                ready = true;
                RefreshState();
            }
            catch (OperationCanceledException) { if (version == bindVersion) Release(); }
            catch (Exception exception) { if (version == bindVersion) { Release(); Debug.LogException(exception, this); } }
        }

        private void Update()
        {
            if (pendingOpen)
            {
                pendingOpen = false;
                if (IsReady && !host.Input.IsSuspended && !host.Scenes.IsLoading &&
                    host.UI.ModalCount <= (host.Pause.IsPaused ? 1 : 0)) Open();
            }
            if (IsOpen) RefreshState();
        }

        private void Open()
        {
            if (!IsReady || IsOpen || busy || host.Scenes.IsLoading || host.Input.IsSuspended) return;
            host.Pause.Pause();
            viewCancellation?.Dispose();
            viewCancellation = new CancellationTokenSource();
            host.UI.OpenModal("Saves");
            Run(List);
        }

        private void ChangeSlot(int delta)
        {
            if (busy || !IsOpen) return;
            slotIndex = (slotIndex + delta + 3) % 3;
            RefreshState();
            Run(List);
        }

        private void Increase() { if (!busy) { Counter = Math.Min(1000000, Counter + 1); RefreshState(); } }
        private void Decrease() { if (!busy) { Counter = Math.Max(0, Counter - 1); RefreshState(); } }
        private void Save() { if (IsOpen) Run(SaveState); }
        private void Load() { if (IsOpen) Run(LoadState); }
        private void RequestDelete()
        {
            if (!IsOpen || busy) return;
            pendingDeleteSlot = SlotId;
            GameText.Set(deleteText, "save.confirm_delete_demo", slotIndex + 1);
            host.UI.OpenModal("DeleteSave");
        }
        private void ConfirmDelete()
        {
            if (busy || !IsReady || !host.UI.IsOpen("DeleteSave")) return;
            string slot = pendingDeleteSlot;
            host.UI.CloseModal();
            pendingDeleteSlot = null;
            Run(token => DeleteState(slot, token));
        }
        private void CancelDelete() { if (IsReady && host.UI.IsOpen("DeleteSave")) host.UI.CloseModal(); }
        private void Back() { if (IsOpen) host.UI.Close("Saves"); }

        private void Run(Func<CancellationToken, Task> operation)
        {
            if (busy || viewCancellation == null || viewCancellation.IsCancellationRequested) return;
            LastOperation = Execute(operation, viewCancellation.Token, bindVersion);
        }

        private async Task Execute(Func<CancellationToken, Task> operation, CancellationToken token, int version)
        {
            operationSelection = UnityEngine.EventSystems.EventSystem.current != null ?
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            busy = true; SetControls(false);
            try { await operation(token); }
            catch (OperationCanceledException)
            {
                if (this != null && version == bindVersion) SetStatus("save.canceled");
            }
            catch (Exception exception)
            {
                if (this != null && version == bindVersion)
                {
                    SetStatus("save.access_failed");
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                if (this != null && version == bindVersion)
                {
                    busy = false; SetControls(true); RefreshState();
                    if (IsOpen && UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null)
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(
                            operationSelection != null && operationSelection.activeInHierarchy ? operationSelection : slotButton.gameObject);
                }
            }
        }

        private async Task SaveState(CancellationToken token)
        {
            Vector3 position = player.position;
            var data = new GameSaveData { PositionX = position.x, PositionY = position.y,
                PositionZ = position.z, Counter = Counter };
            var result = await host.Persistence.SaveAsync(SlotId, data, schema, token);
            if (token.IsCancellationRequested || this == null || !IsOpen) return;
            Show(result.Status, result.Message, result.RecoveredFromBackup);
        }

        private async Task LoadState(CancellationToken token)
        {
            var result = await host.Persistence.LoadAsync(SlotId, schema, token);
            token.ThrowIfCancellationRequested();
            if (this == null || !isActiveAndEnabled || !IsOpen) return;
            if (result.IsSuccess)
            {
                player.position = new Vector3(result.Data.PositionX, result.Data.PositionY, result.Data.PositionZ);
                Counter = result.Data.Counter;
            }
            Show(result.Status, result.Message, result.RecoveredFromBackup);
        }

        private async Task DeleteState(string slot, CancellationToken token)
        {
            var result = await host.Persistence.DeleteAsync(slot, token);
            if (token.IsCancellationRequested || this == null || !IsOpen) return;
            Show(result.Status, result.Message, false);
        }

        private async Task List(CancellationToken token)
        {
            var result = await host.Persistence.ListSlotsAsync(token);
            token.ThrowIfCancellationRequested();
            if (this == null || !IsOpen) return;
            if (!result.IsSuccess) { Show(result.Status, result.Message, false); return; }
            listedSlots = new List<SaveSlotInfo>(result.Data);
            showingSlots = true;
            RefreshSlotList();
        }

        private void Show(PersistenceStatus status, string message, bool recovered)
        {
            if (this == null || statusText == null) return;
            LastStatus = status;
            SetStatus(status == PersistenceStatus.Success && recovered ? "save.recovered" : "save.result." + status);
        }

        private void SetStatus(string key)
        {
            showingSlots = false;
            GameText.Set(statusText, key);
        }

        private void RefreshSlotList()
        {
            if (!showingSlots || statusText == null || listedSlots == null || !GameText.IsReady) return;
            if (listedSlots.Count == 0)
            {
                GameText.Set(statusText, "save.slots.none");
                return;
            }
            var entries = new List<string>();
            foreach (SaveSlotInfo item in listedSlots)
                entries.Add(GameText.Text("save.slots.entry", item.SlotId,
                    GameText.Text("persistence.status." + item.Status)));
            // The composed entries contain translated fragments; rebuild them on locale changes.
            GameText.Set(statusText, string.Empty);
            statusText.text = GameText.Text("save.slots.list", string.Join("; ", entries));
        }

        private void LocaleChanged(Locale locale)
        {
            if (!ready) return;
            RefreshState();
            RefreshSlotList();
        }

        private void RefreshState()
        {
            if (slotButton == null || stateText == null || player == null || !GameText.IsReady) return;
            slotButton.GetComponentInChildren<TextMeshProUGUI>().text = GameText.Text("save.slot_selector", slotIndex + 1);
            Vector3 p = player.position;
            stateText.text = GameText.Text("save.gameplay_state", Counter, p.x, p.y, p.z);
        }

        private void ScreenClosed(string id)
        {
            if (id == "Saves") { viewCancellation?.Cancel(); pendingDeleteSlot = null; }
            if (id == "DeleteSave") pendingDeleteSlot = null;
        }

        private void SetControls(bool enabled)
        {
            foreach (var button in new UnityEngine.UI.Button[] { slotButton, increaseButton, decreaseButton,
                saveButton, loadButton, deleteButton }) if (button != null) button.interactable = enabled;
        }

        private void Wire(bool wire)
        {
            Button(openButton, Open, wire); Button(pauseButton, Open, wire);
            Button(increaseButton, Increase, wire); Button(decreaseButton, Decrease, wire);
            Button(saveButton, Save, wire); Button(loadButton, Load, wire); Button(deleteButton, RequestDelete, wire);
            Button(backButton, Back, wire); Button(confirmButton, ConfirmDelete, wire); Button(cancelButton, CancelDelete, wire);
            if (slotButton != null) { if (wire) slotButton.ChoiceRequested += ChangeSlot; else slotButton.ChoiceRequested -= ChangeSlot; }
        }
        private static void Button(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action, bool wire)
        { if (button != null) { if (wire) button.onClick.AddListener(action); else button.onClick.RemoveListener(action); } }
        private void OnDisable() { bindVersion++; Release(); }
        private void OnDestroy() { bindVersion++; Release(); }
        private void Release()
        {
            ready = false; pendingOpen = false; busy = false;
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            showingSlots = false;
            listedSlots = null;
            viewCancellation?.Cancel(); viewCancellation?.Dispose(); viewCancellation = null;
            Wire(false);
            if (host != null && host.Coordinator != null) host.UI.ScreenClosed -= ScreenClosed;
            for (int i = subscriptions.Count - 1; i >= 0; i--) subscriptions[i].Dispose();
            subscriptions.Clear();
        }
    }
}
