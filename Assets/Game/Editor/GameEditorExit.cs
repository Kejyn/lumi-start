using Lumivale.Start.Game;
using UnityEditor;
using UnityEngine;

namespace Lumivale.Start.Game.Editor
{
    /// <summary>Editor-only final exit; runtime assemblies contain no UnityEditor dependency.</summary>
    [InitializeOnLoad]
    internal static class GameEditorExit
    {
        private static GameMenuController observed;

        static GameEditorExit()
        {
            EditorApplication.update += ObserveMenu;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private static void ObserveMenu()
        {
            if (!EditorApplication.isPlaying) return;
            var current = Object.FindFirstObjectByType<GameMenuController>();
            if (ReferenceEquals(current, observed)) return;
            Detach();
            observed = current;
            if (observed != null) observed.EditorQuitRequested += StopPlaying;
        }

        private static void StopPlaying()
        {
            // Let the awaited shutdown and its event callback finish before the domain exits.
            EditorApplication.delayCall += FinishExit;
        }

        private static void FinishExit()
        {
            EditorApplication.delayCall -= FinishExit;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.delayCall -= FinishExit;
            Detach();
        }

        private static void Detach()
        {
            if (observed != null) observed.EditorQuitRequested -= StopPlaying;
            observed = null;
        }
    }
}
