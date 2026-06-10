using System;
using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // The "drop it on one thing and tune it right there" animator. No shared
    // profile asset, no Animator, no clips - just tweens you wire up in the
    // Inspector per element.
    //
    // This is the freeform cousin of PanelMotion. PanelMotion drives a whole
    // panel of children off the locked main-menu MotionProfile; this one is for
    // a single element you want to feel however you want, all set on the spot:
    // a popup, a toast, a floating icon, a battle banner, whatever.
    //
    // Three moments you can set up:
    //   Entry  - plays when it shows up (auto on enable, or call PlayIn()).
    //   Exit   - plays when it leaves (call PlayOut() / Hide() - NOT SetActive(false)).
    //   Idle   - loops forever once it's settled, while it's still on screen.
    //
    // Each of Entry and Exit can move, scale, fade and rotate all at once - tick
    // the channels you want and leave the rest off. Idle picks one gentle loop.
    //
    // Heads up on hiding: a tween can't run on an object that's already inactive,
    // so don't SetActive(false) if you want the exit to play. Call Hide() (it runs
    // the exit, THEN deactivates) or PlayOut(onDone) and do your own thing in the
    // callback.
    [DisallowMultipleComponent]
    public class UIAnimator : MonoBehaviour
    {
        // ---- per-channel config shared by Entry and Exit ------------------------

        // One transition (entry OR exit). Tick whichever channels you want; each
        // one reads "where it sits at rest" and animates relative to that. For
        // Entry these values are where it STARTS (then flies home to rest). For
        // Exit they're where it ENDS UP (starts at rest, then flies out to here).
        [Serializable]
        public class Transition
        {
            [Header("Timing")]
            [Tooltip("How long this transition takes, in seconds. Smaller is snappier.")]
            public float duration = 0.28f;
            [Tooltip("The easing curve. OutBack gives a nice springy overshoot on entry; InBack yanks it away on exit.")]
            public Ease ease = Ease.OutBack;
            [Tooltip("Only used by the Back/Elastic eases - how hard it punches past the target before settling. 1.6 is a tasty slam.")]
            public float overshoot = 1.6f;
            [Tooltip("Wait this long before the transition starts.")]
            public float delay = 0f;

            [Header("Move (anchoredPosition)")]
            [Tooltip("Animate position?")]
            public bool move = true;
            [Tooltip("Offset from rest, in pixels. Entry: starts here and slides home. Exit: slides out to here. e.g. (0,-240) comes up from below / drops down on the way out.")]
            public Vector2 positionOffset = new Vector2(0f, -240f);

            [Header("Scale (localScale)")]
            [Tooltip("Animate scale?")]
            public bool scale = false;
            [Tooltip("Multiplier on rest scale. Entry: starts at this and grows to rest. Exit: shrinks to this. 0.6 = pops up from 60%.")]
            public float scaleMultiplier = 0.6f;

            [Header("Fade (CanvasGroup alpha)")]
            [Tooltip("Animate alpha? Adds a CanvasGroup automatically if the object doesn't have one.")]
            public bool fade = true;

            [Header("Rotate (z only, degrees)")]
            [Tooltip("Animate rotation?")]
            public bool rotate = false;
            [Tooltip("Degrees offset from rest. Entry: starts tilted by this and untwists to rest. Exit: spins out to this. e.g. -15 leans in from the left.")]
            public float rotationOffset = -15f;
        }

        // ---- idle loop config ---------------------------------------------------

        public enum IdleMode
        {
            None,           // no idle loop
            BobVertical,    // gentle up/down float - good for a floating icon or prompt
            PanHorizontal,  // slow left/right slide - good for a watermark or banner
            Breathe,        // tiny scale in/out, like it's breathing
            Rotate,         // small rocking tilt back and forth
            Pulse           // a repeating punch-scale beat, for a "look at me" element
        }

        [Serializable]
        public class Idle
        {
            [Tooltip("Which resting loop to run once the element has settled. None = stays put.")]
            public IdleMode mode = IdleMode.None;
            [Tooltip("How far it travels from rest. Pixels for Bob/Pan, degrees for Rotate, fraction for Breathe (0.03 = +/-3%), punch strength for Pulse.")]
            public float amplitude = 8f;
            [Tooltip("Seconds for one full loop (one half for a yoyo there-and-back). Bigger = slower and lazier.")]
            public float duration = 2.5f;
            [Tooltip("Easing for the loop. InOutSine is the classic soft drift.")]
            public Ease ease = Ease.InOutSine;
            [Tooltip("Wait this long after the entry finishes before the idle kicks in.")]
            public float startDelay = 0.15f;
        }

        // ---- inspector ----------------------------------------------------------

        [Header("Entry (on show)")]
        [SerializeField] private Transition entry = new Transition();

        [Header("Exit (on hide)")]
        [SerializeField] private Transition exit = new Transition
        {
            ease = Ease.InBack,
            duration = 0.20f
        };

        [Header("Idle (loops while on screen)")]
        [SerializeField] private Idle idle = new Idle();

        [Header("Behaviour")]
        [Tooltip("Play the entry automatically the moment this object enables. Turn off if a controller calls PlayIn() itself.")]
        [SerializeField] private bool playInOnEnable = true;
        [Tooltip("Run idle even when no entry plays (e.g. an element that's just always on screen).")]
        [SerializeField] private bool idleWithoutEntry = true;
        [Tooltip("Ignore timeScale so this still animates while the game is paused. Usually what you want for UI.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("Put this element on its own nested Canvas so its motion only rebatches ITSELF, not the whole parent canvas. Big win for an idle loop sharing a busy HUD - otherwise every little bob rebuilds the entire HUD batch that frame. Adds a Canvas at runtime if there isn't one already. Leave off for mostly-static elements: each sub-canvas is an extra batch boundary, so it's only worth it for things that actually move a lot.")]
        [SerializeField] private bool isolateOnOwnCanvas = true;
        [Tooltip("Pause the idle loop automatically when this element can't be seen (a parent CanvasGroup faded to 0, or a canvas above it switched off). No point tweening + rebatching something nobody's looking at. Uses Unity's change callbacks, so the check itself costs nothing per frame.")]
        [SerializeField] private bool pauseIdleWhenHidden = true;
        [Tooltip("When this hides, also play the EXIT on any child UIAnimators, and wait for the slowest one before we deactivate. You only need this when a child has its OWN distinct exit motion - otherwise SetActive(false) would cut it off mid-tween. Children always ride this object's transform + CanvasGroup for free, and run their own entry on enable, so leave this OFF unless a child genuinely animates differently on the way out.")]
        [SerializeField] private bool driveChildExits = false;

        [Header("Audio (optional - needs an AudioManager in the scene)")]
        [Tooltip("Plays the moment the entry starts. Drag a clip here for a one-off sound, OR leave it empty and type a library id below. Both empty = silent entry.")]
        [SerializeField] private AudioClip entrySfxClip;
        [Tooltip("Library sfx id played on entry. Used only when no Entry Sfx Clip is set above. Empty = silent.")]
        [SerializeField] private string entrySfxId = "";
        [Tooltip("Plays the moment the exit starts. A clip here wins over the id below.")]
        [SerializeField] private AudioClip exitSfxClip;
        [Tooltip("Library sfx id played on exit. Used only when no Exit Sfx Clip is set above. Empty = silent.")]
        [SerializeField] private string exitSfxId = "";

        // ---- runtime ------------------------------------------------------------

        private RectTransform _rt;
        private CanvasGroup _cg;

        // rest values, grabbed once so every transition is relative to the same home
        private Vector2 _restPos;
        private Vector3 _restScale;
        private Vector3 _restRot;
        private float _restAlpha = 1f;
        private bool _captured;

        private Tween _transitionTween;  // the current entry/exit
        private Tween _idleTween;        // the looping idle
        private Canvas _ownCanvas;       // the isolation canvas, if we added one
        private bool _idleUserPaused;    // someone called PauseIdle() by hand - don't auto-resume over them
        private UIAnimator[] _children;  // nested UIAnimators we drive on exit (only when driveChildExits)

        void Awake()
        {
            _rt = (RectTransform)transform;
            if (isolateOnOwnCanvas) EnsureIsolationCanvas();
            Capture();
        }

        // The nested-canvas trick. When a UI element moves / scales / rotates, Unity
        // marks its whole Canvas dirty and rebuilds that canvas's batch THAT frame.
        // So an element idling on a shared HUD canvas drags the entire HUD into a
        // rebatch every single frame, just for one little bob. Giving the element
        // its own Canvas walls that rebuild off so it only ever rebatches itself.
        // A nested canvas inherits the parent's sorting by default, so nothing looks
        // different - it's purely a batching boundary, not a visual change.
        private void EnsureIsolationCanvas()
        {
            // already has one (the designer added it, or this ran already)? leave it.
            if (GetComponent<Canvas>() != null) return;
            _ownCanvas = gameObject.AddComponent<Canvas>();
            // inherit the parent's sorting / render mode - we only want the batch
            // split, not a different draw order.
            _ownCanvas.overrideSorting = false;
            // heads up: a nested canvas is still covered by the parent's
            // GraphicRaycaster, so we do NOT add another raycaster here - that'd
            // just be extra cost for no reason.
        }

        void OnEnable()
        {
            if (playInOnEnable) PlayIn();
            else if (idleWithoutEntry) StartIdle(idle.startDelay);
        }

        void OnDisable()
        {
            // object's going away - stop everything and snap back so we never
            // leave it parked mid-tween for next time it enables.
            _transitionTween?.Kill();
            _idleTween?.Kill();
            _transitionTween = null;
            _idleTween = null;
            RestoreRest();
        }

        // Unity fires this on us whenever a CanvasGroup somewhere up the tree changes
        // (alpha, interactable, enabled). Free callback - no polling. We use it to
        // catch a parent panel fading us out so we can pause the idle.
        void OnCanvasGroupChanged()
        {
            if (pauseIdleWhenHidden) RefreshIdleForVisibility();
        }

        // Same deal but for a Canvas above us getting switched on/off.
        void OnCanvasHierarchyChanged()
        {
            if (pauseIdleWhenHidden) RefreshIdleForVisibility();
        }

        // Grab the home pose and make sure we have a CanvasGroup if we'll need one.
        // Only does the work once. If a layout group shoves this element around
        // after first enable, call CaptureNow() yourself once the layout settles.
        private void Capture()
        {
            if (_captured) return;
            if (_rt == null) _rt = (RectTransform)transform;

            _restPos = _rt.anchoredPosition;
            _restScale = _rt.localScale;
            _restRot = _rt.localEulerAngles;

            bool needsFade = entry.fade || exit.fade;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null && needsFade) _cg = gameObject.AddComponent<CanvasGroup>();
            if (_cg != null) _restAlpha = _cg.alpha;

            // grab nested UIAnimators once (minus ourselves) so PlayOut can drive
            // their exits too - otherwise SetActive(false) cuts a child's exit short.
            // include inactive ones so a child that's currently off still gets wired.
            if (driveChildExits)
            {
                var found = GetComponentsInChildren<UIAnimator>(true);
                var list = new System.Collections.Generic.List<UIAnimator>(found.Length);
                foreach (var a in found) if (a != this) list.Add(a);
                _children = list.ToArray();
            }

            _captured = true;
        }

        // Re-grab the rest pose from wherever the element sits right now. Use this
        // if a layout group repositioned things after Awake.
        public void CaptureNow()
        {
            _captured = false;
            Capture();
        }

        private void RestoreRest()
        {
            if (_rt == null) return;
            _rt.anchoredPosition = _restPos;
            _rt.localScale = _restScale;
            _rt.localEulerAngles = _restRot;
            if (_cg != null) _cg.alpha = _restAlpha;
        }

        // ---- public API ---------------------------------------------------------

        // Bring it on screen. Parks everything at the entry's start pose, then
        // tweens home, then hands off to the idle loop. Returns the tween so you
        // can chain or wait on it. Spamming PlayIn/PlayOut never strands a piece -
        // we kill whatever's in flight first.
        public Tween PlayIn()
        {
            Capture();
            PlaySfx(entrySfxClip, entrySfxId);
            _transitionTween?.Kill();
            _idleTween?.Kill();

            Sequence s = DOTween.Sequence();
            ApplyDefaults(s);

            // park at the start pose for every channel that's on
            if (entry.move)   _rt.anchoredPosition = _restPos + entry.positionOffset;
            if (entry.scale)  _rt.localScale = _restScale * entry.scaleMultiplier;
            if (entry.rotate) _rt.localEulerAngles = _restRot + new Vector3(0f, 0f, entry.rotationOffset);
            if (entry.fade && _cg != null) _cg.alpha = 0f;

            // then fly everything home together
            if (entry.move)
                s.Join(_rt.DOAnchorPos(_restPos, entry.duration).SetEase(entry.ease, entry.overshoot));
            if (entry.scale)
                s.Join(_rt.DOScale(_restScale, entry.duration).SetEase(entry.ease, entry.overshoot));
            if (entry.rotate)
                s.Join(_rt.DOLocalRotate(_restRot, entry.duration).SetEase(entry.ease, entry.overshoot));
            if (entry.fade && _cg != null)
                s.Join(_cg.DOFade(_restAlpha, entry.duration).SetEase(Ease.OutQuad));

            s.SetDelay(entry.delay);
            // once it's home, settle into the idle loop
            s.OnComplete(() => StartIdle(idle.startDelay));

            // nothing ticked? just go straight to idle so the element isn't dead
            if (s.Duration() <= 0f)
            {
                s.Kill();
                _transitionTween = null;
                StartIdle(idle.startDelay);
                return null;
            }

            _transitionTween = s;
            return s;
        }

        // Send it away. onDone fires once it's fully gone - perfect spot to hang
        // SetActive(false), a scene load, an unpause, whatever. The idle loop is
        // killed first so it doesn't fight the exit.
        public Tween PlayOut(Action onDone = null)
        {
            Capture();
            PlaySfx(exitSfxClip, exitSfxId);
            _idleTween?.Kill();
            _idleTween = null;
            _transitionTween?.Kill();

            // drive any child UIAnimators' exits in concert, and remember the slowest
            // so we don't deactivate (in onDone) before a child finishes its own exit.
            // children run their OWN exit only - WE own the SetActive(false) for the tree.
            float childExitWait = 0f;
            if (driveChildExits && _children != null)
            {
                foreach (var child in _children)
                {
                    if (child == null || !child.isActiveAndEnabled) continue;
                    Tween ct = child.PlayOut();
                    if (ct != null) childExitWait = Mathf.Max(childExitWait, ct.Duration());
                }
            }

            Sequence s = DOTween.Sequence();
            ApplyDefaults(s);

            // start from rest (or wherever we are) and fly out to the exit pose
            if (exit.move)
                s.Join(_rt.DOAnchorPos(_restPos + exit.positionOffset, exit.duration).SetEase(exit.ease, exit.overshoot));
            if (exit.scale)
                s.Join(_rt.DOScale(_restScale * exit.scaleMultiplier, exit.duration).SetEase(exit.ease, exit.overshoot));
            if (exit.rotate)
                s.Join(_rt.DOLocalRotate(_restRot + new Vector3(0f, 0f, exit.rotationOffset), exit.duration).SetEase(exit.ease, exit.overshoot));
            if (exit.fade && _cg != null)
                s.Join(_cg.DOFade(0f, exit.duration).SetEase(Ease.InQuad));

            s.SetDelay(exit.delay);

            // if a child's exit runs longer than ours, pad the tail so the deactivate
            // in onDone holds off until the slowest child is fully done.
            float ownDur = s.Duration();
            if (childExitWait > ownDur) s.AppendInterval(childExitWait - ownDur);

            s.OnComplete(() => onDone?.Invoke());

            // nothing to animate? still fire the callback so the caller isn't stuck
            if (s.Duration() <= 0f)
            {
                s.Kill();
                _transitionTween = null;
                onDone?.Invoke();
                return null;
            }

            _transitionTween = s;
            return s;
        }

        // Convenience: make sure we're active, then play the entry. Use this from
        // a button instead of SetActive(true) so the entry always runs.
        public void Show()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);  // OnEnable handles PlayIn if playInOnEnable
            else PlayIn();
        }

        // Convenience: play the exit, THEN deactivate. Use this instead of
        // SetActive(false) so the exit actually gets to play.
        public void Hide()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            PlayOut(() => gameObject.SetActive(false));
        }

        // ---- idle loop ----------------------------------------------------------

        // Kick off the resting loop. Safe to call anytime - it kills any existing
        // idle first. Does nothing if the mode is None.
        public void StartIdle(float delay = 0f)
        {
            _idleTween?.Kill();
            _idleTween = null;
            if (idle.mode == IdleMode.None) return;
            Capture();

            float amp = idle.amplitude;
            float dur = idle.duration;

            switch (idle.mode)
            {
                case IdleMode.BobVertical:
                    _rt.anchoredPosition = _restPos - new Vector2(0f, amp);
                    _idleTween = _rt.DOAnchorPosY(_restPos.y + amp, dur).SetEase(idle.ease).SetLoops(-1, LoopType.Yoyo);
                    break;

                case IdleMode.PanHorizontal:
                    _rt.anchoredPosition = _restPos - new Vector2(amp, 0f);
                    _idleTween = _rt.DOAnchorPosX(_restPos.x + amp, dur).SetEase(idle.ease).SetLoops(-1, LoopType.Yoyo);
                    break;

                case IdleMode.Breathe:
                    _rt.localScale = _restScale * (1f - amp);
                    _idleTween = _rt.DOScale(_restScale * (1f + amp), dur).SetEase(idle.ease).SetLoops(-1, LoopType.Yoyo);
                    break;

                case IdleMode.Rotate:
                    _rt.localEulerAngles = _restRot - new Vector3(0f, 0f, amp);
                    _idleTween = _rt.DOLocalRotate(_restRot + new Vector3(0f, 0f, amp), dur).SetEase(idle.ease).SetLoops(-1, LoopType.Yoyo);
                    break;

                case IdleMode.Pulse:
                    // a repeating punch beat - punch already returns to rest on its own,
                    // so we restart-loop it with the duration as the gap between beats
                    _idleTween = _rt.DOPunchScale(Vector3.one * amp, dur, 8, 0.6f).SetLoops(-1, LoopType.Restart);
                    break;
            }

            if (_idleTween != null)
            {
                _idleTween.SetDelay(delay);
                ApplyDefaults(_idleTween);
                // fresh loop starts playing - if we're hidden right now, park it
                // straight away so it doesn't tick a single wasted frame.
                if (pauseIdleWhenHidden && !_idleUserPaused && !IsEffectivelyVisible())
                    _idleTween.Pause();
            }
        }

        // Stop the idle and ease back to rest so it doesn't snap.
        public void StopIdle()
        {
            _idleTween?.Kill();
            _idleTween = null;
            _idleUserPaused = false;
            RestoreRest();
        }

        // Pause the idle loop by hand - for a culling / off-screen system that knows
        // this element isn't worth animating right now. Stays paused until you call
        // ResumeIdle(), even if the auto-visibility check thinks it's fine.
        public void PauseIdle()
        {
            _idleUserPaused = true;
            _idleTween?.Pause();
        }

        // Undo a hand PauseIdle(). Only actually resumes if it's also genuinely
        // visible (when auto-pause is on), so we don't fight that check.
        public void ResumeIdle()
        {
            _idleUserPaused = false;
            if (_idleTween == null) return;
            if (!pauseIdleWhenHidden || IsEffectivelyVisible()) _idleTween.Play();
        }

        // Auto-pause driver, called from the canvas-change callbacks. Plays or pauses
        // the running idle to match whether we're actually on screen. Skips out if
        // someone paused by hand - their call wins.
        private void RefreshIdleForVisibility()
        {
            if (_idleTween == null || _idleUserPaused) return;
            if (IsEffectivelyVisible()) _idleTween.Play();
            else _idleTween.Pause();
        }

        // Cheap-ish "can anyone see me" walk: up the parents, combine CanvasGroup
        // alphas and watch for a switched-off Canvas. Runs only on the change
        // callbacks (not per frame), so the walk is fine. GetComponent per level
        // doesn't allocate.
        private bool IsEffectivelyVisible()
        {
            if (!isActiveAndEnabled) return false;
            Transform t = transform;
            while (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    if (cg.alpha <= 0.001f) return false;
                    if (cg.ignoreParentGroups) break; // groups above here don't affect us
                }
                var cv = t.GetComponent<Canvas>();
                if (cv != null && !cv.isActiveAndEnabled) return false;
                t = t.parent;
            }
            return true;
        }

        // ---- shared tween tail --------------------------------------------------

        // Same as the rest of the Motion system: optionally ignore timeScale, and
        // link to this object so DOTween auto-kills the tween if we get destroyed
        // mid-flight (no leaks, no null-ref spam).
        private T ApplyDefaults<T>(T t) where T : Tween
        {
            if (t == null) return null;
            t.SetUpdate(useUnscaledTime);
            t.SetLink(gameObject);
            return t;
        }

        // Fire a one-off through the global AudioManager. A clip wins over an id; if
        // both are empty (or there's no AudioManager yet) this is a quiet no-op, so
        // it's always safe to call. This is how an entry/exit gets its own voice
        // without UIAnimator knowing anything about the audio system.
        private void PlaySfx(AudioClip clip, string id)
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            if (clip != null) am.PlaySfx(clip);
            else if (!string.IsNullOrEmpty(id)) am.PlaySfx(id);
        }
    }
}
