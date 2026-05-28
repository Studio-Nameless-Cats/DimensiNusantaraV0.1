using UnityEngine;

/// <summary>
/// Visual marker placed at the spot where the player defeated an overworld
/// enemy. Spawned by <see cref="GameController"/> on every overworld scene
/// load, one per entry in <see cref="DefeatedEnemyRegistry.DefeatPositions"/>.
///
/// The component itself is intentionally minimal — it just records which
/// enemy id this marker represents so we can find / clean up specific
/// markers later (e.g. if a story event resurrects a foe). All the *visual*
/// lives on the prefab (sprite, mesh, particle, etc.).
///
/// Lifetime: lives until the scene unloads. The registry decides whether a
/// marker should reappear after the next scene load; <c>BoneMarker</c>
/// itself is stateless across scenes and does not persist.
/// </summary>
public class BoneMarker : MonoBehaviour
{
    /// <summary>EnemyId of the defeated enemy this marker represents. Set by GameController on spawn.</summary>
    public string EnemyId { get; private set; }

    /// <summary>Called immediately after Instantiate. Stores the source enemy id.</summary>
    public void Initialize(string enemyId)
    {
        EnemyId = enemyId;
    }
}
