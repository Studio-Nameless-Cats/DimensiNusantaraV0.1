using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    // Button events the BattleSystem listens to.
    public event Action OnAttackPressed;
    public event Action OnSkillPressed;
    public event Action OnSpecialPressed;
    public event Action OnRunPressed;

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
        actionPanel?.SetActive(visible);
    }

    public void EnableButtons(bool enabled)
    {
        if (attackButton)       attackButton.interactable       = enabled;
        if (skillButton)        skillButton.interactable        = enabled;
        if (specialSkillButton) specialSkillButton.interactable = enabled;
        if (runButton)          runButton.interactable          = enabled;
    }
}
