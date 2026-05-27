using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades a full-screen black Image in and out for scene transitions.
///
/// Setup:
///   1. In the GameController's Canvas, add an Image that covers the whole screen.
///   2. Set the Image color to black (alpha can start at 0).
///   3. Assign the Image to this component.
///   4. Make sure the Canvas has a very high Sort Order so it renders on top of everything.
///   5. Add this component to the same GameObject (or the Canvas root).
/// </summary>
public class Fader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    void Awake()
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        // Start fully transparent
        SetAlpha(0f);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Fades the screen TO black (transparent → opaque).</summary>
    public IEnumerator FadeToBlack(float duration = 0.5f)
        => Fade(0f, 1f, duration);

    /// <summary>Fades the screen FROM black (opaque → transparent).</summary>
    public IEnumerator FadeFromBlack(float duration = 0.5f)
        => Fade(1f, 0f, duration);

    // ── Internals ──────────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float duration)
    {
        SetAlpha(from);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}
