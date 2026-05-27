using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a panel listing alive enemy units so the player can choose which one to attack.
///
/// Flow:
///   1. BattleSystem calls Show(aliveEnemies, callback) from HandleAttack().
///   2. One button per enemy is spawned inside the panel, showing Name + HP.
///   3. Player clicks a button → Hide() is called and callback fires with the chosen BattleUnit.
///
/// Scene / Prefab setup:
///   - Add a "TargetSelectorPanel" Canvas child (set inactive by default).
///   - Add a vertical LayoutGroup child called "ButtonContainer" for the target buttons.
///   - Assign a simple TextButton prefab (Button + TMP child) to 'targetButtonPrefab'.
///   - Put the TargetSelector component on the BattleSystem GameObject (always active),
///     assign the panel root to 'panelRoot' and the container to 'buttonContainer'.
///
/// Optionally set a panel title text (e.g., "Pilih musuh:") via 'titleText'.
/// </summary>
public class TargetSelector : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The entire selector panel. Disabled by default.")]
    [SerializeField] private GameObject panelRoot;

    [Header("UI")]
    [Tooltip("Optional header label shown above the target buttons (e.g. 'Pilih musuh:').")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("Parent container for spawned target buttons (should have a VerticalLayoutGroup).")]
    [SerializeField] private Transform buttonContainer;

    [Header("Prefab")]
    [Tooltip("Button prefab: root has Button + Image; child has TextMeshProUGUI for the label.")]
    [SerializeField] private GameObject targetButtonPrefab;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Display the selector with one button per unit in 'targets'.
    /// onSelected fires when the player picks a target.
    /// </summary>
    public void Show(List<BattleUnit> targets, Action<BattleUnit> onSelected)
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TargetSelector] panelRoot is not assigned! ❌");
            // Fallback: auto-pick first alive target so battle isn't stuck
            if (targets != null && targets.Count > 0) onSelected?.Invoke(targets[0]);
            return;
        }

        ClearButtons();

        if (titleText) titleText.text = "Pilih musuh:";

        foreach (var unit in targets)
        {
            if (unit == null || unit.Member.IsFainted) continue;

            var go = Instantiate(targetButtonPrefab, buttonContainer);

            // Set label: "Name   HP: X / MaxHP"
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label)
                label.text = $"{unit.Member.Name}   HP: {unit.Member.CurrentHp} / {unit.Member.MaxHp}";

            // Wire click — capture local reference for lambda
            var captured = unit;
            var btn = go.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() =>
                {
                    Hide();
                    onSelected?.Invoke(captured);
                });
            else
                Debug.LogWarning("[TargetSelector] targetButtonPrefab has no Button component! ❌");

            spawnedButtons.Add(go);
        }

        panelRoot.SetActive(true);
    }

    /// <summary>Hide the panel and destroy spawned buttons.</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        ClearButtons();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ClearButtons()
    {
        foreach (var go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();
    }
}
