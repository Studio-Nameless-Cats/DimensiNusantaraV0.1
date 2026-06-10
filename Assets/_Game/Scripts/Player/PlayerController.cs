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

    // The interactable we're standing next to right now (drives the prompt chip).
    private NPCController nearbyNpc;

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
        RefreshNearbyInteractable();
    }

    // Look for the closest NPC in range and pop the interaction chip with its verb.
    // Nothing near? Tuck the chip away. Runs only in FreeRoam (GameController gates
    // HandleUpdate), so the prompt never lingers during dialog/battle.
    private void RefreshNearbyInteractable()
    {
        NPCController closest = null;
        float closestSqr = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, npcLayer);
        foreach (var hit in hits)
        {
            var npc = hit.GetComponent<NPCController>();
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

            float sqr = (npc.transform.position - transform.position).sqrMagnitude;
            if (sqr < closestSqr) { closestSqr = sqr; closest = npc; }
        }

        nearbyNpc = closest;

        var prompt = InteractionPrompt.Instance;
        if (prompt == null) return;

        if (closest != null) prompt.Show(closest.PromptVerb, this);
        else prompt.Hide(this);
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
        // Prefer the NPC the prompt's already pointing at (closest in range). Fall back
        // to a fresh overlap in case we got here without a refresh this frame.
        var npc = nearbyNpc;
        if (npc == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, npcLayer);
            foreach (var hit in hits)
            {
                npc = hit.GetComponent<NPCController>();
                if (npc != null) break; // just talk to the first one we find
            }
        }

        if (npc == null) return;

        // Chip's done its job - hide it before the dialog/recruit takes over (state
        // leaves FreeRoam, so RefreshNearbyInteractable won't run to hide it for us).
        InteractionPrompt.Instance?.Hide(this);
        nearbyNpc = null;
        npc.Interact(this);
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
