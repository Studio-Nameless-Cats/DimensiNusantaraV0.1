using UnityEngine;

/// <summary>
/// Stand still at the current position for <c>EnemyAIData.IdleWaitAtWaypoint</c>
/// seconds, then resume patrolling.
///
/// Used both as the very first state (so a freshly-spawned enemy doesn't dart
/// off immediately) and as the pause between patrol legs.
///
/// If the enemy has no waypoints assigned, Idle loops back to itself so the
/// enemy just stands in place — no NRE on an empty patrol path.
/// </summary>
public class IdleState : IEnemyState
{
    private float waitTimer;

    public void Enter(OverworldEnemyController enemy)
    {
        waitTimer = enemy.AIData.IdleWaitAtWaypoint;
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

        // Keep gravity ticking so we stay glued to the floor.
        enemy.ApplyGravityOnly();

        waitTimer -= Time.deltaTime;
        if (waitTimer > 0f) return;

        // Wait done — go patrol the next waypoint (or re-enter Idle if no path).
        if (enemy.HasWaypoints)
            enemy.ChangeState(enemy.PatrolState);
        else
            enemy.ChangeState(enemy.IdleState); // re-arm the timer
    }

    public void Exit(OverworldEnemyController enemy) { }
}
