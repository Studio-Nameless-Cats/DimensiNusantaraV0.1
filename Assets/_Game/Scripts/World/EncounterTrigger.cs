using UnityEngine;

/// <summary>
/// Place this on a trigger collider (e.g. a grass zone) to generate random
/// battle encounters when the player walks through it.
///
/// Setup:
///   1. Create a GameObject, add a Box/Mesh Collider — set Is Trigger = true.
///   2. Add this component and assign an EnemyEncounterData asset.
///   3. Set the grass/trigger object to a layer checked by the player's CharacterController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EncounterTrigger : MonoBehaviour
{
    [Header("Encounter Data")]
    [SerializeField] private EnemyEncounterData encounterData;

    [Header("Step Settings")]
    [Tooltip("Minimum number of steps before a battle can trigger.")]
    [SerializeField] private int minSteps = 5;
    [Tooltip("Maximum number of steps before a battle triggers.")]
    [SerializeField] private int maxSteps = 12;
    [Tooltip("Distance the player must travel to count as one step.")]
    [SerializeField] private float stepSize = 1f;

    // ── State ────────────────────────────────────────────────────────────────
    private PlayerController playerInZone;
    private Vector3          lastStepPosition;
    private int              stepsRemaining;
    private bool             battleTriggered;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        ResetCounter();
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        playerInZone     = player;
        lastStepPosition = player.transform.position;
        battleTriggered  = false;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            playerInZone = null;
    }

    void Update()
    {
        if (playerInZone == null || battleTriggered) return;

        float distanceMoved = Vector3.Distance(playerInZone.transform.position, lastStepPosition);

        if (distanceMoved >= stepSize)
        {
            lastStepPosition = playerInZone.transform.position;
            stepsRemaining--;

            if (stepsRemaining <= 0)
            {
                battleTriggered = true;
                ResetCounter();
                playerInZone.TriggerEncounter(encounterData);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ResetCounter()
    {
        stepsRemaining = Random.Range(minSteps, maxSteps + 1);
    }
}
