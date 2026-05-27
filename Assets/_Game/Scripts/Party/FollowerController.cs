using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a character follow the player (or another leader) by replaying
/// the leader's past positions with a delay — creating a smooth "chain" effect.
/// Attach to follower NPCs that have joined the party.
/// </summary>
public class FollowerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float stopDistance = 0.15f;

    [Header("Follow Delay")]
    [Tooltip("How many position samples behind the leader this follower trails.")]
    [SerializeField] private int followDelayFrames = 8;

    private Transform leader;
    private readonly List<Vector3> positionHistory = new List<Vector3>();
    private PlayerAnimator animator;
    private CharacterController characterController;

    // ── Setup ────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator          = GetComponentInChildren<PlayerAnimator>();
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Call this after spawning the follower to assign its leader.
    /// The follower will instantly teleport to the leader's position
    /// to avoid a "snap" across the map on the first frame.
    /// </summary>
    public void SetLeader(Transform leaderTransform)
    {
        leader = leaderTransform;

        // Pre-fill history so the follower starts at the leader's position
        positionHistory.Clear();
        for (int i = 0; i < followDelayFrames + 1; i++)
            positionHistory.Add(leaderTransform.position);

        transform.position = leaderTransform.position;
    }

    // ── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        if (leader == null) return;

        // Record the current leader position each frame
        positionHistory.Insert(0, leader.position);

        // Keep the buffer from growing indefinitely
        while (positionHistory.Count > followDelayFrames + 1)
            positionHistory.RemoveAt(positionHistory.Count - 1);

        // Target = position the leader was at N frames ago
        Vector3 targetPos = positionHistory[Mathf.Min(followDelayFrames, positionHistory.Count - 1)];

        float dist = Vector3.Distance(transform.position, targetPos);

        if (dist > stopDistance)
        {
            Vector3 moveDir = (targetPos - transform.position).normalized;

            if (characterController != null)
                characterController.Move(moveDir * moveSpeed * Time.deltaTime);
            else
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

            animator?.UpdateAnimation(moveDir);
        }
        else
        {
            animator?.UpdateAnimation(Vector3.zero);
        }
    }
}
