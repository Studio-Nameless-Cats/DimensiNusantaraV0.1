using UnityEngine;

/// <summary>
/// Pursue the player at <c>AIData.MoveSpeed</c>.
///
/// Per-tick outcomes:
///   - Within touchRange  → hand off to <see cref="AttackState"/> (starts the battle).
///   - Still in sight     → reset the grace timer and walk toward the player.
///   - Sight lost         → start <c>graceTimer</c>; while it's under
///                          <c>AIData.LoseSightGracePeriod</c> we keep walking
///                          toward the last-known position so brief occlusions
///                          (corners, pillars) don't visibly stop the chase.
///                          Once the grace expires, hand off to
///                          <see cref="InvestigateState"/>.
///   - Player ref null    → fall straight back to Patrol/Idle (the player
///                          GameObject is gone, e.g. mid scene-change).
///
/// The grace period is the cure for the "corner flicker" — without it, an
/// enemy chasing the player around a wall would oscillate Chase↔Investigate
/// every frame as the LOS raycast clips on and off.
/// </summary>
public class ChaseState : IEnemyState
{
    private float graceTimer;

    public void Enter(OverworldEnemyController enemy)
    {
        graceTimer = 0f;
        // Alert "!" bubble + cone tint hook in step 4.
    }

    public void Tick(OverworldEnemyController enemy)
    {
        var player = enemy.Player;
        if (player == null)
        {
            FallBackToPatrol(enemy);
            return;
        }

        // Within touch range → start the battle.
        Vector3 toPlayer = player.transform.position - enemy.transform.position;
        toPlayer.y = 0f;
        float playerDist = toPlayer.magnitude;
        if (playerDist <= enemy.AIData.TouchRange)
        {
            enemy.ChangeState(enemy.AttackState);
            return;
        }

        bool canSee = enemy.Perception.TryDetect(out _);

        if (canSee)
        {
            graceTimer = 0f;
        }
        else
        {
            graceTimer += Time.deltaTime;
            if (graceTimer >= enemy.AIData.LoseSightGracePeriod)
            {
                enemy.AlertBubble?.Show(AlertBubble.BubbleKind.Question);
                enemy.ChangeState(enemy.InvestigateState);
                return;
            }
        }

        // Walk toward the player if we can see them; otherwise walk toward
        // where we last saw them. Keeps motion smooth across brief occlusions.
        Vector3 target  = canSee ? player.transform.position : enemy.Perception.LastKnownPlayerPosition;
        Vector3 toGoal  = target - enemy.transform.position;
        toGoal.y = 0f;
        float goalDist = toGoal.magnitude;
        if (goalDist < 0.0001f) { enemy.ApplyGravityOnly(); return; }

        Vector3 dir = toGoal / goalDist;
        enemy.FaceDirection(dir);
        enemy.MoveHorizontal(dir);
    }

    public void Exit(OverworldEnemyController enemy) { }

    private void FallBackToPatrol(OverworldEnemyController enemy)
    {
        enemy.ChangeState(enemy.HasWaypoints ? (IEnemyState)enemy.PatrolState : enemy.IdleState);
    }
}
