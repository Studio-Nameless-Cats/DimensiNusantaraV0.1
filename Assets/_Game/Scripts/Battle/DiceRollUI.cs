using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dice Roll modal — interrupts Basic Attack to let the player roll a D20 for a critical hit.
///
/// Flow:
///   1. BattleSystem calls Show() when a 30% crit chance triggers.
///   2. Modal appears, timer counts down 3 seconds.
///   3. Player taps [LEMPAR DADU] or timer runs out → auto-roll.
///   4. Die animates, settles on a final value (1–20).
///   5. Result ≥ critThreshold → Critical Hit (2× damage).
///   6. Modal closes, onComplete(isCrit) fires back to BattleSystem.
///
/// Scene setup — see Unity Editor guide below.
/// </summary>
public class DiceRollUI : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The entire modal + overlay panel. Set inactive by default in the scene.")]
    [SerializeField] private GameObject modalRoot;

    [Header("Dynamic Text")]
    [Tooltip("e.g. 'Bima menyerang Buto Ijo'")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [Tooltip("Shows '?' while waiting, then the rolled number.")]
    [SerializeField] private TextMeshProUGUI dieValueText;
    [Tooltip("Shows 'CRITICAL HIT!' or 'Normal...' after roll.")]
    [SerializeField] private TextMeshProUGUI resultText;
    [Tooltip("Small hint: 'Auto-roll dalam X detik'")]
    [SerializeField] private TextMeshProUGUI autoRollHintText;

    [Header("Controls")]
    [SerializeField] private Slider timerBar;
    [SerializeField] private Button rollButton;

    [Header("Settings")]
    [Tooltip("Seconds before auto-roll fires.")]
    [SerializeField] private float timerDuration    = 3f;
    [Tooltip("D20 value at or above this = Critical Hit.")]
    [SerializeField] private int   critThreshold    = 11;
    [Tooltip("How long the die-number animation plays before settling.")]
    [SerializeField] private float rollAnimDuration = 0.75f;
    [Tooltip("Interval between number changes during animation.")]
    [SerializeField] private float rollAnimInterval = 0.05f;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color CritColor   = new Color(1.00f, 0.85f, 0.10f); // gold
    private static readonly Color NormalColor = new Color(0.80f, 0.80f, 0.80f); // grey

    // ── State ─────────────────────────────────────────────────────────────────
    private bool playerPressedRoll;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
        rollButton?.onClick.AddListener(OnRollPressed);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from BattleSystem. Coroutine completes when the modal closes.
    /// onComplete is called with true if crit, false if normal.
    /// </summary>
    public IEnumerator Show(string attackerName, string targetName, Action<bool> onComplete)
    {
        // ── Reset ─────────────────────────────────────────────────────────────
        playerPressedRoll = false;

        if (subtitleText)   subtitleText.text = $"{attackerName} menyerang {targetName}";
        if (dieValueText)   dieValueText.text  = "?";
        if (resultText)     { resultText.text  = ""; resultText.gameObject.SetActive(false); }
        if (timerBar)       timerBar.value     = 1f;
        if (rollButton)     rollButton.interactable = true;

        modalRoot.SetActive(true);

        // ── Countdown ─────────────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < timerDuration && !playerPressedRoll)
        {
            elapsed += Time.deltaTime;
            if (timerBar) timerBar.value = 1f - (elapsed / timerDuration);
            if (autoRollHintText)
            {
                int secsLeft = Mathf.CeilToInt(timerDuration - elapsed);
                autoRollHintText.text = $"Auto-roll dalam {secsLeft} detik";
            }
            yield return null;
        }

        // ── Roll ──────────────────────────────────────────────────────────────
        if (rollButton) rollButton.interactable = false;
        if (autoRollHintText) autoRollHintText.text = "";

        int rollResult = UnityEngine.Random.Range(1, 21); // 1–20 inclusive
        yield return StartCoroutine(AnimateDie(rollResult));

        // ── Show result ───────────────────────────────────────────────────────
        bool isCrit = rollResult >= critThreshold;

        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text  = isCrit ? "✦ CRITICAL HIT! ✦" : "Normal...";
            resultText.color = isCrit ? CritColor : NormalColor;
        }

        Debug.Log($"[DiceRollUI] Rolled {rollResult} (threshold {critThreshold}) → {(isCrit ? "CRITICAL" : "Normal")}");

        yield return new WaitForSeconds(1.2f);

        // ── Close ─────────────────────────────────────────────────────────────
        modalRoot.SetActive(false);
        onComplete?.Invoke(isCrit);
    }

    // ── Button callback ───────────────────────────────────────────────────────

    private void OnRollPressed()
    {
        playerPressedRoll = true;
    }

    // ── Die animation ─────────────────────────────────────────────────────────

    private IEnumerator AnimateDie(int finalValue)
    {
        if (dieValueText == null) yield break;

        float elapsed = 0f;
        while (elapsed < rollAnimDuration)
        {
            elapsed          += rollAnimInterval;
            dieValueText.text = UnityEngine.Random.Range(1, 21).ToString();
            yield return new WaitForSeconds(rollAnimInterval);
        }

        dieValueText.text = finalValue.ToString();
    }
}
