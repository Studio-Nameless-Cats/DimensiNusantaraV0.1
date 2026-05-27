using UnityEngine;

/// <summary>
/// Pure C# perception module owned by an OverworldEnemyController.
///
/// Detection rules (all evaluated on the XZ-plane, ignoring Y):
///   1. Close radius — the player is detected if they are within
///      <c>AIData.CloseRadius</c> of the enemy regardless of facing
///      (someone walking right behind the enemy is noticed).
///   2. Vision cone — the player is detected if they are within
///      <c>AIData.VisionRange</c> AND the angle between the enemy's
///      forward vector and the direction to the player is within
///      <c>AIData.VisionHalfAngle</c>.
///   3. Line of sight — both checks require an unobstructed raycast against
///      the <c>AIData.SightObstacles</c> LayerMask. Walls block sight.
///
/// The enemy's "facing" is just <c>transform.forward</c>, which is kept up to
/// date by <c>OverworldEnemyController.FaceDirection</c> while the enemy moves.
/// </summary>
public class EnemyPerception
{
    private readonly OverworldEnemyController owner;

    public Vector3 LastKnownPlayerPosition { get; private set; }
    public bool    HasPlayerInSight        { get; private set; }

    public EnemyPerception(OverworldEnemyController owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// Runs one detection pass. If the player is spotted, returns true and
    /// fills <paramref name="player"/>; also updates <c>LastKnownPlayerPosition</c>.
    /// </summary>
    public bool TryDetect(out PlayerController player)
    {
        player = owner.Player;
        HasPlayerInSight = false;
        if (player == null) return false;

        Vector3 toPlayer = player.transform.position - owner.transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.0001f) { Spot(player); return true; } // standing inside us

        var data = owner.AIData;

        // (1) Close-radius — 360° detection, still gated by LOS.
        if (dist <= data.CloseRadius && HasLineOfSight(player, dist))
        {
            Spot(player);
            return true;
        }

        // (2) Vision cone.
        if (dist <= data.VisionRange)
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(forward, toPlayer);
                if (angle <= data.VisionHalfAngle && HasLineOfSight(player, dist))
                {
                    Spot(player);
                    return true;
                }
            }
        }

        return false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void Spot(PlayerController player)
    {
        LastKnownPlayerPosition = player.transform.position;
        HasPlayerInSight        = true;
    }

    /// <summary>
    /// True if nothing in <c>SightObstacles</c> blocks the line from the enemy
    /// to the player. Origin and target are lifted slightly off the ground so
    /// the floor collider doesn't always block the ray.
    /// </summary>
    private bool HasLineOfSight(PlayerController player, float dist)
    {
        const float eyeHeight = 0.5f;

        Vector3 origin = owner.transform.position    + Vector3.up * eyeHeight;
        Vector3 target = player.transform.position   + Vector3.up * eyeHeight;
        Vector3 dir    = target - origin;
        float   rayLen = dir.magnitude;
        if (rayLen < 0.0001f) return true;
        dir /= rayLen;

        return !Physics.Raycast(origin, dir, rayLen, owner.AIData.SightObstacles);
    }
}
