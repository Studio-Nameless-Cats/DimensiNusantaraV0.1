using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

// The dice-roll popup. It cuts into a basic attack and lets the player roll a D20 to
// try for a critical hit.
//
// How it goes:
//   1. BattleSystem calls Show() when the crit chance rolls in our favour.
//   2. The popup shows up and a 3-second timer starts ticking down.
//   3. Player taps [LEMPAR DADU], or the timer runs out and it auto-rolls.
//   4. The die spins through numbers, then lands on a value (1 to 20).
//   5. Land on critThreshold or higher and it's a Critical Hit (double damage).
//   6. Popup closes and onComplete(isCrit) reports back to BattleSystem.
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

    [Header("Motion (optional)")]
    [Tooltip("Assign to make the dice popup punch in. Leave null for instant.")]
    [SerializeField] private MotionProfile motionProfile;
    [Tooltip("The modal CONTENT panel to scale in (not the full-screen dim). Defaults to modalRoot if left empty.")]
    [SerializeField] private RectTransform popTarget;

    [Header("Settings")]
    [Tooltip("Seconds before auto-roll fires.")]
    [SerializeField] private float timerDuration    = 3f;
    [Tooltip("D20 value at or above this = Critical Hit.")]
    [SerializeField] private int   critThreshold    = 11;
    [Tooltip("How long the die-number animation plays before settling.")]
    [SerializeField] private float rollAnimDuration = 0.75f;
    [Tooltip("Interval between number changes during animation.")]
    [SerializeField] private float rollAnimInterval = 0.05f;

    private static readonly Color CritColor   = new Color(1.00f, 0.85f, 0.10f); // gold
    private static readonly Color NormalColor = new Color(0.80f, 0.80f, 0.80f); // grey

    private bool playerPressedRoll;
    private Vector3 _popHome = Vector3.one;   // pop target's resting scale, captured once
    private bool    _popHomeSet;

    void Awake()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
        rollButton?.onClick.AddListener(OnRollPressed);
    }

    // BattleSystem calls this. The coroutine finishes once the popup closes, and
    // onComplete gets true for a crit, false for a normal hit.
    public IEnumerator Show(string attackerName, string targetName, Action<bool> onComplete)
    {
        // Reset everything to a clean slate.
        playerPressedRoll = false;

        if (subtitleText)   subtitleText.text = $"{attackerName} menyerang {targetName}";
        if (dieValueText)   dieValueText.text  = "?";
        if (resultText)     { resultText.text  = ""; resultText.gameObject.SetActive(false); }
        if (timerBar)       timerBar.value     = 1f;
        if (rollButton)     rollButton.interactable = true;

        modalRoot.SetActive(true);
        PlayPopIn();

        // Tick the timer down (or stop early if they tap the roll button).
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

        // Time to actually roll.
        if (rollButton) rollButton.interactable = false;
        if (autoRollHintText) autoRollHintText.text = "";

        int rollResult = UnityEngine.Random.Range(1, 21); // gives 1 to 20
        yield return StartCoroutine(AnimateDie(rollResult));

        // Did we crit?
        bool isCrit = rollResult >= critThreshold;

        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text  = isCrit ? "CRITICAL HIT!" : "Normal...";
            resultText.color = isCrit ? CritColor : NormalColor;
        }

        Debug.Log($"[DiceRollUI] Rolled {rollResult} (threshold {critThreshold}) -> {(isCrit ? "CRITICAL" : "Normal")}");

        yield return new WaitForSeconds(1.2f);

        // All done, close up.
        modalRoot.SetActive(false);
        onComplete?.Invoke(isCrit);
    }

    private void OnRollPressed()
    {
        playerPressedRoll = true;
    }

    // Punchy scale-in for the dice modal. Animates popTarget (or modalRoot if that's
    // not set). No profile wired = it just appears.
    private void PlayPopIn()
    {
        if (motionProfile == null) return;
        RectTransform rt = popTarget != null ? popTarget : modalRoot.transform as RectTransform;
        if (rt == null) return;

        if (!_popHomeSet) { _popHome = rt.localScale; _popHomeSet = true; }
        rt.DOKill();
        rt.ScalePopIn(motionProfile, _popHome);
    }

    // Spins the die number for a bit, then lands it on the real value.
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
