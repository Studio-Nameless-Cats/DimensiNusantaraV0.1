using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One skill card inside the SkillPanel: icon + name + cost sitting on a Button.
// It goes dim and unclickable when the user can't afford it, or shows a blank "-"
// placeholder when the slot has no skill in it.
//
// Prefab layout:
//   Root (Button + Image background + CanvasGroup + SkillCard)
//     - Icon     (Image)
//     - NameText (TextMeshProUGUI)
//     - CostText (TextMeshProUGUI)
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

    // Fills in this card. Pass skill == null for a blank placeholder. affordable == false
    // makes it dim and unclickable. onChosen only fires for a real skill you can afford.
    public void Bind(SkillData skill, bool affordable, string costSuffix, Action onChosen)
    {
        if (button == null)      button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (button != null) button.onClick.RemoveAllListeners();

        if (skill == null)
        {
            if (nameText) nameText.text = "-";
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
