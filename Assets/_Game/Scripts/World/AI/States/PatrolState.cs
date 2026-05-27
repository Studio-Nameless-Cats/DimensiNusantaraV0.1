using UnityEngine;

/// <summary>
/// Walk toward the current waypoint at <c>EnemyAIData.MoveSpeed</c>. When
/// within <c>WaypointReachedDistance</c>, advance the index according to
/// <c>EnemyAIData.PatrolMode</c> (Loop or PingPong) and drop into IdleState
/// so the enemy pauses before the next leg.
///
/// The waypoint index and ping-pong direction live on the controller, so the
/// path resumes correctly after Idle → Patrol cycles or future Chase →
/// Investigate → Patrol returns.
/// </summary>
public class PatrolState : IEnemyState
{
    public void Enter(OverworldEnemyController enemy)
    {
        // Clamp the index in case waypoints were edited at runtime.
        if (enemy.CurrentWaypointIndex >= enemy.Waypoints.Length)
            enemy.CurrentWaypointIndex = 0;
    }

    public void Tick(OverworldEnemyController enemy)
    {
        // Spot check — drop into chase the instant the player is visible.
        if (enemy.Perception.TryDetect(out _))
        {
            enemy.AlertBubble?.Show(AlertBubble.BubbleKind.Alert);
            enemy.ChangeState(enemy.ChaseState);
            return;
        }

        if (!enemy.HasWaypoints)
        {
            // Defensive: shouldn't happen because IdleState gates entry, but
            // be safe if someone changes states directly.
            enemy.ChangeState(enemy.IdleState);
            return;
        }

        Transform target = enemy.Waypoints[enemy.CurrentWaypointIndex];
        if (target == null)
        {
            // Skip a missing waypoint rather than NRE.
            AdvanceWaypoint(enemy);
            return;
        }

        // XZ-plane direction to the next waypoint.
        Vector3 to = target.position - enemy.transform.position;
        to.y = 0f;
        float distance = to.magnitude;

        if (distance <= enemy.AIData.WaypointReachedDistance)
        {
            AdvanceWaypoint(enemy);
            enemy.ChangeState(enemy.IdleState); // pause at the waypoint
            return;
        }

        Vector3 dir = to / distance; // already non-zero (checked above)
        enemy.FaceDirection(dir);
        enemy.MoveHorizontal(dir);
    }

    public void Exit(OverworldEnemyController enemy) { }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AdvanceWaypoint(OverworldEnemyController enemy)
    {
        int  count = enemy.Waypoints.Length;
        int  idx   = enemy.CurrentWaypointIndex;

        switch (enemy.AIData.Mode)
        {
            case EnemyAIData.PatrolMode.Loop:
                enemy.CurrentWaypointIndex = (idx + 1) % count;
                break;

            case EnemyAIData.PatrolMode.PingPong:
                if (count <= 1)
                {
                    enemy.CurrentWaypointIndex = 0;
                    break;
                }
                int next = idx + enemy.PatrolDirection;
                if (next >= count || next < 0)
                {
                    // Bounce off the end and step the other direction.
                    enemy.PatrolDirection      = -enemy.PatrolDirection;
                    next                       = idx + enemy.PatrolDirection;
                }
                enemy.CurrentWaypointIndex = next;
                break;
        }
    }
}
