using System;
using UnityEngine;

/// <summary>
/// Standalone interactable that lets the player "rest" — heals the party fully
/// and respawns every overworld enemy that was defeated in the current region.
///
/// Why rest exists (design):
///   - Replaces wall-clock respawn timers, which break immersion in a story RPG.
///   - Gives the player agency over when the world resets (the bonfire / inn
///     pattern from Dark Souls / Octopath / Bravely Default).
///   - Provides a natural grind loop: rest → enemies back → fight → rest.
///
/// Setup:
///   1. Empty GameObject in the Overworld scene, drop a trigger Collider on it
///      (BoxCollider with Is Trigger ON, sized to the rest spot — e.g. a
///      campfire or inn doorway).
///   2. Attach this component. Optionally drag a PartySystem reference into
///      <c>party</c>; if blank we find it in the scene at rest-time.
///   3. (Visual) Add a child sprite / mesh for the campfire / inn / shrine.
///
/// Interaction model:
///   - Player overlaps the trigger → debug "press [restKey] to rest" prompt.
///   - Player presses <c>restKey</c> while overlapping AND game is FreeRoam.
///   - One key press = one rest. Player must exit + re-enter to rest again.
///
/// Phase B will hook <see cref="OnRestTaken"/> to advance TimeOfDay.Phase.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RestPoint : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Key the player presses while overlapping the rest point to rest.")]
    [SerializeField] private KeyCode restKey = KeyCode.E;

    [Header("References (optional)")]
    [Tooltip("PartySystem to heal. If blank, we find one in the scene the first time the player rests.")]
    [SerializeField] private PartySystem party;

    /// <summary>Fires after a successful rest. Phase B's TimeOfDay will subscribe here to advance the cycle.</summary>
    public event Action OnRestTaken;

    private bool playerInside;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Reset()
    {
        // Sensible default for designers: the Collider should be a trigger.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInside = true;
        Debug.Log($"[RestPoint] '{name}': press {restKey} to rest.");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInside = false;
    }

    void Update()
    {
        if (!playerInside) return;

        // Only allow rest during free roam — don't let the player rest mid-dialog
        // or mid-cutscene.
        if (GameController.Instance != null && GameController.Instance.State != GameState.FreeRoam)
            return;

        if (!Input.GetKeyDown(restKey)) return;

        DoRest();
    }

    // ── Rest action ──────────────────────────────────────────────────────────

    private void DoRest()
    {
        // 1. Heal the party.
        if (party == null) party = FindFirstObjectByType<PartySystem>();
        if (party != null) party.HealAll();
        else Debug.LogWarning("[RestPoint] No PartySystem found in scene — skipping party heal.");

        // 2. Wipe defeated-enemy registry so all enemies respawn next scene load.
        //    Bone markers are spawned from the same registry, so they go too.
        DefeatedEnemyRegistry.Clear();

        // 3. Fire the rest event. Phase B's TimeOfDay subscribes here.
        OnRestTaken?.Invoke();

        Debug.Log($"[RestPoint] Rested at '{name}'. Party healed, defeated-enemy registry cleared.");
    }
}
