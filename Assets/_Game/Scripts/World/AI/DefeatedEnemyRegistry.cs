using System;
using System.Collections.Generic;
using UnityEngine;
using Nusantara.SaveSystem;

/// <summary>
/// Static registry of overworld-enemy IDs that have been defeated in battle,
/// tracked PER REGION (per overworld scene name) so revisiting an old area
/// remembers which enemies you already killed.
///
/// Why static (not a MonoBehaviour field): every battle reloads the Overworld
/// scene, destroying every <see cref="OverworldEnemyController"/>. Persisting ids
/// in a static collection survives scene reloads transparently (the AppDomain
/// holds them). The save system serializes the whole multi-region map to disk via
/// <see cref="Export"/> / <see cref="Import"/>.
///
/// Per id, per region we store:
///   1. Membership — drives <see cref="IsDefeated"/> (each enemy checks it in Awake).
///   2. Defeat position — read by GameController on overworld load to spawn bone markers.
///
/// "Current region" model: GameController calls <see cref="SetCurrentRegion"/> on every
/// overworld load. All the legacy single-region methods (<see cref="IsDefeated"/>,
/// <see cref="MarkDefeated(string,Vector3)"/>, <see cref="Clear"/>, <see cref="Count"/>,
/// <see cref="DefeatPositions"/>) operate on the current region, so existing callers
/// are unchanged.
///
/// Lifetime:
///   - Battle → Overworld (same region) : preserved (defeated stay defeated, markers respawn).
///   - Region → different region        : current region SWITCHES (no longer wiped — kills persist).
///   - Rest action                       : <see cref="Clear"/> wipes the CURRENT region only.
///   - New game                          : <see cref="ClearAll"/> wipes everything.
///   - Load game                         : <see cref="Import"/> replaces everything.
///   - Editor recompile / app restart    : statics drop; restored from save on load.
/// </summary>
public static class DefeatedEnemyRegistry
{
    private class RegionRecord
    {
        public readonly HashSet<string> Defeated = new HashSet<string>();
        public readonly Dictionary<string, Vector3> Positions = new Dictionary<string, Vector3>();
    }

    private static readonly Dictionary<string, RegionRecord> regions = new Dictionary<string, RegionRecord>();
    private static string currentRegion = string.Empty;

    /// <summary>Fires whenever an id is added or a region is cleared/imported.</summary>
    public static event Action OnRegistryChanged;

    // ── Region management ──────────────────────────────────────────────────────

    /// <summary>
    /// Sets the active region (overworld scene name). Called by GameController on every
    /// overworld scene load. Creates the record if this region hasn't been seen yet.
    /// Does NOT clear — kills persist across region visits.
    /// </summary>
    public static void SetCurrentRegion(string sceneName)
    {
        currentRegion = sceneName ?? string.Empty;
        if (!regions.ContainsKey(currentRegion))
            regions[currentRegion] = new RegionRecord();
    }

    private static RegionRecord Current
    {
        get
        {
            if (!regions.TryGetValue(currentRegion, out var rec))
            {
                rec = new RegionRecord();
                regions[currentRegion] = rec;
            }
            return rec;
        }
    }

    // ── Queries / mutations (operate on the current region) ─────────────────────

    /// <summary>True if the given id has been recorded as defeated in the current region.</summary>
    public static bool IsDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return false;
        return Current.Defeated.Contains(enemyId);
    }

    /// <summary>
    /// Position-aware add. Records the id AND the spot where the enemy fell (used to
    /// spawn bone markers). No-op for null/empty ids.
    /// </summary>
    public static void MarkDefeated(string enemyId, Vector3 defeatPosition)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        Current.Defeated.Add(enemyId);
        Current.Positions[enemyId] = defeatPosition;
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Membership-only add (no position → no bone marker). No-op for null/empty ids.</summary>
    public static void MarkDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        Current.Defeated.Add(enemyId);
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Wipe the CURRENT region's defeated ids and positions (the rest action).</summary>
    public static void Clear()
    {
        Current.Defeated.Clear();
        Current.Positions.Clear();
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Wipe EVERY region. Use on New Game.</summary>
    public static void ClearAll()
    {
        regions.Clear();
        if (!string.IsNullOrEmpty(currentRegion))
            regions[currentRegion] = new RegionRecord();
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Defeated count in the current region. Useful for debug overlays.</summary>
    public static int Count => Current.Defeated.Count;

    /// <summary>Read-only view of the current region's defeat positions (for bone markers).</summary>
    public static IReadOnlyDictionary<string, Vector3> DefeatPositions => Current.Positions;

    // ── Save / load ─────────────────────────────────────────────────────────────

    /// <summary>Serializes every region into the world save section.</summary>
    public static WorldSaveData Export()
    {
        var world = new WorldSaveData();
        foreach (var kvp in regions)
        {
            var region = new RegionSaveData { sceneName = kvp.Key };
            var rec = kvp.Value;
            foreach (var id in rec.Defeated)
            {
                bool hasPos = rec.Positions.TryGetValue(id, out var pos);
                region.defeated.Add(new DefeatedEnemySaveData
                {
                    id          = id,
                    hasPosition = hasPos,
                    position    = hasPos ? (Vec3)pos : default
                });
            }
            world.regions.Add(region);
        }
        return world;
    }

    /// <summary>Replaces all regions from a loaded world save section.</summary>
    public static void Import(WorldSaveData world)
    {
        regions.Clear();
        if (world?.regions != null)
        {
            foreach (var region in world.regions)
            {
                var rec = new RegionRecord();
                foreach (var e in region.defeated)
                {
                    rec.Defeated.Add(e.id);
                    if (e.hasPosition) rec.Positions[e.id] = e.position;
                }
                regions[region.sceneName ?? string.Empty] = rec;
            }
        }
        OnRegistryChanged?.Invoke();
    }
}
