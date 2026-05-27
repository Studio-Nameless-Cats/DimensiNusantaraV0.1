using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays HP information for one battle unit.
///
/// UI Setup (one per BattleUnit):
///   - NameText  → TextMeshProUGUI
///   - HpSlider  → Slider  (Min=0, Max=1)
///   - HpText    → TextMeshProUGUI  (shows "current / max")
///
/// The HP bar animates smoothly when UpdateHP() is called.
/// </summary>
public class BattleHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Animation")]
    [SerializeField] private float animSpeed = 1.5f; // slider units per second

    private float targetFill;
    private Coroutine animCoroutine;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Populates the HUD instantly (called when battle starts).</summary>
    public void SetData(PartyMember member)
    {
        nameText.text = member.Name;
        targetFill    = NormalizedHp(member);

        hpSlider.value = targetFill;
        RefreshHpText(member);
    }

    /// <summary>Updates HP bar with a smooth animation (called after damage/heal).</summary>
    public void UpdateHP(PartyMember member)
    {
        targetFill = NormalizedHp(member);
        RefreshHpText(member);

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateHpBar());
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private IEnumerator AnimateHpBar()
    {
        while (!Mathf.Approximately(hpSlider.value, targetFill))
        {
            hpSlider.value = Mathf.MoveTowards(hpSlider.value, targetFill, animSpeed * Time.deltaTime);
            UpdateSliderColor();
            yield return null;
        }
        hpSlider.value = targetFill;
        UpdateSliderColor();
    }

    /// <summary>Tints the HP bar fill green → yellow → red as HP drops.</summary>
    private void UpdateSliderColor()
    {
        var fill = hpSlider.fillRect?.GetComponent<Image>();
        if (fill == null) return;

        fill.color = hpSlider.value > 0.5f ? Color.green
                   : hpSlider.value > 0.25f ? Color.yellow
                   : Color.red;
    }

    private void RefreshHpText(PartyMember member)
    {
        if (hpText != null)
            hpText.text = $"{member.CurrentHp} / {member.MaxHp}";
    }

    private static float NormalizedHp(PartyMember member)
        => (float)member.CurrentHp / member.MaxHp;
}
