using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the Turn Order bar shown during battle.
/// Spawns one TurnOrderSlot per unit, highlights the active unit,
/// and greys out units that have fainted.
///
/// Scene setup:
///   1. In the Battle Canvas, create a Panel: name it "TurnOrderPanel".
///   2. Add this component to TurnOrderPanel.
///   3. Inside TurnOrderPanel:
///        - Optional: a TextMeshProUGUI label ("URUTAN\nGILIRAN")
///        - A child Panel named "SlotsContainer" with a Horizontal Layout Group.
///   4. Create a slot Prefab (see TurnOrderSlot.cs) and assign it below.
///   5. Assign the SlotsContainer Transform below.
///   6. Assign this TurnOrderDisplay on the BattleSystem component.
/// </summary>
public class TurnOrderDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab with Image (background circle) + TurnOrderSlot script + child Image (icon).")]
    [SerializeField] private GameObject   slotPrefab;
    [Tooltip("Parent transform with a Horizontal Layout Group.")]
    [SerializeField] private Transform    slotsContainer;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private List<TurnOrderSlot> slots = new List<TurnOrderSlot>();
    private List<BattleUnit>    order = new List<BattleUnit>();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once after the battle setup is complete with the full sorted turn order.
    /// Destroys any existing slots and creates fresh ones.
    /// </summary>
    public void Initialise(List<BattleUnit> turnOrder)
    {
        // Clear previous slots
        foreach (var slot in slots)
            if (slot != null) Destroy(slot.gameObject);
        slots.Clear();

        order = new List<BattleUnit>(turnOrder);

        if (slotPrefab == null)
        {
            Debug.LogError("[TurnOrderDisplay] slotPrefab is not assigned! ❌ Assign it in the Inspector.");
            return;
        }

        if (slotsContainer == null)
        {
            Debug.LogError("[TurnOrderDisplay] slotsContainer is not assigned! ❌ Assign it in the Inspector.");
            return;
        }

        // Spawn one slot per unit
        foreach (var unit in turnOrder)
        {
            var go   = Instantiate(slotPrefab, slotsContainer);
            var slot = go.GetComponent<TurnOrderSlot>();

            if (slot == null)
            {
                Debug.LogError("[TurnOrderDisplay] slotPrefab has no TurnOrderSlot component! ❌");
                continue;
            }

            slot.Initialise(unit.Member.Base.Icon, unit.IsPlayerUnit);
            slots.Add(slot);
        }

        Debug.Log($"[TurnOrderDisplay] Initialised with {slots.Count} slot(s).");
    }

    /// <summary>
    /// Call at the start of each turn to highlight the active unit's slot.
    /// </summary>
    public void UpdateCurrentTurn(int currentIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetActive(i == currentIndex);
        }
    }

    /// <summary>
    /// Call when a unit faints — greys out their slot in the bar.
    /// </summary>
    public void MarkFainted(BattleUnit unit)
    {
        int idx = order.IndexOf(unit);
        if (idx >= 0 && idx < slots.Count && slots[idx] != null)
        {
            slots[idx].SetFainted();
            Debug.Log($"[TurnOrderDisplay] Marked slot {idx} ({unit.Member.Name}) as fainted.");
        }
    }
}
