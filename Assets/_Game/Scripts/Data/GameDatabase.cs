using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central asset registry that maps stable string ids → ScriptableObject assets.
/// The save system stores ids only (never object references); on load it asks the
/// database to resolve each id back to the live asset.
///
/// Why this exists:
///   You cannot serialize a reference to a <see cref="CharacterData"/> asset. The
///   save file stores its <see cref="CharacterData.Id"/>; this database turns that
///   id back into the asset so a <see cref="PartyMember"/> can be rebuilt.
///
/// ── Unity setup (editor walkthrough — do later) ──────────────────────────────
///   1. Create the asset: Right-click in Project → RPG → Game Database.
///   2. Put it in a folder named exactly "Resources" (e.g.
///      Assets/_Game/Resources/GameDatabase.asset) and name it "GameDatabase"
///      so <see cref="Instance"/> can Resources.Load it at runtime.
///   3. Drag every player/recruitable CharacterData into the Characters list.
///      (Enemies are tracked by their own EnemyId string and don't need entries.)
///
/// Add future asset types (items, skills, quests) as extra lists + lookups here.
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "RPG/Game Database")]
public class GameDatabase : ScriptableObject
{
    // ── Runtime singleton (Resources-loaded, no scene wiring needed) ──────────
    private static GameDatabase _instance;

    /// <summary>
    /// Lazily loads the GameDatabase asset from a Resources folder. Returns null
    /// (with a clear error) if no asset named "GameDatabase" exists in Resources.
    /// </summary>
    public static GameDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameDatabase>("GameDatabase");
                if (_instance == null)
                    Debug.LogError("[GameDatabase] No 'GameDatabase' asset found in a Resources folder. " +
                                   "Create one (RPG → Game Database) and place it at Assets/_Game/Resources/GameDatabase.asset.");
                else
                    _instance.BuildLookup();
            }
            return _instance;
        }
    }

    [Header("Characters")]
    [Tooltip("Every player / recruitable character. Each must have a unique Id.")]
    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    private Dictionary<string, CharacterData> _characterById;

    // ── Lookup ────────────────────────────────────────────────────────────────

    private void BuildLookup()
    {
        _characterById = new Dictionary<string, CharacterData>();
        foreach (var c in characters)
        {
            if (c == null || string.IsNullOrEmpty(c.Id)) continue;
            if (_characterById.ContainsKey(c.Id))
            {
                Debug.LogWarning($"[GameDatabase] Duplicate character id '{c.Id}' ({c.Name}) — only the first is kept.");
                continue;
            }
            _characterById[c.Id] = c;
        }
    }

    /// <summary>Resolves a saved id back to its CharacterData asset, or null if unknown.</summary>
    public CharacterData GetCharacter(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_characterById == null) BuildLookup();
        return _characterById.TryGetValue(id, out var data) ? data : null;
    }
}
