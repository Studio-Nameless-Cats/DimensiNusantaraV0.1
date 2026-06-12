using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

// The battle text box plus the player's command buttons (Attack / Skill /
// Special Skill / Run).
//
// How to set up the UI (Canvas children):
//   - DialogPanel  -> Image background
//     - DialogText -> TextMeshProUGUI
//   - ActionPanel  -> holds the 4 command buttons
//     - AttackButton, SkillButton, SpecialSkillButton, RunButton
//       (each one is a Button with a TextMeshProUGUI child)
public class BattleDialogBox : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private TextMeshProUGUI dialogText;
    [SerializeField] private float           typeSpeed = 40f; // characters per second

    [Header("Action Selector")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button     attackButton;
    [SerializeField] private Button     skillButton;
    [SerializeField] private Button     specialSkillButton;
    [SerializeField] private Button     runButton;
    [Tooltip("Optional ITEM command button. Leave empty until the battle item flow is wired - everything else works without it.")]
    [SerializeField] private Button     itemButton;

    [Header("Motion (optional)")]
    [Tooltip("Assign to make the command buttons pop in (cascade) and the menu pop out when an action is chosen. Leave null for the old instant show/hide.")]
    [SerializeField] private MotionProfile motionProfile;

    // Button events the BattleSystem listens to.
    public event Action OnAttackPressed;
    public event Action OnSkillPressed;
    public event Action OnSpecialPressed;
    public event Action OnRunPressed;
    public event Action OnItemPressed;

    // The action panel's resting scale, grabbed before we ever animate it so the
    // pop-out always returns to the right size.
    private Vector3 _actionPanelHome = Vector3.one;

    void Awake()
    {
        // Yell in the console if someone forgot to wire a reference in the Inspector.
        if (dialogText == null)
            Debug.LogError("[BattleDialogBox] dialogText is NOT assigned in the Inspector! Assign the TextMeshProUGUI component for the dialog text.");

        if (actionPanel == null)
            Debug.LogError("[BattleDialogBox] actionPanel is NOT assigned in the Inspector! Assign the Action Panel GameObject (the one containing Attack and Run buttons).");

        if (attackButton == null)
            Debug.LogError("[BattleDialogBox] attackButton is NOT assigned in the Inspector!");

        if (skillButton == null)
            Debug.LogError("[BattleDialogBox] skillButton is NOT assigned in the Inspector!");

        if (specialSkillButton == null)
            Debug.LogError("[BattleDialogBox] specialSkillButton is NOT assigned in the Inspector!");

        if (runButton == null)
            Debug.LogError("[BattleDialogBox] runButton is NOT assigned in the Inspector!");

        attackButton?.onClick.AddListener(()       => OnAttackPressed?.Invoke());
        skillButton?.onClick.AddListener(()        => OnSkillPressed?.Invoke());
        specialSkillButton?.onClick.AddListener(() => OnSpecialPressed?.Invoke());
        runButton?.onClick.AddListener(()          => OnRunPressed?.Invoke());
        itemButton?.onClick.AddListener(()         => OnItemPressed?.Invoke());   // optional, no error if unwired

        // Remember the panel's normal scale before anything animates it.
        if (actionPanel != null) _actionPanelHome = actionPanel.transform.localScale;

        // On purpose: keep the buttons hidden at the start. They only pop up
        // when it's actually the player's turn.
        ShowActionSelector(false);
        Debug.Log("[BattleDialogBox] Awake complete. Action panel hidden on purpose — shows when player's turn starts.");
    }

    // Prints the text one letter at a time for that classic typewriter feel.
    // Await this; it waits a tiny bit at the end before moving on.
    public IEnumerator TypeDialog(string message)
    {
        dialogText.text = "";

        foreach (char c in message)
        {
            dialogText.text += c;
            yield return new WaitForSeconds(1f / typeSpeed);
        }

        yield return new WaitForSeconds(0.6f); // small pause before the next line
    }

    // Just slam the text in instantly, no typewriter.
    public void SetMessage(string message)
    {
        dialogText.text = message;
    }

    public void ShowActionSelector(bool visible)
    {
        if (actionPanel == null) return;

        // No profile wired? Behave exactly like before — instant on/off.
        if (motionProfile == null)
        {
            actionPanel.SetActive(visible);
            return;
        }

        RectTransform panelRt = (RectTransform)actionPanel.transform;
        panelRt.DOKill();

        if (visible)
        {
            panelRt.localScale = _actionPanelHome;
            actionPanel.SetActive(true);
            // Pop the four buttons in one after another (scale + fade, so it's safe
            // even though they sit in a Layout Group). Reset to a clean scale first so
            // a fast reopen mid-tween can't leave a button shrunk.
            var rects = ActionButtonRects();
            foreach (var r in rects) { r.DOKill(); r.localScale = Vector3.one; }
            rects.ScaleCascade(motionProfile);
        }
        else if (actionPanel.activeSelf)
        {
            // Shrink the menu away, then actually disable it once the pop-out finishes.
            panelRt.ScalePopOut(motionProfile, _actionPanelHome)
                   .OnComplete(() =>
                   {
                       actionPanel.SetActive(false);
                       panelRt.localScale = _actionPanelHome;
                   });
        }
        else
        {
            actionPanel.SetActive(false);
        }
    }

    // The four command buttons as RectTransforms, skipping any that aren't wired.
    private List<RectTransform> ActionButtonRects()
    {
        var list = new List<RectTransform>(5);
        if (attackButton)       list.Add((RectTransform)attackButton.transform);
        if (skillButton)        list.Add((RectTransform)skillButton.transform);
        if (specialSkillButton) list.Add((RectTransform)specialSkillButton.transform);
        if (itemButton)         list.Add((RectTransform)itemButton.transform);
        if (runButton)          list.Add((RectTransform)runButton.transform);
        return list;
    }

    public void EnableButtons(bool enabled)
    {
        if (attackButton)       attackButton.interactable       = enabled;
        if (skillButton)        skillButton.interactable        = enabled;
        if (specialSkillButton) specialSkillButton.interactable = enabled;
        if (itemButton)         itemButton.interactable         = enabled;
        if (runButton)          runButton.interactable          = enabled;
    }
}
