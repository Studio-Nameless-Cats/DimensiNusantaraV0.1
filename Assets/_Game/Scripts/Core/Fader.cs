using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fades a full-screen black Image in and out for scene transitions.
//
// Setup:
//   1. In the GameController's Canvas, add an Image that covers the whole screen.
//   2. Make the Image black (alpha can start at 0).
//   3. Assign the Image to this component.
//   4. Give the Canvas a really high Sort Order so it draws on top of everything.
//   5. Put this component on the same GameObject (or the Canvas root).
public class Fader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    void Awake()
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        // Start see-through.
        SetAlpha(0f);
    }

    // Fade the screen to black (see-through to solid).
    public IEnumerator FadeToBlack(float duration = 0.5f)
        => Fade(0f, 1f, duration);

    // Fade the screen back from black (solid to see-through).
    public IEnumerator FadeFromBlack(float duration = 0.5f)
        => Fade(1f, 0f, duration);

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
