using System;
using Lumivale.Start.Game;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>One launch's presentation state, owned by the persistent Bootstrap, with no scene references.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "GameSessionState")]
    public sealed class GameSessionState : MonoBehaviour
    {
        private GameSaveData pendingSnapshot;
        private GamePreferences preferences;
        private string selectedSlotId = "demo-1";

        public bool BootDismissed { get; set; }
        public string SelectedSlotId => selectedSlotId;
        public bool HasPendingSnapshot => pendingSnapshot != null;
        public GamePreferences Preferences => preferences ??
            (preferences = new GamePreferences(GetComponent<GameBootstrap>().PreferenceKey));

        public static GameSessionState GetOrCreate(GameBootstrap host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            return host.GetComponent<GameSessionState>() ?? host.gameObject.AddComponent<GameSessionState>();
        }

        public void SelectSlot(string slotId)
        {
            if (slotId != "demo-1" && slotId != "demo-2" && slotId != "demo-3")
                throw new ArgumentException("The Starter uses demo-1, demo-2 and demo-3.", nameof(slotId));
            selectedSlotId = slotId;
        }

        public void PrepareLoadedGame(string slotId, GameSaveData snapshot)
        {
            GameSaveData.CreateSchema().Validate(snapshot);
            SelectSlot(slotId);
            pendingSnapshot = Copy(snapshot);
        }

        public void PrepareNewGame(string slotId)
        {
            SelectSlot(slotId);
            pendingSnapshot = new GameSaveData { PositionY = 0.5f };
        }

        public bool TryConsumeSnapshot(out GameSaveData snapshot, out string slotId)
        {
            slotId = selectedSlotId;
            snapshot = pendingSnapshot;
            pendingSnapshot = null;
            return snapshot != null;
        }

        public void ClearPendingSnapshot() => pendingSnapshot = null;

        private static GameSaveData Copy(GameSaveData value) => new GameSaveData
        {
            PositionX = value.PositionX, PositionY = value.PositionY,
            PositionZ = value.PositionZ, Counter = value.Counter
        };
    }
}
