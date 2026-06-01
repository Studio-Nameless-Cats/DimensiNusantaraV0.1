using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Multi-layer parallax background for UI canvases.
/// Each layer is a RawImage whose uvRect is shifted every frame, creating a
/// seamless infinite scroll. Layers with different scroll speeds create depth.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Under your Canvas, create one RawImage per parallax layer.
///      Set each RawImage's texture to a tileable art asset.
///      Texture Import Settings → Wrap Mode = Repeat (required for tiling).
///   2. Add THIS component to any persistent GameObject in the scene
///      (e.g., a "Background" empty or the Canvas itself).
///   3. In the Inspector, expand Layers and add one entry per RawImage:
///        • Image       → drag the RawImage
///        • Scroll Speed → e.g. (0.02, 0) for slow rightward drift
///                         Back layers should be slower (0.01), front faster (0.04).
///   4. Tick Pause In Menus if you want scrolling to freeze when the game is paused.
///
/// Ordering tip: put the sky/far background at layer 0 (slowest), closer
/// elements at higher indices (faster). Each RawImage should be a separate
/// child so you can set their sort order via Hierarchy position or Canvas Group.
/// </summary>
public class ScrollingBackground : MonoBehaviour
{
    [Serializable]
    public class ParallaxLayer
    {
        [Tooltip("The RawImage to scroll.")]
        public RawImage image;

        [Tooltip("UV units scrolled per second on each axis. " +
                 "Typical range 0.01–0.1. Negative values reverse direction.")]
        public Vector2 scrollSpeed = new Vector2(0.02f, 0f);
    }

    [SerializeField] private ParallaxLayer[] layers = Array.Empty<ParallaxLayer>();

    [Tooltip("When true, scrolling pauses while Time.timeScale == 0 (Unity pause).")]
    [SerializeField] private bool pauseInMenus = false;

    void Update()
    {
        float dt = pauseInMenus ? Time.deltaTime : Time.unscaledDeltaTime;

        foreach (var layer in layers)
        {
            if (layer.image == null) continue;

            Rect uv = layer.image.uvRect;
            uv.x += layer.scrollSpeed.x * dt;
            uv.y += layer.scrollSpeed.y * dt;

            // Wrap to [0,1) to prevent float drift over long sessions.
            uv.x = uv.x % 1f;
            uv.y = uv.y % 1f;

            layer.image.uvRect = uv;
        }
    }

    // ── Runtime API ───────────────────────────────────────────────────────────

    /// <summary>Multiply all scroll speeds by a factor (0 = freeze, 1 = normal).</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        // Store base speeds once on first call if needed — simple version: just scale directly.
        // For a more robust version, cache original speeds in Awake.
        foreach (var layer in layers)
            layer.scrollSpeed *= multiplier;
    }

    /// <summary>Snap all layers back to uvRect origin (use on scene reset).</summary>
    public void ResetUVs()
    {
        foreach (var layer in layers)
        {
            if (layer.image == null) continue;
            Rect uv = layer.image.uvRect;
            uv.x = 0f; uv.y = 0f;
            layer.image.uvRect = uv;
        }
    }
}
