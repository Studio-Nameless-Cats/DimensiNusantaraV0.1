using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One status-effect badge in a unit's HUD: an icon (optionally tinted with the status'
// colour) plus a little "turns left" number. These get pooled and driven by
// BattleHud.ShowStatuses, so you never wire one up per status, just build a single prefab.
//
// Prefab setup:
//   Root (this script) with an Image -> assign to iconImage.
//   Optional child TextMeshProUGUI (bottom-right) -> assign to durationText.
// Keep it small (say 28x28). Drop the prefab + a container with a Horizontal Layout
// Group onto the BattleHud (StatusIconPrefab / StatusIconContainer).
public class StatusIcon : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI durationText;

    void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
    }

    // Draw this badge for one active status.
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
