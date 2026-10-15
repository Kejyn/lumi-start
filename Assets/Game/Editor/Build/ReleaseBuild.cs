using System;
using System.IO;
using System.Linq;
using Lumivale.Start.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.CrashReporting;
using UnityEditor.EngineDiagnostics;
using UnityEngine;
using Unity.Pipeline.Config;

namespace Lumivale.Start.Game.Editor
{
    /// <summary>Reproducible Windows builds; validation identity and tooling never alter release settings.</summary>
    public static class ReleaseBuild
    {
        public const string ValidationCompany = "Lumivale Studio Validation";
        public const string ValidationProduct = "Lumi-start Phase5 Validation";
        private const string PipelineSettings = "ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json";

        public static readonly string[] Scenes =
        {
            GameScenePaths.MenuPath,
            GameScenePaths.GameplayPath
        };

        [MenuItem("Lumivale/Build/Windows x64 Mono")]
        public static void BuildWindowsMono() => Execute(ScriptingImplementation.Mono2x, false);

        [MenuItem("Lumivale/Build/Windows x64 validation")]
        public static void BuildWindowsValidation() => Execute(ScriptingImplementation.Mono2x, true);

        [MenuItem("Lumivale/Build/Windows x64 IL2CPP")]
        public static void BuildWindowsIl2Cpp() => Execute(ScriptingImplementation.IL2CPP, false);

        /// <summary>Queues the native build on the Editor main loop without blocking command processing.</summary>
        public static void QueueWindowsMono() => Queue(ScriptingImplementation.Mono2x, false);
        public static void QueueWindowsValidation() => Queue(ScriptingImplementation.Mono2x, true);
        public static void QueueWindowsIl2Cpp() => Queue(ScriptingImplementation.IL2CPP, false);

        private static void Queue(ScriptingImplementation backend, bool validation)
        {
            Directory.CreateDirectory("Builds/Reports");
            File.WriteAllText("Builds/Reports/Build-" + Name(backend, validation) + ".json", "{\"status\":\"queued\"}");
            EditorApplication.CallbackFunction pump = null;
            pump = () =>
            {
                EditorApplication.update -= pump;
                try { Execute(backend, validation); }
                catch (Exception exception) { Debug.LogException(exception); }
            };
            EditorApplication.update += pump;
        }

        private static string Name(ScriptingImplementation backend, bool validation) =>
            validation ? "Validation" : backend == ScriptingImplementation.IL2CPP ? "Il2Cpp" : "Mono";

