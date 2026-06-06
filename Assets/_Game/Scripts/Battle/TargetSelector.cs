using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

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

    [Header("Motion (optional)")]
    [Tooltip("Assign to make the target buttons pop in (cascade) and pop out on pick/cancel. Leave null for instant show/hide.")]
    [SerializeField] private MotionProfile motionProfile;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private Vector3 _containerHome = Vector3.one;   // button container's resting scale

    void Awake()
    {
        if (buttonContainer is RectTransform brt) _containerHome = brt.localScale;
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

        // Pop the target buttons in one after another (scale + fade, layout-safe).
        if (motionProfile != null)
        {
            if (buttonContainer is RectTransform brt) { brt.DOKill(); brt.localScale = _containerHome; }
            var rects = new List<RectTransform>(spawnedButtons.Count);
            foreach (var go in spawnedButtons)
                if (go != null) rects.Add((RectTransform)go.transform);
            rects.ScaleCascade(motionProfile);
        }
    }

    // Hide the panel and clean up the buttons we spawned.
    public void Hide()
    {
        if (panelRoot == null) { ClearButtons(); return; }

        // No profile, or no container to scale? Snap it off like before.
        if (motionProfile == null || !(buttonContainer is RectTransform brt))
        {
            panelRoot.SetActive(false);
            ClearButtons();
            return;
        }

        // Buttons stick around for the ~0.2s pop-out, so kill their clicks now —
        // otherwise a quick second tap could pick a second target.
        foreach (var go in spawnedButtons)
        {
            var b = go != null ? go.GetComponent<Button>() : null;
            if (b != null) b.interactable = false;
        }

        // Shrink the buttons away as a group, THEN disable + destroy them (so they
        // don't vanish before the pop-out plays).
        brt.DOKill();
        brt.ScalePopOut(motionProfile, _containerHome)
           .OnComplete(() =>
           {
               panelRoot.SetActive(false);
               brt.localScale = _containerHome;
               ClearButtons();
           });
    }

    private void ClearButtons()
    {
        foreach (var go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();
    }
}
