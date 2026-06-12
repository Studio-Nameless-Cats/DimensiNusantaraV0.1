using System.Collections.Generic;
using UnityEngine;

// A central little registry that maps stable string ids to ScriptableObject assets.
// The save system only stores ids (never actual object references); on load it asks this
// database to turn each id back into the real asset.
//
// Why it has to exist:
//   You can't serialize a reference to a CharacterData asset. The save file keeps its
//   CharacterData.Id instead, and this database turns that id back into the asset so a
//   PartyMember can be rebuilt.
//
// Unity setup (an editor task for later):
//   1. Make the asset: Right-click in Project -> RPG -> Game Database.
//   2. Put it in a folder named exactly "Resources" (e.g.
//      Assets/_Game/Resources/GameDatabase.asset) and name it "GameDatabase" so
//      Instance can Resources.Load it at runtime.
//   3. Drag every player / recruitable CharacterData into the Characters list.
//      (Enemies are tracked by their own EnemyId string and don't need entries.)
//
// Got more asset types later (items, skills, quests)? Add extra lists + lookups here.
[CreateAssetMenu(fileName = "GameDatabase", menuName = "RPG/Game Database")]
public class GameDatabase : ScriptableObject
{
    // The runtime singleton, loaded from Resources so there's no scene wiring needed.
    private static GameDatabase _instance;

    // Loads the GameDatabase asset from a Resources folder the first time it's needed.
    // Returns null (with a clear error) if there's no asset named "GameDatabase" in Resources.
    public static GameDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameDatabase>("GameDatabase");
                if (_instance == null)
                    Debug.LogError("[GameDatabase] No 'GameDatabase' asset found in a Resources folder. " +
                                   "Make one (RPG -> Game Database) and drop it at Assets/_Game/Resources/GameDatabase.asset.");
                else
                    _instance.BuildLookup();
            }
            return _instance;
        }
    }

    [Header("Characters")]
    [Tooltip("Every player / recruitable character. Each must have a unique Id.")]
    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    [Header("Items")]
    [Tooltip("Every ItemData in the game. Each must have a unique Id. The save system " +
             "uses this to turn saved item ids back into assets.")]
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    private Dictionary<string, CharacterData> _characterById;
    private Dictionary<string, ItemData> _itemById;

    // Builds the id -> asset lookup tables.
    private void BuildLookup()
    {
        _characterById = new Dictionary<string, CharacterData>();
        foreach (var c in characters)
        {
            if (c == null || string.IsNullOrEmpty(c.Id)) continue;
            if (_characterById.ContainsKey(c.Id))
            {
                Debug.LogWarning($"[GameDatabase] Two characters share id '{c.Id}' ({c.Name}). Keeping only the first one.");
                continue;
            }
            _characterById[c.Id] = c;
        }

        _itemById = new Dictionary<string, ItemData>();
        foreach (var i in items)
        {
            if (i == null || string.IsNullOrEmpty(i.Id)) continue;
            if (_itemById.ContainsKey(i.Id))
            {
                Debug.LogWarning($"[GameDatabase] Two items share id '{i.Id}' ({i.Name}). Keeping only the first one.");
                continue;
            }
            _itemById[i.Id] = i;
        }
    }

    // Turns a saved id back into its CharacterData asset, or null if we don't know it.
    public CharacterData GetCharacter(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_characterById == null) BuildLookup();
        return _characterById.TryGetValue(id, out var data) ? data : null;
    }

    // Same deal for items: saved id in, ItemData asset out (or null).
    public ItemData GetItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_itemById == null) BuildLookup();
        return _itemById.TryGetValue(id, out var data) ? data : null;
    }
}
