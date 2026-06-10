using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

namespace Nusantara.UI.Motion
{
    // The thing that makes a menu row feel alive. Drop it on a Button (it's the
    // beefier replacement for ButtonHoverEffect). It listens to Unity's real
    // selection events so it works the same under a mouse OR a gamepad/keyboard:
    // when a row gets focus it pops, slams to red, shows its chevron and blips.
    // On confirm it punches and shouts "confirm".
    //
    // Mouse hover just routes through the EventSystem (we select ourselves), so
    // there's exactly one code path for "this row is focused" no matter the input.
    [RequireComponent(typeof(Button))]
    public class MotionButton : MonoBehaviour,
        IPointerEnterHandler,
        ISelectHandler, IDeselectHandler,
        ISubmitHandler, IPointerClickHandler
    {
        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Graphics to recolor on focus")]
        [Tooltip("The row's background/fill graphic. Slams to the profile's selected fill.")]
        [SerializeField] private Graphic fillGraphic;
        [Tooltip("The label graphic (Text or TMP). Slams to the profile's selected text color.")]
        [SerializeField] private Graphic labelGraphic;

        [Header("Optional chevron")]
        [Tooltip("Little arrow/marker shown next to the focused row. Leave null if you don't have one.")]
        [SerializeField] private GameObject chevron;

        [Header("Optional click sound override")]
        [Tooltip("Custom click sound for THIS button instead of the menu's shared confirm blip. Drag a clip for a one-off, OR use the library id below. Leave both empty and the button uses the normal confirm sound (via MotionEvents). Needs an AudioManager in the scene.")]
        [SerializeField] private AudioClip clickSfxClip;
        [Tooltip("Library sfx id for this button's click. Used only when no Click Sfx Clip is set above.")]
        [SerializeField] private string clickSfxId = "";

        // cached rest values so we always pop/return relative to where the row lives
        private RectTransform _rt;
        private Vector3 _baseScale;
        private float _baseX;
        private Color _restFill;
        private Color _restText;
        private bool _focused;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            _baseX = _rt.anchoredPosition.x;

            // Resolve graphics to THIS button's own hierarchy. We accept a hand-wired
            // ref only if it actually belongs to this button; otherwise we grab our
            // own. This self-heals the classic mistake of every row pointing at one
            // button's image/label (so they'd all recolor the same row on focus).
            if (!BelongsToMe(fillGraphic))
            {
                Button btn = GetComponent<Button>();
                fillGraphic = btn != null && btn.targetGraphic != null ? btn.targetGraphic : GetComponent<Graphic>();
            }

            if (!BelongsToMe(labelGraphic))
                labelGraphic = FindOwnLabel(fillGraphic);

            if (fillGraphic != null)  _restFill = fillGraphic.color;
            if (labelGraphic != null) _restText = labelGraphic.color;

            if (chevron != null) chevron.SetActive(false);
        }

        // Capture rest again right before the entrance plays, in case layout moved
        // us around after Awake. MenuSequencer calls this on everything first.
        public void CaptureRest()
        {
            _baseScale = _rt.localScale;
            _baseX = _rt.anchoredPosition.x;
        }

        // Lets the sequencer drop a row in already looking selected (the active
        // "Lanjutkan" row is supposed to land in its red state).
        public void SetFocusedInstant(bool on)
        {
            _focused = on;
            if (fillGraphic != null)  fillGraphic.color = on && profile != null ? profile.selectedFillColor : _restFill;
            if (labelGraphic != null) labelGraphic.color = on && profile != null ? profile.selectedTextColor : _restText;
            if (chevron != null) chevron.SetActive(on);
        }

        // Mouse moving over the row just tells the EventSystem to select it, so
        // OnSelect below is the single place focus is actually handled.
        public void OnPointerEnter(PointerEventData _)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData _)
        {
            if (_focused) return;
            _focused = true;
            if (profile == null) return;

            _rt.SelectPop(profile, _baseScale, _baseX);
            if (fillGraphic != null)  fillGraphic.ColorSlam(profile.selectedFillColor, profile);
            if (labelGraphic != null) labelGraphic.ColorSlam(profile.selectedTextColor, profile);
            if (chevron != null) chevron.SetActive(true);

            MotionEvents.RaiseMove();
        }

        public void OnDeselect(BaseEventData _)
        {
            if (!_focused) return;
            _focused = false;
            if (profile == null) return;

            _rt.Deselect(profile, _baseScale, _baseX);
            if (fillGraphic != null)  fillGraphic.ColorSlam(_restFill, profile);
            if (labelGraphic != null) labelGraphic.ColorSlam(_restText, profile);
            if (chevron != null) chevron.SetActive(false);
        }

        // Gamepad/keyboard "submit" and mouse click both land here.
        public void OnSubmit(BaseEventData _)      => Confirm();
        public void OnPointerClick(PointerEventData _) => Confirm();

        private void Confirm()
        {
            if (profile == null) return;
            _rt.Pulse(profile);

            // If this button has its own click sound, play that and skip the shared
            // confirm blip so we don't double up. Otherwise raise the normal event
            // and let AudioManager play the menu-wide confirm sound.
            if (HasClickOverride()) PlayClickOverride();
            else MotionEvents.RaiseConfirm();
        }

        private bool HasClickOverride() => clickSfxClip != null || !string.IsNullOrEmpty(clickSfxId);

        private void PlayClickOverride()
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            if (clickSfxClip != null) am.PlaySfx(clickSfxClip);
            else am.PlaySfx(clickSfxId);
        }

        // True only if 'g' exists and sits somewhere inside THIS button's hierarchy.
        // (IsChildOf is true for the transform itself too, so a graphic on the button
        // root counts.)
        private bool BelongsToMe(Graphic g)
        {
            return g != null && g.transform.IsChildOf(transform);
        }

        // Digs through our own children for the label - the first Text/TMP graphic
        // that isn't the fill. Returns null if the button has no text.
        private Graphic FindOwnLabel(Graphic skip)
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g == skip) continue;
                if (g is TMP_Text || g is Text) return g;
            }
            return null;
        }

        void OnDisable()
        {
            // leave the row clean if it gets hidden mid-tween
            _rt.DOKill();
            _rt.localScale = _baseScale;
            Vector2 pos = _rt.anchoredPosition; pos.x = _baseX; _rt.anchoredPosition = pos;
            _focused = false;
        }
    }
}
