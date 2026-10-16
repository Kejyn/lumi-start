using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.UI;
using Lumivale.Core.Settings;
using Lumivale.Core.Audio;
using Lumivale.Core.Persistence;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using AudioConfiguration = Lumivale.Core.Audio.AudioConfiguration;

namespace Lumivale.Start.Game
{
    /// <summary>Starter-only composition for the interactive Input and uGUI demonstration.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase2DemoBootstrap")]
    public sealed class GameBootstrap : LumiBootstrap
    {

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private bool freezeTimeOnPause = true;
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioClip demoMusic;
        [SerializeField] private AudioClip uiSound;
        [SerializeField] private AudioClip gameplay2DSound;
        [SerializeField] private AudioClip gameplay3DSound;
        [SerializeField] private string preferenceKey = DefaultPreferenceKey;
        [SerializeField] private bool enablePersistence;
        [SerializeField] private string saveGameId = DefaultSaveGameId;
        [SerializeField] private string saveRootOverride;

        public const string DefaultSaveGameId = "lumivale-lumi-start-demo";
        public bool HasPersistence => enablePersistence;
        public PersistenceService Persistence => Coordinator.Context.Get<PersistenceService>();

        public void ConfigurePersistence(string gameId = DefaultSaveGameId, string rootPath = null)
        {
            if (Coordinator != null)
                throw new InvalidOperationException("Configure Persistence before the bootstrap wakes.");
            enablePersistence = true;
            saveGameId = gameId;
            saveRootOverride = rootPath;
        }

        public const string DefaultPreferenceKey = "com.lumivale.lumi-start.preferences";

        public InputActionAsset SourceAsset => inputActions;
        public string PreferenceKey => preferenceKey;
        public InputService Input => Coordinator.Context.Get<InputService>();
        public GameCursorService Cursor => Coordinator.Context.Get<GameCursorService>();
        public UiService UI => Coordinator.Context.Get<UiService>();
        public PauseService Pause => Coordinator.Context.Get<PauseService>();
        public SceneFlowService Scenes => Coordinator.Context.Get<SceneFlowService>();
        public bool HasSettingsAudio => audioMixer != null;
        public SettingsService Settings => Coordinator.Context.Get<SettingsService>();
        public GameLocalizationService Localization => Coordinator.Context.Get<GameLocalizationService>();
        public AudioService Audio => Coordinator.Context.Get<AudioService>();
        public AudioMixer Mixer => audioMixer;
        public AudioClip Music => demoMusic;
        public AudioClip UiSound => uiSound;
        public AudioClip Gameplay2DSound => gameplay2DSound;
        public AudioClip Gameplay3DSound => gameplay3DSound;
        public int CompletedLoads { get; private set; }

        public void Configure(InputActionAsset actions)
        {
            if (Coordinator != null)
            {
                throw new InvalidOperationException("Configure the demo before its bootstrap wakes.");
            }
            inputActions = actions;
        }

        public void ConfigureSettingsAudio(AudioMixer mixer, AudioMixerGroup master,
            AudioMixerGroup music, AudioMixerGroup sfx, AudioClip musicClip,
            AudioClip uiClip, AudioClip sound2D, AudioClip sound3D,
            string storageKey = DefaultPreferenceKey)
        {
            if (Coordinator != null)
                throw new InvalidOperationException("Configure Phase 3 before the bootstrap wakes.");
            audioMixer = mixer;
            masterGroup = master;
            musicGroup = music;
            sfxGroup = sfx;
            demoMusic = musicClip;
            uiSound = uiClip;
            gameplay2DSound = sound2D;
            gameplay3DSound = sound3D;
            preferenceKey = storageKey;
        }

        protected override IEnumerable<ILumiService> CreateServices()
        {
            var contexts = new Dictionary<InputContext, string[]>
            {
                [InputContext.Gameplay] = new[] { "Gameplay", "Common" },
                [InputContext.UI] = new[] { "UI", "Common" },
                [InputContext.Pause] = new[] { "UI", "Common" }
            };
            var input = new InputService(inputActions, contexts, InputContext.UI);
            var cursor = new GameCursorService();
            var ui = new UiService();
            var pause = new PauseService("Pause", controlTimeScale: freezeTimeOnPause);
            var flow = new SceneFlowService(defaultTransition: new GameSceneTransition(input, ui, pause));
            flow.LoadCompleted += _ => CompletedLoads++;
            var services = new List<ILumiService>();
            if (enablePersistence)
                services.Add(new PersistenceService(new PersistenceConfiguration(saveGameId,
                    string.IsNullOrEmpty(saveRootOverride) ? null : saveRootOverride)));
            if (!HasSettingsAudio)
            {
                services.AddRange(new ILumiService[] { input, cursor, flow, ui, pause });
                return services;
            }

            var graphics = new GameGraphicsBackend();
            var capabilities = graphics.GetCapabilities();
            // Player first-run defaults ignore Unity's remembered window size. Stored settings still take priority.
            var nativeResolution = Screen.currentResolution;
            var defaults = GameGraphicsPresets.Apply(new SettingsValues
            {
                Width = Application.isEditor ? capabilities.CurrentResolution.Width : nativeResolution.width,
                Height = Application.isEditor ? capabilities.CurrentResolution.Height : nativeResolution.height,
                DisplayMode = Application.isEditor ? capabilities.CurrentMode : FullScreenMode.FullScreenWindow,
                QualityName = capabilities.CurrentQualityName,
                MasterVolume = 1f, MusicVolume = 0.5f, SfxVolume = 0.7f,
                LanguageCode = GameLocalizationService.DetectLanguage(Application.systemLanguage)
            }, GameGraphicsPreset.Medium, graphics.RenderingCapabilities);
            var settings = new SettingsService(new SettingsConfiguration(defaults, preferenceKey,
                new[] { "en", "fr" }), graphics: graphics);
            var audio = new AudioService(new AudioConfiguration(audioMixer, masterGroup,
                musicGroup, sfxGroup));
            var binding = new GameSettingsAudioBinding(demoMusic,
                new[] { demoMusic, uiSound, gameplay2DSound, gameplay3DSound });
            services.AddRange(new ILumiService[] { settings, new GameLocalizationService(), audio, binding, input, cursor, flow, ui, pause });
            return services;
        }

        private sealed class GameSceneTransition : ISceneTransition
        {
            private readonly InputService input;
            private readonly UiService ui;
            private readonly PauseService pause;
            private IDisposable suspension;

            public GameSceneTransition(InputService input, UiService ui, PauseService pause)
            {
                this.input = input;
                this.ui = ui;
                this.pause = pause;
            }

            public Task BeforeLoadAsync(SceneLoadRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                suspension = input.Suspend();
                try
                {
                    pause.Resume();
                    ui.CloseAll();
                    return Task.CompletedTask;
                }
                catch
                {
                    ReleaseSuspension();
                    throw;
                }
            }

            public async Task AfterLoadAsync(SceneLoadResult result, CancellationToken cancellationToken)
            {
                // Let the newly activated scene register its views and select its input context
                // before accepting input. This frame wait also works while timeScale is zero.
                await Awaitable.NextFrameAsync(cancellationToken);
                ReleaseSuspension();
            }

            public Task RecoverAsync(SceneLoadRequest request, Exception exception,
                CancellationToken cancellationToken)
            {
                // A cancelled Unity load can still activate the destination. Its controller
                // owns the resulting screen/context; recovery only releases the input lease.
                ReleaseSuspension();
                return Task.CompletedTask;
            }

            private void ReleaseSuspension()
            {
                var lease = suspension;
                suspension = null;
                lease?.Dispose();
            }
        }
    }
}
