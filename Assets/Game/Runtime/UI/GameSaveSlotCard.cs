using Lumivale.Core.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

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
        private int presentedSlot;
        private SaveSlotInfo presentedInfo;
        private bool hasPresentation;

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
            presentedSlot = slotNumber;
            presentedInfo = info;
            hasPresentation = true;
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (!hasPresentation || !GameText.IsReady) return;
            SaveSlotInfo info = presentedInfo;
            bool empty = info != null && info.Status == PersistenceStatus.NotFound;
            bool saved = info != null && info.Status == PersistenceStatus.Success;
            string stateKey = info == null ? "save.checking" : empty ? "save.empty" : saved ?
                (info.RecoveredFromBackup ? "save.recoverable" : "save.saved") : "persistence.status." + info.Status;
            if (title != null) title.text = GameText.Text("save.slot_title", presentedSlot, GameText.Text(stateKey));
            if (status != null) GameText.Set(status, info == null ? "save.reading" : empty ?
                "save.available" : saved ?
                (info.RecoveredFromBackup ? "save.backup_available" : "save.manual") : "save.unavailable");
            if (date != null) date.text = info?.Metadata != null ?
                info.Metadata.SavedAtUtc.ToLocalTime().ToString("dd MMM yyyy  HH:mm",
                    LocalizationSettings.SelectedLocale?.Identifier.CultureInfo ?? System.Globalization.CultureInfo.InvariantCulture) :
                empty ? GameText.Text("save.no_data") : "";
            if (previewCaption != null) GameText.Set(previewCaption,
                empty ? "save.preview.empty" : saved ? "save.preview.saved" : "common.unavailable_mark");
            if (thumbnail != null) thumbnail.color = empty ?
                new Color(.075f, .075f, .075f, 1f) : new Color(.15f, .15f, .15f, 1f);
        }

        private void LocaleChanged(Locale locale) => RefreshPresentation();
        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
            RefreshPresentation();
        }
        private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null && selectedFrame.activeSelf != selected) selectedFrame.SetActive(selected);
        }
    }
}
