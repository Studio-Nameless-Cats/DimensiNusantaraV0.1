using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Osu-style TAP circle that appears during a Parry prompt.
///
/// Flow:
///   1. ParrySystem calls Activate(duration) to start the window.
///   2. A large approach ring shrinks down to the button size over 'duration'.
///   3. If the player taps before time runs out  → green flash, WasTapped = true.
///   4. If time runs out without a tap           → red flash,   WasTapped = false.
///   5. 0.25 s feedback pause, then the button hides itself.
///
/// Prefab layout:
///   Root          (100×100, Button + Image + ParryButton)
///     ├── LabelText   (TMP — "TAP")
///     └── ApproachRing (Image — circle/ring sprite, starts at ~220×220 in Editor
///                       but Activate() overrides size at runtime)
///
/// Assign 'approachRing' in the Inspector to the ApproachRing child's RectTransform.
/// </summary>
public class ParryButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image         buttonImage;
    [SerializeField] private RectTransform approachRing; // outer shrinking ring

    [Header("Settings")]
    [Tooltip("Diameter the approach ring starts at (px). Should be visibly larger than the button.")]
    [SerializeField] private float approachRingStartSize = 220f;
    [Tooltip("Diameter the ring shrinks to before the window closes (should match button size ~100).")]
    [SerializeField] private float approachRingEndSize   = 100f;
    [Tooltip("Button normal color.")]
    [SerializeField] private Color normalColor    = new Color(0.20f, 0.60f, 1.00f); // blue
    [Tooltip("Flash color when successfully tapped.")]
    [SerializeField] private Color tapSuccessColor = new Color(0.30f, 0.85f, 0.40f); // green
    [Tooltip("Flash color when the window expires without a tap.")]
    [SerializeField] private Color tapFailColor    = new Color(0.90f, 0.25f, 0.20f); // red
    [Tooltip("How long the visual feedback (green/red) is shown before the button disappears.")]
    [SerializeField] private float feedbackDuration = 0.25f;

    // ── State ─────────────────────────────────────────────────────────────────
    /// <summary>True if the player tapped this button during its active window.</summary>
    public bool WasTapped { get; private set; }

    private bool windowOpen = false;

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
    ///   show → ring shrinks over 'duration' → feedback → hide.
    /// Await this coroutine; check WasTapped afterward.
    /// </summary>
    public IEnumerator Activate(float duration)
    {
        // ── Reset ─────────────────────────────────────────────────────────────
        WasTapped  = false;
        windowOpen = true;

        if (buttonImage)      buttonImage.color = normalColor;
        if (approachRing)     approachRing.sizeDelta = new Vector2(approachRingStartSize, approachRingStartSize);

        gameObject.SetActive(true);

        // ── Shrink ring until tapped or window closes ──────────────────────
        float elapsed = 0f;
        while (elapsed < duration && windowOpen)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (approachRing)
            {
                float size = Mathf.Lerp(approachRingStartSize, approachRingEndSize, t);
                approachRing.sizeDelta = new Vector2(size, size);
            }

            yield return null;
        }

        windowOpen = false;

        // ── Feedback ──────────────────────────────────────────────────────────
        if (buttonImage)
            buttonImage.color = WasTapped ? tapSuccessColor : tapFailColor;

        // Hide approach ring during feedback
        if (approachRing) approachRing.gameObject.SetActive(false);

        yield return new WaitForSeconds(feedbackDuration);

        // ── Cleanup ───────────────────────────────────────────────────────────
        if (approachRing) approachRing.gameObject.SetActive(true); // restore for reuse
        gameObject.SetActive(false);

        Debug.Log($"[ParryButton] Window closed — {(WasTapped ? "TAPPED ✅" : "MISSED ❌")}");
    }

    // ── Tap handler ───────────────────────────────────────────────────────────

    private void HandleTap()
    {
        if (!windowOpen || WasTapped) return;
        WasTapped  = true;
        windowOpen = false; // closes the Activate loop early
        Debug.Log("[ParryButton] Tapped!");
    }
}
