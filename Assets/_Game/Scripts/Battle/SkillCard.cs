using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One skill card inside the SkillPanel: icon + name + cost, on a Button.
/// Greyed (dimmed + non-interactable) when the user can't afford it, or shown
/// as an empty "—" placeholder when the slot has no skill.
///
/// Prefab layout:
///   Root (Button + Image background + CanvasGroup + SkillCard)
///     ├── Icon     (Image)
///     ├── NameText (TextMeshProUGUI)
///     └── CostText (TextMeshProUGUI)
/// </summary>
public class SkillCard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image           icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button          button;
    [Tooltip("CanvasGroup on the root — used to dim the card when unaffordable. Optional.")]
    [SerializeField] private CanvasGroup     canvasGroup;

    [Header("Display")]
    [Tooltip("Suffix after the cost number, e.g. 'MP' or 'SP'. Set per-show by SkillPanel.")]
    [SerializeField] private float disabledAlpha = 0.4f;

    /// <summary>
    /// Populate this card. skill == null → empty placeholder. affordable == false →
    /// dimmed and non-clickable. onChosen fires only on an affordable, real skill.
    /// </summary>
    public void Bind(SkillData skill, bool affordable, string costSuffix, Action onChosen)
    {
        if (button == null)      button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (button != null) button.onClick.RemoveAllListeners();

        if (skill == null)
        {
            if (nameText) nameText.text = "—";
            if (costText) costText.text = "";
            if (icon)     icon.enabled  = false;
            SetEnabled(false);
            return;
        }

        if (nameText) nameText.text = skill.Name;
        if (costText) costText.text = skill.Cost > 0 ? $"{skill.Cost} {costSuffix}".Trim() : "";
        if (icon)
        {
            icon.enabled = skill.Icon != null;
            icon.sprite  = skill.Icon;
        }

        SetEnabled(affordable);
        if (affordable && button != null)
            button.onClick.AddListener(() => onChosen?.Invoke());
    }

    private void SetEnabled(bool on)
    {
        if (button)      button.interactable = on;
        if (canvasGroup) canvasGroup.alpha   = on ? 1f : disabledAlpha;
    }
}
