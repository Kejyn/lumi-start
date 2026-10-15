using System;
using UnityEngine.EventSystems;

namespace Lumivale.Start.Game
{
    /// <summary>A demo selector: click/Submit advances; horizontal navigation cycles both ways.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase3ChoiceButton")]
    public sealed class GameChoiceButton : UnityEngine.UI.Button
    {
        public event Action<int> ChoiceRequested;

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
            base.OnPointerClick(eventData);
            ChoiceRequested?.Invoke(1);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            base.OnSubmit(eventData);
            ChoiceRequested?.Invoke(1);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (IsActive() && IsInteractable() &&
                (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                ChoiceRequested?.Invoke(eventData.moveDir == MoveDirection.Left ? -1 : 1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }
    }
}
