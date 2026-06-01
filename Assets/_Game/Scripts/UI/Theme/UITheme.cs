using UnityEngine;
using TMPro;

/// <summary>
/// Shared batik/wood UI kit — the single source of truth for colors, sprites
/// and fonts across Main Menu, Overworld and Battle. Create ONE asset and
/// assign it on a <see cref="UIThemeProvider"/> in each scene; every
/// <see cref="ThemedElement"/> then re-skins from this one asset.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Project window ▸ right-click ▸ Create ▸ RPG ▸ UI ▸ Theme.
///   2. Name it e.g. "Theme_Nusantara".
///   3. Drag in your 9-slice panel sprite (Border set in the Sprite Editor),
///      button sprites, and the TMP font asset.
///   4. Tune the palette colours to taste. Defaults are a warm wood +
///      batik-gold scheme with an indigo/maroon accent.
///   5. Assign this asset to the UIThemeProvider in every scene.
/// </summary>
[CreateAssetMenu(fileName = "Theme_Nusantara", menuName = "RPG/UI/Theme")]
public class UITheme : ScriptableObject
{
    // ── Palette ────────────────────────────────────────────────────────────
    // Named slots so element scripts never hard-code colours. Re-skinning the
    // whole game is editing these few fields.
    [Header("Palette — Surfaces")]
    [Tooltip("Darkest wood — deep panel backgrounds, drop shadows.")]
    public Color woodDark = new Color(0.231f, 0.165f, 0.102f, 1f);   // #3B2A1A

    [Tooltip("Mid wood — default panel fill.")]
    public Color woodMid = new Color(0.361f, 0.263f, 0.161f, 1f);    // #5C4329

    [Tooltip("Parchment / cream — light surfaces, dialog interior.")]
    public Color parchment = new Color(0.910f, 0.851f, 0.710f, 1f);  // #E8D9B5

    [Header("Palette — Accents")]
    [Tooltip("Batik gold — borders, highlights, primary accent.")]
    public Color batikGold = new Color(0.788f, 0.635f, 0.153f, 1f);  // #C9A227

    [Tooltip("Indigo — batik blue secondary accent.")]
    public Color indigo = new Color(0.180f, 0.290f, 0.420f, 1f);     // #2E4A6B

    [Tooltip("Maroon — danger / negative accent (e.g. enemy side).")]
    public Color maroon = new Color(0.549f, 0.176f, 0.102f, 1f);     // #8C2D1A

    [Header("Palette — Text")]
    [Tooltip("Light text on dark wood surfaces.")]
    public Color textLight = new Color(0.957f, 0.910f, 0.784f, 1f);  // #F4E8C8

    [Tooltip("Dark text on parchment surfaces.")]
    public Color textDark = new Color(0.165f, 0.106f, 0.055f, 1f);   // #2A1B0E

    [Header("Palette — HP bar gradient")]
    public Color hpHigh = new Color(0.314f, 0.667f, 0.275f, 1f);     // green
    public Color hpMid  = new Color(0.847f, 0.706f, 0.196f, 1f);     // gold-yellow
    public Color hpLow  = new Color(0.745f, 0.220f, 0.180f, 1f);     // red

    // ── Sprites ─────────────────────────────────────────────────────────────
    [Header("Sprites — 9-slice")]
    [Tooltip("9-slice panel sprite (set Border in the Sprite Editor). Used for all framed panels.")]
    public Sprite panelSprite;

    [Tooltip("9-slice button sprite — normal state.")]
    public Sprite buttonSprite;

    [Tooltip("9-slice button sprite — pressed/active state. Optional.")]
    public Sprite buttonPressedSprite;

    [Tooltip("Optional ornamental corner/divider sprite for headers.")]
    public Sprite ornamentSprite;

    // ── Fonts ────────────────────────────────────────────────────────────────
    [Header("Fonts")]
    [Tooltip("Display font for titles / headers.")]
    public TMP_FontAsset headerFont;

    [Tooltip("Body font for dialog and labels. Falls back to headerFont if unset.")]
    public TMP_FontAsset bodyFont;

    // ── Sizing defaults ──────────────────────────────────────────────────────
    [Header("Type scale (pt)")]
    public float headerSize = 42f;
    public float bodySize   = 24f;
    public float buttonSize = 28f;

    /// <summary>Body font with a sensible fallback to the header font.</summary>
    public TMP_FontAsset BodyFontOrFallback => bodyFont != null ? bodyFont : headerFont;

    /// <summary>
    /// Returns the HP-gradient colour for a 0..1 fill value
    /// (green &gt; 0.5, gold &gt; 0.25, red below).
    /// </summary>
    public Color HpColor(float normalized)
        => normalized > 0.5f ? hpHigh
         : normalized > 0.25f ? hpMid
         : hpLow;
}
