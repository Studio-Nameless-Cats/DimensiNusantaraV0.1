using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nusantara.SaveSystem;

/// <summary>
/// Manages the player's party of characters.
/// Attach this to the Player GameObject.
/// </summary>
public class PartySystem : MonoBehaviour
{
    [Header("Starting Party")]
    [Tooltip("Drag CharacterData ScriptableObjects here for the player's starting party (max 4).")]
    [SerializeField] private List<CharacterData> startingPartyData;

    // ⚠️ STATIC so the party SURVIVES scene reloads. Every battle reloads the
    // overworld scene, which destroys + recreates the Player (and this component).
    // If members were an instance field it would rebuild to full HP on every battle
    // return, throwing away battle damage AND any recruited members. Keeping the
    // list static (same pattern as DefeatedEnemyRegistry) makes the PartyMember
    // objects persist across scene loads — battle damage and recruits stick.
    private static readonly List<PartyMember> members = new List<PartyMember>();
    private static bool initialized;

    public event Action OnPartyUpdated;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    // Statics survive editor "Enter Play Mode (no domain reload)" — clear them at
    // the start of every Play session so a fresh run never inherits a stale party.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticsOnPlay()
    {
        members.Clear();
        initialized = false;
    }

    void Awake()
    {
        // Only build the starting party ONCE per game. On later scene loads the
        // party already exists in the static list (with its current HP) — keep it.
        if (initialized) return;
        BuildStartingParty();
    }

    private void BuildStartingParty()
    {
        members.Clear();
        foreach (var data in startingPartyData)
        {
            if (data != null)
                members.Add(new PartyMember(data));
        }
        initialized = true;
    }

    /// <summary>Wipes the persistent party so the next scene rebuilds the starting party. Call on New Game.</summary>
    public static void ResetParty()
    {
        members.Clear();
        initialized = false;
    }

    // ── Party queries ────────────────────────────────────────────────────────

    public List<PartyMember> Members         => members;
    public List<PartyMember> HealthyMembers  => members.Where(m => !m.IsFainted).ToList();
    public bool HasHealthyMember             => members.Any(m => !m.IsFainted);
    public int Count                         => members.Count;

    // ── Party management ─────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new character to the party (max 4 members).
    /// Returns true if the character was added successfully.
    /// </summary>
    public bool AddMember(CharacterData characterData)
    {
        if (characterData == null) return false;

        if (members.Count >= 4)
        {
            Debug.Log("[PartySystem] Party is full — cannot add more members.");
            return false;
        }

        members.Add(new PartyMember(characterData));
        OnPartyUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// Replaces the entire party from saved data. Resolves each saved characterId
    /// back to its CharacterData via <see cref="GameDatabase"/>, rebuilding members
    /// with their saved HP. Called by SaveManager right after the scene loads.
    /// </summary>
    public void LoadFromSave(List<PartyMemberSaveData> saved)
    {
        members.Clear();

        if (saved != null)
        {
            var db = GameDatabase.Instance;
            foreach (var entry in saved)
            {
                var data = db != null ? db.GetCharacter(entry.characterId) : null;
                if (data == null)
                {
                    Debug.LogWarning($"[PartySystem] Saved character id '{entry.characterId}' not found in GameDatabase — skipped.");
                    continue;
                }
                members.Add(new PartyMember(data, entry.currentHp, entry.currentMp));
            }
        }

        initialized = true;   // loaded party is now the live party; don't rebuild on scene loads
        OnPartyUpdated?.Invoke();
    }

    /// <summary>Fully restores HP of every party member.</summary>
    public void HealAll()
    {
        foreach (var member in members)
            member.HealFull();

        OnPartyUpdated?.Invoke();
    }
}
