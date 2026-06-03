using System.Collections;
using UnityEngine;

/// <summary>
/// Smooth, decaying camera shake — adds "life" to battle impacts without the
/// jittery, nauseating feel of a pure-random jolt. Uses Perlin noise for a soft,
/// continuous wobble whose amplitude eases back to zero over the shake duration.
///
/// ── Unity setup ──────────────────────────────────────────────────────────────
///   • Drop this on the Battle Camera (or, better, on a parent "CameraRig" with the
///     Camera as a child — then the shake offset never fights other camera logic).
///   • It animates this transform's LOCAL position around whatever its local
///     position is at Awake, so it works whether it's the camera itself or a rig.
///   • Assign the BattleSystem's "Camera Shake" reference to this component.
///
/// Calling: <see cref="Shake()"/> uses the inspector defaults; the overload lets a
/// caller scale the punch (e.g. a crit shakes harder than a normal hit).
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Default shake")]
    [Tooltip("How long a default shake lasts, in seconds.")]
    [SerializeField] private float duration = 0.25f;

    [Tooltip("Peak positional offset (world units) of a default shake.")]
    [SerializeField] private float magnitude = 0.18f;

    [Tooltip("How fast the Perlin wobble oscillates. Higher = busier/buzzier shake.")]
    [SerializeField] private float frequency = 22f;

    [Tooltip("Amplitude falloff curve over the shake's lifetime (1 = full at start, 0 = none at end). " +
             "Leave default for a smooth ease-out.")]
    [SerializeField] private AnimationCurve falloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ── State ──────────────────────────────────────────────────────────────────
    private Vector3   _baseLocalPos;
    private Coroutine _shakeRoutine;

    // Distinct Perlin sample lanes per axis so X and Y don't move in lockstep.
    private float _seedX;
    private float _seedY;

    void Awake()
    {
        _baseLocalPos = transform.localPosition;
        _seedX = Random.value * 100f;
        _seedY = Random.value * 100f;
    }

    void OnDisable()
    {
        // Make sure we don't leave the camera parked off-centre if disabled mid-shake.
        if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        transform.localPosition = _baseLocalPos;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Shake with the inspector default duration + magnitude.</summary>
    public void Shake() => Shake(magnitude, duration);

    /// <summary>
    /// Shake with an explicit magnitude (and optional duration). Pass a bigger
    /// magnitude for heavier impacts (crits, specials). Re-capturing the base
    /// position each call keeps repeated/overlapping shakes from drifting.
    /// </summary>
    public void Shake(float shakeMagnitude, float shakeDuration = -1f)
    {
        if (shakeDuration <= 0f) shakeDuration = duration;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            transform.localPosition = _baseLocalPos;   // reset before re-capturing
        }

        _shakeRoutine = StartCoroutine(ShakeRoutine(shakeMagnitude, shakeDuration));
    }

    private IEnumerator ShakeRoutine(float shakeMagnitude, float shakeDuration)
    {
        _baseLocalPos = transform.localPosition;   // anchor to current rest position
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / shakeDuration);
            float amp    = shakeMagnitude * falloff.Evaluate(t);
            float sample = elapsed * frequency;

            // Perlin returns 0..1; remap to -1..1 for a centred wobble.
            float offsetX = (Mathf.PerlinNoise(_seedX, sample) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(_seedY, sample) - 0.5f) * 2f;

            transform.localPosition = _baseLocalPos + new Vector3(offsetX, offsetY, 0f) * amp;
            yield return null;
        }

        transform.localPosition = _baseLocalPos;
        _shakeRoutine = null;
    }
}
