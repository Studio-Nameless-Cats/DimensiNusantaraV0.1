using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skill-picker overlay that opens ON TOP of the 4-button command menu when the
/// player presses SKILL or SPECIAL SKILL. Shows one SkillCard per skill (greyed
/// when unaffordable), an empty label when the character has no skills, and a
/// full-screen backdrop button so tapping outside the cards cancels.
///
/// Returns the chosen skill via the Show() callback, or null if cancelled
/// (tapped outside) — BattleSystem reopens the command menu on null.
///
/// Component placement (mirrors ParrySystem):
///   Put SkillPanel on an ALWAYS-ACTIVE object (e.g. the BattleSystem GO or Canvas),
///   and assign 'panelRoot' to the overlay panel it toggles.
///
/// Suggested hierarchy for panelRoot:
///   SkillPanelRoot (disabled by default)
///     ├── Backdrop  (full-screen transparent Image + Button) ← tap-outside-to-close
///     ├── HeaderText (TMP — "— PILIH SKILL —")
///     ├── EmptyLabel (TMP — "Belum ada skill") [optional]
///     └── CardsContainer (Grid/Horizontal Layout Group) ← cards spawn here
/// </summary>
public class SkillPanel : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The overlay panel toggled on/off. Disabled by default.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Full-screen button behind the cards. Clicking it cancels (closes without choosing).")]
    [SerializeField] private Button backdropButton;

    [Header("Header / empty")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private string normalHeader  = "— PILIH SKILL —";
    [SerializeField] private string specialHeader = "— SKILL SPESIAL —";
    [Tooltip("Shown when the character has no skills in this category. Optional.")]
    [SerializeField] private GameObject emptyLabel;

    [Header("Cards")]
    [Tooltip("Prefab with a SkillCard component.")]
    [SerializeField] private GameObject skillCardPrefab;
    [Tooltip("Parent (with a Layout Group) the cards spawn under.")]
    [SerializeField] private Transform cardsContainer;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private readonly List<SkillCard> pool = new List<SkillCard>();
    private Action<SkillData> onComplete;

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>
    /// Open the picker for one character. category selects header + which resource
    /// gates affordability. onChosen receives the picked skill, or null if cancelled.
    /// </summary>
    public void Show(IReadOnlyList<SkillData> skills, PartyMember user,
                     SkillCategory category, Action<SkillData> onChosen)
    {
        if (panelRoot == null || skillCardPrefab == null || cardsContainer == null)
        {
            Debug.LogError("[SkillPanel] panelRoot / skillCardPrefab / cardsContainer not assigned! ❌");
            onChosen?.Invoke(null);
            return;
        }

        onComplete = onChosen;

        if (headerText)
            headerText.text = category == SkillCategory.Special ? specialHeader : normalHeader;

        if (backdropButton)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Cancel);
        }

        int    count  = skills?.Count ?? 0;
        string suffix = category == SkillCategory.Special ? "SP" : "MP";

        if (emptyLabel) emptyLabel.SetActive(count == 0);

        EnsurePool(count);
        for (int i = 0; i < pool.Count; i++)
        {
            if (i < count)
            {
                var  skill      = skills[i];
                bool affordable = category == SkillCategory.Special
                    ? user.CanAffordSpecial(skill.Cost)
                    : user.CanAffordMp(skill.Cost);

                pool[i].gameObject.SetActive(true);
                pool[i].Bind(skill, affordable, suffix, () => Choose(skill));
            }
            else
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        panelRoot.SetActive(true);
    }

    // ── Internals ───────────────────────────────────────────────────────────────

    private void Choose(SkillData skill)
    {
        var cb = onComplete;
        onComplete = null;
        Hide();
        cb?.Invoke(skill);
    }

    private void Cancel()
    {
        var cb = onComplete;
        onComplete = null;
        Hide();
        cb?.Invoke(null);
    }

    private void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    private void EnsurePool(int needed)
    {
        while (pool.Count < needed)
        {
            var go   = Instantiate(skillCardPrefab, cardsContainer);
            var card = go.GetComponent<SkillCard>();
            if (card == null)
            {
                Debug.LogError("[SkillPanel] skillCardPrefab has no SkillCard component! ❌");
                Destroy(go);
                return;
            }
            go.SetActive(false);
            pool.Add(card);
        }
    }
}
