using UnityEngine;

/// <summary>
/// Controls the player's Animator based on movement direction.
/// Attach to the same GameObject as (or child of) PlayerController.
///
/// Required Animator parameters:
///   - isMoving  (Bool)
///   - moveX     (Float)  — horizontal direction (-1 to 1)
///   - moveY     (Float)  — forward/back direction (-1 to 1)
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    // Cached parameter hashes (faster than string lookups every frame)
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int MoveX    = Animator.StringToHash("moveX");
    private static readonly int MoveY    = Animator.StringToHash("moveY");

    private Animator       animator;
    private SpriteRenderer spriteRenderer;

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
        }
        // When standing still we leave flipX alone — the character keeps facing
        // whichever direction they last walked.

        animator.SetBool(IsMoving, moving);
    }
}
