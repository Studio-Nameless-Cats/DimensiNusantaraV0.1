using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Handles the player walking around and interacting in the overworld.
//
// What you need to set up:
//   1. Add a CharacterController component to this GameObject.
//   2. Add a PlayerInput component (New Input System) and set Behavior to "Send Messages".
//   3. Make an InputActions asset with:
//        - Action Map: "Player"
//        - Action "Move"     (Value, Vector2)
//        - Action "Interact" (Button)
//   4. Assign that InputActions asset to the PlayerInput component.
//   5. Add a PartySystem component to this GameObject.
//   6. Optionally add a PlayerAnimator to a child GameObject.
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

    private CharacterController cc;
    private PlayerAnimator       playerAnimator;
    private PartySystem          partySystem;

    private Vector2 inputVector;
    private float   verticalVelocity;

    // Fired when the player walks into an encounter trigger.
    public event Action<EnemyEncounterData> OnEncounterTriggered;

    public PartySystem Party => partySystem;

    void Awake()
    {
        cc            = GetComponent<CharacterController>();
        partySystem   = GetComponent<PartySystem>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
    }

    // GameController calls this every frame while state == FreeRoam.
    public void HandleUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        // Gravity. Stick to the ground when grounded, otherwise keep falling faster.
        if (cc.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        moveDir.y = verticalVelocity;

        cc.Move(moveDir * moveSpeed * Time.deltaTime);

        // Feed the animator just the horizontal movement (no gravity).
        Vector3 horizontalDir = new Vector3(inputVector.x, 0f, inputVector.y);
        playerAnimator?.UpdateAnimation(horizontalDir);
    }

    // --- Input System callbacks (PlayerInput set to "Send Messages") ---

    // Gets the Move action from PlayerInput.
    public void OnMove(InputValue value)
    {
        inputVector = value.Get<Vector2>();
    }

    // Gets the Interact action from PlayerInput.
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            playerAnimator?.TriggerInteract(); // fires interactTrigger + resets idle timer
            TryInteract();
        }
    }

    // --- Interacting ---

    private void TryInteract()
    {
        // Look for an NPC right around the player.
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, npcLayer);

        foreach (var hit in hits)
        {
            var npc = hit.GetComponent<NPCController>();
            if (npc != null)
            {
                npc.Interact(this);
                return; // just talk to the first one we find
            }
        }
    }

    // --- Called by EncounterTrigger ---

    public void TriggerEncounter(EnemyEncounterData encounterData)
    {
        OnEncounterTriggered?.Invoke(encounterData);
    }

    // Draws the interact range as a wire sphere when this is selected (editor only).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
