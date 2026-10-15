using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using Lumivale.Core.SceneFlow;
using Lumivale.Core.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumivale.Start.Game
{
    /// <summary>Project-specific view wiring; the Core owns no menu style or gameplay actions.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase2DemoController")]
    public sealed class GameSceneController : MonoBehaviour
    {
        [SerializeField] private bool gameplay;
        [SerializeField] private UiScreen[] screens;
        [SerializeField] private InputSystemUiBridge bridge;
        [SerializeField] private UnityEngine.UI.Button playButton;
        [SerializeField] private UnityEngine.UI.Button controlsButton;
        [SerializeField] private UnityEngine.UI.Button resumeButton;
        [SerializeField] private UnityEngine.UI.Button returnButton;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private UnityEngine.UI.Button cancelButton;
        [SerializeField] private Transform player;
        [SerializeField] private string menuScenePath = GameScenePaths.MenuPath;
        [SerializeField] private string gameplayScenePath = GameScenePaths.GameplayPath;
        [SerializeField] private string initialScreenId = "MainMenu";
        private float movementMultiplier = 1f;
        private bool invertMovementY;

        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private GameBootstrap host;
        private int contextChangeFrame = -1;
        private int bindVersion;
        private bool started;
        private bool pendingPause;
        private bool pendingGamepadPause;
        private bool pendingCancel;
        private bool ready;

        public bool IsReady => ready && host != null && host.IsReady &&
            host.Input.IsInitialized && host.UI.IsInitialized;
        private bool CanInteract => IsReady && isActiveAndEnabled &&
            !host.Input.IsSuspended && !host.Scenes.IsLoading;
        public bool IsGameplay => gameplay;
        public Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;
        public Task LastNavigation { get; private set; } = Task.CompletedTask;
        public UnityEngine.UI.Button PlayButton => playButton;
        public UnityEngine.UI.Button ControlsButton => controlsButton;
        public UnityEngine.UI.Button ResumeButton => resumeButton;
        public UnityEngine.UI.Button ReturnButton => returnButton;
        public UnityEngine.UI.Button ConfirmButton => confirmButton;
        public UnityEngine.UI.Button CancelButton => cancelButton;
        /// <summary>Optional project presenter interception; true means the Back request was handled.</summary>
        public Func<bool> BackInterceptor { get; set; }

        /// <summary>Sets project routes; an empty initial screen lets a product presenter own startup.</summary>
        public void ConfigureRouting(string menuPath, string gameplayPath, string startupScreenId = "MainMenu")
        {
            if (string.IsNullOrWhiteSpace(menuPath) || string.IsNullOrWhiteSpace(gameplayPath))
                throw new ArgumentException("Both navigation scene paths are required.");
            menuScenePath = menuPath;
            gameplayScenePath = gameplayPath;
            initialScreenId = startupScreenId ?? "";
        }

        /// <summary>Project-owned gameplay preferences; default values preserve the original demonstration.</summary>
        public void ConfigureGameplayOptions(float multiplier, bool invertY)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0.25f || multiplier > 3f)
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Movement multiplier must be between 0.25 and 3.");
            movementMultiplier = multiplier;
            invertMovementY = invertY;
        }

        public void Configure(bool isGameplay, UiScreen[] views, InputSystemUiBridge inputBridge,
            UnityEngine.UI.Button play, UnityEngine.UI.Button controls, UnityEngine.UI.Button resume,
            UnityEngine.UI.Button returnToMenu, UnityEngine.UI.Button confirm,
            UnityEngine.UI.Button cancel, Transform movablePlayer)
        {
            gameplay = isGameplay;
            screens = views;
            bridge = inputBridge;
            playButton = play;
            controlsButton = controls;
            resumeButton = resume;
            returnButton = returnToMenu;
            confirmButton = confirm;
            cancelButton = cancel;
            player = movablePlayer;
        }

        private void Start()
        {
            started = true;
            InitializeScene();
        }

        private void OnEnable()
        {
            if (started)
            {
                InitializeScene();
            }
        }

        private async void InitializeScene()
        {
            var version = ++bindVersion;
            try
            {
                if (screens == null || screens.Length == 0 || bridge == null)
                {
                    throw new InvalidOperationException("Configure the demo views and Input System UI bridge.");
                }
                foreach (var screen in screens)
                {
                    if (screen == null)
                    {
                        throw new InvalidOperationException("The demo has a missing screen reference.");
                    }
                }
                host = LumiBootstrap.Instance as GameBootstrap;
                if (host == null)
                {
                    throw new InvalidOperationException("The Phase 2 scene needs its GameBootstrap.");
                }
                await host.Ready;
                if (this == null || !isActiveAndEnabled || version != bindVersion ||
                    host == null || !host.IsReady || !host.Input.IsInitialized || !host.UI.IsInitialized)
                {
                    return;
                }

                foreach (var screen in screens)
                {
                    subscriptions.Add(host.UI.Register(screen));
                }
                bridge.Bind(host.Input);
                subscriptions.Add(host.Input.Subscribe("Common/Pause", OnPauseAction));
                subscriptions.Add(host.Input.Subscribe("UI/Cancel", OnCancelAction));
                WireButtons(true);
                host.Input.SetContext(gameplay ? InputContext.Gameplay : InputContext.UI);
                if (!gameplay && !string.IsNullOrEmpty(initialScreenId))
                {
                    host.UI.Open(initialScreenId, rememberHistory: false);
                }
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

        private void Update()
        {
            if (CanInteract)
            {
                ProcessPendingInput();
            }
            if (!CanInteract || !gameplay || player == null || host.Pause.IsPaused)
            {
                return;
            }
            var movement = host.Input.GetAction("Gameplay/Move").ReadValue<Vector2>();
            player.position += new Vector3(movement.x, 0f, invertMovementY ? -movement.y : movement.y) *
                (2.5f * movementMultiplier * Time.deltaTime);
        }

        private void OnPauseAction(InputAction.CallbackContext context)
        {
            if (!CanInteract || !gameplay || contextChangeFrame == Time.frameCount)
            {
                return;
            }
            pendingPause = true;
            pendingGamepadPause = context.control.device is Gamepad;
        }

        private void OnCancelAction(InputAction.CallbackContext context)
        {
            if (!CanInteract || contextChangeFrame == Time.frameCount)
            {
                return;
            }
            pendingCancel = true;
        }

        private void ProcessPendingInput()
        {
            bool pauseRequested = pendingPause;
            bool gamepadPause = pendingGamepadPause;
            bool cancelRequested = pendingCancel;
            pendingPause = false;
            pendingGamepadPause = false;
            pendingCancel = false;

            // Finish native action callbacks before disabling their maps. Input System 1.19's
            // UI module still reads CallbackContext.control from later callbacks in that chain.
            if (pauseRequested && gameplay)
            {
                if (!host.Pause.IsPaused)
                {
                    contextChangeFrame = Time.frameCount;
                    host.Pause.Pause();
                }
                else if (gamepadPause)
                {
                    CloseOrResume();
                }
            }

            if (!cancelRequested || contextChangeFrame == Time.frameCount)
            {
                return;
            }
            if (BackInterceptor?.Invoke() == true) return;
            if (gameplay && host.Pause.IsPaused)
            {
                CloseOrResume();
            }
            else if (host.UI.ModalCount > 0)
            {
                host.UI.Back();
            }
        }

        private void CloseOrResume()
        {
            if (BackInterceptor?.Invoke() == true) return;
            if (host.UI.ModalCount > 1)
            {
                host.UI.CloseModal();
            }
            else
            {
                Resume();
            }
        }

        private void Play() => BeginNavigation(gameplayScenePath);
        private void ShowControls()
        {
            if (CanInteract) host.UI.OpenModal("Controls");
        }
        private void ShowConfirmation()
        {
            if (CanInteract) host.UI.OpenModal("ConfirmReturn");
        }
        private void CancelConfirmation()
        {
            if (CanInteract) host.UI.Back();
        }
        private void ConfirmReturn() => BeginNavigation(menuScenePath);

        private void Resume()
        {
            if (!CanInteract)
            {
                return;
            }
            contextChangeFrame = Time.frameCount;
            host.Pause.Resume();
        }

        private void BeginNavigation(string destination)
        {
            if (!CanInteract)
            {
                return;
            }
            LastNavigation = host.Scenes.LoadAsync(new SceneLoadRequest(destination));
            ObserveNavigationAsync(LastNavigation);
        }

        private async void ObserveNavigationAsync(Task navigation)
        {
            try
            {
                await navigation;
            }
            catch (OperationCanceledException)
            {
                // Explicit shutdown or transition cancellation is an expected lifecycle event.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void WireButtons(bool wire)
        {
            Wire(playButton, Play, wire);
            Wire(controlsButton, ShowControls, wire);
            Wire(resumeButton, Resume, wire);
            Wire(returnButton, ShowConfirmation, wire);
            Wire(confirmButton, ConfirmReturn, wire);
            Wire(cancelButton, CancelConfirmation, wire);
        }

        private static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction listener, bool wire)
        {
            if (button == null)
            {
                return;
            }
            if (wire)
            {
                button.onClick.AddListener(listener);
            }
            else
            {
                button.onClick.RemoveListener(listener);
            }
        }

        private void OnDisable()
        {
            bindVersion++;
            ReleaseBindings();
        }

        private void OnDestroy()
        {
            bindVersion++;
            ReleaseBindings();
        }

        private void ReleaseBindings()
        {
            pendingPause = false;
            pendingGamepadPause = false;
            pendingCancel = false;
            ready = false;
            WireButtons(false);
            if (bridge != null)
            {
                bridge.Unbind();
            }
            for (var index = subscriptions.Count - 1; index >= 0; index--)
            {
                subscriptions[index].Dispose();
            }
            subscriptions.Clear();
        }
    }
}
