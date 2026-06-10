using UnityEngine;
using TMPro;
using Nusantara.UI;

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
    // Defaults now pull from NusantaraPalette.Role.* so a fresh theme asset matches the
    // locked charcoal+gold scheme out of the box. These are still serialized fields you can
    // override per asset in the Inspector. Heads up: an asset SAVED before this change keeps
    // its old values - Reset it (gear menu > Reset) to pick up the palette defaults.
    [Header("Palette — Surfaces")]
    [Tooltip("Darkest surface — deep panel backgrounds, drop shadows.")]
    public Color woodDark = NusantaraPalette.Role.Surface;          // was wood #3B2A1A

    [Tooltip("Raised surface — default panel fill, cells.")]
    public Color woodMid = NusantaraPalette.Role.SurfaceRaised;     // was wood #5C4329

    [Tooltip("Parchment / cream — light surfaces, dialog interior.")]
    // No "parchment" role exists, so borrow the palette's cream (Gading) - same job, light surface.
    public Color parchment = NusantaraPalette.TextLight;            // Gading cream, was #E8D9B5

    [Header("Palette — Accents")]
    [Tooltip("Gold — borders, highlights, primary accent.")]
    public Color batikGold = NusantaraPalette.Role.FieldBg;         // Kuning, was #C9A227

    [Tooltip("Indigo — cool/magic secondary accent.")]
    public Color indigo = NusantaraPalette.Role.Magic;             // Nila, was #2E4A6B

    [Tooltip("Maroon — danger / negative accent (e.g. enemy side).")]
    public Color maroon = NusantaraPalette.Role.Danger;            // DangerDeep, was #8C2D1A

    [Header("Palette — Text")]
    [Tooltip("Light text on dark surfaces.")]
    public Color textLight = NusantaraPalette.Role.OnDark;          // Gading, was #F4E8C8

    [Tooltip("Dark text on parchment / field surfaces.")]
    public Color textDark = NusantaraPalette.Role.OnField;          // Hitam, was #2A1B0E

    [Header("Palette — HP bar gradient")]
    public Color hpHigh = NusantaraPalette.Role.Success;           // Pandan green
    public Color hpMid  = NusantaraPalette.Role.Warning;           // Jingga orange
    public Color hpLow  = NusantaraPalette.Role.Danger;            // DangerDeep red

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
