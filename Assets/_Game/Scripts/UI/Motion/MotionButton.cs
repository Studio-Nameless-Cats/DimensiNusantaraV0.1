using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

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

            // auto-grab the fill from the Button if it wasn't wired by hand
            if (fillGraphic == null)
            {
                Button btn = GetComponent<Button>();
                fillGraphic = btn != null ? btn.targetGraphic : GetComponent<Graphic>();
            }

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
            MotionEvents.RaiseConfirm();
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
