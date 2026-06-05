using UnityEngine;

// Drives the player's Animator based on which way they're moving. Put it on the same
// GameObject as PlayerController (or a child of it).
//
// Animator parameters it needs:
//   - isMoving        (Bool)
//   - idleTrigger     (Trigger), fires after idleThreshold seconds of standing still
//   - interactTrigger (Trigger), fired by PlayerController on Interact input
//
// How the animation works (see PROGRESS.md 2026-06-02 pt.2, change #2):
//   ONE left-facing clip per action (Standby, Walking, Idle_1, Interact). There are NO
//   Up/Down/Right states and NO blend tree. Facing right is just the left clip mirrored
//   with SpriteRenderer.flipX, set from the last horizontal input. Walking straight
//   up/down keeps whatever way you were already facing.
//
// Animator states:
//   Standby (default, single clip) <-> Walking (single clip) via isMoving
//   Standby -> Idle_1 on idleTrigger
//   AnyState -> Interact on interactTrigger
//   Idle_1 and Interact each have a ResetIdleOnExit StateMachineBehaviour attached.
//
// We don't check for FreeRoam here on purpose: UpdateAnimation() only ever gets called
// from PlayerController.HandleUpdate(), and GameController only ticks that while state ==
// FreeRoam. So the check is already handled upstream.
public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle")]
    [Tooltip("Seconds of standing still before idleTrigger fires (Standby to Idle_1).")]
    [SerializeField] private float idleThreshold = 7f;

    // Pre-hashed parameter names (cheaper than string lookups every frame).
    private static readonly int IsMoving        = Animator.StringToHash("isMoving");
    private static readonly int IdleTrigger     = Animator.StringToHash("idleTrigger");
    private static readonly int InteractTrigger = Animator.StringToHash("interactTrigger");

    private Animator       animator;
    private SpriteRenderer spriteRenderer;

    private float _idleTimer;
    private bool  _idleFired; // stops us re-firing the trigger every frame once we're past the threshold

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

    // Call this every frame from PlayerController with the current move direction.
    // Pass Vector3.zero when the player isn't moving.
    public void UpdateAnimation(Vector3 moveDir)
    {
        bool moving = moveDir.sqrMagnitude > 0.001f;

        if (moving)
        {
            // We've only got the left-facing clip. Facing right is just that same clip
            // mirrored with flipX, set from the last horizontal input. Walking straight
            // up or down has no horizontal part, so we leave flipX alone and the character
            // keeps whichever way it was last facing.
            if (spriteRenderer != null && Mathf.Abs(moveDir.x) > 0.001f)
                spriteRenderer.flipX = moveDir.x > 0f;   // moving right, so mirror the left clip

            ResetIdleTimer();
        }
        else if (!_idleFired)
        {
            // Standing still, so count down to the idle animation. Once it fires, _idleFired
            // latches true so we don't keep re-firing it every frame past the threshold.
            // ResetIdleOnExit (the StateMachineBehaviour on Idle_1 and Interact) calls
            // ResetIdleTimer() when those states finish, which clears _idleFired so the
            // next 7-second countdown can start fresh.
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= idleThreshold)
            {
                animator.SetTrigger(IdleTrigger);
                _idleFired = true;
            }
        }
        // While standing still we leave flipX alone, so the character keeps facing whichever
        // way they last walked.

        animator.SetBool(IsMoving, moving);
    }

    // Resets the standing-still timer. Called when moving, on Interact, and by
    // ResetIdleOnExit when the Idle_1 / Interact states exit.
    public void ResetIdleTimer()
    {
        _idleTimer = 0f;
        _idleFired = false;
    }

    // Fires the interactTrigger and resets the idle timer.
    public void TriggerInteract()
    {
        animator.SetTrigger(InteractTrigger);
        ResetIdleTimer();
    }
}
