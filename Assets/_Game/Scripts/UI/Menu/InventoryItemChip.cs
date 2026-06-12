using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    // One item row in the inventory grid: name on the left, "x3" count on the right.
    // InventoryPanel spawns these from a prefab, one per stack, and tells each one
    // whether it's the selected chip. Selected = the loud red breakout (Accent fill,
    // arrow visible, content nudged proud); everything else sits back on SurfaceRaised.
    public class InventoryItemChip : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The chip's background Image (the sheared slab).")]
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [Tooltip("Red arrow shown to the left of the selected chip. Starts hidden.")]
        [SerializeField] private GameObject selectionArrow;
        [Tooltip("Inner content holder that gets nudged when selected. The chips live in a " +
                 "Layout Group, so we nudge a CHILD instead of the chip itself (a position " +
                 "tween on the chip would fight the layout - UI_RULES rule 15).")]
        [SerializeField] private RectTransform content;
        [SerializeField] private Button button;

        [Header("Look")]
        [Tooltip("How far the content slides right when selected (the 'sits proud' nudge).")]
        [SerializeField] private float selectedNudge = 8f;

        private Action _onClicked;
        private Vector2 _contentHome;
        private bool _homeCaptured;

        public ItemData Item { get; private set; }

        void Awake()
        {
            // Self-heal: fall back to the Button on this same object if the field's empty.
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => _onClicked?.Invoke());
            CaptureHome();
        }

        private void CaptureHome()
        {
            if (_homeCaptured || content == null) return;
            _contentHome  = content.anchoredPosition;
            _homeCaptured = true;
        }

        // Fill the chip in for one stack. The panel calls this on every refresh.
        public void Setup(ItemData item, int count, Action onClicked)
        {
            Item       = item;
            _onClicked = onClicked;

            if (nameText  != null) nameText.text  = item != null ? item.Name : "";
            if (countText != null) countText.text = count > 0 ? $"x{count}" : "";

            SetSelected(false);
        }

        // Flip between the loud selected look and the quiet resting one.
        public void SetSelected(bool selected)
        {
            CaptureHome();

            if (background != null)
                background.color = selected ? NusantaraPalette.Role.Accent
                                            : NusantaraPalette.Role.SurfaceRaised;

            // Text stays OnDark in both states - it reads fine on the red too.
            if (nameText  != null) nameText.color  = NusantaraPalette.Role.OnDark;
            if (countText != null) countText.color = selected ? NusantaraPalette.Role.OnDark
                                                              : NusantaraPalette.Role.Muted;

            if (selectionArrow != null) selectionArrow.SetActive(selected);

            if (content != null)
                content.anchoredPosition = selected
                    ? _contentHome + new Vector2(selectedNudge, 0f)
                    : _contentHome;
        }
    }
}
