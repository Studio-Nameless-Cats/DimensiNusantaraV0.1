using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icon/blip minimap for the overworld. Draws the player as a fixed centre marker
/// and every registered <see cref="MinimapIcon"/> as a blip positioned by its
/// world XZ offset from the player. No second camera, no render texture.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Canvas (Screen Space - Overlay).
///   2. Empty UI object "Minimap" anchored bottom-right.
///        • Optional child "Background"  (Image, the circular frame art).
///        • Child "Mask" (Image = a solid white circle sprite) + add a UI > Mask
///          component, "Show Mask Graphic" OFF. This clips blips to the circle.
///            └ Child "BlipContainer" (empty RectTransform, stretched to fill Mask).
///                 → assign to <c>blipContainer</c>. Blips are spawned in here.
///            └ Child "PlayerMarker" (Image, centred) → assign to <c>playerMarker</c>.
///   3. Add THIS component to "Minimap". Assign <c>blipContainer</c>,
///      <c>playerMarker</c>, <c>playerTarget</c> (the player's transform), and a
///      <c>blipPrefab</c> (a UI object with just an Image — can be a disabled
///      template in the scene or a project prefab).
///   4. Set <c>radius</c> to match the masked circle's pixel radius and
///      <c>worldRange</c> to how many world units the edge represents.
///
/// Drop a <see cref="MinimapIcon"/> on any NPC/enemy/POI and it shows up
/// automatically — this controller never needs editing to add new blips.
/// </summary>
public class Minimap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform the minimap centres on — the player root.")]
    [SerializeField] private Transform playerTarget;

    [Tooltip("RectTransform that blips are parented under (inside the circular mask).")]
    [SerializeField] private RectTransform blipContainer;

    [Tooltip("The fixed centre marker representing the player. Optional.")]
    [SerializeField] private RectTransform playerMarker;

    [Tooltip("Prefab/template for one blip. Must have an Image on the root. " +
             "If it's a scene object, it will be hidden and cloned.")]
    [SerializeField] private Image blipPrefab;

    [Header("Projection")]
    [Tooltip("Pixel radius of the visible minimap circle (blip area).")]
    [SerializeField] private float radius = 90f;

    [Tooltip("World units from player to the edge of the minimap. Smaller = more zoomed in.")]
    [SerializeField] private float worldRange = 25f;

    [Tooltip("If true the map rotates so the player's facing is always 'up'. " +
             "If false the map is fixed-north (recommended — less disorienting).")]
    [SerializeField] private bool rotateWithPlayer = false;

    [Header("Default blip sprites (used when a MinimapIcon has none)")]
    [SerializeField] private Sprite defaultIcon;
    [SerializeField] private Sprite npcIcon;
    [SerializeField] private Sprite enemyIcon;
    [SerializeField] private Sprite questIcon;
    [SerializeField] private Sprite itemIcon;
    [SerializeField] private Sprite restPointIcon;

    // ── Blip pool: one Image per active MinimapIcon ───────────────────────────
    private readonly Dictionary<MinimapIcon, Image> _blips = new Dictionary<MinimapIcon, Image>();
    private readonly List<MinimapIcon> _toRemove = new List<MinimapIcon>();

    void Awake()
    {
        // If the prefab is a scene object, keep the template hidden.
        if (blipPrefab != null && blipPrefab.gameObject.scene.IsValid())
            blipPrefab.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (playerTarget == null || blipContainer == null || blipPrefab == null)
            return;

        // Blips are positioned mathematically (we rotate each offset, not the container),
        // so the container stays upright and blip sprites never get smeared.
        blipContainer.localRotation = Quaternion.identity;

        float yaw = rotateWithPlayer ? playerTarget.eulerAngles.y : 0f;

        Vector3 playerPos = playerTarget.position;
        float   scale     = (worldRange > 0.001f) ? radius / worldRange : 1f;
        float   yawRad    = yaw * Mathf.Deg2Rad; // rotate offsets so player-forward points up
        float   cos       = Mathf.Cos(yawRad);
        float   sin       = Mathf.Sin(yawRad);

        var active = MinimapIcon.Active;

        for (int i = 0; i < active.Count; i++)
        {
            MinimapIcon icon = active[i];
            if (icon == null || icon.Type == MinimapIcon.IconType.Player)
                continue;

            // World XZ offset → minimap XY (Unity X→UI x, Unity Z→UI y).
            Vector3 d = icon.Tf.position - playerPos;
            Vector2 p = new Vector2(d.x, d.z) * scale;

            // Apply rotation when the map turns with the player.
            if (rotateWithPlayer)
                p = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);

            float dist = p.magnitude;
            bool visible;

            if (dist > radius)
            {
                if (icon.ClampToEdge)
                {
                    p = p.normalized * radius;   // pin to the rim
                    visible = true;
                }
                else
                {
                    visible = false;             // off-map → hide
                }
            }
            else
            {
                visible = true;
            }

            Image blip = GetOrCreateBlip(icon);
            blip.gameObject.SetActive(visible);
            if (!visible)
                continue;

            // Position.
            blip.rectTransform.anchoredPosition = p;

            // Sprite + tint (refresh in case it changed at runtime).
            blip.sprite = icon.Icon != null ? icon.Icon : DefaultSpriteFor(icon.Type);
            blip.color  = icon.Color;

            // Size.
            blip.rectTransform.sizeDelta = new Vector2(icon.BlipSize, icon.BlipSize);

            // Per-blip rotation (counter-rotate so it stays upright if the map spins).
            float blipZ = icon.RotateWithObject ? -icon.Tf.eulerAngles.y : 0f;
            if (icon.RotateWithObject && rotateWithPlayer) blipZ += yaw; // heading relative to player
            blip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, blipZ);
        }

        PruneDeadBlips();

        // Player marker: keep it centred; optionally face up.
        if (playerMarker != null)
        {
            playerMarker.anchoredPosition = Vector2.zero;
            float markerZ = rotateWithPlayer ? 0f : -playerTarget.eulerAngles.y;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, markerZ);
        }
    }

    // ── Pool helpers ──────────────────────────────────────────────────────────

    private Image GetOrCreateBlip(MinimapIcon icon)
    {
        if (_blips.TryGetValue(icon, out Image existing) && existing != null)
            return existing;

        Image blip = Instantiate(blipPrefab, blipContainer);
        blip.gameObject.SetActive(true);
        blip.rectTransform.anchorMin = blip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        blip.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
        _blips[icon] = blip;
        return blip;
    }

    /// <summary>Destroy blips whose MinimapIcon was disabled/destroyed.</summary>
    private void PruneDeadBlips()
    {
        _toRemove.Clear();
        foreach (var kvp in _blips)
        {
            MinimapIcon icon = kvp.Key;
            // OnDisable removes icons from the registry, so this also catches disabled ones.
            bool stillActive = icon != null && icon.isActiveAndEnabled;
            if (!stillActive)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                _toRemove.Add(icon);
            }
        }
        for (int i = 0; i < _toRemove.Count; i++)
            _blips.Remove(_toRemove[i]);
    }

    private Sprite DefaultSpriteFor(MinimapIcon.IconType type)
    {
        switch (type)
        {
            case MinimapIcon.IconType.NPC:       return npcIcon       != null ? npcIcon       : defaultIcon;
            case MinimapIcon.IconType.Enemy:     return enemyIcon     != null ? enemyIcon     : defaultIcon;
            case MinimapIcon.IconType.Quest:     return questIcon     != null ? questIcon     : defaultIcon;
            case MinimapIcon.IconType.Item:      return itemIcon      != null ? itemIcon      : defaultIcon;
            case MinimapIcon.IconType.RestPoint: return restPointIcon != null ? restPointIcon : defaultIcon;
            default:                             return defaultIcon;
        }
    }
}
