using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumivale.Start.Game
{
    /// <summary>Preserves the actual uGUI activation source for the Starter's save slots.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "GameSlotActivation")]
    public sealed class GameSlotActivation : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        private UnityEngine.UI.Button button;

        /// <summary>True for a left pointer click; false for keyboard/gamepad Submit.</summary>
        public event Action<bool> Activated;

        private bool CanActivate
        {
            get
            {
                if (button == null) button = GetComponent<UnityEngine.UI.Button>();
                return isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && CanActivate)
                Activated?.Invoke(true);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanActivate) Activated?.Invoke(false);
        }
    }
}
