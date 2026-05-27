using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the player's party of characters.
/// Attach this to the Player GameObject.
/// </summary>
public class PartySystem : MonoBehaviour
{
    [Header("Starting Party")]
    [Tooltip("Drag CharacterData ScriptableObjects here for the player's starting party (max 4).")]
    [SerializeField] private List<CharacterData> startingPartyData;

    private readonly List<PartyMember> members = new List<PartyMember>();

    public event Action OnPartyUpdated;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        foreach (var data in startingPartyData)
        {
            if (data != null)
                members.Add(new PartyMember(data));
        }
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

    /// <summary>Fully restores HP of every party member.</summary>
    public void HealAll()
    {
        foreach (var member in members)
            member.HealFull();

        OnPartyUpdated?.Invoke();
    }
}
