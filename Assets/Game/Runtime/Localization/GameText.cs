using System;
using TMPro;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace Lumivale.Start.Game
{
    /// <summary>Small adapter to the official preloaded String Table and LocalizeStringEvent APIs.</summary>
    public static class GameText
    {
        public const string Table = "GameUI";

        /// <summary>Whether synchronous fragments are available from the native preloaded table.</summary>
        public static bool IsReady
        {
            get
            {
                if (!LocalizationSettings.HasSettings) return false;
                var locale = LocalizationSettings.SelectedLocaleAsync;
                if (!locale.IsDone || locale.Result == null) return false;
                var initialization = LocalizationSettings.InitializationOperation;
                if (!initialization.IsDone || initialization.OperationException != null) return false;
                var table = LocalizationSettings.StringDatabase.GetTableAsync(Table, locale.Result);
                return table.IsDone && table.Result != null;
            }
        }

        public static string Text(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var localeOperation = LocalizationSettings.SelectedLocaleAsync;
            if (!localeOperation.IsDone || localeOperation.Result == null) return "[" + key + "]";
            var locale = localeOperation.Result;
            var operation = LocalizationSettings.StringDatabase.GetTableAsync(Table, locale);
            // Bootstrap preloads both tables. Never synchronously wait for an Addressables load here.
            if (!operation.IsDone || operation.Result == null) return "[" + key + "]";
            var entry = operation.Result.GetEntry(key);
            return entry != null ? entry.GetLocalizedString(locale, args) : "[" + key + "]";
        }

        /// <summary>Measures existing button groups against their English design text, independent of the displayed locale.</summary>
        internal static string ReferenceLayoutText(TMP_Text label)
        {
            var localized = label.GetComponent<LocalizeStringEvent>();
            if (localized == null || localized.StringReference.IsEmpty || !IsReady) return null;
            var english = LocalizationSettings.AvailableLocales.GetLocale("en");
            if (english == null) return null;
            var operation = LocalizationSettings.StringDatabase.GetTableAsync(localized.StringReference.TableReference, english);
            if (!operation.IsDone || operation.Result == null) return null;
            var reference = localized.StringReference.TableEntryReference;
            var entry = reference.KeyId != 0 ? operation.Result.GetEntry(reference.KeyId) : operation.Result.GetEntry(reference.Key);
            return entry?.GetLocalizedString(english, localized.StringReference.Arguments);
        }

        public static void Set(TMP_Text label, string key, params object[] args)
        {
            if (label == null) return;
            var localizer = label.GetComponent<LocalizeStringEvent>();
            if (string.IsNullOrEmpty(key))
            {
                if (localizer != null)
                {
                    localizer.enabled = false;
                    localizer.StringReference = new LocalizedString();
                }
                label.text = string.Empty;
                return;
            }
            if (localizer == null) localizer = label.gameObject.AddComponent<LocalizeStringEvent>();
            bool authoredListener = false;
            for (int index = 0; index < localizer.OnUpdateString.GetPersistentEventCount(); index++)
                if (localizer.OnUpdateString.GetPersistentTarget(index) == label &&
                    localizer.OnUpdateString.GetPersistentMethodName(index) == "set_text") authoredListener = true;
            if (!authoredListener)
            {
                var listener = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), label, "set_text");
                localizer.OnUpdateString.RemoveListener(listener);
                localizer.OnUpdateString.AddListener(listener);
            }
            localizer.StringReference = new LocalizedString(Table, key) { Arguments = args };
            localizer.enabled = true;
            // Keep the previously rendered text while the official locale preload is pending.
            // LocalizeStringEvent will supply the translated value when that load completes.
            if (IsReady) label.text = Text(key, args);
            localizer.RefreshString();
        }
    }
}
