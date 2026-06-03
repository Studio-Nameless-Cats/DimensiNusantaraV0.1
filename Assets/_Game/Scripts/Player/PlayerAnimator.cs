using UnityEngine;

/// <summary>
/// Controls the player's Animator based on movement direction.
/// Attach to the same GameObject as (or child of) PlayerController.
///
/// Required Animator parameters:
///   - isMoving        (Bool)
///   - idleTrigger     (Trigger) — fired after idleThreshold seconds of standing still
///   - interactTrigger (Trigger) — fired by PlayerController on Interact input
///
/// Animation model (see PROGRESS.md 2026-06-02 pt.2, change #2):
///   ONE left-facing clip per action (Standby, Walking, Idle_1, Interact).
///   There are NO Up/Down/Right states and NO blend tree — facing right is just
///   the left clip mirrored via SpriteRenderer.flipX, driven by the last
///   horizontal input. Pure-vertical movement keeps the current facing.
///
/// Animator states:
///   Standby (default, single clip) ↔ Walking (single clip) on isMoving
///   Standby → Idle_1 on idleTrigger
///   AnyState → Interact on interactTrigger
///   Idle_1 and Interact each have a ResetIdleOnExit StateMachineBehaviour attached.
///
/// FreeRoam gating is implicit: UpdateAnimation() is only called from
/// PlayerController.HandleUpdate(), which GameController only ticks while
/// state == FreeRoam. No explicit state check needed here.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle")]
    [Tooltip("Seconds of standing still before idleTrigger fires (Standby → Idle_1).")]
    [SerializeField] private float idleThreshold = 7f;

    // Cached parameter hashes (faster than string lookups every frame)
    private static readonly int IsMoving        = Animator.StringToHash("isMoving");
    private static readonly int IdleTrigger     = Animator.StringToHash("idleTrigger");
    private static readonly int InteractTrigger = Animator.StringToHash("interactTrigger");

    private Animator       animator;
    private SpriteRenderer spriteRenderer;

    private float _idleTimer;
    private bool  _idleFired; // prevents re-firing trigger every frame past threshold

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Used to mirror the left-walk clip into a right-walk visual.
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Call this every frame from PlayerController with the current move direction.
    /// Pass Vector3.zero when the player is standing still.
    /// </summary>
    public void UpdateAnimation(Vector3 moveDir)
    {
        bool moving = moveDir.sqrMagnitude > 0.001f;

        if (moving)
        {
            // Single left-facing clip set. Facing right = the same clip mirrored
            // via flipX, driven purely by the last horizontal input. Pure-vertical
            // movement has no horizontal component, so we leave flipX as-is and the
            // character keeps whichever way it was last facing.
            if (spriteRenderer != null && Mathf.Abs(moveDir.x) > 0.001f)
                spriteRenderer.flipX = moveDir.x > 0f;   // moving right → mirror left clip

            ResetIdleTimer();
        }
        else if (!_idleFired)
        {
            // Standing still — accumulate the idle countdown.
            // Once we fire, _idleFired latches true so we don't re-fire every frame
            // past the threshold. ResetIdleOnExit (StateMachineBehaviour on Idle_1
            // and Interact) calls ResetIdleTimer() when those states finish, which
            // clears _idleFired so the next 7-second countdown can start cleanly.
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= idleThreshold)
            {
                animator.SetTrigger(IdleTrigger);
                _idleFired = true;
            }
        }
        // When standing still we leave flipX alone — the character keeps facing
        // whichever direction they last walked.

        animator.SetBool(IsMoving, moving);
    }

    /// <summary>Resets the standing-still timer. Called on movement, on Interact,
    /// and via ResetIdleOnExit when Idle_1 / Interact states exit.</summary>
    public void ResetIdleTimer()
    {
        _idleTimer = 0f;
        _idleFired = false;
    }

    /// <summary>Fires the interactTrigger and resets the idle timer.</summary>
    public void TriggerInteract()
    {
        animator.SetTrigger(InteractTrigger);
        ResetIdleTimer();
    }
}
