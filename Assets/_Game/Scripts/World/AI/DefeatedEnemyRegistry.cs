using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of overworld-enemy IDs that have been defeated in battle.
///
/// Why static (not a MonoBehaviour field): every battle reloads the Overworld
/// scene, destroying every <see cref="OverworldEnemyController"/>. A naive
/// "SetActive(false)" on the defeated enemy would be lost on the very next
/// scene load — the enemy would respawn at its waypoint as if it had never
/// been fought. Persisting the IDs in a static collection survives scene
/// reloads transparently (the AppDomain holds them).
///
/// What's stored per id:
///   1. Membership (the <see cref="defeated"/> set) — drives the
///      <see cref="IsDefeated"/> check that each enemy runs in Awake.
///   2. Defeat position (the <see cref="defeatPositions"/> dict) — populated
///      by <see cref="MarkDefeated(string, Vector3)"/>. <see cref="GameController"/>
///      reads this on overworld scene-load to spawn bone markers at the exact
///      death sites.
///
/// Lifetime:
///   - Battle  → Overworld (same region) : registry preserved (defeated enemies stay defeated, markers respawn).
///   - Region  → different region        : <see cref="Clear"/> called by GameController; registry empties.
///   - Rest action                       : <see cref="Clear"/> called by RestPoint; all enemies respawn.
///   - Editor recompile / app restart    : statics drop; this is expected.
/// </summary>
public static class DefeatedEnemyRegistry
{
    private static readonly HashSet<string> defeated = new HashSet<string>();
    private static readonly Dictionary<string, Vector3> defeatPositions = new Dictionary<string, Vector3>();

    /// <summary>Fires whenever an id is added or the whole registry is cleared.</summary>
    public static event Action OnRegistryChanged;

    /// <summary>True if the given id has been recorded as defeated.</summary>
    public static bool IsDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return false;
        return defeated.Contains(enemyId);
    }

    /// <summary>
    /// Position-aware add. Records the id AND the spot where the enemy fell —
    /// used to spawn bone markers on the next overworld scene load. No-op for
    /// null/empty ids.
    /// </summary>
    public static void MarkDefeated(string enemyId, Vector3 defeatPosition)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        defeated.Add(enemyId);
        defeatPositions[enemyId] = defeatPosition;
        OnRegistryChanged?.Invoke();
    }

    /// <summary>
    /// Membership-only add. Doesn't record a position, so no bone marker will
    /// spawn for this id. Kept for callers that don't have a meaningful
    /// position (e.g. scripted "this enemy is already dead before the player
    /// shows up" setups).
    /// </summary>
    public static void MarkDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        defeated.Add(enemyId);
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Wipe all defeated ids and positions. Fires <see cref="OnRegistryChanged"/>.</summary>
    public static void Clear()
    {
        defeated.Clear();
        defeatPositions.Clear();
        OnRegistryChanged?.Invoke();
    }

    /// <summary>How many ids are currently recorded as defeated. Useful for debug overlays.</summary>
    public static int Count => defeated.Count;

    /// <summary>
    /// Read-only view of every defeated enemy's death position. Iterate this
    /// (not the full <c>defeated</c> set) when spawning bone markers — only
    /// entries added via <see cref="MarkDefeated(string, Vector3)"/> appear here.
    /// </summary>
    public static IReadOnlyDictionary<string, Vector3> DefeatPositions => defeatPositions;
}