        private static void Execute(ScriptingImplementation backend, bool validation)
        {
            string name = Name(backend, validation);
            string reportPath = "Builds/Reports/Build-" + name + ".json";
            Directory.CreateDirectory("Builds/Reports");
            var summary = new BuildEvidence { status = "building", backend = backend.ToString(), validation = validation,
                unityVersion = Application.unityVersion, scenes = (string[])Scenes.Clone() };
            Write(reportPath, summary);
            var target = NamedBuildTarget.Standalone;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(target);
            ManagedStrippingLevel previousStripping = PlayerSettings.GetManagedStrippingLevel(target);
            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            bool previousDevelopment = EditorUserBuildSettings.development;
            bool previousBackground = PlayerSettings.runInBackground;
            bool previousEngineDiagnostics = EngineDiagnosticsSettings.enabled;
            bool previousCloudDiagnostics = CrashReportingSettings.enabled;
            summary.engineDiagnosticsPreviouslyEnabled = previousEngineDiagnostics;
            summary.cloudDiagnosticsPreviouslyEnabled = previousCloudDiagnostics;
            bool hadConfig = File.Exists(PipelineSettings);
            byte[] configBytes = hadConfig ? File.ReadAllBytes(PipelineSettings) : null;
            // BuildPlayer serializes temporary native settings before finally restores their
            // in-memory values. Preserve the original files too, so reopening keeps the game identity.
            const string playerSettingsPath = "ProjectSettings/ProjectSettings.asset";
            const string connectSettingsPath = "ProjectSettings/UnityConnectSettings.asset";
            byte[] playerSettingsBytes = File.ReadAllBytes(playerSettingsPath);
            byte[] connectSettingsBytes = File.ReadAllBytes(connectSettingsPath);
            RuntimePipelineConfig config = null;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Stop Play before building.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Windows x64 build support is not installed.");
                if (Scenes.Any(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null))
                    throw new InvalidOperationException("A required build scene is missing.");
                // Unity 6 has a separate Engine Diagnostics switch. The legacy crash
                // reporting flag alone does not prevent the native symbol-upload step.
                EngineDiagnosticsSettings.enabled = false;
                CrashReportingSettings.enabled = false;
                if (EngineDiagnosticsSettings.enabled || CrashReportingSettings.enabled)
                    throw new InvalidOperationException("Could not disable Unity diagnostics for this local build.");
                summary.diagnosticsDisabledForBuild = true;
                // The installed Windows postprocessor uses this native gate before
                // invoking its uploader. Fail closed if that gate cannot be verified.
                var uploadGate = typeof(CrashReportingSettings).GetProperty("canUploadReports",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static);
                if (uploadGate == null || uploadGate.PropertyType != typeof(bool) || (bool)uploadGate.GetValue(null))
                    throw new InvalidOperationException("Cannot verify that Unity native symbol uploads are disabled.");
                summary.nativeSymbolUploadGateVerified = true;
                PlayerSettings.SetScriptingBackend(target, backend);
                PlayerSettings.SetManagedStrippingLevel(target, backend == ScriptingImplementation.IL2CPP ?
                    ManagedStrippingLevel.Minimal : ManagedStrippingLevel.Disabled);
                PlayerSettings.bundleVersion = "0.1.0";
                // The installed Pipeline build processor inspects this flag when deciding
                // whether to bake its QA settings into a scripted Development build.
                EditorUserBuildSettings.development = validation;
                if (validation)
                {
                    PlayerSettings.companyName = ValidationCompany;
                    PlayerSettings.productName = ValidationProduct;
                    PlayerSettings.runInBackground = true;
                }
                // Runtime Pipeline is an existing Editor tool, enabled only for the isolated QA build.
                config = RuntimePipelineConfig.Load() ?? ScriptableObject.CreateInstance<RuntimePipelineConfig>();
                config.enableInBuilds = validation;
                config.autoStart = true;
                config.port = 0;
                config.Save();
                string folder = Path.GetFullPath(Path.Combine("Builds", "Windows", name));
                Directory.CreateDirectory(folder);
                string executable = Path.Combine(folder, validation ? "LumiStartValidation.exe" : "LumiStart.exe");
                summary.company = PlayerSettings.companyName;
                summary.product = PlayerSettings.productName;
                summary.executable = executable;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = (string[])Scenes.Clone(),
                    locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.DetailedBuildReport | (validation ? BuildOptions.Development : BuildOptions.None)
                });
                summary.result = report.summary.result.ToString();
                summary.totalErrors = report.summary.totalErrors;
                summary.totalWarnings = report.summary.totalWarnings;
                summary.totalBytes = report.summary.totalSize;
                summary.durationSeconds = report.summary.totalTime.TotalSeconds;
                summary.files = report.GetFiles().Select(file => file.path).ToArray();
                summary.messages = report.steps.SelectMany(step => step.messages).Select(message =>
                    new BuildMessage { type = message.type.ToString(), content = message.content }).ToArray();
                summary.status = report.summary.result == BuildResult.Succeeded ? "succeeded" : "failed";
                Write(reportPath, summary);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows " + name + " build failed; see " + reportPath);
                Debug.Log("[Lumi Build] " + name + " succeeded: " + executable);
            }
            catch (Exception exception)
            {
                summary.status = "failed"; summary.exception = exception.ToString();
                Write(reportPath, summary); throw;
            }
            finally
            {
                PlayerSettings.companyName = previousCompany;
                PlayerSettings.productName = previousProduct;
                PlayerSettings.bundleVersion = previousVersion;
                EditorUserBuildSettings.development = previousDevelopment;
                PlayerSettings.runInBackground = previousBackground;
                PlayerSettings.SetScriptingBackend(target, previousBackend);
                PlayerSettings.SetManagedStrippingLevel(target, previousStripping);
                CrashReportingSettings.enabled = previousCloudDiagnostics;
                EngineDiagnosticsSettings.enabled = previousEngineDiagnostics;
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
                if (hadConfig) File.WriteAllBytes(PipelineSettings, configBytes);
                else RuntimePipelineConfig.DeleteSettingsFileForTesting();
                File.WriteAllBytes(playerSettingsPath, playerSettingsBytes);
                File.WriteAllBytes(connectSettingsPath, connectSettingsBytes);
                summary.settingsFilesRestored = File.ReadAllBytes(playerSettingsPath).SequenceEqual(playerSettingsBytes) &&
                    File.ReadAllBytes(connectSettingsPath).SequenceEqual(connectSettingsBytes);
                Write(reportPath, summary);
            }
        }

        private static void Write(string path, BuildEvidence evidence) => File.WriteAllText(path, JsonUtility.ToJson(evidence, true));

        [Serializable]
        private sealed class BuildEvidence
        {
            public string status, backend, unityVersion, company, product, executable, result, exception;
            public bool validation, nativeSymbolUploadGateVerified;
            public bool diagnosticsDisabledForBuild, engineDiagnosticsPreviouslyEnabled, cloudDiagnosticsPreviouslyEnabled;
            public bool settingsFilesRestored;
            public int totalErrors, totalWarnings;
            public ulong totalBytes;
            public double durationSeconds;
            public string[] scenes, files;
            public BuildMessage[] messages;
        }
        [Serializable]
        private sealed class BuildMessage { public string type, content; }
    }
}
