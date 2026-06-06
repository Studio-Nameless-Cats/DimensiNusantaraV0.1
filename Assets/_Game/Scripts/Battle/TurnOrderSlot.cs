using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

// One circle in the turn-order bar. Shows the character's icon, gets highlighted when
// it's their turn, and greys out if they faint.
//
// Prefab setup:
//   Root GameObject  (50x50, Image = circle background, + TurnOrderSlot script)
//     - Child Image  (50x50, no sprite; set at runtime)  -> assign to 'iconImage'
public class TurnOrderSlot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image background; // the circle behind the icon
    [SerializeField] private Image iconImage;  // the character/enemy icon sprite

    private static readonly Color PlayerColor  = new Color(0.25f, 0.50f, 0.90f, 0.85f);
    private static readonly Color EnemyColor   = new Color(0.85f, 0.28f, 0.28f, 0.85f);
    private static readonly Color ActiveColor  = new Color(0.10f, 0.10f, 0.10f, 1.00f);
    private static readonly Color FaintedColor = new Color(0.40f, 0.40f, 0.40f, 0.30f);

    private Color baseColor;
    private const float ActiveScale = 1.18f;   // how much the acting slot pumps up
    private MotionProfile _profile;            // optional; null = instant scale like before

    // Call once at battle start to set the icon and the team colour. Pass a profile to
    // make the active-turn scale animate (pop) instead of snapping.
    public void Initialise(Sprite icon, bool isPlayer, MotionProfile profile = null)
    {
        _profile         = profile;
        baseColor        = isPlayer ? PlayerColor : EnemyColor;
        background.color = baseColor;

        if (iconImage != null)
        {
            iconImage.sprite           = icon;
            iconImage.color            = Color.white;
            // If the CharacterData SO has no icon sprite, just hide the icon image.
            iconImage.gameObject.SetActive(icon != null);
        }

        transform.localScale = Vector3.one;
    }

    // Call this whenever the active turn changes.
    public void SetActive(bool isActive)
    {
        background.color = isActive ? ActiveColor : baseColor;

        // Pump the active slot up a touch so it stands out — tweened if we have a
        // profile, snapped otherwise.
        float target = isActive ? ActiveScale : 1f;
        if (_profile != null)
        {
            transform.DOKill();
            ((RectTransform)transform)
                .DOScale(Vector3.one * target, _profile.selectPopDuration)
                .SetEase(_profile.selectPopEase, _profile.selectPopOvershoot)
                .SetUpdate(_profile.useUnscaledTime)
                .SetLink(gameObject);
        }
        else
        {
            transform.localScale = Vector3.one * target;
        }

        // Keep the icon full-bright no matter what.
        if (iconImage != null)
            iconImage.color = Color.white;
    }

    // Call this when the unit faints to grey the slot out.
    public void SetFainted()
    {
        background.color = FaintedColor;
        if (_profile != null) transform.DOKill();
        transform.localScale = Vector3.one;

        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, 0.30f);
    }
}
