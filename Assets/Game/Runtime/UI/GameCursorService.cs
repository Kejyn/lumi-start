using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumivale.Start.Game
{
    /// <summary>Session-owned cursor and UI pointer presentation; device detection remains owned by Input.</summary>
    public sealed class GameCursorService : ILumiService
    {
        private static readonly IReadOnlyCollection<string> RequiredServices = Array.AsReadOnly(new[] { "input" });
        private InputService input;
        private readonly List<GraphicRaycaster> suppressedRaycasters = new List<GraphicRaycaster>();
        private bool originalVisibility;
        private bool ownsVisibility;

        public string Id => "starter-cursor";
        public IReadOnlyCollection<string> Dependencies => RequiredServices;
        public bool IsInitialized { get; private set; }

        public Task InitializeAsync(ServiceContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsInitialized) return Task.CompletedTask;
            if (context == null) throw new ArgumentNullException(nameof(context));
            var service = context.Get<InputService>();
            if (!service.IsInitialized) throw new InvalidOperationException("Initialize Input before cursor presentation.");
            input = service;
            originalVisibility = Cursor.visible;
            ownsVisibility = true;
            IsInitialized = true;
            input.ActiveDeviceChanged += DeviceChanged;
            InputSystem.onAfterUpdate += RefreshPointerPolicy;
            DeviceChanged(input.ActiveDeviceFamily);
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken cancellationToken)
        {
            // Releasing the session's presentation is mandatory, even when cleanup is cancelled.
            if (input != null) input.ActiveDeviceChanged -= DeviceChanged;
            InputSystem.onAfterUpdate -= RefreshPointerPolicy;
            input = null;
            IsInitialized = false;
            RestoreRaycasters();
            if (ownsVisibility)
            {
                ownsVisibility = false;
                Cursor.visible = originalVisibility;
            }
            return Task.CompletedTask;
        }

        private void DeviceChanged(InputDeviceFamily family)
        {
            if (!IsInitialized) return;
            Cursor.visible = family == InputDeviceFamily.KeyboardMouse;
            if (family == InputDeviceFamily.Gamepad) SuppressRaycasters();
            else RestoreRaycasters();
        }

        private void RefreshPointerPolicy()
        {
            if (IsInitialized && input.ActiveDeviceFamily == InputDeviceFamily.Gamepad)
                SuppressRaycasters();
        }

        private void SuppressRaycasters()
        {
            for (int index = suppressedRaycasters.Count - 1; index >= 0; index--)
                if (suppressedRaycasters[index] == null) suppressedRaycasters.RemoveAt(index);

            // The native registry includes newly activated scenes and TMP popup canvases.
            // Iterate backwards because disabling a raycaster unregisters it immediately.
            // Pointer actions remain active, so meaningful mouse input can reclaim the UI
            // before EventSystem.Process and its very first click is still delivered.
            var raycasters = RaycasterManager.GetRaycasters();
            for (int index = raycasters.Count - 1; index >= 0; index--)
            {
                if (!(raycasters[index] is GraphicRaycaster raycaster) || !raycaster.enabled) continue;
                if (!suppressedRaycasters.Contains(raycaster)) suppressedRaycasters.Add(raycaster);
                raycaster.enabled = false;
            }
        }

        private void RestoreRaycasters()
        {
            // Only raycasters that this session found enabled and suppressed are restored.
            // Pre-existing disabled components are never registered and remain untouched.
            for (int index = suppressedRaycasters.Count - 1; index >= 0; index--)
                if (suppressedRaycasters[index] != null) suppressedRaycasters[index].enabled = true;
            suppressedRaycasters.Clear();
        }
    }
}
