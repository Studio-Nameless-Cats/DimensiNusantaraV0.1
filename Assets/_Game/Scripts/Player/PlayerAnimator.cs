using UnityEngine;

/// <summary>
/// Controls the player's Animator based on movement direction.
/// Attach to the same GameObject as (or child of) PlayerController.
///
/// Required Animator parameters:
///   - isMoving        (Bool)
///   - moveX           (Float)   — horizontal direction (-1 to 1), latched to last-facing
///   - moveY           (Float)   — forward/back direction (-1 to 1), latched to last-facing
///   - idleTrigger     (Trigger) — fired after idleThreshold seconds of standing still
///   - interactTrigger (Trigger) — fired by PlayerController on Interact input
///
/// Animator states (see PROGRESS.md 2026-05-29):
///   Standby (default, 4-dir blend tree on moveX/moveY) ↔ Walking (existing blend tree)
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
    private static readonly int MoveX           = Animator.StringToHash("moveX");
    private static readonly int MoveY           = Animator.StringToHash("moveY");
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
            animator.SetFloat(MoveX, moveDir.x);
            animator.SetFloat(MoveY, moveDir.z); // Z = forward in 3D

            // We only have a Left clip — Right reuses Left but mirrored.
            // Mirror when the dominant axis is horizontal; un-mirror when
            // walking vertically so Up/Down clips render correctly.
            if (spriteRenderer != null)
            {
                if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.z))
                    spriteRenderer.flipX = moveDir.x > 0f;   // right → flip
                else
                    spriteRenderer.flipX = false;            // up/down → straight
            }

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
