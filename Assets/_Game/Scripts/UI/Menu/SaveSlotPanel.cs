using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nusantara.SaveSystem;

namespace Nusantara.UI
{
    /// <summary>
    /// A 3-slot save/load picker, reused for BOTH saving and loading (set by
    /// <see cref="Open"/>'s mode). Lists every slot with its metadata header and
    /// routes clicks to <see cref="SaveManager"/>.
    ///
    ///   • Save mode  — every slot is tappable. Tapping a USED slot asks to confirm
    ///                  overwrite (via the shared ConfirmDialog); empty slots save
    ///                  immediately. The list refreshes after writing.
    ///   • Load mode  — only populated slots are tappable. Tapping one loads it
    ///                  (SaveManager loads the saved scene + restores), so we close
    ///                  the menu and restore Time.timeScale first.
    ///
    /// ── Unity setup ──────────────────────────────────────────────────────────
    ///   SaveSlotPanelRoot (panel, starts INACTIVE)
    ///     ├ Backdrop (optional Button → Close)
    ///     ├ HeaderText (TMP) — "SIMPAN" / "MUAT"
    ///     ├ SlotsContainer (Vertical Layout Group) — rows are pooled in here
    ///     └ CloseButton (Button)
    ///   Assign a SaveSlotRow prefab + the SlotsContainer. Wire the shared
    ///   ConfirmDialog so overwrite prompts work.
    /// </summary>
    public class SaveSlotPanel : MonoBehaviour
    {
        public enum Mode { Save, Load }

        [Header("References")]
        [SerializeField] private GameObject      root;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private Transform       slotsContainer;
        [SerializeField] private SaveSlotRow     slotRowPrefab;
        [SerializeField] private Button          closeButton;
        [SerializeField] private Button          backdropButton;
        [SerializeField] private ConfirmDialog   confirmDialog;

        [Header("Labels")]
        [SerializeField] private string saveHeader = "— SIMPAN —";
        [SerializeField] private string loadHeader = "— MUAT —";

        private readonly List<SaveSlotRow> _rows = new List<SaveSlotRow>();
        private Mode _mode;

        void Awake()
        {
            if (closeButton    != null) closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            Close();
        }

        // ── Public API ──────────────────────────────────────────────────────────

        public void Open(Mode mode)
        {
            _mode = mode;
            if (headerText != null) headerText.text = mode == Mode.Save ? saveHeader : loadHeader;
            if (root != null) root.SetActive(true);
            else gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            else gameObject.SetActive(false);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void Refresh()
        {
            EnsureRows(SaveManager.SlotCount);

            for (int i = 0; i < SaveManager.SlotCount; i++)
            {
                var meta = SaveManager.GetMetadata(i);
                bool hasSave = SaveManager.HasSave(i);

                // Save: any slot selectable. Load: only populated slots.
                bool selectable = _mode == Mode.Save || hasSave;

                _rows[i].gameObject.SetActive(true);
                _rows[i].Bind(i, meta, selectable, OnSlotClicked, OnDeleteClicked);
            }

            // Hide any extra pooled rows beyond the slot count.
            for (int i = SaveManager.SlotCount; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);
        }

        private void EnsureRows(int count)
        {
            if (slotRowPrefab == null || slotsContainer == null)
            {
                Debug.LogError("[SaveSlotPanel] slotRowPrefab or slotsContainer not assigned — cannot build slot list.");
                return;
            }
            while (_rows.Count < count)
                _rows.Add(Instantiate(slotRowPrefab, slotsContainer));
        }

        private void OnSlotClicked(int slot)
        {
            if (_mode == Mode.Save) DoSave(slot);
            else                    DoLoad(slot);
        }

        private void DoSave(int slot)
        {
            if (SaveManager.HasSave(slot) && confirmDialog != null)
            {
                confirmDialog.Show(
                    $"Timpa simpanan di Slot {slot + 1}?",
                    onConfirm: () => { WriteSlot(slot); });
                return;
            }
            WriteSlot(slot);
        }

        private void WriteSlot(int slot)
        {
            bool ok = SaveManager.Save(slot);
            Debug.Log(ok ? $"[SaveSlotPanel] Saved to slot {slot}." : $"[SaveSlotPanel] Save to slot {slot} FAILED.");
            Refresh();   // show the new metadata
        }

        private void DoLoad(int slot)
        {
            if (!SaveManager.HasSave(slot)) return;

            // Loading swaps scenes + restores; make sure time is running and the
            // pause menu is torn down BEFORE the scene load kicks in.
            Time.timeScale = 1f;
            if (GameController.Instance != null) GameController.Instance.SetPaused(false);
            Close();

            SaveManager.Load(slot);
        }

        private void OnDeleteClicked(int slot)
        {
            if (confirmDialog == null) { SaveManager.DeleteSave(slot); Refresh(); return; }

            confirmDialog.Show(
                $"Hapus simpanan di Slot {slot + 1}?",
                onConfirm: () => { SaveManager.DeleteSave(slot); Refresh(); });
        }
    }
}
