using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // A skewed full-screen panel that lives off-screen and wipes across to cover
    // everything. Play() slams it on (use right before a scene load), Reveal()
    // slides it back off (call on arrival to uncover the new screen). Same wipe
    // gets reused for every scene change later.
    //
    // Setup: make a big skewed Image that fully covers the canvas when centered,
    // park it at offscreenPos in the Inspector, drop this on it.
    public class ScreenTransition : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Panel")]
        [Tooltip("The covering panel. Defaults to this object's RectTransform.")]
        [SerializeField] private RectTransform panel;

        [Header("Positions (anchoredPos)")]
        [Tooltip("Where the panel sits when it fully covers the screen. Usually (0,0).")]
        [SerializeField] private Vector2 coveredPos = Vector2.zero;
        [Tooltip("Where the panel parks when it's out of the way. Push it off along the skew.")]
        [SerializeField] private Vector2 offscreenPos = new Vector2(2600f, 0f);

        void Awake()
        {
            if (panel == null) panel = (RectTransform)transform;
        }

        // Wipe the panel away to uncover the screen. Call this on scene arrival.
        public Tween Reveal()
        {
            panel.anchoredPosition = coveredPos;
            return panel.DOAnchorPos(offscreenPos, profile.screenWipeDuration)
                        .SetEase(profile.screenWipeEase)
                        .ApplyMenuDefaults(profile, gameObject);
        }

        // Wipe the panel across to cover the screen. Call this before loading the
        // next scene, then load behind it once the tween's done.
        public Tween Play()
        {
            panel.anchoredPosition = offscreenPos;
            return panel.DOAnchorPos(coveredPos, profile.screenWipeDuration)
                        .SetEase(profile.screenWipeEase)
                        .ApplyMenuDefaults(profile, gameObject);
        }

        // Drop the panel straight onto the screen with no animation - handy if you
        // want to start a scene already covered, then Reveal().
        public void SnapCovered()
        {
            if (panel == null) panel = (RectTransform)transform;
            panel.anchoredPosition = coveredPos;
        }
    }
}
