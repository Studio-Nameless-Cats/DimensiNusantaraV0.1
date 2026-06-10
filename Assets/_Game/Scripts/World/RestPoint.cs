using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [Tooltip("Key the player presses while overlapping the rest point to rest (new Input System).")]
    [SerializeField] private Key restKey = Key.E;

    [Header("Autosave")]
    [Tooltip("Save the game when the player rests (checkpoint / bonfire pattern).")]
    [SerializeField] private bool autosaveOnRest = true;
    [Tooltip("Which save slot the rest autosave writes to (0..2).")]
    [SerializeField] private int autosaveSlot = 0;

    [Header("World reset")]
    [Tooltip("Reload the overworld after resting so defeated enemies VISIBLY respawn immediately " +
             "(bonfire pattern). When autosave is on, reloads from the fresh save so the player " +
             "stays at the rest point. When autosave is off, reloads the current scene (player " +
             "returns to the scene's default spawn). Turn off to only clear the data — enemies " +
             "then reappear on the next natural scene load (after a battle / zone change).")]
    [SerializeField] private bool reloadWorldOnRest = true;

    [Header("References (optional)")]
    [Tooltip("PartySystem to heal. If blank, we find one in the scene the first time the player rests.")]
    [SerializeField] private PartySystem party;

    [Header("Prompt UI (optional)")]
    [Tooltip("The 'Press E to Rest' prompt. Slides in when the player's near, out when they leave. " +
             "Start its GameObject DISABLED in the scene so it begins hidden. Leave blank to skip.")]
    [SerializeField] private Nusantara.UI.Motion.UIAnimator restPrompt;

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
        // slide the prompt in + start its idle loop
        if (restPrompt != null) restPrompt.Show();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInside = false;
        // slide the prompt back out, then it deactivates itself
        if (restPrompt != null) restPrompt.Hide();
    }

    void Update()
    {
        if (!playerInside) return;

        // Only allow rest during free roam — don't let the player rest mid-dialog
        // or mid-cutscene.
        if (GameController.Instance != null && GameController.Instance.State != GameState.FreeRoam)
            return;

        var kb = Keyboard.current;
        if (kb == null || !kb[restKey].wasPressedThisFrame) return;

        DoRest();
    }

    // ── Rest action ──────────────────────────────────────────────────────────

    private void DoRest()
    {
        // prompt's job is done the moment they rest - slide it away. on the reload
        // path the scene reload destroys it mid-slide, but DOTween's SetLink kills
        // the tween cleanly, so this is safe either way.
        if (restPrompt != null) restPrompt.Hide();

        // 1. Heal the party.
        if (party == null) party = FindFirstObjectByType<PartySystem>();
        if (party != null) party.HealAll();
        else Debug.LogWarning("[RestPoint] No PartySystem found in scene — skipping party heal.");

        // 2. Wipe defeated-enemy registry so all enemies respawn next scene load.
        //    Bone markers are spawned from the same registry, so they go too.
        DefeatedEnemyRegistry.Clear();

        // 3. Fire the rest event. Phase B's TimeOfDay subscribes here.
        OnRestTaken?.Invoke();

        // 4. Autosave (checkpoint/bonfire pattern). Rest only happens in FreeRoam,
        //    so this is always a safe save point.
        bool saved = false;
        if (autosaveOnRest)
        {
            saved = SaveSystem.Save(autosaveSlot);
            Debug.Log(saved
                ? $"[RestPoint] Autosaved to slot {autosaveSlot}."
                : $"[RestPoint] Autosave to slot {autosaveSlot} failed — see error above.");
        }

        Debug.Log($"[RestPoint] Rested at '{name}'. Party healed, current-region enemies cleared.");

        // 5. World reset. Clearing the registry above only wipes the DATA; enemies that
        //    already SetActive(false) in Awake won't re-check it until the scene reloads.
        //    Reload so they visibly respawn now.
        if (reloadWorldOnRest)
        {
            if (saved)
            {
                // Reload from the fresh save: respawns enemies (cleared registry was
                // captured into the save) AND restores the player at the rest point.
                Debug.Log($"[RestPoint] Reloading world from slot {autosaveSlot} — enemies respawn, position restored.");
                SaveSystem.Load(autosaveSlot);
            }
            else
            {
                // No save to reload from — reload the active scene directly so enemies
                // still respawn. Player returns to the scene's default spawn point.
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Debug.Log($"[RestPoint] Reloading scene '{active}' — enemies respawn (no autosave; player to default spawn).");
                UnityEngine.SceneManagement.SceneManager.LoadScene(active);
            }
        }
    }
}
