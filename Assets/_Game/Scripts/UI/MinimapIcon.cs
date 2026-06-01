using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any world object that should show up as a blip on the minimap
/// (NPCs, overworld enemies, quest markers, rest points, loot, etc.).
///
/// This is a pure data + self-registration component — it does NOT touch the UI.
/// The <see cref="Minimap"/> controller reads the static <see cref="Active"/> list
/// every frame and draws/pools a blip Image for each entry.
///
/// Setup:
///   1. Add this component to a world GameObject.
///   2. Assign an <c>icon</c> sprite (or leave null to use the Minimap's default
///      sprite for this <c>type</c>).
///   3. Pick a <c>type</c> (used for tinting / future filtering) and optionally
///      override <c>color</c>.
///
/// The PLAYER does not need this component — the Minimap draws the player as a
/// fixed centre marker. (You may still add one with type = Player if you prefer
/// to drive the centre marker from here; the Minimap will skip Player-type icons
/// when drawing relative blips.)
/// </summary>
public class MinimapIcon : MonoBehaviour
{
    public enum IconType
    {
        Default,
        Player,
        NPC,
        Enemy,
        Quest,
        Item,
        RestPoint,
    }

    [Header("Appearance")]
    [Tooltip("Blip sprite. If null, the Minimap supplies a default sprite for this type.")]
    [SerializeField] private Sprite icon;

    [Tooltip("Blip tint. White = use the sprite's own colours.")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Category — used for tinting and future filtering.")]
    [SerializeField] private IconType type = IconType.Default;

    [Header("Behaviour")]
    [Tooltip("Pixel size of the blip on the minimap (square).")]
    [SerializeField] private float blipSize = 18f;

    [Tooltip("If true, the blip rotates to match this object's world Y-rotation " +
             "(only visible when the minimap is in fixed-north mode).")]
    [SerializeField] private bool rotateWithObject = false;

    [Tooltip("If true and this blip is outside the minimap radius, clamp it to the " +
             "edge so the player still sees its direction. If false, it's hidden.")]
    [SerializeField] private bool clampToEdge = false;

    // ── Static registry ──────────────────────────────────────────────────────
    private static readonly List<MinimapIcon> _active = new List<MinimapIcon>();

    /// <summary>All currently-enabled minimap icons. Read-only for consumers.</summary>
    public static IReadOnlyList<MinimapIcon> Active => _active;

    // ── Accessors used by the Minimap controller ─────────────────────────────
    public Sprite   Icon              => icon;
    public Color    Color             => color;
    public IconType Type              => type;
    public float    BlipSize          => blipSize;
    public bool     RotateWithObject  => rotateWithObject;
    public bool     ClampToEdge       => clampToEdge;
    public Transform Tf               => transform;

    // ── Lifecycle: self-register so the Minimap stays decoupled ───────────────
    void OnEnable()
    {
        if (!_active.Contains(this))
            _active.Add(this);
    }

    void OnDisable()
    {
        _active.Remove(this);
    }

    /// <summary>Change the blip sprite at runtime (e.g. quest marker state).</summary>
    public void SetIcon(Sprite newIcon) => icon = newIcon;

    /// <summary>Change the blip tint at runtime.</summary>
    public void SetColor(Color newColor) => color = newColor;
}
