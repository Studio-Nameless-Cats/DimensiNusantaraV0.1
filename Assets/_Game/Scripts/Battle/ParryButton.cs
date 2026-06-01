using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Precision tier for a single parry tap.</summary>
public enum ParryTier { Miss, Good, Perfect }

/// <summary>
/// One Osu-style TAP circle in a Parry prompt. A FIXED hit circle (this button)
/// sits still while a separate APPROACH RING shrinks from large down onto it.
/// The player taps when the ring overlaps the hit circle; how closely they align
/// at tap-time sets the precision tier (Perfect / Good / Miss).
///
/// Flow:
///   1. ParrySystem calls Activate(duration).
///   2. The approach ring shrinks startSize → minSize over 'duration', passing
///      through the hit-circle size (= perfect alignment) partway through.
///   3. Tap inside the scoring band → Perfect/Good (window closes).
///      Tap while the ring is still far too big → ignored (too early, keep waiting).
///      Ring shrinks past the band / time runs out with no tap → Miss.
///   4. Brief tier-coloured feedback, then the button hides itself.
///
/// Prefab layout:
///   Root (hit circle: Button + Image + ParryButton)
///     ├── LabelText    (TMP — "TAP")
///     └── ApproachRing (Image — HOLLOW ring sprite; size driven at runtime)
///
/// Assign 'approachRing' to the ApproachRing child's RectTransform. The script
/// forces it concentric (centre anchor + pivot, zero offset) every Activate, so a
/// mis-anchored prefab can't drift it off-centre while it scales.
/// </summary>
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

    // ── State ─────────────────────────────────────────────────────────────────
    /// <summary>Precision tier of this circle once its window has closed.</summary>
    public ParryTier Result { get; private set; } = ParryTier.Miss;

    private bool  windowOpen      = false;
    private float currentRingSize = 0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(HandleTap);
        else
            Debug.LogWarning("[ParryButton] No Button component on root. ❌ Add one.");

        // Start hidden — ParrySystem will enable via Activate()
        gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full lifecycle of one parry circle:
    ///   show → ring shrinks over 'duration' → tier feedback → hide.
    /// Await this coroutine; read <see cref="Result"/> afterward.
    /// </summary>
    public IEnumerator Activate(float duration)
    {
        // ── Reset ─────────────────────────────────────────────────────────────
        Result          = ParryTier.Miss;
        windowOpen      = true;
        currentRingSize = approachRingStartSize;

        if (buttonImage) buttonImage.color = normalColor;

        if (approachRing)
        {
            // Force concentric so scaling can never drift the ring off the hit circle.
            approachRing.anchorMin        = new Vector2(0.5f, 0.5f);
            approachRing.anchorMax        = new Vector2(0.5f, 0.5f);
            approachRing.pivot            = new Vector2(0.5f, 0.5f);
            approachRing.anchoredPosition = Vector2.zero;
            approachRing.sizeDelta        = new Vector2(approachRingStartSize, approachRingStartSize);
            approachRing.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);

        // ── Shrink ring start → min (passes through hitCircleSize = perfect) ────
        float elapsed = 0f;
        while (elapsed < duration && windowOpen)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            currentRingSize = Mathf.Lerp(approachRingStartSize, approachRingMinSize, t);
            if (approachRing)
                approachRing.sizeDelta = new Vector2(currentRingSize, currentRingSize);

            yield return null;
        }

        windowOpen = false;

        // ── Feedback (tier colour) ──────────────────────────────────────────────
        if (buttonImage)
            buttonImage.color = Result == ParryTier.Perfect ? perfectColor
                              : Result == ParryTier.Good    ? goodColor
                                                            : missColor;

        if (approachRing) approachRing.gameObject.SetActive(false);

        yield return new WaitForSeconds(feedbackDuration);

        // ── Cleanup ───────────────────────────────────────────────────────────
        if (approachRing) approachRing.gameObject.SetActive(true); // restore for reuse
        gameObject.SetActive(false);

        Debug.Log($"[ParryButton] Window closed — {Result}.");
    }

    // ── Tap handler ───────────────────────────────────────────────────────────

    private void HandleTap()
    {
        if (!windowOpen) return;

        // Too early — ring still well outside the hit circle. Ignore the tap and let
        // the player wait for alignment rather than punishing a premature click.
        if (currentRingSize > hitCircleSize + goodThreshold) return;

        float delta = Mathf.Abs(currentRingSize - hitCircleSize);
        Result     = delta <= perfectThreshold ? ParryTier.Perfect
                   : delta <= goodThreshold    ? ParryTier.Good
                                               : ParryTier.Miss;
        windowOpen = false; // closes the Activate loop early
        Debug.Log($"[ParryButton] Tapped at ring {currentRingSize:F0}px (Δ{delta:F0}) → {Result}.");
    }
}
