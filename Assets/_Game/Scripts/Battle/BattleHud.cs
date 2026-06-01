using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays HP for one battle unit with a framed, two-layer "damage trail" bar.
///
/// How the two layers read:
///   • <see cref="hpSlider"/>      — the MAIN fill. Snaps quickly to the new HP.
///   • <see cref="damageTrailSlider"/> (optional) — a CHIP bar sitting BEHIND the
///     main fill. On damage it holds at the old value, pauses briefly, then drains
///     down to meet the main fill — the classic "you lost this much" flash.
///     On heal it jumps up instantly so it never sits below the main fill.
///
/// Colours come from the shared <see cref="UITheme"/> (via UIThemeProvider.Active)
/// so the bar matches the rest of the batik/wood UI. If no theme is present it
/// falls back to plain green/yellow/red.
///
/// ── Unity setup (one per BattleUnit) ────────────────────────────────────────
///   NameText          → TextMeshProUGUI
///   HpSlider          → Slider (Min=0, Max=1, Interactable OFF). Its Fill Image
///                       is what gets tinted by the HP gradient.
///   DamageTrailSlider → (optional) a second Slider stacked directly UNDER the
///                       HpSlider in the hierarchy (drawn first = behind). Same
///                       rect/size. Give its Fill a flat trail colour (see
///                       trailColor). Min=0, Max=1, Interactable OFF.
///   HpText            → TextMeshProUGUI ("current / max")
///   Frame             → (optional) the framed panel RectTransform to punch on hit.
///                       Add a ThemedElement (Role = Panel) to it for the 9-slice.
/// </summary>
public class BattleHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Tooltip("Optional chip/lag bar stacked behind the main fill. Leave null to disable the trail effect.")]
    [SerializeField] private Slider damageTrailSlider;

    [Tooltip("Optional frame RectTransform to punch-scale when the unit takes damage.")]
    [SerializeField] private RectTransform frame;

    [Header("Main fill tween")]
    [Tooltip("Main bar speed, slider-units per second.")]
    [SerializeField] private float animSpeed = 1.5f;

    [Header("Damage trail")]
    [Tooltip("Seconds the trail holds before it starts draining.")]
    [SerializeField] private float trailDelay = 0.35f;

    [Tooltip("Trail drain speed, slider-units per second (slower than the main bar reads best).")]
    [SerializeField] private float trailSpeed = 0.6f;

    [Tooltip("Flat colour of the chip/trail fill (the 'damage taken' streak).")]
    [SerializeField] private Color trailColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    [Header("Hit punch")]
    [Tooltip("Frame scale punch on damage. 1 = no punch.")]
    [SerializeField] private float hitPunchScale = 1.06f;
    [SerializeField] private float hitPunchTime  = 0.12f;

    // ── State ────────────────────────────────────────────────────────────────
    private float     targetFill;
    private Coroutine mainCoroutine;
    private Coroutine trailCoroutine;
    private Coroutine punchCoroutine;
    private Vector3   frameBaseScale = Vector3.one;

    void Awake()
    {
        if (frame != null) frameBaseScale = frame.localScale;

        // Paint the trail fill once so the chip colour is correct.
        var trailFill = damageTrailSlider != null ? damageTrailSlider.fillRect?.GetComponent<Image>() : null;
        if (trailFill != null) trailFill.color = trailColor;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Populates the HUD instantly (called when battle starts).</summary>
    public void SetData(PartyMember member)
    {
        if (nameText != null) nameText.text = member.Name;

        targetFill = NormalizedHp(member);
        hpSlider.value = targetFill;
        if (damageTrailSlider != null) damageTrailSlider.value = targetFill;

        UpdateFillColor();
        RefreshHpText(member);
    }

    /// <summary>Updates the HP bar with tween + damage trail (called after damage/heal).</summary>
    public void UpdateHP(PartyMember member)
    {
        float previous = targetFill;
        targetFill = NormalizedHp(member);
        RefreshHpText(member);

        bool tookDamage = targetFill < previous - 0.0001f;

        // Main fill always tweens toward the new value.
        if (mainCoroutine != null) StopCoroutine(mainCoroutine);
        mainCoroutine = StartCoroutine(AnimateMain());

        if (damageTrailSlider != null)
        {
            if (tookDamage)
            {
                // Trail holds at the old value, then drains down to meet the fill.
                if (trailCoroutine != null) StopCoroutine(trailCoroutine);
                trailCoroutine = StartCoroutine(AnimateTrail());
            }
            else
            {
                // Heal (or no change): trail jumps up so it never sits below the fill.
                if (trailCoroutine != null) { StopCoroutine(trailCoroutine); trailCoroutine = null; }
                damageTrailSlider.value = targetFill;
            }
        }

        if (tookDamage && frame != null && hitPunchScale > 1f)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchFrame());
        }
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator AnimateMain()
    {
        while (!Mathf.Approximately(hpSlider.value, targetFill))
        {
            hpSlider.value = Mathf.MoveTowards(hpSlider.value, targetFill, animSpeed * Time.deltaTime);
            UpdateFillColor();
            yield return null;
        }
        hpSlider.value = targetFill;
        UpdateFillColor();
    }

    private IEnumerator AnimateTrail()
    {
        yield return new WaitForSeconds(trailDelay);
        while (damageTrailSlider.value > targetFill + 0.0001f)
        {
            damageTrailSlider.value = Mathf.MoveTowards(damageTrailSlider.value, targetFill, trailSpeed * Time.deltaTime);
            yield return null;
        }
        damageTrailSlider.value = targetFill;
    }

    private IEnumerator PunchFrame()
    {
        Vector3 peak = frameBaseScale * hitPunchScale;
        float half = hitPunchTime * 0.5f;

        // Out
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            frame.localScale = Vector3.Lerp(frameBaseScale, peak, t / half);
            yield return null;
        }
        // Back
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            frame.localScale = Vector3.Lerp(peak, frameBaseScale, t / half);
            yield return null;
        }
        frame.localScale = frameBaseScale;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Tints the main fill via the theme HP gradient (green→gold→red).</summary>
    private void UpdateFillColor()
    {
        var fill = hpSlider.fillRect?.GetComponent<Image>();
        if (fill == null) return;

        var theme = UIThemeProvider.Active;
        fill.color = theme != null
            ? theme.HpColor(hpSlider.value)
            : (hpSlider.value > 0.5f ? Color.green
             : hpSlider.value > 0.25f ? Color.yellow
             : Color.red);
    }

    private void RefreshHpText(PartyMember member)
    {
        if (hpText != null)
            hpText.text = $"{member.CurrentHp} / {member.MaxHp}";
    }

    private static float NormalizedHp(PartyMember member)
        => (float)member.CurrentHp / member.MaxHp;
}
