using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on any UI object to style it from the shared <see cref="UITheme"/>.
/// Pick a <see cref="Role"/> and the script applies the matching sprite, colour
/// and/or font from the active theme (resolved via <see cref="UIThemeProvider"/>).
/// One component covers panels, buttons, headers, body text and accents, so the
/// whole game re-skins by editing a single theme asset.
///
/// ── Roles ──────────────────────────────────────────────────────────────────
///   Panel        → Image set to 9-slice panel sprite, tinted wood-mid.
///   PanelDark    → same sprite, tinted wood-dark (nested / inset panels).
///   Parchment    → panel sprite tinted parchment (dialog interior).
///   Button       → Button + Image: applies button sprite + sets ColorBlock,
///                  and styles a child TMP label with the button font.
///   Header       → TMP_Text: header font, header size, gold colour.
///   BodyLight    → TMP_Text: body font, light colour (on dark surfaces).
///   BodyDark     → TMP_Text: body font, dark colour (on parchment).
///   AccentGold / AccentIndigo / AccentMaroon
///                → Image tinted to that accent (dividers, frames, side tags).
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Ensure the scene has a UIThemeProvider with a theme assigned.
///   2. Add this component to the UI object, choose the Role.
///   3. (Optional) override colour/sprite/font flags to skip parts you set by hand.
///   Works in edit mode too (ExecuteAlways) so you can preview in the Scene view
///   if a theme is assigned via the override field.
/// </summary>
[ExecuteAlways]
public class ThemedElement : MonoBehaviour
{
    public enum Role
    {
        Panel, PanelDark, Parchment,
        Button,
        Header, BodyLight, BodyDark,
        AccentGold, AccentIndigo, AccentMaroon
    }

    [Tooltip("Which themed style to apply to this object.")]
    [SerializeField] private Role role = Role.Panel;

    [Header("Overrides")]
    [Tooltip("If set, use this theme instead of the scene's UIThemeProvider. Useful for edit-mode preview.")]
    [SerializeField] private UITheme themeOverride;

    [Tooltip("Apply the theme colour. Turn off to keep a hand-picked colour.")]
    [SerializeField] private bool applyColor = true;

    [Tooltip("Apply the theme sprite (panels/buttons). Turn off to keep a hand-picked sprite.")]
    [SerializeField] private bool applySprite = true;

    [Tooltip("Apply the theme font (text/buttons). Turn off to keep a hand-picked font.")]
    [SerializeField] private bool applyFont = true;

    void OnEnable()
    {
        UIThemeProvider.Register(this);
        Apply(ResolveTheme());
    }

    void OnDisable()
    {
        UIThemeProvider.Unregister(this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Live preview while editing in the Inspector.
        if (!Application.isPlaying)
            Apply(ResolveTheme());
    }
#endif

    private UITheme ResolveTheme()
        => themeOverride != null ? themeOverride : UIThemeProvider.Active;

    /// <summary>Applies the given theme to this element according to its role.</summary>
    public void Apply(UITheme theme)
    {
        if (theme == null) return;

        switch (role)
        {
            case Role.Panel:        StylePanel(theme, theme.panelSprite, theme.woodMid);   break;
            case Role.PanelDark:    StylePanel(theme, theme.panelSprite, theme.woodDark);  break;
            case Role.Parchment:    StylePanel(theme, theme.panelSprite, theme.parchment); break;

            case Role.Button:       StyleButton(theme);                                    break;

            case Role.Header:       StyleText(theme, theme.headerFont,            theme.batikGold, theme.headerSize); break;
            case Role.BodyLight:    StyleText(theme, theme.BodyFontOrFallback,    theme.textLight, theme.bodySize);   break;
            case Role.BodyDark:     StyleText(theme, theme.BodyFontOrFallback,    theme.textDark,  theme.bodySize);   break;

            case Role.AccentGold:   StyleAccent(theme.batikGold); break;
            case Role.AccentIndigo: StyleAccent(theme.indigo);    break;
            case Role.AccentMaroon: StyleAccent(theme.maroon);    break;
        }
    }

    // ── Role implementations ──────────────────────────────────────────────────

    private void StylePanel(UITheme theme, Sprite sprite, Color color)
    {
        var img = GetComponent<Image>();
        if (img == null) return;

        if (applySprite && sprite != null)
        {
            img.sprite = sprite;
            img.type   = Image.Type.Sliced;
        }
        if (applyColor) img.color = color;
    }

    private void StyleButton(UITheme theme)
    {
        var img = GetComponent<Image>();
        if (img != null)
        {
            if (applySprite && theme.buttonSprite != null)
            {
                img.sprite = theme.buttonSprite;
                img.type   = Image.Type.Sliced;
            }
            if (applyColor) img.color = Color.white; // let ColorBlock drive tinting
        }

        var btn = GetComponent<Button>();
        if (btn != null && applyColor)
        {
            if (applySprite && theme.buttonPressedSprite != null)
            {
                btn.transition       = Selectable.Transition.SpriteSwap;
                var ss               = btn.spriteState;
                ss.pressedSprite     = theme.buttonPressedSprite;
                ss.selectedSprite    = theme.buttonSprite;
                ss.highlightedSprite = theme.buttonSprite;
                btn.spriteState      = ss;
            }
            else
            {
                btn.transition = Selectable.Transition.ColorTint;
                var cb = btn.colors;
                cb.normalColor      = Color.white;
                cb.highlightedColor = theme.batikGold;
                cb.pressedColor     = theme.woodDark;
                cb.selectedColor    = theme.batikGold;
                cb.disabledColor    = new Color(theme.woodMid.r, theme.woodMid.g, theme.woodMid.b, 0.4f);
                btn.colors          = cb;
            }
        }

        // Style the button's text label if it has one.
        var label = GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            if (applyFont && theme.BodyFontOrFallback != null) label.font = theme.BodyFontOrFallback;
            if (applyColor) label.color = theme.textLight;
            label.fontSize = theme.buttonSize;
        }
    }

    private void StyleText(UITheme theme, TMP_FontAsset font, Color color, float size)
    {
        var tmp = GetComponent<TMP_Text>();
        if (tmp == null) return;

        if (applyFont && font != null) tmp.font = font;
        if (applyColor) tmp.color = color;
        tmp.fontSize = size;
    }

    private void StyleAccent(Color color)
    {
        var img = GetComponent<Image>();
        if (img != null && applyColor) img.color = color;
    }
}
