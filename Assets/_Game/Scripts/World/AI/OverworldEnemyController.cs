using UnityEngine;

/// <summary>
/// Visible overworld enemy that patrols a list of waypoints. The behaviour is
/// driven by a small finite state machine — see IEnemyState and the State/
/// folder for the individual state classes.
///
/// Step 1 ships Idle + Patrol only. Perception, chase, attack, alerts, and the
/// defeated-enemy registry come in later steps and slot in without changing
/// this skeleton's public surface.
///
/// Movement deliberately mirrors PlayerController: CharacterController on the
/// XZ plane with a verticalVelocity / gravity loop, so player and enemies fall
/// the same way and stand on the same colliders.
///
/// Setup requirements:
///   1. Add a CharacterController component to this GameObject (radius/height
///      sized to your sprite — same as PlayerController setup).
///   2. Assign an EnemyAIData asset to <c>aiData</c>.
///   3. Drop empty GameObjects into the scene to mark patrol points and drag
///      their Transforms into the <c>waypoints</c> array — in order.
///      ⚠️ Do NOT make waypoints children of the enemy prefab; the prefab
///      serialization trap will scramble them. Park them under a sibling
///      "PatrolPoints" GameObject in the scene.
///   4. (Optional) Add a sprite child for visuals.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class OverworldEnemyController : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique-per-instance string. Recorded in DefeatedEnemyRegistry when this enemy is beaten so it doesn't respawn on scene reload. " +
             "⚠️ Each enemy in a scene MUST have a different ID. If you duplicate (Ctrl+D) an enemy, change this immediately.")]
    [SerializeField] private string enemyId;

    [Header("AI Configuration")]
    [Tooltip("ScriptableObject with all tuning values (speed, patrol mode, perception, etc.).")]
    [SerializeField] private EnemyAIData aiData;

    [Header("Patrol Path")]
    [Tooltip("Ordered list of waypoint Transforms. Place these in the scene, NOT under this prefab.")]
    [SerializeField] private Transform[] waypoints;

    [Header("Visual Feedback (optional)")]
    [Tooltip("Pop-up '!' / '?' bubble shown on state transitions. Leave empty to skip.")]
    [SerializeField] private AlertBubble alertBubble;

    // ── Components ───────────────────────────────────────────────────────────
    private CharacterController cc;

    // ── State machine ────────────────────────────────────────────────────────
    private IEnemyState currentState;
    // Cached state instances — created once in Awake, reused forever.
    public IdleState         IdleState        { get; private set; }
    public PatrolState       PatrolState      { get; private set; }
    public ChaseState        ChaseState       { get; private set; }
    public InvestigateState  InvestigateState { get; private set; }
    public AttackState       AttackState      { get; private set; }

    // ── Perception ───────────────────────────────────────────────────────────
    public EnemyPerception Perception { get; private set; }

    // Lazy-cached player reference. Cleared if the player is destroyed (e.g. scene change).
    private PlayerController cachedPlayer;
    public  PlayerController Player
    {
        get
        {
            if (cachedPlayer == null)
                cachedPlayer = FindFirstObjectByType<PlayerController>();
            return cachedPlayer;
        }
    }

    // ── Patrol bookkeeping (lives on the controller so it survives Idle↔Patrol) ─
    private int  currentWaypointIndex;
    private int  patrolDirection = 1; // +1 forward, -1 backward (PingPong only)

    // ── Gravity (matches PlayerController) ───────────────────────────────────
    private float verticalVelocity;

    // ── Public accessors for state classes ───────────────────────────────────
    public EnemyAIData        AIData     => aiData;
    public Transform[]        Waypoints  => waypoints;
    public CharacterController CC        => cc;
    public bool               HasWaypoints => waypoints != null && waypoints.Length > 0;
    public AlertBubble        AlertBubble => alertBubble;
    public string             EnemyId     => enemyId;

    /// <summary>Read-only view of the current FSM state. Used by VisionConeRenderer to tint by state.</summary>
    public IEnemyState        CurrentState => currentState;

    public int  CurrentWaypointIndex { get => currentWaypointIndex; set => currentWaypointIndex = value; }
    public int  PatrolDirection      { get => patrolDirection;      set => patrolDirection = value; }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        // Defeated-enemy persistence: if this enemy was beaten earlier in the
        // session, the registry remembers its id across the battle → overworld
        // scene reload. Disable the GameObject so it doesn't tick / draw / patrol.
        if (DefeatedEnemyRegistry.IsDefeated(enemyId))
        {
            gameObject.SetActive(false);
            return;
        }

        if (string.IsNullOrEmpty(enemyId))
            Debug.LogWarning($"[OverworldEnemyController] '{name}' has an empty EnemyId — it will respawn after every battle. Set a unique id in the Inspector.", this);

        cc = GetComponent<CharacterController>();

        // Build the state instances once and reuse them.
        IdleState        = new IdleState();
        PatrolState      = new PatrolState();
        ChaseState       = new ChaseState();
        InvestigateState = new InvestigateState();
        AttackState      = new AttackState();

        Perception = new EnemyPerception(this);

        if (aiData == null)
            Debug.LogError($"[OverworldEnemyController] '{name}' has no EnemyAIData assigned — disabling.", this);
    }

    void Start()
    {
        if (aiData == null) { enabled = false; return; }

        // Start in Idle — its wait timer creates a brief pause before the
        // first patrol step (and is the only state available if waypoints are empty).
        ChangeState(IdleState);
    }

    void Update()
    {
        // Pause the AI whenever the game isn't in FreeRoam (battle, dialog, cutscene).
        // GameController.State is the source of truth; if the manager is missing we
        // still tick so the enemy is visible during isolated test scenes.
        if (GameController.Instance != null &&
            GameController.Instance.State != GameState.FreeRoam)
        {
            // Still keep grounded so we don't accumulate fall velocity while paused.
            ApplyGravityOnly();
            return;
        }

        currentState?.Tick(this);
    }

    // ── FSM API ──────────────────────────────────────────────────────────────

    /// <summary>Swap to a new state. Safe to call from inside a Tick().</summary>
    public void ChangeState(IEnemyState newState)
    {
        if (newState == null) return;

        currentState?.Exit(this);
        currentState = newState;
        currentState.Enter(this);
    }

    // ── Movement helpers (called by states) ──────────────────────────────────

    /// <summary>
    /// Move horizontally in worldDir (will be normalised) at aiData.MoveSpeed
    /// and apply gravity. Pass Vector3.zero to stand still while staying grounded.
    /// </summary>
    public void MoveHorizontal(Vector3 worldDir)
    {
        if (worldDir.sqrMagnitude > 1f) worldDir.Normalize();

        Vector3 motion = worldDir * aiData.MoveSpeed;

        // Gravity — same pattern as PlayerController.
        if (cc.isGrounded) verticalVelocity = -1f;
        else               verticalVelocity += aiData.Gravity * Time.deltaTime;
        motion.y = verticalVelocity;

        cc.Move(motion * Time.deltaTime);
    }

    /// <summary>Stand still but keep the gravity loop ticking so we don't drift.</summary>
    public void ApplyGravityOnly()
    {
        if (cc.isGrounded) verticalVelocity = -1f;
        else               verticalVelocity += aiData.Gravity * Time.deltaTime;

        cc.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    /// <summary>Smoothly rotate the body to face the given horizontal direction.</summary>
    public void FaceDirection(Vector3 horizontalDir)
    {
        horizontalDir.y = 0f;
        if (horizontalDir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(horizontalDir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, aiData.TurnSpeed * Time.deltaTime);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Patrol path
        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, 0.2f);

                Transform next = waypoints[(i + 1) % waypoints.Length];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }

        if (aiData != null)
        {
            // Touch range — battle starts when player enters this sphere.
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, aiData.TouchRange);

            // Close radius — 360° detection bubble.
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, aiData.CloseRadius);

            // Vision cone — two edge rays + a forward ray.
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.7f);
            Vector3 origin    = transform.position + Vector3.up * 0.05f;
            Vector3 forward   = transform.forward;     forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                Quaternion left  = Quaternion.Euler(0f, -aiData.VisionHalfAngle, 0f);
                Quaternion right = Quaternion.Euler(0f,  aiData.VisionHalfAngle, 0f);
                Gizmos.DrawRay(origin, forward       * aiData.VisionRange);
                Gizmos.DrawRay(origin, left * forward * aiData.VisionRange);
                Gizmos.DrawRay(origin, right * forward * aiData.VisionRange);
            }
        }
    }
}
