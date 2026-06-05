using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // Drop this on any panel root - the pause menu, a battle HUD group, a dialog
    // box - and list the bits you want flying in. Call PlayIn() to bring it on
    // screen and PlayOut() to send it away.
    //
    // Think of it as the generic cousin of MenuSequencer: MenuSequencer is the
    // hand-tuned main-menu timeline with named slots, this is the "any panel,
    // wired in the Inspector" version so we don't write a fresh sequencer for
    // every screen. Same MotionProfile drives both, so everything feels related.
    //
    // How it works: each Element picks where it slides in from (or just pops /
    // fades). On PlayIn we park everything at its offset, then tween home one
    // after another with a small stagger. PlayOut runs the list backwards - last
    // thing in is the first thing out. All of it goes through UIMotor so the raw
    // DOTween still lives in one place.
    public class PanelMotion : MonoBehaviour
    {
        // Where an element comes from / how it shows up.
        public enum Entry { SlideLeft, SlideRight, SlideUp, SlideDown, SkewSlide, ScalePop, FadeOnly }

        [Serializable]
        public class Element
        {
            [Tooltip("The thing that moves. Its anchoredPosition at first enable is treated as 'home'.")]
            public RectTransform target;
            [Tooltip("Optional fade partner. Auto-grabbed off the target if you leave it empty.")]
            public CanvasGroup group;
            [Tooltip("Where it comes from, or how it appears.")]
            public Entry entry = Entry.SlideDown;
            [Tooltip("How far offscreen a slide starts, in pixels. Ignored by ScalePop / FadeOnly.")]
            public float distance = 240f;
        }

        [Header("Profile")]
        [Tooltip("The shared motion knob-box. Use the same asset the main menu uses.")]
        [SerializeField] private MotionProfile profile;

        [Header("Backdrop (optional)")]
        [Tooltip("Full-screen dim behind the panel. Fades in with the panel and out when it leaves.")]
        [SerializeField] private CanvasGroup backdrop;
        [Tooltip("How opaque the backdrop gets when the panel is open.")]
        [Range(0f, 1f)]
        [SerializeField] private float backdropAlpha = 0.65f;

        [Header("Elements (animate in this order, top first)")]
        [SerializeField] private List<Element> elements = new List<Element>();

        [Header("Behaviour")]
        [Tooltip("Run PlayIn automatically the moment this object enables. Leave off if a controller drives it.")]
        [SerializeField] private bool playOnEnable = false;

        // homes captured once so we always return to the right spot even after a
        // half-finished open/close gets interrupted
        private readonly List<Vector2> _homePos = new List<Vector2>();
        private readonly List<Vector3> _homeScale = new List<Vector3>();
        private bool _captured;
        private Sequence _seq;

        void Awake() => Capture();

        void OnEnable()
        {
            if (playOnEnable) PlayIn();
        }

        // Remember where everything rests and grab any missing CanvasGroups. Safe
        // to call as often as you like - it only does the work once. Heads up: it
        // captures on first enable, so if a layout group shoves things around after
        // that, capture homes yourself once the layout has settled.
        public void Capture()
        {
            if (_captured) return;
            _homePos.Clear();
            _homeScale.Clear();
            foreach (Element e in elements)
            {
                if (e == null || e.target == null)
                {
                    _homePos.Add(Vector2.zero);
                    _homeScale.Add(Vector3.one);
                    continue;
                }
                _homePos.Add(e.target.anchoredPosition);
                _homeScale.Add(e.target.localScale);
                if (e.group == null) e.group = e.target.GetComponent<CanvasGroup>();
            }
            _captured = true;
        }

        // Turn an entry style into an offset, scaled by the element's distance.
        private Vector2 OffsetFor(Element e)
        {
            switch (e.entry)
            {
                case Entry.SlideLeft:  return new Vector2(-e.distance, 0f);
                case Entry.SlideRight: return new Vector2( e.distance, 0f);
                case Entry.SlideUp:    return new Vector2(0f,  e.distance);
                case Entry.SlideDown:  return new Vector2(0f, -e.distance);
                case Entry.SkewSlide:  return profile != null ? profile.skewSlideOffset : Vector2.zero;
                default:               return Vector2.zero; // ScalePop / FadeOnly don't travel
            }
        }

        // Bring the panel on screen. Kills any in-flight motion first, so spamming
        // open/close never leaves a piece stranded halfway.
        public Sequence PlayIn()
        {
            if (profile == null) return null;
            Capture();
            _seq?.Kill();
            _seq = DOTween.Sequence();

            if (backdrop != null)
            {
                backdrop.alpha = 0f;
                _seq.Insert(0f, backdrop.DOFade(backdropAlpha, profile.fastInDuration)
                                        .SetEase(Ease.OutQuad)
                                        .ApplyMenuDefaults(profile, backdrop.gameObject));
            }

            for (int i = 0; i < elements.Count; i++)
            {
                Element e = elements[i];
                if (e == null || e.target == null) continue;
                Tween t = BuildIn(e, i);
                if (t != null) _seq.Insert(i * profile.cascadeStagger, t);
            }

            _seq.ApplyMenuDefaults(profile, gameObject);
            return _seq;
        }

        private Tween BuildIn(Element e, int i)
        {
            // start from home every time so the offset math is always relative to rest
            e.target.anchoredPosition = _homePos[i];
            e.target.localScale = _homeScale[i];

            switch (e.entry)
            {
                case Entry.ScalePop:
                    return e.target.ScalePopIn(profile, _homeScale[i], e.group);
                case Entry.FadeOnly:
                    return e.group != null ? e.group.FadeIn(profile, e.target.gameObject) : null;
                default:
                    return e.target.SlideIn(profile, OffsetFor(e), e.group);
            }
        }

        // Send the panel away. onDone fires once everything's fully gone - a
        // controller hooks SetActive(false) / unpause onto it so the panel only
        // really closes after the slide-out finishes.
        public Sequence PlayOut(Action onDone = null)
        {
            if (profile == null) { onDone?.Invoke(); return null; }
            Capture();
            _seq?.Kill();
            _seq = DOTween.Sequence();

            int n = elements.Count;
            for (int i = 0; i < n; i++)
            {
                Element e = elements[i];
                if (e == null || e.target == null) continue;
                int fromEnd = (n - 1) - i;          // last in, first out
                Tween t = BuildOut(e, i);
                if (t != null) _seq.Insert(fromEnd * profile.cascadeStagger, t);
            }

            if (backdrop != null)
                _seq.Insert(0f, backdrop.DOFade(0f, profile.fastOutDuration)
                                       .SetEase(Ease.InQuad)
                                       .ApplyMenuDefaults(profile, backdrop.gameObject));

            _seq.ApplyMenuDefaults(profile, gameObject);
            _seq.OnComplete(() => onDone?.Invoke());

            // nothing to animate? still fire the callback so the caller isn't stuck
            if (_seq.Duration() <= 0f) { _seq.Kill(); onDone?.Invoke(); return null; }
            return _seq;
        }

        private Tween BuildOut(Element e, int i)
        {
            switch (e.entry)
            {
                case Entry.ScalePop:
                    return e.target.ScalePopOut(profile, _homeScale[i], e.group);
                case Entry.FadeOnly:
                    return e.group != null ? e.group.FadeOut(profile, e.target.gameObject) : null;
                default:
                    return e.target.SlideOut(profile, OffsetFor(e), e.group);
            }
        }
    }
}
