using UnityEngine;

/// <summary>
/// Pins this GameObject's WORLD rotation regardless of how its parent rotates.
/// Runs in LateUpdate so it overrides anything Update wrote to the parent.
///
/// Why this exists: OverworldEnemyController rotates the enemy's root every
/// frame so <c>transform.forward</c> aligns with the patrol/chase direction
/// (the perception cone reads forward). With no compensation, the sprite child
/// rotates with the root and visibly spins. Dropping this component on the
/// Sprite child decouples the visual rotation from the body rotation.
///
/// Setup:
///   1. Place on the Sprite child of OverworldEnemyTemplate (or any visual
///      that must stay facing the camera while its parent rotates).
///   2. Authoring rotation: set the Inspector rotation to whatever angle the
///      sprite should keep (e.g. 0, -90, 0 for your current setup).
///      Leave "Use Start Rotation" ticked — Awake captures this value and
///      restores it every frame.
///   3. If you'd rather hard-code the locked rotation, untick "Use Start
///      Rotation" and fill in "Locked Euler".
/// </summary>
[DefaultExecutionOrder(1000)] // run after most other LateUpdates, just in case
public class LockWorldRotation : MonoBehaviour
{
    [Tooltip("If true, the world rotation captured in Awake is used.\n" +
             "If false, the 'Locked Euler' values below are used instead.")]
    [SerializeField] private bool useStartRotation = true;

    [Tooltip("World-space Euler rotation to lock to (only used when 'Use Start Rotation' is false).")]
    [SerializeField] private Vector3 lockedEuler;

    private Quaternion target;

    void Awake()
    {
        target = useStartRotation
            ? transform.rotation
            : Quaternion.Euler(lockedEuler);
    }

    void LateUpdate()
    {
        transform.rotation = target;
    }
}
