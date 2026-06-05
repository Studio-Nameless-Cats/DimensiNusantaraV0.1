using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nusantara.SaveSystem;

// Looks after the player's party of characters. Stick this on the Player GameObject.
public class PartySystem : MonoBehaviour
{
    [Header("Starting Party")]
    [Tooltip("Drag CharacterData ScriptableObjects here for the player's starting party (max 4).")]
    [SerializeField] private List<CharacterData> startingPartyData;

    [Header("Battle Selection")]
    [Tooltip("How many members may be marked ACTIVE (sent into battle) at once. " +
             "Should match the number of player spawn points on the Battle scene's BattleSystem.")]
    [SerializeField] private int maxActiveBattle = 3;

    // This is STATIC on purpose so the party survives scene reloads. Every battle reloads
    // the overworld scene, which destroys and recreates the Player (and this component
    // with it). If 'members' were a normal instance field, it'd rebuild to full HP every
    // time you came back from a battle, throwing away all the damage taken AND any members
    // you recruited. Keeping the list static (same trick as DefeatedEnemyRegistry) lets the
    // PartyMember objects live through scene loads, so damage and recruits actually stick.
    private static readonly List<PartyMember> members = new List<PartyMember>();
    private static bool initialized;

    public event Action OnPartyUpdated;

    // Statics survive the editor's "Enter Play Mode (no domain reload)" option, so clear
    // them at the start of every Play session, otherwise a fresh run inherits a stale party.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticsOnPlay()
    {
        members.Clear();
        initialized = false;
    }

    void Awake()
    {
        // Build the starting party only ONCE per game. On later scene loads the party's
        // already sitting in the static list (with its current HP), so leave it alone.
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

    // Wipes the persistent party so the next scene rebuilds the starting party. Call on New Game.
    public static void ResetParty()
    {
        members.Clear();
        initialized = false;
    }

    // --- Asking about the party ---

    public List<PartyMember> Members         => members;
    public List<PartyMember> HealthyMembers  => members.Where(m => !m.IsFainted).ToList();
    public bool HasHealthyMember             => members.Any(m => !m.IsFainted);
    public int Count                         => members.Count;

    // --- Picking who fights (active vs reserve) ---

    // Most members that can be active (sent into battle) at once.
    public int MaxActiveBattle => maxActiveBattle;

    // Members marked to fight, no matter their HP.
    public List<PartyMember> ActiveBattleMembers => members.Where(m => m.IsActiveInBattle).ToList();

    // Active AND healthy members. This is who actually spawns into battle.
    public List<PartyMember> ActiveHealthyBattleMembers
        => members.Where(m => m.IsActiveInBattle && !m.IsFainted).ToList();

    public int ActiveCount  => members.Count(m => m.IsActiveInBattle);
    public bool CanActivateMore => ActiveCount < maxActiveBattle;

    // Mark a member active or benched, with two rules:
    //  - can't go over maxActiveBattle active members;
    //  - can't bench the last active member (somebody has to fight).
    // Returns true if the state actually changed.
    public bool SetActive(PartyMember member, bool active)
    {
        if (member == null || !members.Contains(member)) return false;
        if (member.IsActiveInBattle == active) return false;

        if (active)
        {
            if (ActiveCount >= maxActiveBattle) return false;   // already full
        }
        else
        {
            if (ActiveCount <= 1) return false;                 // gotta keep at least one fighter
        }

        member.IsActiveInBattle = active;
        OnPartyUpdated?.Invoke();
        return true;
    }

    // Flip a member's active state, following the same rules as SetActive.
    public bool ToggleActive(PartyMember member)
        => member != null && SetActive(member, !member.IsActiveInBattle);

    // --- Managing the party ---

    // Add a new character to the party (max 4). Returns true if it actually got added.
    public bool AddMember(CharacterData characterData)
    {
        if (characterData == null) return false;

        if (members.Count >= 4)
        {
            Debug.Log("[PartySystem] Party's full, can't add anyone else.");
            return false;
        }

        members.Add(new PartyMember(characterData));
        OnPartyUpdated?.Invoke();
        return true;
    }

    // Swap the whole party out for one loaded from a save. It looks up each saved
    // characterId in the GameDatabase and rebuilds the members with their saved HP.
    // SaveManager calls this right after the scene loads.
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
                    Debug.LogWarning($"[PartySystem] Saved character id '{entry.characterId}' isn't in the GameDatabase, so we skipped them.");
                    continue;
                }
                var member = new PartyMember(data, entry.currentHp, entry.currentMp, entry.level, entry.currentExp);
                member.RestoreLoadout(entry.equippedSkillIds);
                member.IsActiveInBattle = entry.isActive;
                members.Add(member);
            }
        }

        initialized = true;   // the loaded party is the live party now; don't rebuild it on scene loads
        OnPartyUpdated?.Invoke();
    }

    // Heal every party member back to full HP.
    public void HealAll()
    {
        foreach (var member in members)
            member.HealFull();

        OnPartyUpdated?.Invoke();
    }
}
