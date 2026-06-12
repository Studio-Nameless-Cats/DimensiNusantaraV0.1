using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Nusantara.UI.Motion;

namespace Nusantara.UI
{
    // The INVENTARIS panel inside the pause menu (GameMenu shows/hides the root, we run
    // everything inside). Layout per UI_REWORK_PLAN section 3:
    //   - left third: character switcher (Q/E), 4 equipment slots, stat strip
    //   - right two-thirds: category tabs (SEMUA/OBAT/PERANG/BAHAN) + the item chip grid
    //   - tooltip card for the selected item, with Z PAKAI / X BUANG actions
    //
    // Backend lives in InventorySystem (the bag + equipment); we just draw it and forward
    // clicks/keys. Refreshes whenever the panel opens or the inventory changes.
    public class InventoryPanel : MonoBehaviour
    {
        // One category tab button. Order in the list must be: SEMUA, OBAT, PERANG, BAHAN.
        [System.Serializable]
        public class TabRef
        {
            public Button button;
            public TextMeshProUGUI label;
            [Tooltip("The gold underline bar under the active tab. Starts hidden on all but SEMUA.")]
            public GameObject underline;
        }

        // One equipment slot slab on the left column.
        [System.Serializable]
        public class SlotRef
        {
            public EquipSlot slot;
            [Tooltip("Which jimat slot this is (1 or 2). Ignored for Senjata/Zirah.")]
            public int jimatIndex = 1;
            public TextMeshProUGUI itemNameText;
            [Tooltip("The gold diamond icon. Tinted down when the slot is empty.")]
            public Image diamond;
            [Tooltip("Group on the whole slab - dropped to 60% alpha when empty.")]
            public CanvasGroup group;
            [Tooltip("Button on the slab - clicking opens the equip picker for this slot.")]
            public Button button;
        }

        [Header("Character switcher (arrows + Q/E)")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI switcherNameText;
        [Tooltip("Optional - the name pulses when you switch characters. Null = no animation.")]
        [SerializeField] private MotionProfile switchMotion;

        [Header("Equipment (left column)")]
        [Tooltip("The 4 slot slabs: Senjata, Zirah, Jimat 1, Jimat 2.")]
        [SerializeField] private List<SlotRef> equipSlots = new List<SlotRef>();
        [Tooltip("Shown in an empty slot's name spot.")]
        [SerializeField] private string emptySlotText = "- kosong -";
        [Tooltip("The popup that lists compatible gear when a slot slab is clicked.")]
        [SerializeField] private EquipPicker equipPicker;

        [Header("Stat strip")]
        [SerializeField] private TextMeshProUGUI atkText;
        [SerializeField] private TextMeshProUGUI defText;
        [SerializeField] private TextMeshProUGUI spdText;
        [Tooltip("The gold '+6' total gear bonus. Hidden when nothing's equipped.")]
        [SerializeField] private TextMeshProUGUI bonusText;

        [Header("Tabs (order: SEMUA, OBAT, PERANG, BAHAN)")]
        [SerializeField] private List<TabRef> tabs = new List<TabRef>();

        [Header("Item grid")]
        [Tooltip("Layout Group parent the chips get spawned under.")]
        [SerializeField] private Transform chipContainer;
        [SerializeField] private InventoryItemChip chipPrefab;
        [Tooltip("Shown when the current tab has no items.")]
        [SerializeField] private TextMeshProUGUI emptyListText;
        [Tooltip("Optional ScrollRect around the chip list. Wire it and arrow-key navigation " +
                 "keeps the selected chip in view; leave null if the list doesn't scroll.")]
        [SerializeField] private ScrollRect chipScroll;

        [Header("Header")]
        [Tooltip("The '24 / 60' capacity counter next to the INVENTARIS title.")]
        [SerializeField] private TextMeshProUGUI capacityText;

        [Header("Tooltip card")]
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private TextMeshProUGUI tooltipName;
        [Tooltip("The category tag chip's label (OBAT/PERANG/BAHAN).")]
        [SerializeField] private TextMeshProUGUI tooltipTag;
        [SerializeField] private TextMeshProUGUI tooltipDescription;
        [Tooltip("The gold effect line, e.g. '+20 HP'. For equipment we show the stat bonuses here.")]
        [SerializeField] private TextMeshProUGUI tooltipEffect;
        [Tooltip("The muted scope label, e.g. 'satu anggota tim'.")]
        [SerializeField] private TextMeshProUGUI tooltipScope;
        [Tooltip("The 'Z PAKAI' key chip. Hidden for items that can't be used or equipped.")]
        [SerializeField] private GameObject useHint;
        [Tooltip("The 'X BUANG' key chip.")]
        [SerializeField] private GameObject discardHint;
        [Tooltip("Optional pop-in animation for the tooltip card. Null = instant.")]
        [SerializeField] private UIAnimator tooltipAnimator;

        private readonly List<InventoryItemChip> _chips = new List<InventoryItemChip>();
        private List<InventorySystem.ItemStack> _visibleStacks = new List<InventorySystem.ItemStack>();
        private List<PartyMember> _members = new List<PartyMember>();
        private PartySystem _party;

        private int _selectedMember = 0;
        private int _selectedChip   = -1;
        private int _activeTab      = 0;   // 0=SEMUA 1=OBAT 2=PERANG 3=BAHAN

        // Tab index -> category filter. Index 0 (SEMUA) = null = everything.
        private static readonly ItemCategory?[] TabFilters =
            { null, ItemCategory.Obat, ItemCategory.Perang, ItemCategory.Bahan };

        void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => CycleMember(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => CycleMember(+1));

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture
                if (tabs[i].button != null)
                    tabs[i].button.onClick.AddListener(() => SelectTab(index));
            }

