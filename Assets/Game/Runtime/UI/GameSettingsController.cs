using System;
using System.Collections.Generic;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Settings;
using Lumivale.Core.UI;
using TMPro;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>Owns the Starter Options draft and view wiring; Core owns validation and persistence.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase3DemoController")]
    public sealed class GameSettingsController : MonoBehaviour
    {
        [SerializeField] private GameSceneController navigation;
        [SerializeField] private UiScreen optionsScreen;
        [SerializeField] private UnityEngine.UI.Button menuOptions;
        [SerializeField] private UnityEngine.UI.Button pauseOptions;
        [SerializeField] private UnityEngine.UI.Slider master;
        [SerializeField] private UnityEngine.UI.Slider music;
        [SerializeField] private UnityEngine.UI.Slider sfx;
        [SerializeField] private TextMeshProUGUI masterValue;
        [SerializeField] private TextMeshProUGUI musicValue;
        [SerializeField] private TextMeshProUGUI sfxValue;
        [SerializeField] private GameChoiceButton resolution;
        [SerializeField] private GameChoiceButton mode;
        [SerializeField] private GameChoiceButton quality;
        [SerializeField] private GameChoiceButton language;
        [SerializeField] private UnityEngine.UI.Button apply;
        [SerializeField] private UnityEngine.UI.Button reset;
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private Transform spatialPosition;
        [SerializeField] private UnityEngine.UI.Button[] feedbackButtons;

        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private GameBootstrap host;
        private SettingsValues draft;
        private int bindVersion;
        private bool started;
        private bool ready;

        public bool IsReady => ready && host != null && host.IsReady && host.HasSettingsAudio &&
            host.Settings.IsInitialized && host.Audio.IsInitialized && navigation != null && navigation.IsReady;
        public bool IsOpen => IsReady && host.UI.IsOpen("Options");
        public SettingsValues Draft => draft?.Clone();
        public UnityEngine.UI.Button MenuOptionsButton => menuOptions;
        public UnityEngine.UI.Button PauseOptionsButton => pauseOptions;
        public UnityEngine.UI.Slider MasterSlider => master;
        public UnityEngine.UI.Slider MusicSlider => music;
        public UnityEngine.UI.Slider SfxSlider => sfx;
        public GameChoiceButton ResolutionButton => resolution;
        public GameChoiceButton ModeButton => mode;
        public GameChoiceButton QualityButton => quality;
        public GameChoiceButton LanguageButton => language;
        public UnityEngine.UI.Button ApplyButton => apply;
        public UnityEngine.UI.Button ResetButton => reset;
        public UnityEngine.UI.Button BackButton => back;
        public int UiSoundCount { get; private set; }
        public int Sound2DCount { get; private set; }
        public int Sound3DCount { get; private set; }
        public event Action DraftApplied;
        public event Action DraftReset;
        public event Action DraftOpened;
        /// <summary>Optional product draft owner; return validated/applied values or throw on failure.</summary>
        public Func<SettingsValues, SettingsValues> DraftApplyOverride { get; set; }
        public Func<bool> BackRequested { get; set; }
        public string StatusMessage => status != null ? status.text : null;

        public void Configure(GameSceneController sceneNavigation, UiScreen options,
            UnityEngine.UI.Button mainOptions, UnityEngine.UI.Button pausedOptions,
            UnityEngine.UI.Slider masterSlider, UnityEngine.UI.Slider musicSlider,
            UnityEngine.UI.Slider sfxSlider, TextMeshProUGUI masterLabel,
            TextMeshProUGUI musicLabel, TextMeshProUGUI sfxLabel,
            GameChoiceButton resolutionChoice, GameChoiceButton modeChoice,
            GameChoiceButton qualityChoice, GameChoiceButton languageChoice,
            UnityEngine.UI.Button applyButton, UnityEngine.UI.Button resetButton,
            UnityEngine.UI.Button backButton, TextMeshProUGUI message, Transform spatialEmitter,
            UnityEngine.UI.Button[] sceneFeedback)
        {
            navigation = sceneNavigation;
            optionsScreen = options;
            menuOptions = mainOptions;
            pauseOptions = pausedOptions;
            master = masterSlider;
            music = musicSlider;
            sfx = sfxSlider;
            masterValue = masterLabel;
            musicValue = musicLabel;
            sfxValue = sfxLabel;
            resolution = resolutionChoice;
            mode = modeChoice;
            quality = qualityChoice;
            language = languageChoice;
            apply = applyButton;
            reset = resetButton;
            back = backButton;
            status = message;
            spatialPosition = spatialEmitter;
            feedbackButtons = sceneFeedback;
        }

        private void Start()
        {
            started = true;
            InitializeScene();
        }

        private void OnEnable()
        {
            if (started) InitializeScene();
        }

        private async void InitializeScene()
        {
            int version = ++bindVersion;
            try
            {
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null || !host.HasSettingsAudio)
                    throw new InvalidOperationException("The Phase 3 view requires its configured Starter bootstrap.");
                await host.Ready;
                if (this == null || !isActiveAndEnabled || version != bindVersion || host == null || !host.IsReady) return;
                if (optionsScreen == null || navigation == null || master == null || music == null || sfx == null ||
                    resolution == null || mode == null || quality == null || language == null ||
                    apply == null || reset == null || back == null || status == null)
                    throw new InvalidOperationException("Configure all Phase 3 Options view references.");
                subscriptions.Add(host.UI.Register(optionsScreen));
                host.UI.ScreenClosed += OnScreenClosed;
                if (navigation.IsGameplay)
                {
                    subscriptions.Add(host.Input.Subscribe("Gameplay/Sfx2D", _ => PlayGameplay2D()));
                    subscriptions.Add(host.Input.Subscribe("Gameplay/Sfx3D", _ => PlayGameplay3D()));
                }
                Wire(true);
                ready = true;
            }
            catch (OperationCanceledException)
            {
                if (version == bindVersion) ReleaseBindings();
            }
            catch (Exception exception)
            {
                if (version == bindVersion)
                {
                    ReleaseBindings();
                    Debug.LogException(exception, this);
                }
            }
        }

        private void OpenOptions()
        {
            if (!CanInteract || host.UI.IsOpen("Options")) return;
            draft = host.Settings.Current;
            RefreshView();
            status.text = DraftApplyOverride == null ? "Apply saves changes. Back discards unapplied changes." :
                string.Empty;
            host.UI.OpenModal("Options");
            DraftOpened?.Invoke();
            PlayUiSound();
        }

        private bool CanInteract => IsReady && isActiveAndEnabled &&
            !host.Input.IsSuspended && !host.Scenes.IsLoading;

        private void ApplyDraft()
        {
            TryApplyDraft();
        }

        public bool TryApplyDraft()
        {
            if (!CanInteract || !IsOpen || draft == null) return false;
            try
            {
                if (DraftApplyOverride != null) draft = DraftApplyOverride(draft.Clone());
                else
                {
                    draft = host.Settings.Update(draft);
                    host.Settings.ApplyGraphics();
                    host.Settings.Save();
                    draft = host.Settings.Current;
                }
                RefreshView();
                status.text = "Applied and saved.";
                PlayUiSound();
                DraftApplied?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                status.text = $"Changes could not be fully applied/saved: {exception.Message}";
                return false;
            }
        }

        private void ResetDraft()
        {
            if (!CanInteract || !IsOpen) return;
            draft = host.Settings.Defaults;
            RefreshView();
            status.text = DraftApplyOverride == null ? "Defaults selected. Apply to save, or Back to discard." :
                "Defaults selected.";
            PlayUiSound();
            DraftReset?.Invoke();
        }

        private void Back()
        {
            if (!CanInteract || !IsOpen) return;
            if (BackRequested?.Invoke() == true) return;
            PlayUiSound();
            host.UI.Close("Options");
        }

        private void OnScreenClosed(string id)
        {
            if (id == "Options") draft = null;
        }

        private void MasterChanged(float value) { if (draft != null) { draft.MasterVolume = value; RefreshVolumeLabels(); } }
        private void MusicChanged(float value) { if (draft != null) { draft.MusicVolume = value; RefreshVolumeLabels(); } }
        private void SfxChanged(float value) { if (draft != null) { draft.SfxVolume = value; RefreshVolumeLabels(); } }

        private void ResolutionChanged(int delta)
        {
            if (draft == null) return;
            var choices = host.Settings.Capabilities.Resolutions;
            int current = 0;
            for (int index = 0; index < choices.Count; index++)
                if (choices[index].Width == draft.Width && choices[index].Height == draft.Height) current = index;
            var chosen = choices[Wrap(current + delta, choices.Count)];
            draft.Width = chosen.Width;
            draft.Height = chosen.Height;
            RefreshChoiceLabels();
        }

        private void ModeChanged(int delta)
        {
            if (draft == null) return;
            var choices = host.Settings.Capabilities.Modes;
            int current = IndexOf(choices, draft.DisplayMode);
            draft.DisplayMode = choices[Wrap(current + delta, choices.Count)];
            RefreshChoiceLabels();
        }

        private void QualityChanged(int delta)
        {
            if (draft == null) return;
            var choices = host.Settings.Capabilities.QualityNames;
            draft.QualityName = choices[Wrap(IndexOf(choices, draft.QualityName) + delta, choices.Count)];
            RefreshChoiceLabels();
        }

        private void LanguageChanged(int delta)
        {
            if (draft == null) return;
            var choices = host.Settings.SupportedLanguages;
            draft.LanguageCode = choices[Wrap(IndexOf(choices, draft.LanguageCode) + delta, choices.Count)];
            RefreshChoiceLabels();
        }

        private static int IndexOf<T>(IReadOnlyList<T> choices, T value)
        {
            for (int index = 0; index < choices.Count; index++)
                if (EqualityComparer<T>.Default.Equals(choices[index], value)) return index;
            return 0;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;

        private void RefreshView()
        {
            master.SetValueWithoutNotify(draft.MasterVolume);
            music.SetValueWithoutNotify(draft.MusicVolume);
            sfx.SetValueWithoutNotify(draft.SfxVolume);
            RefreshVolumeLabels();
            RefreshChoiceLabels();
        }

        private void RefreshVolumeLabels()
        {
            masterValue.text = $"Master Volume: {Mathf.RoundToInt(draft.MasterVolume * 100f)}%";
            musicValue.text = $"Music Volume: {Mathf.RoundToInt(draft.MusicVolume * 100f)}%";
            sfxValue.text = $"SFX Volume: {Mathf.RoundToInt(draft.SfxVolume * 100f)}%";
        }

        private void RefreshChoiceLabels()
        {
            Label(resolution, $"Resolution: {draft.Width} x {draft.Height}");
            Label(mode, $"Display: {draft.DisplayMode}");
            Label(quality, $"Quality: {draft.QualityName}");
            Label(language, $"Language preference: {draft.LanguageCode}");
        }

        private static void Label(GameChoiceButton button, string value)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().text = value;
        }

        private void PlayUiSound()
        {
            if (!CanInteract) return;
            if (host.Audio.PlaySfx2D(host.UiSound, 0.5f)) UiSoundCount++;
        }

        private void PlayGameplay2D()
        {
            if (!CanInteract || host.Pause.IsPaused) return;
            if (host.Audio.PlaySfx2D(host.Gameplay2DSound, 0.7f)) Sound2DCount++;
        }

        private void PlayGameplay3D()
        {
            if (!CanInteract || host.Pause.IsPaused) return;
            Vector3 position = spatialPosition != null ? spatialPosition.position : Vector3.zero;
            if (host.Audio.PlaySfx3D(host.Gameplay3DSound, position, 0.8f)) Sound3DCount++;
        }

        private void Wire(bool wire)
        {
            Button(menuOptions, OpenOptions, wire);
            Button(pauseOptions, OpenOptions, wire);
            Button(apply, ApplyDraft, wire);
            Button(reset, ResetDraft, wire);
            Button(back, Back, wire);
            if (wire)
            {
                master.onValueChanged.AddListener(MasterChanged);
                music.onValueChanged.AddListener(MusicChanged);
                sfx.onValueChanged.AddListener(SfxChanged);
                resolution.ChoiceRequested += ResolutionChanged;
                mode.ChoiceRequested += ModeChanged;
                quality.ChoiceRequested += QualityChanged;
                language.ChoiceRequested += LanguageChanged;
            }
            else
            {
                if (master != null) master.onValueChanged.RemoveListener(MasterChanged);
                if (music != null) music.onValueChanged.RemoveListener(MusicChanged);
                if (sfx != null) sfx.onValueChanged.RemoveListener(SfxChanged);
                if (resolution != null) resolution.ChoiceRequested -= ResolutionChanged;
                if (mode != null) mode.ChoiceRequested -= ModeChanged;
                if (quality != null) quality.ChoiceRequested -= QualityChanged;
                if (language != null) language.ChoiceRequested -= LanguageChanged;
            }
            if (feedbackButtons != null)
                foreach (var button in feedbackButtons) Button(button, PlayUiSound, wire);
        }

        private static void Button(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction listener, bool wire)
        {
            if (button == null) return;
            if (wire) button.onClick.AddListener(listener);
            else button.onClick.RemoveListener(listener);
        }

        private void OnDisable() { bindVersion++; ReleaseBindings(); }
        private void OnDestroy() { bindVersion++; ReleaseBindings(); }

        private void ReleaseBindings()
        {
            ready = false;
            draft = null;
            Wire(false);
            if (host != null && host.Coordinator != null) host.UI.ScreenClosed -= OnScreenClosed;
            for (int index = subscriptions.Count - 1; index >= 0; index--) subscriptions[index].Dispose();
            subscriptions.Clear();
        }
    }
}
