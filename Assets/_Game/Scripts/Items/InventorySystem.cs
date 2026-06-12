using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nusantara.SaveSystem;

// The party's bag plus everyone's equipped gear. Stick this on the Player GameObject
// next to PartySystem.
//
// Same static-data trick as PartySystem: every battle reloads the overworld scene,
// destroying and recreating the Player. If the bag were a normal instance field you'd
// come back from every fight with a fresh inventory. Keeping the lists static lets the
// items survive scene loads; the MonoBehaviour is just the scene-facing shell.
//
// Saving: implements ISaveParticipant and stashes its own JSON blob under the
// "inventory" module key, so the core SaveData never needs touching.
public class InventorySystem : MonoBehaviour, ISaveParticipant
{
    // One pile of the same item. The bag is just a list of these.
    public class ItemStack
    {
        public ItemData Data;
        public int Count;

        public ItemStack(ItemData data, int count)
        {
            Data  = data;
            Count = count;
        }
    }

    // What one character is wearing. Two jimat slots, hence the index on jimat.
    public class Equipment
    {
        public ItemData Senjata;
        public ItemData Zirah;
        public ItemData Jimat1;
        public ItemData Jimat2;

        public IEnumerable<ItemData> All()
        {
            if (Senjata != null) yield return Senjata;
            if (Zirah   != null) yield return Zirah;
            if (Jimat1  != null) yield return Jimat1;
            if (Jimat2  != null) yield return Jimat2;
        }

        public bool IsEmpty => Senjata == null && Zirah == null && Jimat1 == null && Jimat2 == null;
    }

    [Header("Capacity")]
    [Tooltip("Max TOTAL items the bag holds (sum of all stack counts). The panel shows this as '24 / 60'.")]
    [SerializeField] private int capacity = 60;

    [Header("Starting Items")]
    [Tooltip("Items the party starts a new game with. Handy for testing the panel too.")]
    [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

    [Serializable]
    public class StartingItem
    {
        public ItemData item;
        public int count = 1;
    }

    // Static so they survive scene reloads (see the class comment).
    private static readonly List<ItemStack> stacks = new List<ItemStack>();
    private static readonly Dictionary<string, Equipment> equipmentByCharacter = new Dictionary<string, Equipment>();
    private static bool initialized;

    public static InventorySystem Instance { get; private set; }

    // Fired whenever the bag or anyone's equipment changes. The panel listens to this.
    public event Action OnInventoryChanged;

    // Statics survive the editor's "Enter Play Mode (no domain reload)" option, so clear
    // them at the start of every Play session or a fresh run inherits the old bag.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticsOnPlay()
    {
        stacks.Clear();
        equipmentByCharacter.Clear();
        initialized = false;
    }

    void Awake()
    {
        Instance = this;

        // Seed the starting items only ONCE per game; later scene loads keep the live bag.
        if (initialized) return;
        foreach (var s in startingItems)
        {
            if (s != null && s.item != null && s.count > 0)
                Add(s.item, s.count);
        }
        initialized = true;
    }

    void OnEnable()  => SaveManager.Register(this);

    void OnDisable()
    {
        SaveManager.Unregister(this);
        if (Instance == this) Instance = null;
    }

    // Wipes everything so the next scene re-seeds the starting items. Call on New Game.
    public static void ResetInventory()
    {
        stacks.Clear();
        equipmentByCharacter.Clear();
        initialized = false;
    }

    // --- Asking about the bag ---

    public IReadOnlyList<ItemStack> Stacks => stacks;
    public int Capacity   => capacity;
    public int TotalCount => stacks.Sum(s => s.Count);

    // Stacks filtered for one tab. Pass null for SEMUA.
    public List<ItemStack> GetStacks(ItemCategory? category)
    {
        if (category == null) return new List<ItemStack>(stacks);
        return stacks.Where(s => s.Data != null && s.Data.Category == category.Value).ToList();
    }

    public int GetCount(ItemData item)
    {
        var stack = FindStack(item);
        return stack != null ? stack.Count : 0;
    }

    private ItemStack FindStack(ItemData item)
        => stacks.FirstOrDefault(s => s.Data == item);

    // --- Adding & removing ---

    // Put items in the bag. Respects total capacity and the item's max stack size.
    // Returns how many actually went in (could be less than asked, or 0 if full).
    public int Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return 0;

        int room = Mathf.Max(0, capacity - TotalCount);
        if (room == 0) return 0;

        var stack = FindStack(item);
        int stackRoom = stack != null ? item.MaxStack - stack.Count : item.MaxStack;
        int accepted = Mathf.Min(count, room, Mathf.Max(0, stackRoom));
        if (accepted <= 0) return 0;

        if (stack != null) stack.Count += accepted;
        else               stacks.Add(new ItemStack(item, accepted));

        OnInventoryChanged?.Invoke();
        return accepted;
    }