            // Clicking a slot slab opens the equip picker for that exact slot.
            foreach (var slotRef in equipSlots)
            {
                var captured = slotRef; // capture
                if (captured.button != null)
                    captured.button.onClick.AddListener(() => OpenPickerFor(captured));
            }
        }

        private void OpenPickerFor(SlotRef slotRef)
        {
            var member = CurrentMember;
            if (equipPicker == null || member == null) return;

            int jimatIndex = slotRef.slot == EquipSlot.Jimat ? slotRef.jimatIndex : 0;
            equipPicker.Open(slotRef.slot, jimatIndex, member.Base.Id);
        }

        void OnEnable()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged += OnInventoryChanged;

            _activeTab = 0;
            Refresh();
        }

        void OnDisable()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged -= OnInventoryChanged;
        }

        void Update()
        {
            // The equip picker owns the keys while it's open - don't fight it.
            if (equipPicker != null && equipPicker.IsOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Q/E cycle the character, same as the loadout screen.
            if (_members.Count > 0)
            {
                if (kb.qKey.wasPressedThisFrame) CycleMember(-1);
                if (kb.eKey.wasPressedThisFrame) CycleMember(+1);
            }

            // Up/down walk the item list, Z uses, X discards.
            if (_visibleStacks.Count > 0)
            {
                if (kb.downArrowKey.wasPressedThisFrame) MoveSelection(+1);
                if (kb.upArrowKey.wasPressedThisFrame)   MoveSelection(-1);
                if (kb.zKey.wasPressedThisFrame) UseSelected();
                if (kb.xKey.wasPressedThisFrame) DiscardSelected();
            }
        }

        // The inventory changed under us (something used, equipped, picked up...). Redraw,
        // keeping the current selection on the same item when it still exists.
        private void OnInventoryChanged() => Refresh(keepSelection: true);

        // --- Building the whole panel ---

        public void Refresh(bool keepSelection = false)
        {
            var inv = InventorySystem.Instance;
            _party  = Object.FindFirstObjectByType<PartySystem>();
            _members = _party != null ? new List<PartyMember>(_party.Members) : new List<PartyMember>();

            if (_members.Count > 0)
                _selectedMember = Mathf.Clamp(_selectedMember, 0, _members.Count - 1);
            else
                _selectedMember = 0;

            // Remember which ITEM was selected (indices shift when stacks empty out).
            ItemData keptItem = null;
            if (keepSelection && _selectedChip >= 0 && _selectedChip < _visibleStacks.Count)
                keptItem = _visibleStacks[_selectedChip].Data;

            RefreshTabs();
            RefreshChips(inv);
            RefreshCharacter();

            if (capacityText != null)
                capacityText.text = inv != null ? $"{inv.TotalCount} / {inv.Capacity}" : "";

            // Re-select: same item if it survived, else first chip, else nothing.
            int newIndex = -1;
            if (keptItem != null)
                newIndex = _visibleStacks.FindIndex(s => s.Data == keptItem);
            if (newIndex < 0 && _visibleStacks.Count > 0)
                newIndex = 0;
            SelectChip(newIndex, animateTooltip: !keepSelection);
        }

        // --- Tabs ---

        public void SelectTab(int index)
        {
            if (index < 0 || index >= TabFilters.Length) return;
            _activeTab = index;
            Refresh();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                bool active = i == _activeTab;
                if (tabs[i].label != null)
                    tabs[i].label.color = active ? NusantaraPalette.Role.FieldBg
                                                 : NusantaraPalette.Role.Muted;
                if (tabs[i].underline != null)
                    tabs[i].underline.SetActive(active);
            }
        }

        // --- The chip grid ---

        private void RefreshChips(InventorySystem inv)
        {
            _visibleStacks = inv != null
                ? inv.GetStacks(TabFilters[Mathf.Clamp(_activeTab, 0, TabFilters.Length - 1)])
                : new List<InventorySystem.ItemStack>();

            if (emptyListText != null)
                emptyListText.gameObject.SetActive(_visibleStacks.Count == 0);

            if (chipContainer == null || chipPrefab == null) return;

            // Pool the chips: spawn extras when the list grows, hide leftovers when it shrinks.
            while (_chips.Count < _visibleStacks.Count)
                _chips.Add(Instantiate(chipPrefab, chipContainer));

            for (int i = 0; i < _chips.Count; i++)
            {
                bool used = i < _visibleStacks.Count;
                _chips[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i; // capture
                var stack = _visibleStacks[i];
                _chips[i].Setup(stack.Data, stack.Count, () => SelectChip(index));
            }
        }

        private void MoveSelection(int dir)
        {
            if (_visibleStacks.Count == 0) return;
            int next = (_selectedChip + dir + _visibleStacks.Count) % _visibleStacks.Count;
            SelectChip(next);
        }

        private void SelectChip(int index, bool animateTooltip = true)
        {
            _selectedChip = index;

            for (int i = 0; i < _chips.Count; i++)
                _chips[i].SetSelected(i == index && i < _visibleStacks.Count);

            bool any = index >= 0 && index < _visibleStacks.Count;
            ShowTooltip(any ? _visibleStacks[index].Data : null, animateTooltip);

            if (any) EnsureChipVisible(index);
        }

        // Scrolls the chip list just enough that the selected chip sits inside the
        // viewport. The ScrollRect handles mouse wheel / drag on its own; this covers
        // arrow-key navigation, which the ScrollRect knows nothing about. No-op when
        // no ScrollRect is wired or everything already fits.
        //
        // Assumes the standard vertical setup (see GUIDE_Inventory_Editor): content
        // pivot/anchor at the TOP, so content.anchoredPosition.y = how far we've
        // scrolled down, and each chip's anchoredPosition.y is negative going down.
        private void EnsureChipVisible(int index)
        {
            if (chipScroll == null || chipScroll.content == null) return;
            if (index < 0 || index >= _chips.Count) return;

            // Chips may have just been (re)built this frame - force the Layout Group to
            // place them now, otherwise we'd read stale positions.
            Canvas.ForceUpdateCanvases();

            var content  = chipScroll.content;
            var viewport = chipScroll.viewport != null
                ? chipScroll.viewport
                : (RectTransform)chipScroll.transform;

            float viewHeight = viewport.rect.height;
            float maxScroll  = Mathf.Max(0f, content.rect.height - viewHeight);
            if (maxScroll <= 0f) return;   // everything fits, nothing to do

            var chip = (RectTransform)_chips[index].transform;

            // Chip's top/bottom measured downward from the content's top edge.
            float chipTop    = -chip.anchoredPosition.y - chip.rect.height * (1f - chip.pivot.y);
            float chipBottom = chipTop + chip.rect.height;

            // The window we can currently see, in the same "distance from top" space.
            float scrollY = content.anchoredPosition.y;

            if (chipTop < scrollY)                        // selected chip is above the window
                scrollY = chipTop;
            else if (chipBottom > scrollY + viewHeight)   // below the window
                scrollY = chipBottom - viewHeight;
            else
                return;                                   // already visible

            content.anchoredPosition = new Vector2(
                content.anchoredPosition.x,
                Mathf.Clamp(scrollY, 0f, maxScroll));
        }

        // --- Tooltip card ---

        private void ShowTooltip(ItemData item, bool animate)
        {
            if (tooltipRoot == null) return;

            if (item == null)
            {
                tooltipRoot.SetActive(false);
                return;
            }

            bool wasHidden = !tooltipRoot.activeSelf;
            tooltipRoot.SetActive(true);

            if (tooltipName        != null) tooltipName.text        = item.Name;
            if (tooltipTag         != null) tooltipTag.text         = item.CategoryLabel;
            if (tooltipDescription != null) tooltipDescription.text = item.Description;

            // Effect line: consumables show their heal ("+20 HP"), equipment shows its
            // stat bonuses ("+4 ATK +2 DEF"), materials show nothing.
            string effect = item.EffectLabel;
            if (item.IsEquipment)
            {
                var parts = new List<string>();
                if (item.AttackBonus  != 0) parts.Add($"+{item.AttackBonus} ATK");
                if (item.DefenseBonus != 0) parts.Add($"+{item.DefenseBonus} DEF");
                if (item.SpeedBonus   != 0) parts.Add($"+{item.SpeedBonus} SPD");
                effect = string.Join("  ", parts);
            }
            if (tooltipEffect != null)
            {
                tooltipEffect.text = effect;
                tooltipEffect.gameObject.SetActive(!string.IsNullOrEmpty(effect));
            }
            if (tooltipScope != null)
            {
                string scope = item.IsEquipment ? "dipakai saat dilengkapi" : item.ScopeLabel;
                tooltipScope.text = scope;
                tooltipScope.gameObject.SetActive(!string.IsNullOrEmpty(scope));
            }

            if (useHint     != null) useHint.SetActive(item.IsUsable);
            if (discardHint != null) discardHint.SetActive(true);

            // Little pop when the card first appears. Re-selections just swap the text.
            if (animate && wasHidden && tooltipAnimator != null)
                tooltipAnimator.PlayIn();
        }

        // --- Z PAKAI / X BUANG ---

        private ItemData SelectedItem
            => _selectedChip >= 0 && _selectedChip < _visibleStacks.Count
                ? _visibleStacks[_selectedChip].Data
                : null;

        private PartyMember CurrentMember
            => _members.Count > 0 ? _members[_selectedMember] : null;

        private void UseSelected()
        {
            var item = SelectedItem;
            var inv  = InventorySystem.Instance;
            if (item == null || inv == null) return;

            if (item.IsEquipment)
            {
                var member = CurrentMember;
                if (member != null)
                    inv.EquipItem(item, member.Base.Id);   // refresh comes via OnInventoryChanged
            }
            else if (item.EffectType != ItemEffectType.None)
            {
                inv.UseItem(item, CurrentMember, _party);
            }
            // BAHAN: nothing to do - the PAKAI hint is hidden for these anyway.
        }

        private void DiscardSelected()
        {
            var item = SelectedItem;
            var inv  = InventorySystem.Instance;
            if (item == null || inv == null) return;

            inv.Remove(item, 1);   // refresh comes via OnInventoryChanged
        }

        // --- Left column: character, equipment, stats ---

        private void CycleMember(int dir)
        {
            if (_members.Count == 0) return;
            int next = (_selectedMember + dir + _members.Count) % _members.Count;
            if (next == _selectedMember) return; // solo party, nothing to switch to

            _selectedMember = next;
            RefreshCharacter();

            // Little pulse on the name so the switch feels snappy. Optional.
            if (switchMotion != null && switcherNameText != null)
                switcherNameText.rectTransform.Pulse(switchMotion);
        }

        private void RefreshCharacter()
        {
            var member = CurrentMember;

            if (switcherNameText != null)
                switcherNameText.text = member != null ? member.Name : "";

            string charId = member != null ? member.Base.Id : null;
            var eq = InventorySystem.Instance != null && charId != null
                ? InventorySystem.Instance.GetEquipment(charId)
                : null;

            foreach (var slotRef in equipSlots)
            {
                ItemData equipped = null;
                if (eq != null)
                {
                    switch (slotRef.slot)
                    {
                        case EquipSlot.Senjata: equipped = eq.Senjata; break;
                        case EquipSlot.Zirah:   equipped = eq.Zirah;   break;
                        case EquipSlot.Jimat:   equipped = slotRef.jimatIndex == 2 ? eq.Jimat2 : eq.Jimat1; break;
                    }
                }

                bool filled = equipped != null;
                if (slotRef.itemNameText != null)
                {
                    slotRef.itemNameText.text  = filled ? equipped.Name : emptySlotText;
                    slotRef.itemNameText.color = filled ? NusantaraPalette.Role.OnDark
                                                        : NusantaraPalette.Role.Muted;
                }
                if (slotRef.diamond != null)
                    slotRef.diamond.color = filled ? NusantaraPalette.Role.FieldBg
                                                   : NusantaraPalette.Role.Muted;
                if (slotRef.group != null)
                    slotRef.group.alpha = filled ? 1f : 0.6f;
            }

            // Stat strip. Attack/Defense/Speed already include the gear bonuses
            // (PartyMember asks InventorySystem), so these are the real numbers.
            if (atkText != null) atkText.text = member != null ? member.Attack.ToString("00")  : "--";
            if (defText != null) defText.text = member != null ? member.Defense.ToString("00") : "--";
            if (spdText != null) spdText.text = member != null ? member.Speed.ToString("00")   : "--";

            if (bonusText != null)
            {
                int bonus = charId != null ? InventorySystem.TotalBonusFor(charId) : 0;
                bonusText.gameObject.SetActive(bonus != 0);
                bonusText.text = $"+{bonus}";
            }
        }
    }
}
