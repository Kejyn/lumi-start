using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.Settings;
using Lumivale.Core.UI;
using Lumivale.Start.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Lumivale.Start.Game
{
    /// <summary>
    /// Starter tab presentation and project preferences. GameSettingsController owns the
    /// Settings draft, graphics and mixer integration; Input owns rebinding and its suspension.
    /// Keep this component on the active scene controller, outside individual tab panels.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "ProductOptionsController")]
    public sealed class GameOptionsController : MonoBehaviour
    {
        [Serializable]
        public sealed class ViewReferences
        {
            public UnityEngine.UI.Button[] Tabs;
            public GameObject[] Panels;
            public UnityEngine.UI.Slider MovementSensitivity;
            public UnityEngine.UI.Toggle InvertY;
            public TextMeshProUGUI SensitivityLabel;
            public UnityEngine.UI.CanvasScaler Scaler;
            public UnityEngine.UI.Slider InterfaceScale;
            public TextMeshProUGUI ScaleLabel;
            public UnityEngine.UI.Toggle ShowHints;
            public GameObject[] Hints;
            public GameRebindRow[] BindingRows;
            public UnityEngine.UI.Button ResetBindings;
            public TextMeshProUGUI BindingStatus;
            public UiScreen UnsavedConfirmation;
            public UnityEngine.UI.Button DiscardChanges;
            public UnityEngine.UI.Button SaveChanges;
            public TextMeshProUGUI ConfirmationStatus;
        }

        [SerializeField] private GameSceneController navigation;
        [SerializeField] private GameSettingsController existingOptions;
        [SerializeField] private ViewReferences view;
        private GameBootstrap host;
        private GamePreferences preferences;
        private GamePresentationValues draft;
        private GamePresentationValues presentationSnapshot;
        private SettingsValues settingsSnapshot;
        private Dictionary<Guid, string> effectiveBindingSnapshot;
        private string bindingSnapshot;
        private IDisposable confirmationRegistration;
        private IDisposable previousTabSubscription;
        private IDisposable nextTabSubscription;
        private int pendingTabDelta;
        private Vector2 referenceResolution;
        private bool referenceResolutionCaptured;
        private InputRebindSession operation;
        private GameRebindRow captureRow;
        private GameRebindRow pendingRow;
        private int pendingFrame;
        private int bindVersion;
        private int rebindVersion;
        private bool started;
        private bool ready;
        private bool wasOpen;
        private bool bindingsResetRequested;
        private UnityAction[] tabListeners;

        public bool IsReady => ready && host != null && host.IsReady && navigation != null && navigation.IsReady;
        public bool IsOpen => IsReady && existingOptions.IsOpen;
        public int CurrentTab { get; private set; }
        public GamePresentationValues Draft => draft?.Clone();
        public ViewReferences View => view;
        public bool IsRebinding => IsReady && host.Input.IsRebinding;
        public bool HasUnsavedChanges => IsOpen && draft != null &&
            (!SamePresentation(draft, presentationSnapshot) || !SameSettings(existingOptions.Draft, settingsSnapshot) ||
                BindingsDiffer());

        public void Configure(GameSceneController sceneNavigation,
            GameSettingsController coreOptions, ViewReferences references)
        {
            navigation = sceneNavigation;
            existingOptions = coreOptions;
            view = references;
        }

        private void Start()
        {
            started = true;
            Bind();
        }

        private void OnEnable()
        {
            if (started) Bind();
        }

        private async void Bind()
        {
            int version = ++bindVersion;
            try
            {
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null || existingOptions == null || navigation == null)
                    throw new InvalidOperationException("Product Options requires the configured Starter composition.");
                await host.Ready;
                while (this != null && isActiveAndEnabled && version == bindVersion &&
                    host != null && host.IsReady && !existingOptions.IsReady)
                    await Awaitable.NextFrameAsync();
                if (this == null || !isActiveAndEnabled || version != bindVersion ||
                    host == null || !host.IsReady || !existingOptions.IsReady) return;
                ValidateReferences();
                preferences = GameSessionState.GetOrCreate(host).Preferences;
                preferences.LoadBindingsOnce(host.Input);
                if (!referenceResolutionCaptured)
                {
                    referenceResolution = view.Scaler.referenceResolution;
                    referenceResolutionCaptured = true;
                }
                ApplyPresentation(preferences.Current);
                Wire(true);
                confirmationRegistration = host.UI.Register(view.UnsavedConfirmation);
                host.UI.ScreenClosed += OnScreenClosed;
                host.Input.BindingsChanged += RefreshBindings;
                LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
                existingOptions.DraftOpened += BeginDraft;
                existingOptions.DraftApplied += OnDraftApplied;
                existingOptions.DraftReset += ResetProjectDraft;
                existingOptions.DraftApplyOverride = ApplyCompleteDraft;
                existingOptions.BackRequested = RequestBack;
                navigation.BackInterceptor = RequestBack;
                if (host.Input.RuntimeAsset.FindAction("UI/PreviousOptionsTab") != null)
                    previousTabSubscription = host.Input.Subscribe("UI/PreviousOptionsTab", _ => RequestTab(-1));
                if (host.Input.RuntimeAsset.FindAction("UI/NextOptionsTab") != null)
                    nextTabSubscription = host.Input.Subscribe("UI/NextOptionsTab", _ => RequestTab(1));
                ready = true;
                RefreshBindings();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (version == bindVersion)
                {
                    Release();
                    Debug.LogException(exception, this);
                }
            }
        }

        private void ValidateReferences()
        {
            if (view == null || view.Tabs == null || view.Panels == null ||
                view.Tabs.Length != 5 || view.Panels.Length != 5 ||
                view.MovementSensitivity == null || view.InvertY == null ||
                view.InterfaceScale == null || view.ShowHints == null || view.Scaler == null ||
                view.ResetBindings == null || view.BindingStatus == null ||
                view.BindingRows == null || view.SensitivityLabel == null || view.ScaleLabel == null ||
                view.UnsavedConfirmation == null || view.DiscardChanges == null ||
                view.SaveChanges == null || view.ConfirmationStatus == null)
                throw new InvalidOperationException("Configure all five Product Options tabs and their active controls.");
            for (int index = 0; index < 5; index++)
                if (view.Tabs[index] == null || view.Panels[index] == null)
                    throw new InvalidOperationException("Product Options contains a missing tab or panel.");
        }

        private void Update()
        {
            if (!IsReady) return;
            bool open = existingOptions.IsOpen;
            if (open && !wasOpen) BeginDraft();
            else if (!open && wasOpen) DiscardDraft();
            wasOpen = open;
            if (pendingTabDelta != 0)
            {
                int delta = pendingTabDelta;
                pendingTabDelta = 0;
                if (CanEdit)
                {
                    SelectTab((CurrentTab + delta + view.Tabs.Length) % view.Tabs.Length);
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(view.Tabs[CurrentTab].gameObject);
                }
            }
            if (pendingRow == null || Time.frameCount < pendingFrame || !open) return;
            var row = pendingRow;
            pendingRow = null;
            BeginCapture(row);
        }

        private void BeginDraft()
        {
            if (!IsOpen) return;
            wasOpen = true;
            draft = preferences.Current;
            CaptureAppliedSnapshot();
            RefreshPresentation();
            SelectTab(0);
            SetBindingStatus(preferences.BindingDiagnostic == null ? string.Empty : "options.controls.stored_invalid");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.Tabs[0].gameObject);
        }

        public void SelectTab(int index)
        {
            if (view == null || view.Panels == null || index < 0 || index >= view.Panels.Length) return;
            bool closedDropdown = existingOptions != null && existingOptions.CloseDropdowns(restoreFocus: false);
            CancelCapture();
            CurrentTab = index;
            for (int tab = 0; tab < view.Panels.Length; tab++) view.Panels[tab].SetActive(tab == index);
            existingOptions.RefreshDropdownCaptions();
            RefreshPanelNavigation();
            RefreshBindings();
            if (closedDropdown && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(view.Tabs[index].gameObject);
        }

        private bool CanEdit => IsOpen && !host.Input.IsSuspended && !host.Input.IsRebinding &&
            !existingOptions.IsAnyDropdownOpen && !host.UI.IsOpen(view.UnsavedConfirmation.Id) && !host.Scenes.IsLoading;

        private void RequestTab(int delta)
        {
            if (CanEdit) pendingTabDelta = delta;
        }

        private void RefreshPanelNavigation()
        {
            var controls = view.Panels[CurrentTab].GetComponentsInChildren<UnityEngine.UI.Selectable>(false);
            UnityEngine.UI.Selectable first = null;
            UnityEngine.UI.Selectable last = null;
            foreach (var control in controls)
                if (control.IsActive() && control.IsInteractable())
                {
                    if (first == null) first = control;
                    last = control;
                }
            var tabNavigation = view.Tabs[CurrentTab].navigation;
            tabNavigation.selectOnDown = first ?? existingOptions.BackButton;
            view.Tabs[CurrentTab].navigation = tabNavigation;
            foreach (var action in new[] { existingOptions.BackButton, existingOptions.ResetButton, existingOptions.ApplyButton })
            {
                var buttonNavigation = action.navigation;
                buttonNavigation.selectOnUp = last ?? view.Tabs[CurrentTab];
                action.navigation = buttonNavigation;
            }
        }

        private SettingsValues ApplyCompleteDraft(SettingsValues settingsDraft)
        {
            if (!IsOpen || draft == null || host.Input.IsRebinding)
                throw new InvalidOperationException("Finish editing or cancel the binding capture before applying.");
            if (!GamePreferences.IsValid(draft))
                throw new ArgumentException("Presentation values are outside the supported ranges.");
            var beforeSettings = host.Settings.Current;
            string settingsKey = host.Settings.StorageKey;
            bool hadSettings = PlayerPrefs.HasKey(settingsKey);
            string previousSettingsDocument = PlayerPrefs.GetString(settingsKey);
            try
            {
                host.Settings.Update(settingsDraft);
                host.Settings.ApplyGraphics();
                host.Settings.Save();
                preferences.SaveDraft(draft, host.Input, bindingsResetRequested || BindingsDiffer());
                return host.Settings.Current;
            }
            catch (Exception failure)
            {
                SetBindingStatus("options.status.apply_failed");
                try
                {
                    host.Settings.Update(beforeSettings);
                    host.Settings.ApplyGraphics();
                    if (hadSettings) PlayerPrefs.SetString(settingsKey, previousSettingsDocument);
                    else PlayerPrefs.DeleteKey(settingsKey);
                    PlayerPrefs.Save();
                }
                catch (Exception rollback)
                {
                    throw new AggregateException("Applying failed and restoring the previous settings also failed.", failure, rollback);
                }
                throw;
            }
        }

        private void CaptureAppliedSnapshot()
        {
            presentationSnapshot = preferences.Current;
            settingsSnapshot = host.Settings.Current;
            bindingSnapshot = host.Input.ExportBindingOverrides();
            effectiveBindingSnapshot = CaptureEffectiveBindings();
            bindingsResetRequested = false;
        }

        private void OnDraftApplied()
        {
            draft = preferences.Current;
            CaptureAppliedSnapshot();
            RefreshPresentation();
            SetBindingStatus("options.status.applied");
        }

        private Dictionary<Guid, string> CaptureEffectiveBindings()
        {
            var result = new Dictionary<Guid, string>();
            foreach (var map in host.Input.RuntimeAsset.actionMaps)
                foreach (var binding in map.bindings) result.Add(binding.id, binding.effectivePath);
            return result;
        }

        private bool BindingsDiffer()
        {
            if (effectiveBindingSnapshot == null || host?.Input.RuntimeAsset == null) return false;
            int count = 0;
            foreach (var map in host.Input.RuntimeAsset.actionMaps)
                foreach (var binding in map.bindings)
                {
                    count++;
                    if (!effectiveBindingSnapshot.TryGetValue(binding.id, out string previous) ||
                        !string.Equals(previous, binding.effectivePath, StringComparison.OrdinalIgnoreCase)) return true;
                }
            return count != effectiveBindingSnapshot.Count;
        }

        private static bool SamePresentation(GamePresentationValues left, GamePresentationValues right) =>
            left != null && right != null && left.MovementSensitivity.Equals(right.MovementSensitivity) &&
            left.InvertY == right.InvertY && left.InterfaceScale.Equals(right.InterfaceScale) && left.ShowHints == right.ShowHints;

        private static bool SameSettings(SettingsValues left, SettingsValues right) =>
            left != null && left.ContentEquals(right);

        private bool RequestBack()
        {
            if (!IsOpen) return false;
            if (existingOptions.CloseDropdowns()) return true;
            if (host.Input.IsSuspended || host.Input.IsRebinding || host.Scenes.IsLoading) return true;
            if (host.UI.IsOpen(view.UnsavedConfirmation.Id))
            {
                host.UI.Close(view.UnsavedConfirmation.Id);
                return true;
            }
            if (HasUnsavedChanges)
            {
                GameText.Set(view.ConfirmationStatus, string.Empty);
                host.UI.OpenModal(view.UnsavedConfirmation.Id);
            }
            else host.UI.Close("Options");
            return true;
        }

        private void DiscardAndClose()
        {
            if (!IsOpen || !host.UI.IsOpen(view.UnsavedConfirmation.Id) || host.Input.IsSuspended) return;
            host.UI.Close("Options");
        }

        private void SaveAndClose()
        {
            if (!IsOpen || !host.UI.IsOpen(view.UnsavedConfirmation.Id) || host.Input.IsSuspended) return;
            if (existingOptions.TryApplyDraft()) host.UI.Close("Options");
            else GameText.Set(view.ConfirmationStatus, "options.status.apply_failed");
        }

        private void ResetProjectDraft()
        {
            if (!IsOpen || host.Input.IsRebinding) return;
            draft = new GamePresentationValues();
            host.Input.ResetBindingOverrides();
            bindingsResetRequested = true;
            RefreshPresentation();
            SetBindingStatus("options.status.defaults");
        }

        private void ResetBindings()
        {
            if (!IsOpen || host.Input.IsRebinding) return;
            host.Input.ResetBindingOverrides();
            bindingsResetRequested = true;
            SetBindingStatus("options.controls.defaults");
        }

        private void SensitivityChanged(float value)
        {
            if (draft == null) return;
            draft.MovementSensitivity = value;
            RefreshPresentationLabels();
        }

        private void InvertChanged(bool value)
        {
            if (draft != null) draft.InvertY = value;
        }

        private void ScaleChanged(float value)
        {
            if (draft == null) return;
            draft.InterfaceScale = value;
            ApplyPresentation(draft);
            RefreshPresentationLabels();
        }

        private void HintsChanged(bool value)
        {
            if (draft == null) return;
            draft.ShowHints = value;
            ApplyPresentation(draft);
        }

        private void RefreshPresentation()
        {
            if (draft == null) return;
            view.MovementSensitivity.SetValueWithoutNotify(draft.MovementSensitivity);
            view.InvertY.SetIsOnWithoutNotify(draft.InvertY);
            view.InterfaceScale.SetValueWithoutNotify(draft.InterfaceScale);
            view.ShowHints.SetIsOnWithoutNotify(draft.ShowHints);
            ApplyPresentation(draft);
            RefreshPresentationLabels();
        }

        private void RefreshPresentationLabels()
        {
            GameText.Set(view.SensitivityLabel, "options.gameplay.sensitivity", draft.MovementSensitivity);
            GameText.Set(view.ScaleLabel, "options.interface.scale", Mathf.RoundToInt(draft.InterfaceScale * 100f));
        }

        private void ApplyPresentation(GamePresentationValues values)
        {
            if (view == null || values == null) return;
            if (navigation != null) navigation.ConfigureGameplayOptions(values.MovementSensitivity, values.InvertY);
            if (view.Scaler != null) view.Scaler.referenceResolution = referenceResolution / values.InterfaceScale;
            if (view.Hints != null)
                foreach (var hint in view.Hints)
                    if (hint != null) hint.SetActive(values.ShowHints &&
                        (hint.name != "GameplayHint" || (navigation != null && navigation.IsGameplay)));
        }

        private void RequestRebind(GameRebindRow row)
        {
            if (!IsOpen || host.Input.IsRebinding || row == null) return;
            pendingRow = row;
            pendingFrame = Time.frameCount + 1;
        }

        private void BeginCapture(GameRebindRow row)
        {
            if (!IsOpen || host.Input.IsRebinding || row == null) return;
            int version = ++rebindVersion;
            string beforeCapture = host.Input.ExportBindingOverrides();
            try
            {
                captureRow = row;
                row.SetCapture(true);
                SetBindingStatus(row.ControlsPath.StartsWith("<Gamepad>", StringComparison.Ordinal) ?
                    "options.controls.listening_gamepad" : row.ControlsPath.StartsWith("<Mouse>", StringComparison.Ordinal) ?
                    "options.controls.listening_mouse" : "options.controls.listening_keyboard");
                operation = host.Input.BeginRebind(row.ActionPath, row.BindingId, row.ControlsPath, result =>
                {
                    if (this == null || version != rebindVersion) return;
                    operation = null;
                    ClearCaptureRow();
                    if (!IsOpen) return;
                    bool reserved = !result.IsCanceled &&
                        GamePreferences.IsReservedGameplayControl(host.Input, result.OverridePath);
                    if (reserved) host.Input.ImportBindingOverrides(beforeCapture);
                    RefreshBindings();
                    SetBindingStatus(reserved ? "options.controls.reserved" :
                        result.IsCanceled ? "options.controls.canceled" : "options.controls.changed");
                    if (row != null && row.Button != null && row.Button.IsActive() && EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(row.Button.gameObject);
                });
            }
            catch (Exception exception)
            {
                operation = null;
                ClearCaptureRow();
                SetBindingStatus("options.controls.failed");
                Debug.Log("Binding capture failed: " + exception, this);
            }
        }

        private void CancelCapture()
        {
            pendingRow = null;
            rebindVersion++;
            var previous = operation;
            operation = null;
            ClearCaptureRow();
            previous?.Dispose();
        }

        private void ClearCaptureRow()
        {
            if (captureRow != null) captureRow.SetCapture(false);
            captureRow = null;
        }

        private void RefreshBindings()
        {
            if (host == null || !host.IsReady || view?.BindingRows == null) return;
            foreach (var row in view.BindingRows)
                if (row != null) row.Refresh(host.Input);
        }

        private void LocaleChanged(Locale locale)
        {
            if (draft != null) RefreshPresentationLabels();
            RefreshBindings();
        }

        private void OnScreenClosed(string id)
        {
            if (id != "Options") return;
            DiscardDraft();
            wasOpen = false;
        }

        private void DiscardDraft()
        {
            if (existingOptions != null) existingOptions.CloseDropdowns(restoreFocus: false);
            CancelCapture();
            var previousBindings = bindingSnapshot;
            string appliedLanguage = settingsSnapshot?.LanguageCode;
            bindingSnapshot = null;
            effectiveBindingSnapshot = null;
            settingsSnapshot = null;
            bindingsResetRequested = false;
            draft = null;
            if (host != null && host.IsReady && preferences != null)
            {
                if (appliedLanguage != null && host.Localization.IsInitialized)
                    host.Localization.Preview(appliedLanguage);
                ApplyPresentation(presentationSnapshot ?? preferences.Current);
                if (previousBindings != null) _ = RestoreBindingsAsync(host.Input, previousBindings);
            }
            presentationSnapshot = null;
        }

        private static async Task RestoreBindingsAsync(InputService input, string snapshot)
        {
            var runtime = input.RuntimeAsset;
            while (input.IsInitialized && ReferenceEquals(runtime, input.RuntimeAsset) && input.IsRebinding)
                await Awaitable.NextFrameAsync();
            if (input.IsInitialized && ReferenceEquals(runtime, input.RuntimeAsset))
                input.ImportBindingOverrides(snapshot);
        }

        private void SetBindingStatus(string key)
        {
            if (view?.BindingStatus != null)
            {
                GameText.Set(view.BindingStatus, string.Empty);
                view.BindingStatus.gameObject.SetActive(false);
            }
            if (existingOptions != null) existingOptions.ShowStatus(key);
        }

        private void Wire(bool wire)
        {
            if (view == null) return;
            if (wire)
            {
                tabListeners = new UnityAction[view.Tabs.Length];
                for (int index = 0; index < view.Tabs.Length; index++)
                {
                    int tab = index;
                    tabListeners[index] = () => SelectTab(tab);
                    view.Tabs[index].onClick.AddListener(tabListeners[index]);
                }
                view.MovementSensitivity.onValueChanged.AddListener(SensitivityChanged);
                view.InvertY.onValueChanged.AddListener(InvertChanged);
                view.InterfaceScale.onValueChanged.AddListener(ScaleChanged);
                view.ShowHints.onValueChanged.AddListener(HintsChanged);
                view.ResetBindings.onClick.AddListener(ResetBindings);
                view.DiscardChanges.onClick.AddListener(DiscardAndClose);
                view.SaveChanges.onClick.AddListener(SaveAndClose);
                foreach (var row in view.BindingRows)
                    if (row != null) row.RebindRequested += RequestRebind;
            }
            else
            {
                if (view.Tabs != null && tabListeners != null)
                    for (int index = 0; index < view.Tabs.Length && index < tabListeners.Length; index++)
                        if (view.Tabs[index] != null) view.Tabs[index].onClick.RemoveListener(tabListeners[index]);
                if (view.MovementSensitivity != null) view.MovementSensitivity.onValueChanged.RemoveListener(SensitivityChanged);
                if (view.InvertY != null) view.InvertY.onValueChanged.RemoveListener(InvertChanged);
                if (view.InterfaceScale != null) view.InterfaceScale.onValueChanged.RemoveListener(ScaleChanged);
                if (view.ShowHints != null) view.ShowHints.onValueChanged.RemoveListener(HintsChanged);
                if (view.ResetBindings != null) view.ResetBindings.onClick.RemoveListener(ResetBindings);
                if (view.DiscardChanges != null) view.DiscardChanges.onClick.RemoveListener(DiscardAndClose);
                if (view.SaveChanges != null) view.SaveChanges.onClick.RemoveListener(SaveAndClose);
                if (view.BindingRows != null)
                    foreach (var row in view.BindingRows)
                        if (row != null) row.RebindRequested -= RequestRebind;
                tabListeners = null;
            }
        }

        private void OnDisable() { bindVersion++; Release(); }
        private void OnDestroy() { bindVersion++; Release(); }

        private void Release()
        {
            DiscardDraft();
            Wire(false);
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            if (host != null && host.Coordinator != null)
            {
                host.UI.ScreenClosed -= OnScreenClosed;
                host.Input.BindingsChanged -= RefreshBindings;
            }
            previousTabSubscription?.Dispose(); previousTabSubscription = null;
            nextTabSubscription?.Dispose(); nextTabSubscription = null;
            confirmationRegistration?.Dispose(); confirmationRegistration = null;
            if (existingOptions != null)
            {
                existingOptions.DraftOpened -= BeginDraft;
                existingOptions.DraftApplied -= OnDraftApplied;
                existingOptions.DraftReset -= ResetProjectDraft;
                if (existingOptions.DraftApplyOverride == ApplyCompleteDraft) existingOptions.DraftApplyOverride = null;
                if (existingOptions.BackRequested == RequestBack) existingOptions.BackRequested = null;
            }
            if (navigation != null && navigation.BackInterceptor == RequestBack) navigation.BackInterceptor = null;
            pendingTabDelta = 0;
            ready = false;
            wasOpen = false;
        }
    }
}
