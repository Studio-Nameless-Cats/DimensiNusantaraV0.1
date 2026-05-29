using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player movement and interaction in the overworld.
///
/// Setup requirements:
///   1. Add a CharacterController component to this GameObject.
///   2. Add a PlayerInput component (New Input System) — set Behavior to "Send Messages".
///   3. Create an InputActions asset with:
///        - Action Map: "Player"
///        - Action "Move"     (Value, Vector2)
///        - Action "Interact" (Button)
///   4. Assign the InputActions asset to the PlayerInput component.
///   5. Add a PartySystem component to this GameObject.
///   6. Optionally add a PlayerAnimator to a child GameObject.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PartySystem))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity   = -20f;

    [Header("Interaction")]
    [SerializeField] private float   interactRange = 1.5f;
    [SerializeField] private LayerMask npcLayer;

    // ── Components ───────────────────────────────────────────────────────────
    private CharacterController cc;
    private PlayerAnimator       playerAnimator;
    private PartySystem          partySystem;

    // ── State ────────────────────────────────────────────────────────────────
    private Vector2 inputVector;
    private float   verticalVelocity;

    // ── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired when the player walks into an encounter trigger.</summary>
    public event Action<EnemyEncounterData> OnEncounterTriggered;

    // ── Properties ───────────────────────────────────────────────────────────
    public PartySystem Party => partySystem;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        cc            = GetComponent<CharacterController>();
        partySystem   = GetComponent<PartySystem>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
    }

    // ── Called by GameController every frame when state == FreeRoam ─────────

    public void HandleUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        // Apply gravity
        if (cc.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        moveDir.y = verticalVelocity;

        cc.Move(moveDir * moveSpeed * Time.deltaTime);

        // Update animator — pass only horizontal movement
        Vector3 horizontalDir = new Vector3(inputVector.x, 0f, inputVector.y);
        playerAnimator?.UpdateAnimation(horizontalDir);
    }

    // ── New Input System callbacks (PlayerInput → Send Messages) ─────────────

    /// <summary>Receives Move action from PlayerInput.</summary>
    public void OnMove(InputValue value)
    {
        inputVector = value.Get<Vector2>();
    }

    /// <summary>Receives Interact action from PlayerInput.</summary>
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            playerAnimator?.TriggerInteract(); // fires interactTrigger + resets idle timer
            TryInteract();
        }
    }

    // ── Interaction ──────────────────────────────────────────────────────────

    private void TryInteract()
    {
        // Look for an NPC in front of / around the player
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, npcLayer);

        foreach (var hit in hits)
        {
            var npc = hit.GetComponent<NPCController>();
            if (npc != null)
            {
                npc.Interact(this);
                return; // Only interact with the closest one
            }
        }
    }

    // ── Called by EncounterTrigger ───────────────────────────────────────────

    public void TriggerEncounter(EnemyEncounterData encounterData)
    {
        OnEncounterTriggered?.Invoke(encounterData);
    }

    // ── Gizmos (editor only) ─────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
