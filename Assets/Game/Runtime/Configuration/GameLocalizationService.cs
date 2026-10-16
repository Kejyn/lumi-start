using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Settings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Lumivale.Start.Game
{
    /// <summary>Composes Unity Localization with the existing Settings preference and lifecycle.</summary>
    public sealed class GameLocalizationService : ILumiService
    {
        private static readonly IReadOnlyCollection<string> RequiredServices = new[] { "settings" };
        private Locale previousLocale;
        private bool ownsLocale;
        private bool stopping;
        private bool processingPreview;
        private string requestedLanguage;
        private TaskCompletionSource<bool> previewCompletion;
        private Task shutdownTask;
        private bool nativeInitializationStarted;

        public string Id => "localization";
        public IReadOnlyCollection<string> Dependencies => RequiredServices;
        public bool IsInitialized { get; private set; }
        /// <summary>Completes after the latest preview and the official locale-change callbacks have settled.</summary>
        public Task PreviewReady => previewCompletion?.Task ?? Task.CompletedTask;

        /// <summary>Only complete shipped locales participate; unavailable system languages use English.</summary>
        public static string DetectLanguage(SystemLanguage systemLanguage) =>
            systemLanguage == SystemLanguage.French ? "fr" : "en";

        public async Task InitializeAsync(ServiceContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsInitialized) return;
            if (stopping) throw new InvalidOperationException("A stopped Localization service cannot restart.");
            var settings = context.Get<SettingsService>();
            nativeInitializationStarted = true;
            await WaitForNativeInitializationAsync(cancellationToken);

            previousLocale = LocalizationSettings.SelectedLocale;
            ownsLocale = true;
            foreach (string code in new[] { "en", "fr" })
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale == null) throw new InvalidOperationException("The required locale is unavailable: " + code);
                var table = LocalizationSettings.StringDatabase.GetTableAsync(GameText.Table, locale);
                while (!table.IsDone) await Awaitable.NextFrameAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (table.Result == null || table.OperationException != null)
                    throw new InvalidOperationException("The GameUI string table could not load for " + code + ".", table.OperationException);
            }
            IsInitialized = true;
            Preview(settings.Current.LanguageCode);
            await PreviewReady;
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>Changes the package locale without updating Settings or writing any preference.</summary>
        public void Preview(string languageCode)
        {
            if (!IsInitialized || stopping)
                throw new InvalidOperationException("Initialize Localization before previewing a locale.");
            requestedLanguage = string.Equals(languageCode, "fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
            if (processingPreview) return;
            // A second native SelectedLocale assignment during preloading releases the first
            // operation's table handles. Finish that operation first and retain the latest request.
            processingPreview = true;
            previewCompletion = new TaskCompletionSource<bool>();
            _ = ProcessPreviewAsync(previewCompletion);
        }

        public Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (shutdownTask == null) shutdownTask = StopAsync();
            return shutdownTask;
        }

        private async Task ProcessPreviewAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                while (!stopping && requestedLanguage != null)
                {
                    string code = requestedLanguage;
                    requestedLanguage = null;
                    await WaitForNativeInitializationAsync(CancellationToken.None);
                    if (stopping) break;
                    // Requests arriving while native initialization was finishing supersede this one.
                    if (requestedLanguage != null) continue;
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    if (locale == null) throw new InvalidOperationException("The requested locale is unavailable: " + code);
                    if (LocalizationSettings.SelectedLocale != locale)
                    {
                        LocalizationSettings.SelectedLocale = locale;
                        await WaitForNativeInitializationAsync(CancellationToken.None);
                    }
                }
                processingPreview = false;
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                processingPreview = false;
                completion.TrySetException(exception);
                Debug.LogException(exception);
            }
        }

        private async Task StopAsync()
        {
            stopping = true;
            IsInitialized = false;
            requestedLanguage = null;
            if (!nativeInitializationStarted) return;
            // Native Addressables operations cannot be cancelled safely. Drain them before
            // restoring the previous locale so the next Bootstrap cannot invalidate their handles.
            Exception previewFailure = null;
            try { await PreviewReady; }
            catch (Exception exception) { previewFailure = exception; }
            try
            {
                await WaitForNativeInitializationAsync(CancellationToken.None, requireSuccess: false);
                if (ownsLocale && previousLocale != null && LocalizationSettings.HasSettings)
                {
                    LocalizationSettings.SelectedLocale = previousLocale;
                    await WaitForNativeInitializationAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                if (previewFailure != null)
                    throw new AggregateException("Locale preview and restoration both failed.", previewFailure, exception);
                throw;
            }
            finally
            {
                previousLocale = null;
                ownsLocale = false;
            }
            if (previewFailure != null) throw previewFailure;
        }

        private static async Task WaitForNativeInitializationAsync(CancellationToken cancellationToken, bool requireSuccess = true)
        {
            while (true)
            {
                var initialization = LocalizationSettings.InitializationOperation;
                bool wasPending = !initialization.IsDone;
                while (initialization.IsValid() && !initialization.IsDone)
                    await Awaitable.NextFrameAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!initialization.IsValid()) continue;
                if (requireSuccess && initialization.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException("Unity Localization could not initialize.", initialization.OperationException);
                // The package emits SelectedLocaleChanged from a coroutine after preloading.
                // Yield once after an asynchronous load to let that callback finish as well.
                if (wasPending) await Awaitable.NextFrameAsync(cancellationToken);
                if (initialization.Equals(LocalizationSettings.InitializationOperation)) return;
            }
        }
    }
}
