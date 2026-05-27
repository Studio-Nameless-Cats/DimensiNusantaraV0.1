using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One circle in the Turn Order bar.
/// Shows the character's icon sprite, highlights when it's their turn, dims when fainted.
///
/// Prefab setup:
///   Root GameObject  (50×50, Image = circle background, + TurnOrderSlot script)
///     └── Child Image  (50×50, no sprite → assigned at runtime)  ← assign to 'iconImage'
/// </summary>
public class TurnOrderSlot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image background; // the circle behind the icon
    [SerializeField] private Image iconImage;  // the character/enemy icon sprite

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color PlayerColor  = new Color(0.25f, 0.50f, 0.90f, 0.85f);
    private static readonly Color EnemyColor   = new Color(0.85f, 0.28f, 0.28f, 0.85f);
    private static readonly Color ActiveColor  = new Color(0.10f, 0.10f, 0.10f, 1.00f);
    private static readonly Color FaintedColor = new Color(0.40f, 0.40f, 0.40f, 0.30f);

    private Color baseColor;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call once at battle start to assign the icon and team colour.</summary>
    public void Initialise(Sprite icon, bool isPlayer)
    {
        baseColor        = isPlayer ? PlayerColor : EnemyColor;
        background.color = baseColor;

        if (iconImage != null)
        {
            iconImage.sprite           = icon;
            iconImage.color            = Color.white;
            // Hide the icon image entirely if no sprite was assigned on the CharacterData SO
            iconImage.gameObject.SetActive(icon != null);
        }

        transform.localScale = Vector3.one;
    }

    /// <summary>Call every time the active turn changes.</summary>
    public void SetActive(bool isActive)
    {
        background.color     = isActive ? ActiveColor : baseColor;
        // Slightly enlarge the active slot so it clearly stands out
        transform.localScale = isActive ? Vector3.one * 1.18f : Vector3.one;

        // Keep icon fully bright regardless
        if (iconImage != null)
            iconImage.color = Color.white;
    }

    /// <summary>Call when this unit faints — greys out the slot.</summary>
    public void SetFainted()
    {
        background.color     = FaintedColor;
        transform.localScale = Vector3.one;

        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, 0.30f);
    }
}
