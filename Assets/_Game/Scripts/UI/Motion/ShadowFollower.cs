using UnityEngine;

namespace Nusantara.UI.Motion
{
    // Sits on the offset drop-shadow layer (the Emas Tua block behind a panel,
    // the red layer behind the wordmark) and lazily chases the main layer a few
    // frames behind. That little lag is pure visual seasoning - no gameplay
    // coupling, it just makes movement read as "Persona".
    //
    // We capture the starting gap between shadow and main at Awake, then every
    // frame ease toward (main position + that gap). Bigger smoothing in the
    // profile = the shadow keeps up tighter; smaller = it drags more.
    public class ShadowFollower : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Layers")]
        [Tooltip("The main layer this shadow trails. The shadow keeps its starting offset from it.")]
        [SerializeField] private RectTransform main;

        private RectTransform _rt;
        private Vector2 _offset;   // fixed gap shadow keeps behind main
        private bool _ready;

        void Awake()
        {
            _rt = (RectTransform)transform;
            if (main != null)
            {
                _offset = _rt.anchoredPosition - main.anchoredPosition;
                _ready = true;
            }
        }

        void LateUpdate()
        {
            if (!_ready || profile == null) return;

            Vector2 target = main.anchoredPosition + _offset;

            // framerate-independent smoothing - same feel at 60 or 144 fps.
            // unscaled so it keeps lagging even when the game is paused.
            float t = 1f - Mathf.Exp(-profile.shadowFollowSmoothing * Time.unscaledDeltaTime);
            _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, target, t);
        }
    }
}