    // Take items out of the bag. Returns true if there were enough to remove.
    public bool Remove(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        var stack = FindStack(item);
        if (stack == null || stack.Count < count) return false;

        stack.Count -= count;
        if (stack.Count <= 0) stacks.Remove(stack);

        OnInventoryChanged?.Invoke();
        return true;
    }

    // --- Using consumables ---

    // PAKAI on an OBAT item. 'target' is the character shown in the panel; party-wide
    // effects ignore it and hit everyone. Returns true if the item actually got consumed.
    public bool UseItem(ItemData item, PartyMember target, PartySystem party)
    {
        if (item == null || GetCount(item) <= 0) return false;
        if (item.EffectType == ItemEffectType.None) return false;

        var targets = new List<PartyMember>();
        if (item.EffectScope == ItemEffectScope.WholeParty && party != null)
            targets.AddRange(party.Members);
        else if (target != null)
            targets.Add(target);

        if (targets.Count == 0) return false;

        foreach (var member in targets)
        {
            switch (item.EffectType)
            {
                case ItemEffectType.HealHp:    member.Heal(item.EffectAmount);      break;
                case ItemEffectType.RestoreMp: member.RestoreMp(item.EffectAmount); break;
            }
        }

        Remove(item, 1); // fires OnInventoryChanged for us
        return true;
    }

    // --- Battle access (static) ---
    // The battle scene has no Player GO, so Instance is null there - but the bag itself
    // is static and very much still around. These let BattleSystem reach it directly.

    // Every stack with a use effect (the stuff the ITEM command should list).
    public static List<ItemStack> BattleConsumables()
        => stacks.Where(s => s.Data != null && s.Count > 0
                          && s.Data.EffectType != ItemEffectType.None).ToList();

    // Take one of an item out of the bag. Returns false if it's not there anymore.
    public static bool ConsumeOne(ItemData item)
    {
        if (item == null) return false;
        var stack = stacks.FirstOrDefault(s => s.Data == item);
        if (stack == null || stack.Count <= 0) return false;

        stack.Count--;
        if (stack.Count <= 0) stacks.Remove(stack);

        // Poke the panel if an instance happens to be alive (overworld). In battle
        // there's no instance and nobody listening, which is fine.
        Instance?.OnInventoryChanged?.Invoke();
        return true;
    }

    // --- Equipment ---

    private static Equipment GetOrCreateEquipment(string characterId)
    {
        if (!equipmentByCharacter.TryGetValue(characterId, out var eq))
        {
            eq = new Equipment();
            equipmentByCharacter[characterId] = eq;
        }
        return eq;
    }

    // Read-only peek at what a character is wearing (null entries = empty slots).
    public Equipment GetEquipment(string characterId)
    {
        equipmentByCharacter.TryGetValue(characterId, out var eq);
        return eq ?? new Equipment();
    }

    // PAKAI on a PERANG item: takes one from the bag, puts it in the character's slot,
    // and drops whatever was already there back in the bag. Returns true on success.
    // 'jimatIndex' picks WHICH charm slot when the item is a Jimat: 1 or 2 targets that
    // slot directly (the equip picker uses this when you clicked a specific slab);
    // 0 = auto (fill the first free one, or swap jimat 1 if both are taken).
    public bool EquipItem(ItemData item, string characterId, int jimatIndex = 0)
    {
        if (item == null || !item.IsEquipment || string.IsNullOrEmpty(characterId)) return false;
        if (GetCount(item) <= 0) return false;

        var eq = GetOrCreateEquipment(characterId);

        // Figure out which slot it lands in and what comes off.
        ItemData removed;
        switch (item.Slot)
        {
            case EquipSlot.Senjata:
                removed = eq.Senjata; eq.Senjata = item; break;
            case EquipSlot.Zirah:
                removed = eq.Zirah; eq.Zirah = item; break;
            case EquipSlot.Jimat:
                if      (jimatIndex == 1)   { removed = eq.Jimat1; eq.Jimat1 = item; }
                else if (jimatIndex == 2)   { removed = eq.Jimat2; eq.Jimat2 = item; }
                else if (eq.Jimat1 == null) { removed = null;      eq.Jimat1 = item; }
                else if (eq.Jimat2 == null) { removed = null;      eq.Jimat2 = item; }
                else                        { removed = eq.Jimat1; eq.Jimat1 = item; }
                break;
            default:
                return false;
        }

        // Bag swap AFTER the slot logic so we never lose the old piece: take the new one
        // out, then try to put the old one back. If the bag somehow can't take it
        // (shouldn't happen - we just freed a spot) we still keep the new item equipped.
        Remove(item, 1);
        if (removed != null) Add(removed, 1);

        OnInventoryChanged?.Invoke();
        return true;
    }

    // Take a piece off a character and put it back in the bag. 'jimatIndex' picks which
    // charm slot when slot == Jimat (1 or 2). Returns true if something came off.
    public bool UnequipItem(string characterId, EquipSlot slot, int jimatIndex = 1)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        if (!equipmentByCharacter.TryGetValue(characterId, out var eq)) return false;

