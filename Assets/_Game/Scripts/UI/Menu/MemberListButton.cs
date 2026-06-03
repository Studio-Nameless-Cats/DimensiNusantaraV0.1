using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    /// <summary>
    /// A reusable list entry for a party member — used by both the Character tab
    /// (click to inspect) and the Party tab (roster). Shows a portrait, a name, and
    /// an optional sub-label (e.g. HP). A "selected" highlight marks the active row.
    ///
    /// ── Prefab ───────────────────────────────────────────────────────────────
    ///   MemberButton (Button + this component)
    ///     ├ Portrait (Image, optional)
    ///     ├ NameLabel (TMP)
    ///     ├ SubLabel  (TMP, optional — e.g. "HP 30/50")
    ///     └ SelectedHighlight (GameObject, optional — frame/glow shown when selected)
    /// </summary>
    public class MemberListButton : MonoBehaviour
    {
        [SerializeField] private Button          button;
        [SerializeField] private Image           portrait;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI subLabel;
        [SerializeField] private GameObject      selectedHighlight;

        private Action _onClick;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Bind(PartyMember member, Action onClick, string sub = null)
        {
            _onClick = onClick;

            if (nameLabel != null) nameLabel.text = member.Name;

            if (portrait != null)
            {
                var icon = member.Base != null ? member.Base.Icon : null;
                portrait.sprite  = icon;
                portrait.enabled = icon != null;
            }

            if (subLabel != null)
            {
                subLabel.text = sub ?? $"HP {member.CurrentHp}/{member.MaxHp}";
                subLabel.gameObject.SetActive(true);
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);
        }
    }
}
