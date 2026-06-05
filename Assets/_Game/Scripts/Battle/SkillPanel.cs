using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The skill-picker overlay that pops up on top of the 4-button command menu when the
// player hits SKILL or SPECIAL SKILL. It shows one SkillCard per skill (dimmed if you
// can't afford it), an "empty" label when the character has no skills, and a
// full-screen backdrop button so tapping outside the cards cancels.
//
// It hands the chosen skill back through the Show() callback, or null if cancelled
// (tapped outside). BattleSystem reopens the command menu when it gets null.
//
// Where to put it (same idea as ParrySystem):
//   Put SkillPanel on an always-active object (the BattleSystem GO or the Canvas), and
//   assign 'panelRoot' to the overlay panel it switches on and off.
//
// A good hierarchy for panelRoot:
//   SkillPanelRoot (disabled by default)
//     - Backdrop  (full-screen transparent Image + Button), the tap-outside-to-close bit
//     - HeaderText (TMP, "PILIH SKILL")
//     - EmptyLabel (TMP, "Belum ada skill") [optional]
//     - CardsContainer (Grid/Horizontal Layout Group), where the cards spawn
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

    private readonly List<SkillCard> pool = new List<SkillCard>();
    private Action<SkillData> onComplete;

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // Opens the picker for one character. 'category' picks the header and which resource
    // (MP or Special) decides what's affordable. onChosen gets the picked skill, or null
    // if they cancelled.
    public void Show(IReadOnlyList<SkillData> skills, PartyMember user,
                     SkillCategory category, Action<SkillData> onChosen)
    {
        if (panelRoot == null || skillCardPrefab == null || cardsContainer == null)
        {
            Debug.LogError("[SkillPanel] panelRoot / skillCardPrefab / cardsContainer not assigned!");
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

    // --- Internals ---

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
                Debug.LogError("[SkillPanel] skillCardPrefab has no SkillCard component!");
                Destroy(go);
                return;
            }
            go.SetActive(false);
            pool.Add(card);
        }
    }
}
