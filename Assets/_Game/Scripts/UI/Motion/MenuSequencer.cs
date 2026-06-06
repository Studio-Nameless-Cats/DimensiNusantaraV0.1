using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // The conductor. Owns the whole main-menu entrance timeline from section 5 of
    // ANIM_DESIGN_PLAN.md and builds it as one DOTween Sequence on enable.
    // Everything else just exposes a "play me" method this calls - the sequencer
    // never reaches into anyone's guts.
    //
    // Timeline (offsets into the sequence):
    //   0.00  screen wipe reveals the menu + entrance whoosh
    //   0.10  title lockup slides in, red shadow trails it
    //   0.30  subtitle slides in
    //   0.35  corner streaks slide in, dial pulses as it lands
    //   0.45  buttons cascade in from the left
    //   0.80  version text fades up
    //   ~1.1  everything settled, IdleDrift loops take over on their own delay
    public class MenuSequencer : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Screen transition")]
        [Tooltip("The wipe panel. Reveal() runs at the start of the entrance.")]
        [SerializeField] private ScreenTransition screenTransition;

        [Header("Title block")]
        [SerializeField] private RectTransform titleLockup;
        [SerializeField] private CanvasGroup titleGroup;
        [Tooltip("The red layer behind the wordmark. Trails in a beat late.")]
        [SerializeField] private RectTransform titleShadow;
        [SerializeField] private CanvasGroup titleShadowGroup;

        [Header("Subtitle")]
        [SerializeField] private RectTransform subtitle;
        [SerializeField] private CanvasGroup subtitleGroup;

        [Header("Corner streaks + dial")]
        [SerializeField] private RectTransform cornerStreaks;
        [SerializeField] private CanvasGroup cornerStreaksGroup;
        [Tooltip("The dial that pulses as the streaks land.")]
        [SerializeField] private RectTransform dial;

        [Header("Buttons (top to bottom: Lanjutkan, Mulai Baru, Pengaturan, Keluar)")]
        [SerializeField] private List<MotionButton> buttons = new List<MotionButton>();
        [Tooltip("Index of the row that lands already selected (Lanjutkan = 0).")]
        [SerializeField] private int activeButtonIndex = 0;

        [Header("Version label")]
        [SerializeField] private RectTransform versionText;
        [SerializeField] private CanvasGroup versionGroup;

        [Header("Behaviour")]
        [Tooltip("Play the entrance automatically when this object enables.")]
        [SerializeField] private bool playOnEnable = true;

        private Sequence _in;

        // rest positions captured before we move anything, so PlayOut knows home
        private readonly List<RectTransform> _buttonRects = new List<RectTransform>();
        private readonly List<CanvasGroup> _buttonGroups = new List<CanvasGroup>();

        void Awake()
        {
            // cache the button rects/groups once
            foreach (MotionButton b in buttons)
            {
                if (b == null) continue;
                _buttonRects.Add((RectTransform)b.transform);
                _buttonGroups.Add(b.GetComponent<CanvasGroup>());
                b.CaptureRest();
            }
        }

        void OnEnable()
        {
            if (playOnEnable) PlayIn();
        }

        // Builds and fires the full entrance. Safe to call again - it kills any
        // in-flight entrance first.
        public void PlayIn()
        {
            if (profile == null) return;
            _in?.Kill();

            MotionEvents.RaiseMenuEnter();

            _in = DOTween.Sequence();

            // 0.00 - uncover the menu
            if (screenTransition != null)
                _in.Insert(0f, screenTransition.Reveal());

            // 0.10 - title in, shadow trails
            if (titleLockup != null)
                _in.Insert(0.10f, titleLockup.SkewSlideIn(profile, titleGroup));
            if (titleShadow != null)
                _in.Insert(0.10f, titleShadow.ShadowSlideIn(profile, titleShadowGroup));

            // 0.30 - subtitle
            if (subtitle != null)
                _in.Insert(0.30f, subtitle.SkewSlideIn(profile, subtitleGroup));

            // 0.35 - streaks in, dial pops as they land
            if (cornerStreaks != null)
                _in.Insert(0.35f, cornerStreaks.SkewSlideIn(profile, cornerStreaksGroup));
            if (dial != null)
                _in.Insert(0.42f, dial.Pulse(profile));

            // 0.45 - buttons cascade. The active row lands already red + chevroned.
            for (int i = 0; i < _buttonRects.Count; i++)
            {
                if (buttons[i] != null)
                    buttons[i].SetFocusedInstant(i == activeButtonIndex);
                _in.Insert(0.45f + i * profile.cascadeStagger,
                           _buttonRects[i].SkewSlideIn(profile, _buttonGroups[i]));
            }

            // Tell the EventSystem the active row is the selected one. Without this the
            // row only LOOKS selected (SetFocusedInstant), so hovering another row would
            // light it up without un-lighting this one (two red rows), and keyboard /
            // gamepad nav would have no starting point. SetFocusedInstant already set
            // _focused, so MotionButton.OnSelect early-outs here - no double pop, no SFX.
            SelectActiveButton();

            // 0.80 - version fades up
            if (versionText != null)
            {
                if (versionGroup != null) versionGroup.alpha = 0f;
                _in.Insert(0.80f, versionText.SkewSlideIn(profile, versionGroup));
            }

            _in.ApplyMenuDefaults(profile, gameObject);
        }

        // Points the EventSystem at the active row so focus is real, not just visual.
        private void SelectActiveButton()
        {
            if (activeButtonIndex < 0 || activeButtonIndex >= buttons.Count) return;
            MotionButton active = buttons[activeButtonIndex];
            if (active == null) return;

            EventSystem es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(active.gameObject);
        }

        // Bails the menu out before a scene load: buttons leave in reverse cascade,
        // title and the rest slide off. onComplete fires once everything's gone -
        // chain your ScreenTransition.Play() + scene load off it.
        public void PlayOut(Action onComplete = null)
        {
            if (profile == null) { onComplete?.Invoke(); return; }
            _in?.Kill();

            Sequence outSeq = DOTween.Sequence();

            outSeq.Insert(0f, _buttonRects.CascadeOut(profile, _buttonGroups));
            if (subtitle != null)     outSeq.Insert(0.05f, subtitle.SkewSlideOut(profile, subtitleGroup));
            if (cornerStreaks != null) outSeq.Insert(0.05f, cornerStreaks.SkewSlideOut(profile, cornerStreaksGroup));
            if (titleLockup != null)  outSeq.Insert(0.10f, titleLockup.SkewSlideOut(profile, titleGroup));
            if (titleShadow != null)  outSeq.Insert(0.10f, titleShadow.SkewSlideOut(profile, titleShadowGroup));
            if (versionText != null)  outSeq.Insert(0f, versionText.SkewSlideOut(profile, versionGroup));

            outSeq.ApplyMenuDefaults(profile, gameObject);
            outSeq.OnComplete(() => onComplete?.Invoke());
        }
    }
}
