using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One status-effect badge in a unit's HUD: an icon (optionally tinted by the status'
/// colour) with a small "turns remaining" number. Pooled and driven by
/// <see cref="BattleHud.ShowStatuses"/> — you never set this up per-status, just build
/// one prefab.
///
/// ── Prefab setup ─────────────────────────────────────────────────────────────
///   Root (this script) with an Image  → assign to <see cref="iconImage"/>.
///   Optional child TextMeshProUGUI (bottom-right) → assign to <see cref="durationText"/>.
/// Keep it small (e.g. 28×28). Drop the prefab + a container with a Horizontal Layout
/// Group on the BattleHud (StatusIconPrefab / StatusIconContainer).
/// </summary>
public class StatusIcon : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI durationText;

    void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
    }

    /// <summary>Paint this badge for one active status instance.</summary>
    public void Set(StatusEffectInstance instance)
    {
        if (instance == null || instance.Data == null) { gameObject.SetActive(false); return; }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite  = instance.Data.Icon;
            iconImage.color   = instance.Data.Tint;
            iconImage.enabled = instance.Data.Icon != null;
        }

        if (durationText != null)
            durationText.text = instance.TurnsRemaining.ToString();
    }
}
