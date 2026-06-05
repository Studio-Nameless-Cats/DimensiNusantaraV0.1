using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// How well a single parry tap landed.
public enum ParryTier { Miss, Good, Perfect }

// One Osu-style TAP circle in a parry prompt. The hit circle (this button) just sits
// there while a separate approach ring shrinks down onto it from big. The player taps
// when the ring lines up with the hit circle, and how close they are when they tap
// decides the tier: Perfect, Good, or Miss.
//
// How it goes:
//   1. ParrySystem calls Activate(duration).
//   2. The ring shrinks from big down to small over 'duration', passing through the
//      hit-circle size (that's the perfect spot) somewhere in the middle.
//   3. Tap when the ring's close enough -> Perfect or Good, and the window closes.
//      Tap way too early while the ring's still huge -> ignored, just keep waiting.
//      Ring shrinks past the sweet spot or time runs out with no tap -> Miss.
//   4. Flash a colour for the tier, then the button hides itself.
//
// Prefab layout:
//   Root (hit circle: Button + Image + ParryButton)
//     - LabelText    (TMP, says "TAP")
//     - ApproachRing (Image, a HOLLOW ring sprite; its size is set at runtime)
//
// Assign 'approachRing' to the ApproachRing child's RectTransform. Every Activate the
// script forces it dead-centre (centre anchor + pivot, zero offset), so even a
// badly-anchored prefab can't make the ring drift off-centre as it shrinks.
public class ParryButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image         buttonImage;
    [SerializeField] private RectTransform approachRing; // outer shrinking ring

    [Header("Ring sizes (px diameter)")]
    [Tooltip("Diameter the approach ring starts at. Should be clearly larger than the hit circle.")]
    [SerializeField] private float approachRingStartSize = 220f;
    [Tooltip("Hit-circle diameter — the ring is 'aligned' (Perfect) when it matches this. Match the button size.")]
    [SerializeField] private float hitCircleSize         = 100f;
    [Tooltip("Diameter the ring keeps shrinking to AFTER passing the hit circle, so late taps are possible.")]
    [SerializeField] private float approachRingMinSize   = 55f;

    [Header("Scoring (px from hit-circle size at tap)")]
    [Tooltip("Tap within this size difference of the hit circle = Perfect.")]
    [SerializeField] private float perfectThreshold = 14f;
    [Tooltip("Tap within this size difference = Good. Beyond it (but tappable) = Miss.")]
    [SerializeField] private float goodThreshold    = 38f;

    [Header("Colors")]
    [SerializeField] private Color normalColor  = new Color(0.20f, 0.60f, 1.00f); // blue
    [SerializeField] private Color perfectColor = new Color(1.00f, 0.84f, 0.30f); // gold
    [SerializeField] private Color goodColor    = new Color(0.30f, 0.85f, 0.40f); // green
    [SerializeField] private Color missColor    = new Color(0.90f, 0.25f, 0.20f); // red
    [Tooltip("How long the tier-coloured feedback shows before the button disappears.")]
    [SerializeField] private float feedbackDuration = 0.25f;

    // How well this circle was tapped, readable once its window has closed.
    public ParryTier Result { get; private set; } = ParryTier.Miss;

    private bool  windowOpen      = false;
    private float currentRingSize = 0f;

    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(HandleTap);
        else
            Debug.LogWarning("[ParryButton] No Button component on root. Add one.");

        // Start hidden. ParrySystem turns us on via Activate().
        gameObject.SetActive(false);
    }

    // Handles one parry circle from start to finish: pop it up, shrink the ring over
    // 'duration', flash the tier colour, then hide. Await this, then read Result.
    // Pass an optional timerBar (0..1) to mirror the tap window: it starts full and
    // drains to 0 over 'duration', stopping early if they tap.
    public IEnumerator Activate(float duration, Slider timerBar = null)
    {
        // Reset for a fresh circle.
        Result          = ParryTier.Miss;
        windowOpen      = true;
        currentRingSize = approachRingStartSize;

        if (buttonImage) buttonImage.color = normalColor;
        if (timerBar)
        {
            timerBar.minValue = 0f;
            timerBar.maxValue = 1f;
            timerBar.value    = 1f;
        }

        if (approachRing)
        {
            // Force it dead-centre so shrinking can never push the ring off the hit circle.
            approachRing.anchorMin        = new Vector2(0.5f, 0.5f);
            approachRing.anchorMax        = new Vector2(0.5f, 0.5f);
            approachRing.pivot            = new Vector2(0.5f, 0.5f);
            approachRing.anchoredPosition = Vector2.zero;
            approachRing.sizeDelta        = new Vector2(approachRingStartSize, approachRingStartSize);
            approachRing.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);

        // Shrink the ring from big to small. It passes through hitCircleSize (the perfect
        // spot) on the way down.
        float elapsed = 0f;
        while (elapsed < duration && windowOpen)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            currentRingSize = Mathf.Lerp(approachRingStartSize, approachRingMinSize, t);
            if (approachRing)
                approachRing.sizeDelta = new Vector2(currentRingSize, currentRingSize);

            if (timerBar) timerBar.value = 1f - t;   // drains full to empty over the window

            yield return null;
        }

        windowOpen = false;
        if (timerBar) timerBar.value = 0f;            // snap to empty when the window closes (tap or timeout)

        // Flash the colour for whatever tier they got.
        if (buttonImage)
            buttonImage.color = Result == ParryTier.Perfect ? perfectColor
                              : Result == ParryTier.Good    ? goodColor
                                                            : missColor;

        if (approachRing) approachRing.gameObject.SetActive(false);

        yield return new WaitForSeconds(feedbackDuration);

        // Tidy up so the button's ready to be reused next time.
        if (approachRing) approachRing.gameObject.SetActive(true);
        gameObject.SetActive(false);

        Debug.Log($"[ParryButton] Window closed, result: {Result}.");
    }

    private void HandleTap()
    {
        if (!windowOpen) return;

        // Way too early, ring's still nowhere near the hit circle. Just ignore the tap
        // and let them wait for it to line up, rather than punishing an eager click.
        if (currentRingSize > hitCircleSize + goodThreshold) return;

        float delta = Mathf.Abs(currentRingSize - hitCircleSize);
        Result     = delta <= perfectThreshold ? ParryTier.Perfect
                   : delta <= goodThreshold    ? ParryTier.Good
                                               : ParryTier.Miss;
        windowOpen = false; // ends the shrink loop in Activate early
        Debug.Log($"[ParryButton] Tapped at ring {currentRingSize:F0}px (off by {delta:F0}) -> {Result}.");
    }
}
