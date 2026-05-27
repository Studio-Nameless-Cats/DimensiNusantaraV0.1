using System.Collections;
using UnityEngine;

/// <summary>
/// Pop-up "!" / "?" bubble that appears above an enemy when its state changes.
/// Animation is scale-in → hold → scale-out, all driven by a single coroutine.
///
/// The visuals themselves are GameObjects you assign in the Inspector — sprite,
/// text, UI image, whatever. The script just controls which one is visible and
/// scales the parent transform. This makes the bubble fully re-skinnable.
///
/// Setup:
///   1. Create a child GameObject under the enemy (e.g. "AlertBubble") and
///      position it above the sprite's head.
///   2. Add this component.
///   3. Create two children of the bubble — one with the "!" visual and one
///      with the "?" visual. Drag them into the <c>alertVisual</c> /
///      <c>questionVisual</c> slots.
///   4. Add a LockWorldRotation component on the bubble GameObject so it stays
///      facing the camera while the enemy's body rotates during chase.
///   5. Drag the AlertBubble onto the OverworldEnemyController's
///      <c>alertBubble</c> slot on the root enemy GameObject.
/// </summary>
public class AlertBubble : MonoBehaviour
{
    public enum BubbleKind { Alert, Question }

    [Header("Visuals (assign at least one)")]
    [Tooltip("GameObject shown when Show(Alert) is called. Use any visual — sprite, text, UI image.")]
    [SerializeField] private GameObject alertVisual;
    [Tooltip("GameObject shown when Show(Question) is called.")]
    [SerializeField] private GameObject questionVisual;

    [Header("Animation")]
    [Tooltip("Duration of the pop-in / pop-out scale tween (seconds).")]
    [SerializeField] private float fadeTime = 0.15f;
    [Tooltip("How long the bubble stays at full scale before fading out.")]
    [SerializeField] private float holdDuration = 0.8f;
    [Tooltip("Peak scale at the top of the pop-in tween.")]
    [SerializeField] private float peakScale = 1f;

    private Coroutine running;

    void Awake()
    {
        if (alertVisual    != null) alertVisual.SetActive(false);
        if (questionVisual != null) questionVisual.SetActive(false);
        transform.localScale = Vector3.zero;
    }

    /// <summary>Pop the bubble. If one is already showing, it's restarted.</summary>
    public void Show(BubbleKind kind)
    {
        if (!isActiveAndEnabled) return;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(kind));
    }

    private IEnumerator Animate(BubbleKind kind)
    {
        // Activate exactly one visual.
        if (alertVisual    != null) alertVisual.SetActive(kind == BubbleKind.Alert);
        if (questionVisual != null) questionVisual.SetActive(kind == BubbleKind.Question);

        yield return ScaleTo(peakScale, fadeTime);
        yield return new WaitForSeconds(holdDuration);
        yield return ScaleTo(0f, fadeTime);

        if (alertVisual    != null) alertVisual.SetActive(false);
        if (questionVisual != null) questionVisual.SetActive(false);
        running = null;
    }

    private IEnumerator ScaleTo(float target, float duration)
    {
        Vector3 start = transform.localScale;
        Vector3 end   = Vector3.one * target;

        if (duration <= 0.0001f)
        {
            transform.localScale = end;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Cheap ease-out so it doesn't feel linear.
            t = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(start, end, t);
            yield return null;
        }
        transform.localScale = end;
    }
}
