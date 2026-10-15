using System;
using Lumivale.Core.Bootstrap;
using Lumivale.Start.Game;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>Applies the menu's project DTO after the destination's ordinary controllers are ready.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "ProductGameplayAdapter")]
    public sealed class GameplaySnapshotAdapter : MonoBehaviour
    {
        [SerializeField] private GameSceneController navigation;
        [SerializeField] private GameSaveController persistenceView;
        private bool applied;
        private GameSessionState session;

        public bool IsReady => applied && navigation != null && navigation.IsReady &&
            persistenceView != null && persistenceView.IsReady;

        public void Configure(GameSceneController nav, GameSaveController saveView)
        {
            navigation = nav;
            persistenceView = saveView;
        }

        private void Update()
        {
            if (applied)
            {
                if (session != null && persistenceView != null && persistenceView.IsReady &&
                    session.SelectedSlotId != persistenceView.SlotId)
                    session.SelectSlot(persistenceView.SlotId);
                return;
            }
            if (navigation == null || !navigation.IsReady ||
                persistenceView == null || !persistenceView.IsReady) return;
            var host = LumiBootstrap.Instance as GameBootstrap;
            if (host == null || !host.IsReady) return;
            try
            {
                session = GameSessionState.GetOrCreate(host);
                if (session.TryConsumeSnapshot(out var data, out string slotId))
                    persistenceView.ApplySnapshot(data, slotId);
                else
                    persistenceView.ConfigureSelectedSlot(session.SelectedSlotId);
                applied = true;
            }
            catch (Exception exception)
            {
                applied = true;
                Debug.LogException(exception, this);
            }
        }
    }
}
