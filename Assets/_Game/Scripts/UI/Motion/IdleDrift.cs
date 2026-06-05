using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // The "nothing rests" layer. Stick this on the watermark, the corner streaks,
    // the title lockup - anything that should keep gently moving once the menu
    // settles. Pick a drift type, set a start delay so it kicks in after the
    // entrance finishes, done. Loops forever on a soft yoyo.
    public class IdleDrift : MonoBehaviour
    {
        public enum DriftType
        {
            HorizontalPan,  // slow left/right slide - good for the watermark
            VerticalBob,    // gentle up/down - good for the title lockup
            Shimmer,        // tiny scale breathing - good for the corner streaks
            Rotate          // small rocking tilt
        }

        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Drift")]
        [SerializeField] private DriftType type = DriftType.HorizontalPan;
        [Tooltip("Wait this long before the loop starts, so it takes over after the entrance settles (~1.1s).")]
        [SerializeField] private float startDelay = 1.1f;
        [Tooltip("Optional per-instance multiplier on the profile amplitude. 1 = use profile as-is.")]
        [SerializeField] private float amplitudeScale = 1f;

        private RectTransform _rt;
        private Vector2 _restPos;
        private Vector3 _restScale;
        private Vector3 _restRot;
        private Tween _tween;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _restPos = _rt.anchoredPosition;
            _restScale = _rt.localScale;
            _restRot = _rt.localEulerAngles;
        }

        void OnEnable()
        {
            if (profile == null) return;
            StartDrift();
        }

        void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            // snap back so we don't leave the element parked mid-wobble
            if (_rt != null)
            {
                _rt.anchoredPosition = _restPos;
                _rt.localScale = _restScale;
                _rt.localEulerAngles = _restRot;
            }
        }

        private void StartDrift()
        {
            float dur = profile.idleDriftDuration;
            float amp = profile.idleDriftAmplitude * amplitudeScale;

            switch (type)
            {
                case DriftType.HorizontalPan:
                    _rt.anchoredPosition = _restPos - new Vector2(amp, 0f);
                    _tween = _rt.DOAnchorPosX(_restPos.x + amp, dur);
                    break;

                case DriftType.VerticalBob:
                    _rt.anchoredPosition = _restPos - new Vector2(0f, amp);
                    _tween = _rt.DOAnchorPosY(_restPos.y + amp, dur);
                    break;

                case DriftType.Shimmer:
                    float s = profile.idleDriftScale * amplitudeScale;
                    _rt.localScale = _restScale * (1f - s);
                    _tween = _rt.DOScale(_restScale * (1f + s), dur);
                    break;

                case DriftType.Rotate:
                    float r = profile.idleDriftRotation * amplitudeScale;
                    _rt.localEulerAngles = _restRot - new Vector3(0f, 0f, r);
                    _tween = _rt.DOLocalRotate(_restRot + new Vector3(0f, 0f, r), dur);
                    break;
            }

            _tween.SetEase(profile.idleDriftEase)
                  .SetLoops(-1, LoopType.Yoyo)
                  .SetDelay(startDelay)
                  .ApplyMenuDefaults(profile, gameObject);
        }
    }
}
