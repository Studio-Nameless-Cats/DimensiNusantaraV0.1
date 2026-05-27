using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the battle text box (dialog) and the player action selector (Attack / Run).
///
/// UI Setup (Canvas children):
///   - DialogPanel  → Image background
///     - DialogText → TextMeshProUGUI
///   - ActionPanel  → contains the attack/run buttons
///     - AttackButton → Button + TextMeshProUGUI child
///     - RunButton    → Button + TextMeshProUGUI child
/// </summary>
public class BattleDialogBox : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private TextMeshProUGUI dialogText;
    [SerializeField] private float           typeSpeed = 40f; // characters per second

    [Header("Action Selector")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button     attackButton;
    [SerializeField] private Button     runButton;

    // ── Events ───────────────────────────────────────────────────────────────
    public event Action OnAttackPressed;
    public event Action OnRunPressed;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        if (dialogText == null)
            Debug.LogError("[BattleDialogBox] dialogText is NOT assigned in the Inspector! ❌ Assign the TextMeshProUGUI component for the dialog text.");

        if (actionPanel == null)
            Debug.LogError("[BattleDialogBox] actionPanel is NOT assigned in the Inspector! ❌ Assign the Action Panel GameObject (the one containing Attack and Run buttons).");

        if (attackButton == null)
            Debug.LogError("[BattleDialogBox] attackButton is NOT assigned in the Inspector! ❌");

        if (runButton == null)
            Debug.LogError("[BattleDialogBox] runButton is NOT assigned in the Inspector! ❌");

        attackButton?.onClick.AddListener(() => OnAttackPressed?.Invoke());
        runButton?.onClick.AddListener(()   => OnRunPressed?.Invoke());

        // ✅ This is intentional — the action panel hides at start
        // and only shows when it is the player's turn
        ShowActionSelector(false);
        Debug.Log("[BattleDialogBox] Awake complete. Action panel hidden on purpose — shows when player's turn starts.");
    }

    // ── Dialog ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Displays text character-by-character (typewriter effect).
    /// Await this coroutine; it pauses briefly after finishing.
    /// </summary>
    public IEnumerator TypeDialog(string message)
    {
        dialogText.text = "";

        foreach (char c in message)
        {
            dialogText.text += c;
            yield return new WaitForSeconds(1f / typeSpeed);
        }

        yield return new WaitForSeconds(0.6f); // brief pause before next line
    }

    /// <summary>Sets the dialog text instantly (no typewriter effect).</summary>
    public void SetMessage(string message)
    {
        dialogText.text = message;
    }

    // ── Action selector ──────────────────────────────────────────────────────

    public void ShowActionSelector(bool visible)
    {
        actionPanel?.SetActive(visible);
    }

    public void EnableButtons(bool enabled)
    {
        if (attackButton) attackButton.interactable = enabled;
        if (runButton)    runButton.interactable    = enabled;
    }
}
