using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nusantara.SaveSystem;

namespace Nusantara.UI
{
    /// <summary>
    /// One row in the <see cref="SaveSlotPanel"/> — represents a single save slot.
    /// Shows the slot's metadata (location, party, playtime, timestamp) or an
    /// "empty" state. The whole row is a Button; a separate optional Delete button
    /// wipes the slot.
    ///
    /// ── Unity setup (one prefab, pooled by SaveSlotPanel) ────────────────────
    ///   SlotRow (Button + this component)
    ///     ├ TitleText   (TMP)  e.g. "Slot 1"
    ///     ├ InfoText    (TMP)  location • party • playtime  (filled at runtime)
    ///     ├ DateText    (TMP)  saved-at timestamp           (filled at runtime)
    ///     ├ EmptyText   (TMP)  "Kosong" — shown only for empty slots
    ///     └ DeleteButton (Button, optional)
    /// </summary>
    public class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private Button          rowButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private Button          deleteButton;

        private int             _slot;
        private Action<int>     _onClicked;
        private Action<int>     _onDelete;

        void Awake()
        {
            if (rowButton    != null) rowButton.onClick.AddListener(() => _onClicked?.Invoke(_slot));
            if (deleteButton != null) deleteButton.onClick.AddListener(() => _onDelete?.Invoke(_slot));
        }

        /// <summary>
        /// Binds this row to a slot. <paramref name="meta"/> is null for an empty slot.
        /// <paramref name="selectable"/> controls whether the row Button is interactable
        /// (Load mode greys out empty slots; Save mode keeps every slot tappable).
        /// </summary>
        public void Bind(int slot, SaveMetadata meta, bool selectable,
                         Action<int> onClicked, Action<int> onDelete)
        {
            _slot      = slot;
            _onClicked = onClicked;
            _onDelete  = onDelete;

            if (titleText != null) titleText.text = $"Slot {slot + 1}";

            bool hasSave = meta != null;

            if (emptyText != null) emptyText.gameObject.SetActive(!hasSave);
            if (infoText  != null) infoText.gameObject.SetActive(hasSave);
            if (dateText  != null) dateText.gameObject.SetActive(hasSave);

            if (hasSave)
            {
                string party    = (meta.partyNames != null && meta.partyNames.Length > 0)
                                   ? string.Join(", ", meta.partyNames)
                                   : $"{meta.partyCount} anggota";
                string location = string.IsNullOrEmpty(meta.locationScene) ? "?" : meta.locationScene;

                if (infoText != null) infoText.text = $"{location}  •  {party}  •  {FormatPlaytime(meta.playSeconds)}";
                if (dateText != null) dateText.text = FormatTimestamp(meta.savedAtIso);
            }
            else if (emptyText != null)
            {
                emptyText.text = "Kosong";
            }

            if (rowButton    != null) rowButton.interactable    = selectable;
            if (deleteButton != null) deleteButton.gameObject.SetActive(hasSave);
        }

        // ── Formatting helpers ──────────────────────────────────────────────────

        private static string FormatPlaytime(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}j {t.Minutes}m"
                : $"{t.Minutes}m {t.Seconds}d";
        }

        private static string FormatTimestamp(string iso)
        {
            if (DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            return iso ?? "";
        }
    }
}
