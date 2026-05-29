using UnityEngine;

/// <summary>
/// Attach (as a StateMachineBehaviour) to any Animator state that should reset
/// PlayerAnimator's idle countdown when the state exits.
///
/// Intended targets: Idle_1 (and any future Idle_N) and Interact.
///
/// Why this exists:
///   PlayerAnimator.UpdateAnimation() resets the idle timer in its "moving" branch.
///   If an Idle_1 / Interact animation finishes via Exit Time while the player is
///   still standing still, the moving branch never runs, _idleFired stays true,
///   and no subsequent idleTrigger ever fires. This SMB forces a reset on exit so
///   the next 7-second countdown starts cleanly.
///
/// Movement-driven exits (Idle_1 → Walking, Interact → Walking) also call this,
/// but the reset is idempotent — harmless when the moving branch already reset it.
///
/// Animator wiring:
///   Select Idle_1 state → Inspector → "Add Behaviour" → ResetIdleOnExit.
///   Repeat on Interact state.
/// </summary>
public class ResetIdleOnExit : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // PlayerAnimator lives on the same GameObject as the Animator, or on a parent.
        var playerAnim = animator.GetComponent<PlayerAnimator>();
        if (playerAnim == null)
            playerAnim = animator.GetComponentInParent<PlayerAnimator>();

        if (playerAnim != null)
            playerAnim.ResetIdleTimer();
    }
}
