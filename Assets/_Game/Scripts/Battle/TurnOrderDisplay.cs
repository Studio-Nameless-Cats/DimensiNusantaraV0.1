using System.Collections.Generic;
using UnityEngine;

// Runs the turn-order bar you see during a fight. Spawns one TurnOrderSlot per unit,
// lights up whoever's acting, and drops anyone who faints off the bar.
//
// Scene setup:
//   1. In the Battle Canvas, make a Panel and name it "TurnOrderPanel".
//   2. Put this component on TurnOrderPanel.
//   3. Inside TurnOrderPanel:
//        - Optional: a TextMeshProUGUI label ("URUTAN\nGILIRAN")
//        - A child Panel named "SlotsContainer" with a Horizontal Layout Group.
//   4. Make a slot prefab (see TurnOrderSlot.cs) and assign it below.
//   5. Assign the SlotsContainer Transform below.
//   6. Assign this TurnOrderDisplay on the BattleSystem component.
public class TurnOrderDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab with Image (background circle) + TurnOrderSlot script + child Image (icon).")]
    [SerializeField] private GameObject   slotPrefab;
    [Tooltip("Parent transform with a Horizontal Layout Group.")]
    [SerializeField] private Transform    slotsContainer;

    private List<TurnOrderSlot> slots = new List<TurnOrderSlot>();
    private List<BattleUnit>    order = new List<BattleUnit>();

    // Call this once battle setup is done, passing the full sorted turn order. It tears
    // down any old slots and builds fresh ones.
    public void Initialise(List<BattleUnit> turnOrder)
    {
        // Clear out the old slots first.
        foreach (var slot in slots)
            if (slot != null) Destroy(slot.gameObject);
        slots.Clear();

        order = new List<BattleUnit>(turnOrder);

        if (slotPrefab == null)
        {
            Debug.LogError("[TurnOrderDisplay] slotPrefab is not assigned! Assign it in the Inspector.");
            return;
        }

        if (slotsContainer == null)
        {
            Debug.LogError("[TurnOrderDisplay] slotsContainer is not assigned! Assign it in the Inspector.");
            return;
        }

        // One slot per unit.
        foreach (var unit in turnOrder)
        {
            var go   = Instantiate(slotPrefab, slotsContainer);
            var slot = go.GetComponent<TurnOrderSlot>();

            if (slot == null)
            {
                Debug.LogError("[TurnOrderDisplay] slotPrefab has no TurnOrderSlot component!");
                continue;
            }

            slot.Initialise(unit.Member.Base.Icon, unit.IsPlayerUnit);
            slots.Add(slot);
        }

        Debug.Log($"[TurnOrderDisplay] Initialised with {slots.Count} slot(s).");
    }

    // Call this at the start of each turn to light up the active unit's slot.
    public void UpdateCurrentTurn(int currentIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetActive(i == currentIndex);
        }
    }

    // Call this when a unit faints to pull their icon off the bar. We just deactivate the
    // slot's GameObject (don't destroy it or remove it from the list) so the leftover slot
    // indices stay lined up with the turn order for UpdateCurrentTurn. The Horizontal
    // Layout Group closes the gap for us.
    public void MarkFainted(BattleUnit unit)
    {
        int idx = order.IndexOf(unit);
        if (idx >= 0 && idx < slots.Count && slots[idx] != null)
        {
            slots[idx].gameObject.SetActive(false);
            Debug.Log($"[TurnOrderDisplay] Removed slot {idx} ({unit.Member.Name}) from the turn-order bar.");
        }
    }
}
