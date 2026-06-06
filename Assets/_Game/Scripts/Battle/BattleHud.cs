using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

// The little HP box above one battle unit, with a two-layer "damage trail" bar.
//
// Two bars stacked on top of each other:
//   - hpSlider: the main fill. Snaps quickly to the new HP.
//   - damageTrailSlider (optional): a "chip" bar sitting behind the main one.
//     When you take damage it stays at the old value for a beat, then slides
//     down to catch up. That's the classic "ouch, you lost this much" flash.
//     On a heal it just jumps up instantly so it never lags below the main fill.
//
// Colours come from the shared UITheme (UIThemeProvider.Active) so the bar
// matches the rest of the batik/wood UI. No theme around? Falls back to plain
// green/yellow/red.
//
// Unity setup (one of these per BattleUnit):
//   NameText          -> TextMeshProUGUI
//   HpSlider          -> Slider (Min=0, Max=1, Interactable OFF). Its Fill Image
//                        is the bit that gets tinted by the HP gradient.
//   DamageTrailSlider -> (optional) a second Slider stacked right under HpSlider
//                        in the hierarchy (drawn first = behind). Same size.
//                        Give its Fill a flat trail colour (see trailColor).
//   HpText            -> TextMeshProUGUI ("current / max")
//   Frame             -> (optional) the framed panel to punch-scale on a hit.
//                        Add a ThemedElement (Role = Panel) for the 9-slice.
public class BattleHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("MP (optional)")]
    [Tooltip("Mana bar. Leave null on enemy HUDs (enemies don't show MP).")]
    [SerializeField] private Slider          mpSlider;
    [Tooltip("Optional 'current / max' MP label.")]
    [SerializeField] private TextMeshProUGUI mpText;
    [Tooltip("Flat colour for the MP fill.")]
    [SerializeField] private Color           mpColor = new Color(0.30f, 0.55f, 0.95f, 1f);

    [Header("Special gauge (optional)")]
    [Tooltip("Per-battle Special gauge (0..100). Leave null on enemy HUDs.")]
    [SerializeField] private Slider          specialSlider;
    [Tooltip("Optional Special % label.")]
    [SerializeField] private TextMeshProUGUI specialText;
    [Tooltip("Fill colour when the Special gauge is NOT yet full.")]
    [SerializeField] private Color           specialColor      = new Color(0.95f, 0.75f, 0.20f, 1f);
    [Tooltip("Fill colour once the Special gauge is full (ready to unleash).")]
    [SerializeField] private Color           specialReadyColor = new Color(1f, 0.45f, 0.15f, 1f);

    [Header("Status effects (optional)")]
    [Tooltip("Parent (with a Horizontal Layout Group) that holds the status badges. Leave null to disable the status display.")]
    [SerializeField] private Transform  statusIconContainer;
    [Tooltip("Prefab with a StatusIcon component (icon + duration text). Pooled under the container.")]
    [SerializeField] private GameObject statusIconPrefab;

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

    [Header("Entrance motion (optional)")]
    [Tooltip("Assign to make this HUD pop in when the battle starts. Leave null to just appear.")]
    [SerializeField] private MotionProfile motionProfile;
    [Tooltip("Stagger this HUD's pop-in by this much, so several HUDs cascade instead of popping together. Set per unit (0, 0.06, 0.12...).")]
    [SerializeField] private float entranceDelay = 0f;

    // Internal bookkeeping.
    private float     targetFill;
    private Coroutine mainCoroutine;
    private Coroutine trailCoroutine;
    private Coroutine punchCoroutine;
    private Vector3   frameBaseScale = Vector3.one;
    private readonly System.Collections.Generic.List<StatusIcon> statusIconPool = new System.Collections.Generic.List<StatusIcon>();

    void Awake()
    {
        if (frame != null) frameBaseScale = frame.localScale;

        // Paint the trail fill once so the chip colour is correct.
        var trailFill = damageTrailSlider != null ? damageTrailSlider.fillRect?.GetComponent<Image>() : null;
        if (trailFill != null) trailFill.color = trailColor;

        // Paint the MP fill once (flat colour).
        var mpFill = mpSlider != null ? mpSlider.fillRect?.GetComponent<Image>() : null;
        if (mpFill != null) mpFill.color = mpColor;
    }

    // Fill in everything instantly. Called when the battle kicks off.
    public void SetData(PartyMember member)
    {
        if (nameText != null) nameText.text = member.Name;

        targetFill = NormalizedHp(member);
        hpSlider.value = targetFill;
        if (damageTrailSlider != null) damageTrailSlider.value = targetFill;

        UpdateFillColor();
        RefreshHpText(member);

        UpdateResources(member);
        ShowStatuses(member.Statuses);   // wipes any leftover badges (none at battle start)

        PlayEntrance();
    }

    // Pops the whole HUD in when the battle opens. Scale + fade only, so it doesn't
    // matter where the HUD sits or whether it's following a unit. Optional: no
    // profile wired means the HUD just appears like before.
    private void PlayEntrance()
    {
        if (motionProfile == null) return;
        RectTransform rt = transform as RectTransform;
        if (rt == null) return;

        rt.DOKill();
        Vector3 home = rt.localScale;
        CanvasGroup cg = GetComponent<CanvasGroup>();   // optional fade if one's present
        rt.ScalePopIn(motionProfile, home, cg).SetDelay(entranceDelay);
    }

    // Redraws the status-effect badges from whatever statuses the member has right now.
    // Reuses one StatusIcon per status and hides the spares. Does nothing if the
    // optional container/prefab aren't wired (e.g. enemy HUDs don't bother with these).
    public void ShowStatuses(System.Collections.Generic.IReadOnlyList<StatusEffectInstance> statuses)
    {
        if (statusIconContainer == null || statusIconPrefab == null) return;

        int count = statuses != null ? statuses.Count : 0;

        // Make more icons if we don't have enough for all the active statuses.
        while (statusIconPool.Count < count)
        {
            var go  = Instantiate(statusIconPrefab, statusIconContainer);
            var ico = go.GetComponent<StatusIcon>();
            if (ico == null)
            {
                Debug.LogError("[BattleHud] statusIconPrefab has no StatusIcon component!");
                Destroy(go);
                break;
            }
            statusIconPool.Add(ico);
        }

        // Fill in the ones we need, hide the rest.
        for (int i = 0; i < statusIconPool.Count; i++)
        {
            if (i < count) statusIconPool[i].Set(statuses[i]);
            else           statusIconPool[i].gameObject.SetActive(false);
        }
    }

    // Repaints the MP bar and Special gauge instantly (no animation). Call this
    // whenever a member spends MP or charges up. Does nothing if those optional
    // sliders aren't wired (enemy HUDs skip them).
    public void UpdateResources(PartyMember member)
    {
        if (mpSlider != null)
        {
            mpSlider.value = member.MaxMp > 0 ? (float)member.CurrentMp / member.MaxMp : 0f;
            if (mpText != null) mpText.text = $"{member.CurrentMp} / {member.MaxMp}";
        }

        if (specialSlider != null)
        {
            float n = (float)member.CurrentSpecial / PartyMember.SpecialMax;
            specialSlider.value = n;
            if (specialText != null) specialText.text = $"{member.CurrentSpecial}%";

            var spFill = specialSlider.fillRect?.GetComponent<Image>();
            if (spFill != null)
                spFill.color = member.CurrentSpecial >= PartyMember.SpecialMax ? specialReadyColor : specialColor;
        }
    }

    // Updates the HP bar with the slide animation + damage trail. Call after damage/heal.
    public void UpdateHP(PartyMember member)
    {
        float previous = targetFill;
        targetFill = NormalizedHp(member);
        RefreshHpText(member);
        UpdateResources(member);   // taking a hit often charges the Special gauge too

        bool tookDamage = targetFill < previous - 0.0001f;

        // The main fill always slides toward the new value.
        if (mainCoroutine != null) StopCoroutine(mainCoroutine);
        mainCoroutine = StartCoroutine(AnimateMain());

        if (damageTrailSlider != null)
        {
            if (tookDamage)
            {
                // Trail waits at the old value, then drains down to meet the fill.
                if (trailCoroutine != null) StopCoroutine(trailCoroutine);
                trailCoroutine = StartCoroutine(AnimateTrail());
            }
            else
            {
                // Healed (or no change): snap the trail up so it never sits below the fill.
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

    // The actual animation loops.

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

        // Scale up...
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            frame.localScale = Vector3.Lerp(frameBaseScale, peak, t / half);
            yield return null;
        }
        // ...and back down.
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            frame.localScale = Vector3.Lerp(peak, frameBaseScale, t / half);
            yield return null;
        }
        frame.localScale = frameBaseScale;
    }

    // Small helpers.

    // Tints the main fill using the theme's HP gradient (green to gold to red).
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
