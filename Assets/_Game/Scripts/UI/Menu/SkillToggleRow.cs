using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    /// <summary>
    /// One row in the loadout editor — a single NORMAL skill that can be toggled
    /// in/out of a member's equipped loadout. Shows icon + name + MP cost, an
    /// "equipped" highlight, and dims when it can't be equipped (loadout full).
    ///
    /// ── Prefab ───────────────────────────────────────────────────────────────
    ///   SkillToggleRow (Button + this component)
    ///     ├ Icon (Image, optional)
    ///     ├ NameLabel (TMP)
    ///     ├ CostLabel (TMP, optional — e.g. "5 MP")
    ///     ├ EquippedHighlight (GameObject, optional — frame/check shown when equipped)
    ///     └ (optional CanvasGroup on root → dims when locked)
    /// </summary>
    public class SkillToggleRow : MonoBehaviour
    {
        [SerializeField] private Button          button;
        [SerializeField] private Image           icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private GameObject      equippedHighlight;
        [Tooltip("Optional — alpha lowered when the row is locked (can't equip more).")]
        [SerializeField] private CanvasGroup     canvasGroup;

        [Header("Locked appearance")]
        [Range(0f, 1f)] [SerializeField] private float lockedAlpha = 0.45f;

        private SkillData _skill;
        private Action<SkillData> _onClick;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(_skill));
        }

        /// <summary>
        /// Bind this row to a skill. <paramref name="equipped"/> drives the highlight;
        /// <paramref name="locked"/> dims + disables the button (skill not equipped and
        /// loadout is full). Equipped rows stay interactable so they can be removed.
        /// </summary>
        public void Bind(SkillData skill, bool equipped, bool locked, Action<SkillData> onClick)
        {
            _skill   = skill;
            _onClick = onClick;

            if (nameLabel != null) nameLabel.text = skill != null ? skill.Name : "—";
            if (costLabel != null) costLabel.text = skill != null ? $"{skill.Cost} MP" : "";

            if (icon != null)
            {
                var sprite = skill != null ? skill.Icon : null;
                icon.sprite  = sprite;
                icon.enabled = sprite != null;
            }

            if (equippedHighlight != null) equippedHighlight.SetActive(equipped);

            bool interactable = equipped || !locked;
            if (button != null) button.interactable = interactable;
            if (canvasGroup != null) canvasGroup.alpha = interactable ? 1f : lockedAlpha;
        }
    }
}
