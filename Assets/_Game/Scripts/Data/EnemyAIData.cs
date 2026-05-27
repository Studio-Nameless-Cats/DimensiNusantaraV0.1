using UnityEngine;

/// <summary>
/// ScriptableObject that holds the *tuning data* for an overworld patrolling
/// enemy. No behaviour lives here — the FSM in OverworldEnemyController reads
/// these values to drive movement, perception, and combat triggering.
///
/// Create via: Right-click in Project → RPG → Enemy AI Data
///
/// Notes:
///   - Perception fields (vision*, closeRadius, sightObstacles, lose-sight grace)
///     are read by states added in later steps. They are defined now so the asset
///     does not need to be re-authored when those states ship.
///   - Patrol waypoints are *not* on the SO — they are per-instance Transforms
///     wired in the scene on the enemy GameObject (one SO can be reused across
///     many enemies that patrol different paths).
/// </summary>
[CreateAssetMenu(fileName = "New Enemy AI", menuName = "RPG/Enemy AI Data")]
public class EnemyAIData : ScriptableObject
{
    public enum PatrolMode { Loop, PingPong }

    // ── Movement ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Patrol / chase speed in units per second.")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Gravity applied per second while airborne (negative).")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("How fast the enemy rotates to face its movement direction (deg/sec).")]
    [SerializeField] private float turnSpeed = 720f;

    [Tooltip("Distance from a waypoint at which it counts as 'reached'.")]
    [SerializeField] private float waypointReachedDistance = 0.15f;

    // ── Patrol ───────────────────────────────────────────────────────────────
    [Header("Patrol")]
    [Tooltip("Loop = always advance forward and wrap. PingPong = walk back and forth.")]
    [SerializeField] private PatrolMode patrolMode = PatrolMode.Loop;

    [Tooltip("How long the enemy pauses at each waypoint before walking to the next.")]
    [SerializeField] private float idleWaitAtWaypoint = 1.5f;

    // ── Perception (used by states added in step 2) ──────────────────────────
    [Header("Perception (step 2+)")]
    [Tooltip("How far the vision cone extends.")]
    [SerializeField] private float visionRange = 6f;

    [Tooltip("Half-angle of the vision cone in degrees (full FOV = 2 × this).")]
    [Range(1f, 180f)]
    [SerializeField] private float visionHalfAngle = 45f;

    [Tooltip("Radius around the enemy where the player is detected regardless of facing.")]
    [SerializeField] private float closeRadius = 1.5f;

    [Tooltip("Layers that block line-of-sight raycasts (walls, props, etc.).")]
    [SerializeField] private LayerMask sightObstacles;

    [Tooltip("How long the enemy looks around at the last-known player position before giving up.")]
    [SerializeField] private float investigateLookAroundTime = 2f;

    [Tooltip("Brief grace period after losing sight before switching from Chase to Investigate. Prevents corner-flicker.")]
    [SerializeField] private float loseSightGracePeriod = 0.5f;

    // ── Combat trigger (used by AttackState added in step 2) ─────────────────
    [Header("Combat")]
    [Tooltip("Distance (between transform positions) at which the enemy is considered to have touched the player and triggers a battle.\n\n" +
             "⚠️ Must be LARGER than (player CC radius + enemy CC radius). The two CharacterController capsules physically block each other at that distance, so anything smaller will never fire.\n\n" +
             "Default 1.2 assumes ~0.4 + ~0.5 radii with headroom.")]
    [SerializeField] private float touchRange = 1.2f;

    [Tooltip("Encounter rolled when this enemy catches the player.")]
    [SerializeField] private EnemyEncounterData encounterToTrigger;

    // ── Public accessors ─────────────────────────────────────────────────────
    public float      MoveSpeed                 => moveSpeed;
    public float      Gravity                   => gravity;
    public float      TurnSpeed                 => turnSpeed;
    public float      WaypointReachedDistance   => waypointReachedDistance;
    public PatrolMode Mode                      => patrolMode;
    public float      IdleWaitAtWaypoint        => idleWaitAtWaypoint;
    public float      VisionRange               => visionRange;
    public float      VisionHalfAngle           => visionHalfAngle;
    public float      CloseRadius               => closeRadius;
    public LayerMask  SightObstacles            => sightObstacles;
    public float      InvestigateLookAroundTime => investigateLookAroundTime;
    public float      LoseSightGracePeriod      => loseSightGracePeriod;
    public float      TouchRange                => touchRange;
    public EnemyEncounterData EncounterToTrigger => encounterToTrigger;
}