        ItemData removed = null;
        switch (slot)
        {
            case EquipSlot.Senjata: removed = eq.Senjata; eq.Senjata = null; break;
            case EquipSlot.Zirah:   removed = eq.Zirah;   eq.Zirah   = null; break;
            case EquipSlot.Jimat:
                if (jimatIndex == 2) { removed = eq.Jimat2; eq.Jimat2 = null; }
                else                 { removed = eq.Jimat1; eq.Jimat1 = null; }
                break;
        }

        if (removed == null) return false;

        Add(removed, 1);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // --- Equip stat bonuses (PartyMember reads these, so battle stats include gear) ---
    // Static on purpose: PartyMember is a plain class with no scene access, and the
    // equipment dictionary is static anyway. Safe to call with nothing equipped (returns 0).

    public static int AttackBonusFor(string characterId)  => SumBonus(characterId, i => i.AttackBonus);
    public static int DefenseBonusFor(string characterId) => SumBonus(characterId, i => i.DefenseBonus);
    public static int SpeedBonusFor(string characterId)   => SumBonus(characterId, i => i.SpeedBonus);

    // Everything a character's gear adds, lumped together. The stat strip shows it as "+6".
    public static int TotalBonusFor(string characterId)
        => SumBonus(characterId, i => i.AttackBonus + i.DefenseBonus + i.SpeedBonus);

    private static int SumBonus(string characterId, Func<ItemData, int> pick)
    {
        if (string.IsNullOrEmpty(characterId)) return 0;
        if (!equipmentByCharacter.TryGetValue(characterId, out var eq)) return 0;
        return eq.All().Sum(pick);
    }

    // --- Saving / loading (ISaveParticipant) ---

    public string Key => "inventory";

    // The serializable shape of the module blob. Ids only, never asset references -
    // GameDatabase turns them back into ItemData on load.
    [Serializable]
    private class InventoryModuleData
    {
        public List<StackEntry> items = new List<StackEntry>();
        public List<EquipEntry> equipment = new List<EquipEntry>();
    }

    [Serializable]
    private class StackEntry
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    private class EquipEntry
    {
        public string characterId;
        public string senjataId;
        public string zirahId;
        public string jimat1Id;
        public string jimat2Id;
    }

    public void Capture(SaveData data)
    {
        var blob = new InventoryModuleData();

        foreach (var s in stacks)
        {
            if (s.Data == null || string.IsNullOrEmpty(s.Data.Id)) continue;
            blob.items.Add(new StackEntry { itemId = s.Data.Id, count = s.Count });
        }

        foreach (var pair in equipmentByCharacter)
        {
            if (pair.Value == null || pair.Value.IsEmpty) continue;
            blob.equipment.Add(new EquipEntry
            {
                characterId = pair.Key,
                senjataId   = pair.Value.Senjata != null ? pair.Value.Senjata.Id : "",
                zirahId     = pair.Value.Zirah   != null ? pair.Value.Zirah.Id   : "",
                jimat1Id    = pair.Value.Jimat1  != null ? pair.Value.Jimat1.Id  : "",
                jimat2Id    = pair.Value.Jimat2  != null ? pair.Value.Jimat2.Id  : ""
            });
        }

        data.SetModule(Key, JsonUtility.ToJson(blob));
    }

    public void Restore(SaveData data)
    {
        string json = data.GetModule(Key);
        if (string.IsNullOrEmpty(json)) return; // old save with no inventory - keep what we have

        var blob = JsonUtility.FromJson<InventoryModuleData>(json);
        if (blob == null) return;

        var db = GameDatabase.Instance;

        stacks.Clear();
        equipmentByCharacter.Clear();

        foreach (var entry in blob.items)
        {
            var item = db != null ? db.GetItem(entry.itemId) : null;
            if (item == null)
            {
                Debug.LogWarning($"[InventorySystem] Saved item id '{entry.itemId}' isn't in the GameDatabase, so we skipped it.");
                continue;
            }
            stacks.Add(new ItemStack(item, Mathf.Clamp(entry.count, 1, item.MaxStack)));
        }

        foreach (var entry in blob.equipment)
        {
            if (string.IsNullOrEmpty(entry.characterId)) continue;
            var eq = GetOrCreateEquipment(entry.characterId);
            eq.Senjata = db != null ? db.GetItem(entry.senjataId) : null;
            eq.Zirah   = db != null ? db.GetItem(entry.zirahId)   : null;
            eq.Jimat1  = db != null ? db.GetItem(entry.jimat1Id)  : null;
            eq.Jimat2  = db != null ? db.GetItem(entry.jimat2Id)  : null;
        }

        initialized = true; // the loaded bag is the live bag now; don't re-seed starting items
        OnInventoryChanged?.Invoke();
    }
}
