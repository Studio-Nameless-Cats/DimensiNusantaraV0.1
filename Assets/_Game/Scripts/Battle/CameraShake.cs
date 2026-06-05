using System.Collections;
using UnityEngine;

// Smooth, fading camera shake. Adds a bit of "oomph" to battle hits without that
// nauseating, jittery feel you get from pure random jolts. Uses Perlin noise for a
// soft continuous wobble that eases back to zero over the shake's lifetime.
//
// Unity setup:
//   - Drop this on the Battle Camera, or better, on a parent "CameraRig" with the
//     Camera as a child (that way the shake offset never fights other camera logic).
//   - It wobbles this transform's LOCAL position around wherever it sits at Awake,
//     so it works whether it's the camera itself or a rig.
//   - Assign it to the BattleSystem's "Camera Shake" reference.
//
// Shake() uses the inspector defaults; the overload lets you scale the punch
// (e.g. a crit shakes harder than a normal hit).
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

    private Vector3   _baseLocalPos;
    private Coroutine _shakeRoutine;

    // Separate Perlin sample lanes per axis so X and Y don't move in lockstep.
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
        // Don't leave the camera stuck off-centre if we get disabled mid-shake.
        if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        transform.localPosition = _baseLocalPos;
    }

    // Shake using the inspector default duration + magnitude.
    public void Shake() => Shake(magnitude, duration);

    // Shake with a specific magnitude (and optional duration). Pass a bigger magnitude
    // for heavier hits (crits, specials). We grab the base position fresh each call so
    // repeated or overlapping shakes don't slowly drift the camera away.
    public void Shake(float shakeMagnitude, float shakeDuration = -1f)
    {
        if (shakeDuration <= 0f) shakeDuration = duration;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            transform.localPosition = _baseLocalPos;   // snap back before grabbing the base again
        }

        _shakeRoutine = StartCoroutine(ShakeRoutine(shakeMagnitude, shakeDuration));
    }

    private IEnumerator ShakeRoutine(float shakeMagnitude, float shakeDuration)
    {
        _baseLocalPos = transform.localPosition;   // anchor to wherever the camera is resting now
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / shakeDuration);
            float amp    = shakeMagnitude * falloff.Evaluate(t);
            float sample = elapsed * frequency;

            // Perlin gives us 0..1; shift it to -1..1 so the wobble is centred.
            float offsetX = (Mathf.PerlinNoise(_seedX, sample) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(_seedY, sample) - 0.5f) * 2f;

            transform.localPosition = _baseLocalPos + new Vector3(offsetX, offsetY, 0f) * amp;
            yield return null;
        }

        transform.localPosition = _baseLocalPos;
        _shakeRoutine = null;
    }
}
