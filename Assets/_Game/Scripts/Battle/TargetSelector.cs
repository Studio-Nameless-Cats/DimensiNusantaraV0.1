using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A little panel that lists the living enemies so the player can pick who to attack.
//
// How it goes:
//   1. BattleSystem calls Show(aliveEnemies, callback) from HandleAttack().
//   2. We spawn one button per enemy inside the panel, each showing Name + HP.
//   3. Player taps a button -> Hide() runs and the callback fires with the chosen unit.
//
// Scene / prefab setup:
//   - Add a "TargetSelectorPanel" Canvas child (inactive by default).
//   - Give it a vertical LayoutGroup child called "ButtonContainer" for the buttons.
//   - Assign a simple text button prefab (Button + TMP child) to 'targetButtonPrefab'.
//   - Put the TargetSelector component on the BattleSystem GameObject (always active),
//     and assign 'panelRoot' and 'buttonContainer'.
//
// You can also set a title (like "Pilih musuh:") through 'titleText'.
public class TargetSelector : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The entire selector panel. Disabled by default.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Optional full-screen button behind the panel. Clicking it cancels target selection (backs out to the command menu).")]
    [SerializeField] private Button backdropButton;

    [Header("UI")]
    [Tooltip("Optional header label shown above the target buttons (e.g. 'Pilih musuh:').")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("Parent container for spawned target buttons (should have a VerticalLayoutGroup).")]
    [SerializeField] private Transform buttonContainer;

    [Header("Prefab")]
    [Tooltip("Button prefab: root has Button + Image; child has TextMeshProUGUI for the label.")]
    [SerializeField] private GameObject targetButtonPrefab;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // Shows the picker with one button per unit in 'targets'. onSelected fires when the
    // player picks someone. onCancel (optional) fires if they tap the backdrop to back
    // out, so hook it up to reopen the command menu.
    public void Show(List<BattleUnit> targets, Action<BattleUnit> onSelected, Action onCancel = null)
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TargetSelector] panelRoot is not assigned!");
            // Fallback so the fight doesn't get stuck: just auto-pick the first one.
            if (targets != null && targets.Count > 0) onSelected?.Invoke(targets[0]);
            return;
        }

        ClearButtons();

        if (titleText) titleText.text = "Pilih musuh:";

        // Backdrop is the tap-outside-to-cancel button (only if it's assigned).
        if (backdropButton)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(() =>
            {
                Hide();
                onCancel?.Invoke();
            });
        }

        foreach (var unit in targets)
        {
            if (unit == null || unit.Member.IsFainted) continue;

            var go = Instantiate(targetButtonPrefab, buttonContainer);

            // Label looks like "Name   HP: X / MaxHP".
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label)
                label.text = $"{unit.Member.Name}   HP: {unit.Member.CurrentHp} / {unit.Member.MaxHp}";

            // Hook up the click. Grab a local copy of 'unit' so the lambda captures the right one.
            var captured = unit;
            var btn = go.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() =>
                {
                    Hide();
                    onSelected?.Invoke(captured);
                });
            else
                Debug.LogWarning("[TargetSelector] targetButtonPrefab has no Button component!");

            spawnedButtons.Add(go);
        }

        panelRoot.SetActive(true);
    }

    // Hide the panel and clean up the buttons we spawned.
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        ClearButtons();
    }

    private void ClearButtons()
    {
        foreach (var go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();
    }
}
