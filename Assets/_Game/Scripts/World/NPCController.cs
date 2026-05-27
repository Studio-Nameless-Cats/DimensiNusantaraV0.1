using UnityEngine;

/// <summary>
/// An NPC the player can interact with.
/// If canJoinParty is true, the NPC offers to join the player's party.
/// Otherwise, it displays its dialog lines.
///
/// Setup:
///   1. Add to an NPC GameObject.
///   2. Assign a CharacterData SO.
///   3. Set the GameObject's layer to match PlayerController's npcLayer mask.
///   4. Optionally assign a FollowerController prefab for spawning a follower when recruited.
/// </summary>
public class NPCController : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] private CharacterData characterData;

    [Header("Recruitment")]
    [SerializeField] private bool canJoinParty = false;
    [Tooltip("Prefab with a FollowerController component. Spawned when the NPC joins the party.")]
    [SerializeField] private GameObject followerPrefab;

    [Header("Dialog")]
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogLines;

    // ── State ────────────────────────────────────────────────────────────────
    private bool hasJoined = false;

    // ── Properties ───────────────────────────────────────────────────────────
    public CharacterData CharacterData => characterData;

    // ── Interaction ──────────────────────────────────────────────────────────

    /// <summary>Called by PlayerController when the player presses Interact nearby.</summary>
    public void Interact(PlayerController player)
    {
        if (canJoinParty && !hasJoined)
        {
            GameController.Instance.StartRecruitment(this, player, followerPrefab);
        }
        else
        {
            GameController.Instance.ShowDialog(dialogLines);
        }
    }

    /// <summary>Called by GameController after the player accepts the NPC's request to join.</summary>
    public void OnJoinedParty()
    {
        hasJoined = true;
        gameObject.SetActive(false); // NPC disappears from overworld (now a follower)
    }
}
