using UnityEngine;

// What kind of item this is. Drives the inventory tab filtering and the tag chip
// on the tooltip. OBAT = consumables, PERANG = gear you equip, BAHAN = crafting junk.
public enum ItemCategory
{
    Obat,    // medicine / consumables
    Perang,  // weapons, armor, charms
    Bahan    // materials, quest bits, stuff you just carry
}

// What happens when you PAKAI a consumable. None = the item does nothing on use
// (materials, or gear whose "use" is equipping it instead).
public enum ItemEffectType
{
    None,
    HealHp,     // restore HP
    RestoreMp   // restore MP
}

// Who a consumable hits when used from the inventory.
public enum ItemEffectScope
{
    OneMember,  // just the character currently shown in the panel
    WholeParty  // everyone
}

// Which equipment slot a PERANG item goes into. None = not equippable.
public enum EquipSlot
{
    None,
    Senjata,  // weapon
    Zirah,    // armor
    Jimat     // charm (characters have TWO jimat slots)
}

// A ScriptableObject holding everything about one item type. The inventory itself
// just tracks {ItemData, count} pairs; all the meaning lives here.
// Make one with: Right-click in Project -> RPG -> Item Data
[CreateAssetMenu(fileName = "New Item", menuName = "RPG/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id used by the save system. Auto-assigned in the editor - " +
             "do NOT change it once a build ships, or old saves can't find this item.")]
    [SerializeField] private string id;

    [Header("Basic Info")]
    [SerializeField] private string itemName;
    [SerializeField] private ItemCategory category = ItemCategory.Bahan;
    [Tooltip("Short flavor + what it does, shown on the tooltip card. Keep it to 2-3 lines.")]
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Use Effect (OBAT)")]
    [Tooltip("What happens on PAKAI. Leave None for materials and equipment.")]
    [SerializeField] private ItemEffectType effectType = ItemEffectType.None;
    [Tooltip("How much HP/MP the effect restores.")]
    [SerializeField] private int effectAmount = 0;
    [SerializeField] private ItemEffectScope effectScope = ItemEffectScope.OneMember;

    [Header("Equipment (PERANG)")]
    [Tooltip("Slot this goes into when equipped. None = not equippable.")]
    [SerializeField] private EquipSlot equipSlot = EquipSlot.None;
    [Tooltip("Stat bonuses while equipped. Show up in the inventory stat strip and in battle.")]
    [SerializeField] private int attackBonus = 0;
    [SerializeField] private int defenseBonus = 0;
    [SerializeField] private int speedBonus = 0;

    [Header("Stacking")]
    [Tooltip("How many of this item one stack can hold. Equipment usually wants 1.")]
    [SerializeField] private int maxStack = 9;

    public string Id              => id;
    public string Name            => itemName;
    public ItemCategory Category  => category;
    public string Description     => description;
    public Sprite Icon            => icon;
    public ItemEffectType EffectType   => effectType;
    public int EffectAmount       => Mathf.Max(0, effectAmount);
    public ItemEffectScope EffectScope => effectScope;
    public EquipSlot Slot         => equipSlot;
    public int AttackBonus        => attackBonus;
    public int DefenseBonus       => defenseBonus;
    public int SpeedBonus         => speedBonus;
    public int MaxStack           => Mathf.Max(1, maxStack);

    // Quick asks the UI keeps making, answered once here.
    public bool IsEquipment => equipSlot != EquipSlot.None;
    public bool IsUsable    => effectType != ItemEffectType.None || IsEquipment;

    // The tab/tag label, uppercase like the mock (OBAT / PERANG / BAHAN).
    public string CategoryLabel
    {
        get
        {
            switch (category)
            {
                case ItemCategory.Obat:   return "OBAT";
                case ItemCategory.Perang: return "PERANG";
                default:                  return "BAHAN";
            }
        }
    }

    // The gold effect line on the tooltip, e.g. "+20 HP". Empty when the item has no use effect.
    public string EffectLabel
    {
        get
        {
            switch (effectType)
            {
                case ItemEffectType.HealHp:    return $"+{EffectAmount} HP";
                case ItemEffectType.RestoreMp: return $"+{EffectAmount} MP";
                default:                       return "";
            }
        }
    }

    // The muted scope label next to the effect, e.g. "satu anggota tim".
    public string ScopeLabel
    {
        get
        {
            if (effectType == ItemEffectType.None) return "";
            return effectScope == ItemEffectScope.WholeParty ? "seluruh tim" : "satu anggota tim";
        }
    }

#if UNITY_EDITOR
    // Hand this asset a stable GUID the first time it's made or looked at. Editor-only:
    // the id gets baked into the asset and never regenerated at runtime.
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
