using System.Collections.Generic;
using UnityEngine;

// Makes a character trail the player (or some other leader) by replaying the leader's old
// positions a few frames late. Gives that smooth conga-line "chain" look. Stick this on
// follower NPCs that have joined the party.
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

    void Awake()
    {
        animator          = GetComponentInChildren<PlayerAnimator>();
        characterController = GetComponent<CharacterController>();
    }

    // Call this right after spawning the follower to tell it who to follow. It instantly
    // teleports to the leader so it doesn't go snapping across the whole map on frame one.
    public void SetLeader(Transform leaderTransform)
    {
        leader = leaderTransform;

        // Pre-load the history with the leader's spot so we start right on top of them.
        positionHistory.Clear();
        for (int i = 0; i < followDelayFrames + 1; i++)
            positionHistory.Add(leaderTransform.position);

        transform.position = leaderTransform.position;
    }

    void Update()
    {
        if (leader == null) return;

        // Jot down where the leader is this frame.
        positionHistory.Insert(0, leader.position);

        // Don't let the history buffer balloon forever.
        while (positionHistory.Count > followDelayFrames + 1)
            positionHistory.RemoveAt(positionHistory.Count - 1);

        // Aim for wherever the leader was N frames back.
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
