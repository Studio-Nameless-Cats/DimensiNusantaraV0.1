using UnityEngine;

/// <summary>
/// Played after a chase loses sight. Two phases:
///
///   Walking        — head to <c>Perception.LastKnownPlayerPosition</c>.
///   LookingAround  — stand at that spot and swing the body left/right for
///                    <c>AIData.InvestigateLookAroundTime</c> seconds so the
///                    vision cone sweeps the area.
///
/// During either phase, a successful re-detect snaps us straight back to
/// ChaseState. On timeout we fall back to Patrol (or Idle if no waypoints).
///
/// The body-swing during look-around writes <c>transform.rotation</c>
/// directly — that's fine because perception just reads transform.forward
/// and we want the cone to sweep with the head.
/// </summary>
public class InvestigateState : IEnemyState
{
    private enum Phase { Walking, LookingAround }

    private const float SwingDegrees = 60f; // half-angle of the look-around sweep

    private Phase      phase;
    private Vector3    targetPos;
    private float      lookTimer;
    private Quaternion baseRot;

    public void Enter(OverworldEnemyController enemy)
    {
        targetPos = enemy.Perception.LastKnownPlayerPosition;
        phase     = Phase.Walking;
    }

    public void Tick(OverworldEnemyController enemy)
    {
        // Spotted the player again? Resume the chase immediately.
        if (enemy.Perception.TryDetect(out _))
        {
            enemy.ChangeState(enemy.ChaseState);
            return;
        }

        switch (phase)
        {
            case Phase.Walking:       TickWalking(enemy);       break;
            case Phase.LookingAround: TickLookingAround(enemy); break;
        }
    }

    public void Exit(OverworldEnemyController enemy) { }

    // ── Phases ───────────────────────────────────────────────────────────────

    private void TickWalking(OverworldEnemyController enemy)
    {
        Vector3 to = targetPos - enemy.transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        // Arrived at the last-known spot — switch to looking around.
        if (dist <= enemy.AIData.WaypointReachedDistance)
        {
            phase     = Phase.LookingAround;
            lookTimer = enemy.AIData.InvestigateLookAroundTime;
            baseRot   = enemy.transform.rotation;
            return;
        }

        Vector3 dir = to / dist;
        enemy.FaceDirection(dir);
        enemy.MoveHorizontal(dir);
    }

    private void TickLookingAround(OverworldEnemyController enemy)
    {
        enemy.ApplyGravityOnly();

        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            // Gave up — back to whatever they were doing before.
            enemy.ChangeState(
                enemy.HasWaypoints ? (IEnemyState)enemy.PatrolState : enemy.IdleState);
            return;
        }

        // Smooth left/right swing over the full duration: one complete oscillation.
        float total = enemy.AIData.InvestigateLookAroundTime;
        if (total <= 0.0001f) return;
        float t     = 1f - (lookTimer / total);              // 0..1
        float angle = SwingDegrees * Mathf.Sin(t * Mathf.PI * 2f);

        enemy.transform.rotation = baseRot * Quaternion.Euler(0f, angle, 0f);
    }
}
