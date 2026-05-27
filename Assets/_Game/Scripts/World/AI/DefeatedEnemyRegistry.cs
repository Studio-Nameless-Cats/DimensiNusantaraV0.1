using System.Collections.Generic;

/// <summary>
/// Static registry of overworld-enemy IDs that have been defeated in battle.
///
/// Why it has to be static (not a MonoBehaviour field): every battle reloads
/// the Overworld scene, which destroys every <see cref="OverworldEnemyController"/>
/// in the scene. A naive "SetActive(false)" on the defeated enemy would be lost
/// on the very next scene load — the enemy would respawn at its waypoint as if
/// it had never been fought. Persisting the IDs in a static collection survives
/// scene reloads transparently (the AppDomain is what holds them).
///
/// Lifetime:
///   - Battle  → Overworld (same region) : registry is preserved (defeated enemies stay defeated).
///   - Region  → different region        : <see cref="Clear"/> is called by GameController
///                                          when it detects an overworld-scene name change,
///                                          so visiting an old area re-populates it.
///   - Editor recompile / app restart    : statics drop; this is expected.
///
/// Each <see cref="OverworldEnemyController"/> reads its assigned <c>enemyId</c> in
/// Awake and disables itself if the registry already contains that id.
/// </summary>
public static class DefeatedEnemyRegistry
{
    private static readonly HashSet<string> defeated = new HashSet<string>();

    /// <summary>True if the given id has been recorded as defeated.</summary>
    public static bool IsDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return false;
        return defeated.Contains(enemyId);
    }

    /// <summary>Add this id to the defeated set. No-op for null/empty.</summary>
    public static void MarkDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        defeated.Add(enemyId);
    }

    /// <summary>Wipe all defeated ids. Called on overworld-region change and by Clear-game-data flows.</summary>
    public static void Clear()
    {
        defeated.Clear();
    }

    /// <summary>How many ids are currently recorded as defeated. Useful for debug overlays.</summary>
    public static int Count => defeated.Count;
}
