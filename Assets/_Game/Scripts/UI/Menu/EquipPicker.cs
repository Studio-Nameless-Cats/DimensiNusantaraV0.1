using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Nusantara.UI.Motion;

namespace Nusantara.UI
{
    // The little popup that opens when you click an equipment slot slab: lists every
    // item in the bag that FITS that slot, each with a stat preview vs what's worn now
    // ("+2 ATK  -1 DEF"), plus a "Lepas" row to take the current piece off. Click a row
    // (or arrows + Z) to equip, X or the scrim to back out.
    //
    // Modal rules apply (UI_RULES rule 12): same charcoal surfaces, depth comes from the
    // scrim and sitting on top - no panel hue swap. InventoryPanel owns one of these and
    // pauses its own input while we're open.
    public class EquipPicker : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Whole popup root incl. scrim. Starts INACTIVE.")]
        [SerializeField] private GameObject root;
        [Tooltip("Full-screen dim Button behind the card - clicking it cancels.")]
        [SerializeField] private Button scrim;
        [Tooltip("Header, e.g. 'PILIH SENJATA'.")]
        [SerializeField] private TextMeshProUGUI titleText;
        [Tooltip("Muted line under the title, e.g. 'Terpasang: Keris Tua'.")]
        [SerializeField] private TextMeshProUGUI currentText;
        [Tooltip("Layout Group parent the rows get spawned under.")]
        [SerializeField] private Transform rowContainer;
        [SerializeField] private EquipPickerRow rowPrefab;
        [Tooltip("Shown when the bag has nothing that fits this slot.")]
        [SerializeField] private TextMeshProUGUI emptyText;
        [Tooltip("Optional pop-in/out animation on the card. Null = instant.")]
        [SerializeField] private UIAnimator animator;

        private readonly List<EquipPickerRow> _rows = new List<EquipPickerRow>();

        // What each visible row DOES when picked. Item == null means the Lepas row.
        private class RowAction
        {
            public ItemData Item;
            public int Count;
        }
        private readonly List<RowAction> _actions = new List<RowAction>();

        private EquipSlot _slot;
        private int _jimatIndex;
        private string _characterId;
        private int _selected = -1;

        public bool IsOpen { get; private set; }

        // Rich-text hex strings for the delta preview, baked from the palette once.
        // Gains = gold (menus stay duotone), losses = the deep Danger red.
        private static string _gainHex, _lossHex;
        private static string GainHex => _gainHex ??= ColorUtility.ToHtmlStringRGB(NusantaraPalette.Role.FieldBg);
        private static string LossHex => _lossHex ??= ColorUtility.ToHtmlStringRGB(NusantaraPalette.Role.Danger);

