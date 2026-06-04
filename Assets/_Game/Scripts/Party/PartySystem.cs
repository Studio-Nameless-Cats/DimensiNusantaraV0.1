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

    [Header("Battle Selection")]
    [Tooltip("How many members may be marked ACTIVE (sent into battle) at once. " +
             "Should match the number of player spawn points on the Battle scene's BattleSystem.")]
    [SerializeField] private int maxActiveBattle = 3;

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

    // ── Battle selection (active vs reserve) ──────────────────────────────────

    /// <summary>Max members allowed to be active (sent into battle) at once.</summary>
    public int MaxActiveBattle => maxActiveBattle;

    /// <summary>Members flagged to fight (regardless of HP).</summary>
    public List<PartyMember> ActiveBattleMembers => members.Where(m => m.IsActiveInBattle).ToList();

    /// <summary>Active AND healthy members — what actually spawns into battle.</summary>
    public List<PartyMember> ActiveHealthyBattleMembers
        => members.Where(m => m.IsActiveInBattle && !m.IsFainted).ToList();

    public int ActiveCount  => members.Count(m => m.IsActiveInBattle);
    public bool CanActivateMore => ActiveCount < maxActiveBattle;

    /// <summary>
    /// Set a member's active/reserve state, enforcing the rules:
    ///  • can't exceed <see cref="maxActiveBattle"/> active members;
    ///  • can't deactivate the last remaining active member (someone must fight).
    /// Returns true if the state changed.
    /// </summary>
    public bool SetActive(PartyMember member, bool active)
    {
        if (member == null || !members.Contains(member)) return false;
        if (member.IsActiveInBattle == active) return false;

        if (active)
        {
            if (ActiveCount >= maxActiveBattle) return false;   // party full
        }
        else
        {
            if (ActiveCount <= 1) return false;                 // keep at least one fighter
        }

        member.IsActiveInBattle = active;
        OnPartyUpdated?.Invoke();
        return true;
    }

    /// <summary>Toggle a member's active state under the same rules as <see cref="SetActive"/>.</summary>
    public bool ToggleActive(PartyMember member)
        => member != null && SetActive(member, !member.IsActiveInBattle);

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
                var member = new PartyMember(data, entry.currentHp, entry.currentMp, entry.level, entry.currentExp);
                member.RestoreLoadout(entry.equippedSkillIds);
                member.IsActiveInBattle = entry.isActive;
                members.Add(member);
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
