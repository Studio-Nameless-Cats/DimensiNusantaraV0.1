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

        // How long Play()/Reveal() take, so a caller (e.g. GameController) can wait for
        // the cover to finish before loading the next scene. 0 if no profile is set.
        public float WipeDuration => profile != null ? profile.screenWipeDuration : 0f;

        [Header("Auto reveal on scene load")]
        [Tooltip("Turn this ON for the transition panel in a gameplay scene you load INTO (overworld, battle, etc.). The panel starts covering the screen and wipes away on Start, so the wipe has an EXIT, not just an entry. The menu's panel doesn't need this - its MenuSequencer calls Reveal() itself.")]
        [SerializeField] private bool revealOnStart = false;

        void Awake()
        {
            if (panel == null) panel = (RectTransform)transform;

            // Start already covering the screen so there's no uncovered flash before
            // the wipe-away plays. Awake runs before the first frame renders.
            if (revealOnStart) SnapCovered();
        }

        void Start()
        {
            // Wipe the cover away once the scene's up. We wait a couple of frames first:
            // the very first frame after a scene load has a giant unscaled deltaTime (the
            // load hitch), and since the wipe runs on unscaled time, starting it on that
            // frame fast-forwards the whole tween to the end at once - so it looks like no
            // wipe happened at all. Skipping the hitch frame(s) lets it actually play.
            if (revealOnStart) StartCoroutine(RevealAfterLoadHitch());
        }

        private System.Collections.IEnumerator RevealAfterLoadHitch()
        {
            // Stay covered (set in Awake) through the load hitch, then wipe on a normal frame.
            yield return null;
            yield return null;
            Debug.Log($"[ScreenTransition] Auto-reveal wipe starting on '{name}'.");  // TEMP: remove once confirmed
            Reveal();
        }

        // Wipe the panel away to uncover the screen. Call this on scene arrival.
        // No profile wired? Falls back to a 0s tween (instant) so callers that chain
        // OnComplete / Insert still get a valid tween.
        public Tween Reveal()
        {
            if (panel == null) panel = (RectTransform)transform;
            panel.anchoredPosition = coveredPos;
            float dur = profile != null ? profile.screenWipeDuration : 0f;
            Ease  ease = profile != null ? profile.screenWipeEase : Ease.Linear;
            return panel.DOAnchorPos(offscreenPos, dur)
                        .SetEase(ease)
                        .ApplyMenuDefaults(profile, gameObject);
        }

        // Wipe the panel across to cover the screen. Call this before loading the
        // next scene, then load behind it once the tween's done. Same 0s fallback.
        public Tween Play()
        {
            if (panel == null) panel = (RectTransform)transform;
            panel.anchoredPosition = offscreenPos;
            float dur = profile != null ? profile.screenWipeDuration : 0f;
            Ease  ease = profile != null ? profile.screenWipeEase : Ease.Linear;
            return panel.DOAnchorPos(coveredPos, dur)
                        .SetEase(ease)
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
