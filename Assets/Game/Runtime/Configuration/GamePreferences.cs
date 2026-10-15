using System;
using Lumivale.Core.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumivale.Start.Game
{
    /// <summary>Project presentation preferences, separate from Core settings and gameplay saves.</summary>
    [Serializable]
    public sealed class GamePresentationValues
    {
        public float MovementSensitivity = 1f;
        public bool InvertY;
        public float InterfaceScale = 1f;
        public bool ShowHints = true;

        public GamePresentationValues Clone() => new GamePresentationValues
        {
            MovementSensitivity = MovementSensitivity,
            InvertY = InvertY,
            InterfaceScale = InterfaceScale,
            ShowHints = ShowHints
        };
    }

    /// <summary>
    /// Owns two bounded, versioned Starter keys. Reading invalid/future data never replaces it;
    /// only an explicit Apply saves presentation values or changed binding overrides.
    /// Use on the Unity main thread; the Bootstrap session owns this instance.
    /// </summary>
    public sealed class GamePreferences
    {
        public const string PresentationSuffix = ".presentation.v1";
        public const string BindingsSuffix = ".bindings.v1";
        private const int MaximumPresentationCharacters = 4096;
        private const int MaximumBindingCharacters = 131072;
        private GamePresentationValues current = new GamePresentationValues();
        private InputActionAsset importedRuntime;

        [Serializable]
        private sealed class PresentationDocument
        {
            public int Version;
            public GamePresentationValues Values;
        }

        [Serializable]
        private sealed class BindingDocument
        {
            public int Version;
            public string Overrides;
        }

        [Serializable]
        private sealed class BindingExport
        {
            public BindingEntry[] bindings = null;
        }

        [Serializable]
        private sealed class BindingEntry
        {
            public string action = null;
            public string path = null;
        }

        public GamePreferences(string settingsKey)
        {
            if (string.IsNullOrWhiteSpace(settingsKey))
                throw new ArgumentException("A Starter settings key is required.", nameof(settingsKey));
            PresentationKey = settingsKey + PresentationSuffix;
            BindingsKey = settingsKey + BindingsSuffix;
            ReadPresentation();
        }

        public string PresentationKey { get; }
        public string BindingsKey { get; }
        public GamePresentationValues Current => current.Clone();
        public string PresentationDiagnostic { get; private set; }
        public string BindingDiagnostic { get; private set; }

        public static bool IsValid(GamePresentationValues value) => value != null &&
            IsFinite(value.MovementSensitivity) && value.MovementSensitivity >= 0.25f &&
            value.MovementSensitivity <= 3f && IsFinite(value.InterfaceScale) &&
            value.InterfaceScale >= 0.8f && value.InterfaceScale <= 1.2f;

        public void SavePresentation(GamePresentationValues value)
        {
            if (!IsValid(value))
                throw new ArgumentException("Presentation values are outside the supported ranges.", nameof(value));
            var copy = value.Clone();
            var json = JsonUtility.ToJson(new PresentationDocument { Version = 1, Values = copy });
            PlayerPrefs.SetString(PresentationKey, json);
            PlayerPrefs.Save();
            current = copy;
            PresentationDiagnostic = null;
        }

        public void LoadBindingsOnce(InputService input)
        {
            if (input == null || !input.IsInitialized || input.RuntimeAsset == null)
                throw new InvalidOperationException("Initialize Input before reading binding preferences.");
            if (ReferenceEquals(importedRuntime, input.RuntimeAsset)) return;
            importedRuntime = input.RuntimeAsset;
            BindingDiagnostic = null;
            if (!PlayerPrefs.HasKey(BindingsKey)) return;
            try
            {
                string json = PlayerPrefs.GetString(BindingsKey);
                if (json.Length == 0 || json.Length > MaximumBindingCharacters)
                    throw new FormatException("The binding preference document has an invalid size.");
                var document = JsonUtility.FromJson<BindingDocument>(json);
                if (document == null || document.Version != 1 || document.Overrides == null)
                    throw new FormatException("The binding preference document is invalid or uses an unsupported version.");
                ValidateGameplayBindings(input, document.Overrides);
                input.ImportBindingOverrides(document.Overrides);
            }
            catch (Exception exception)
            {
                BindingDiagnostic = "Stored bindings were preserved; current bindings are unchanged. " + exception.Message;
            }
        }

        public void SaveBindings(InputService input)
        {
            string json = BindingJson(input);
            PlayerPrefs.SetString(BindingsKey, json);
            PlayerPrefs.Save();
            importedRuntime = input.RuntimeAsset;
            BindingDiagnostic = null;
        }

        /// <summary>Validates the complete project draft before writing; no multi-key disk transaction is promised.</summary>
        internal void SaveDraft(GamePresentationValues value, InputService input, bool saveBindings)
        {
            if (!IsValid(value))
                throw new ArgumentException("Presentation values are outside the supported ranges.", nameof(value));
            var copy = value.Clone();
            string presentationJson = JsonUtility.ToJson(new PresentationDocument { Version = 1, Values = copy });
            string bindingsJson = saveBindings ? BindingJson(input) : null;
            bool hadPresentation = PlayerPrefs.HasKey(PresentationKey);
            string previousPresentation = PlayerPrefs.GetString(PresentationKey);
            bool hadBindings = PlayerPrefs.HasKey(BindingsKey);
            string previousBindings = PlayerPrefs.GetString(BindingsKey);
            try
            {
                PlayerPrefs.SetString(PresentationKey, presentationJson);
                if (saveBindings) PlayerPrefs.SetString(BindingsKey, bindingsJson);
                PlayerPrefs.Save();
            }
            catch (Exception failure)
            {
                try
                {
                    RestorePreference(PresentationKey, hadPresentation, previousPresentation);
                    if (saveBindings) RestorePreference(BindingsKey, hadBindings, previousBindings);
                    PlayerPrefs.Save();
                }
                catch (Exception rollback)
                {
                    throw new AggregateException("Saving failed and restoring the previous project preferences also failed.", failure, rollback);
                }
                throw;
            }
            current = copy;
            PresentationDiagnostic = null;
            if (saveBindings)
            {
                importedRuntime = input.RuntimeAsset;
                BindingDiagnostic = null;
            }
        }

        private static void RestorePreference(string key, bool existed, string value)
        {
            if (existed) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static string BindingJson(InputService input)
        {
            if (input == null || !input.IsInitialized)
                throw new InvalidOperationException("Initialize Input before saving binding preferences.");
            string overrides = input.ExportBindingOverrides();
            ValidateGameplayBindings(input, overrides);
            string json = JsonUtility.ToJson(new BindingDocument
            {
                Version = 1,
                Overrides = overrides
            });
            if (json.Length > MaximumBindingCharacters)
                throw new InvalidOperationException("Binding preferences exceed the supported document size.");
            return json;
        }

        private void ReadPresentation()
        {
            if (!PlayerPrefs.HasKey(PresentationKey)) return;
            try
            {
                string json = PlayerPrefs.GetString(PresentationKey);
                if (json.Length == 0 || json.Length > MaximumPresentationCharacters)
                    throw new FormatException("The presentation preference document has an invalid size.");
                var document = JsonUtility.FromJson<PresentationDocument>(json);
                if (document == null || document.Version != 1 || !IsValid(document.Values))
                    throw new FormatException("Presentation preferences are invalid or use an unsupported version.");
                current = document.Values.Clone();
            }
            catch (Exception exception)
            {
                PresentationDiagnostic = "Stored presentation preferences were preserved; defaults remain active. " + exception.Message;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// Pause and the gameplay Save/Load shortcuts stay available independently of rebound
        /// gameplay actions. Common/NewGame is a menu-only shortcut and intentionally shares
        /// Xbox X with the Starter's existing gameplay sound action.
        /// </summary>
        public static bool IsReservedGameplayControl(InputService input, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "null") return false;
            // Interactive capture emits concrete controls. Broad persisted patterns could
            // include reserved shortcuts even when that device is not connected at startup.
            if (path.IndexOfAny(new[] { '*', '?', '{', '}' }) >= 0) return true;
            string normalized = NormalizeControlPath(path);
            if (normalized == "<keyboard>/anykey") return true;
            foreach (string actionPath in new[] { "Common/Pause", "Common/OpenSaves" })
            {
                var action = input?.RuntimeAsset?.FindAction(actionPath);
                if (action == null) continue;
                foreach (var binding in action.bindings)
                    if (string.Equals(normalized, NormalizeControlPath(binding.effectivePath), StringComparison.Ordinal))
                        return true;
            }
            return false;
        }

        private static string NormalizeControlPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int end = path.IndexOf('>');
            if (!path.StartsWith("<", StringComparison.Ordinal) || end <= 1) return path.ToLowerInvariant();
            string layout = path.Substring(1, end - 1);
            foreach (string family in new[] { "Keyboard", "Mouse", "Gamepad" })
                if (string.Equals(layout, family, StringComparison.OrdinalIgnoreCase) ||
                    InputSystem.IsFirstLayoutBasedOnSecond(layout, family))
                { layout = family; break; }
            string control = path.Substring(end + 1).ToLowerInvariant();
            if (layout == "Gamepad" && control == "/startbutton") control = "/start";
            return "<" + layout.ToLowerInvariant() + ">" + control;
        }

        private static void ValidateGameplayBindings(InputService input, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var document = JsonUtility.FromJson<BindingExport>(json);
            if (document?.bindings == null)
                throw new FormatException("The binding override list is missing.");
            foreach (var entry in document.bindings)
            {
                if (entry == null || entry.action == null ||
                    !entry.action.StartsWith("Gameplay/", StringComparison.Ordinal))
                    throw new FormatException("Starter binding preferences may only target Gameplay actions; menu controls are reserved.");
                if (IsReservedGameplayControl(input, entry.path))
                    throw new FormatException("Pause and Save/Load controls are reserved; stored overrides were not applied.");
            }
            // Core then validates the native JSON grammar, ids, paths and action identities,
            // on an isolated clone before applying any override to the active runtime asset.
        }
    }
}