        void Awake()
        {
            if (scrim != null) scrim.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.downArrowKey.wasPressedThisFrame) MoveSelection(+1);
            if (kb.upArrowKey.wasPressedThisFrame)   MoveSelection(-1);
            if (kb.zKey.wasPressedThisFrame) PickSelected();
            if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) Close();
        }

        // --- Opening / closing ---

        // Open for one specific slot of one character. jimatIndex (1 or 2) says which
        // charm slab was clicked; pass 0 for the non-jimat slots.
        public void Open(EquipSlot slot, int jimatIndex, string characterId)
        {
            var inv = InventorySystem.Instance;
            if (inv == null || slot == EquipSlot.None || string.IsNullOrEmpty(characterId)) return;

            _slot        = slot;
            _jimatIndex  = jimatIndex;
            _characterId = characterId;

            if (root != null) root.SetActive(true);
            IsOpen = true;
            ModalGate.Opened(); // claim Esc so GameMenu doesn't close the whole menu under us

            if (titleText != null) titleText.text = $"PILIH {SlotLabel(slot)}";

            var current = CurrentlyEquipped();
            if (currentText != null)
                currentText.text = current != null ? $"Terpasang: {current.Name}" : "Terpasang: kosong";

            BuildRows(inv, current);

            if (animator != null) animator.PlayIn();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ModalGate.Closed(); // hand Esc back to GameMenu (next frame)

            if (animator != null)
                animator.PlayOut(() => { if (root != null) root.SetActive(false); });
            else if (root != null)
                root.SetActive(false);
        }

        private static string SlotLabel(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Senjata: return "SENJATA";
                case EquipSlot.Zirah:   return "ZIRAH";
                default:                return "JIMAT";
            }
        }

        // What's sitting in the EXACT slot we opened for (jimat 1 vs 2 matters).
        private ItemData CurrentlyEquipped()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return null;

            var eq = inv.GetEquipment(_characterId);
            switch (_slot)
            {
                case EquipSlot.Senjata: return eq.Senjata;
                case EquipSlot.Zirah:   return eq.Zirah;
                case EquipSlot.Jimat:   return _jimatIndex == 2 ? eq.Jimat2 : eq.Jimat1;
                default:                return null;
            }
        }

        // --- The rows ---

        private void BuildRows(InventorySystem inv, ItemData current)
        {
            _actions.Clear();

            // Everything in the bag that fits this slot.
            foreach (var stack in inv.Stacks)
            {
                if (stack.Data == null || stack.Data.Slot != _slot) continue;
                _actions.Add(new RowAction { Item = stack.Data, Count = stack.Count });
            }

            // The Lepas (unequip) row, when there's something to take off.
            bool hasLepas = current != null;
            if (hasLepas) _actions.Add(new RowAction { Item = null });

            if (emptyText != null)
                emptyText.gameObject.SetActive(_actions.Count == 0);

            if (rowContainer == null || rowPrefab == null) return;

            while (_rows.Count < _actions.Count)
                _rows.Add(Instantiate(rowPrefab, rowContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _actions.Count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i; // capture
                var action = _actions[i];

                if (action.Item != null)
                    _rows[i].Setup(action.Item.Name, action.Count,
                                   DeltaString(action.Item, current), () => Pick(index));
                else
                    _rows[i].Setup("Lepas", 0,
                                   DeltaString(null, current), () => Pick(index));
            }

            SelectRow(_actions.Count > 0 ? 0 : -1);
        }

        // The "+2 ATK  -1 DEF" preview: candidate's bonuses minus the current piece's.
        // Gains in gold, losses in deep red, zeros skipped. Null candidate = unequipping.
        private static string DeltaString(ItemData candidate, ItemData current)
        {
            int atk = (candidate != null ? candidate.AttackBonus  : 0) - (current != null ? current.AttackBonus  : 0);
            int def = (candidate != null ? candidate.DefenseBonus : 0) - (current != null ? current.DefenseBonus : 0);
            int spd = (candidate != null ? candidate.SpeedBonus   : 0) - (current != null ? current.SpeedBonus   : 0);

            var sb = new StringBuilder();
            AppendDelta(sb, atk, "ATK");
            AppendDelta(sb, def, "DEF");
            AppendDelta(sb, spd, "SPD");
            return sb.ToString();
        }

        private static void AppendDelta(StringBuilder sb, int delta, string label)
        {
            if (delta == 0) return;
            if (sb.Length > 0) sb.Append("  ");
            string hex  = delta > 0 ? GainHex : LossHex;
            string sign = delta > 0 ? "+" : "";
            sb.Append($"<color=#{hex}>{sign}{delta} {label}</color>");
        }

        // --- Selecting & picking ---

        private void MoveSelection(int dir)
        {
            if (_actions.Count == 0) return;
            SelectRow((_selected + dir + _actions.Count) % _actions.Count);
        }

        private void SelectRow(int index)
        {
            _selected = index;
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetSelected(i == index && i < _actions.Count);
        }

        private void PickSelected()
        {
            if (_selected >= 0 && _selected < _actions.Count) Pick(_selected);
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= _actions.Count) return;
            var inv = InventorySystem.Instance;
            if (inv == null) { Close(); return; }

            var action = _actions[index];
            if (action.Item != null)
                inv.EquipItem(action.Item, _characterId, _jimatIndex);
            else
                inv.UnequipItem(_characterId, _slot, _jimatIndex == 0 ? 1 : _jimatIndex);

            // InventoryPanel hears OnInventoryChanged and redraws the slots behind us.
            Close();
        }
    }
}
