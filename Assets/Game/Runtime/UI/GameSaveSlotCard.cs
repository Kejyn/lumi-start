using Lumivale.Core.Persistence;
using TMPro;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>Presentation only; slot selection and loading remain owned by the menu.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Product", sourceAssembly: "Lumivale.Start.Product", sourceClassName: "GameSaveSlotCard")]
    public sealed class GameSaveSlotCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text date;
        [SerializeField] private TMP_Text previewCaption;
        [SerializeField] private UnityEngine.UI.Image thumbnail;
        [SerializeField] private GameObject selectedFrame;

        public bool IsSelected => selectedFrame != null && selectedFrame.activeSelf;

        public void Configure(TMP_Text titleLabel, TMP_Text statusLabel, TMP_Text dateLabel,
            TMP_Text previewLabel, UnityEngine.UI.Image thumbnailImage, GameObject selectionFrame)
        {
            title = titleLabel;
            status = statusLabel;
            date = dateLabel;
            previewCaption = previewLabel;
            thumbnail = thumbnailImage;
            selectedFrame = selectionFrame;
        }

        public void Present(int slotNumber, SaveSlotInfo info)
        {
            bool empty = info != null && info.Status == PersistenceStatus.NotFound;
            bool saved = info != null && info.Status == PersistenceStatus.Success;
            string state = info == null ? "Checking" : empty ? "Empty slot" : saved ?
                (info.RecoveredFromBackup ? "Recoverable save" : "Saved game") : info.Status.ToString();
            if (title != null) title.text = "Slot " + slotNumber + "  /  " + state;
            if (status != null) status.text = info == null ? "Reading save information" : empty ?
                "Available for a new game" : saved ?
                (info.RecoveredFromBackup ? "Backup available" : "Manual save") : "Save unavailable";
            if (date != null) date.text = info?.Metadata != null ?
                info.Metadata.SavedAtUtc.ToLocalTime().ToString("dd MMM yyyy  HH:mm") : empty ? "No save data" : "";
            if (previewCaption != null) previewCaption.text = empty ? "EMPTY" : saved ? "SAVE" : "\u2014";
            if (thumbnail != null) thumbnail.color = empty ?
                new Color(.075f, .075f, .075f, 1f) : new Color(.15f, .15f, .15f, 1f);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null && selectedFrame.activeSelf != selected) selectedFrame.SetActive(selected);
        }
    }
}
