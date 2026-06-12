using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Nusantara.UI.Motion;

// The item-picker overlay for the battle ITEM command. Same idea as SkillPanel:
// pops up over the command menu, shows one card per usable consumable in the bag
// (name + effect + how many you have), backdrop tap cancels. It even reuses the
// SkillCard prefab via the generic Bind, so no new card prefab needed.
//
// Hands the chosen ItemData back through the Show() callback, or null if cancelled.
// BattleSystem reopens the command menu when it gets null.
//
// Hierarchy for panelRoot (mirror your SkillPanelRoot):
//   ItemPanelRoot (disabled by default)
//     - Backdrop  (full-screen transparent Image + Button), tap-outside-to-close
//     - HeaderText (TMP, "PILIH ITEM")
//     - EmptyLabel (TMP, "Tas kosong") [optional]
//     - CardsContainer (Layout Group), where the cards spawn
public class ItemPanel : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The overlay panel toggled on/off. Disabled by default.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Full-screen button behind the cards. Clicking it cancels.")]
    [SerializeField] private Button backdropButton;

    [Header("Header / empty")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private string header = "— PILIH ITEM —";
    [Tooltip("Shown when the bag has no usable consumables. Optional.")]
    [SerializeField] private GameObject emptyLabel;

    [Header("Cards")]
    [Tooltip("Prefab with a SkillCard component - the skill picker's prefab works as-is.")]
    [SerializeField] private GameObject itemCardPrefab;
    [Tooltip("Parent (with a Layout Group) the cards spawn under.")]
    [SerializeField] private Transform cardsContainer;

    [Header("Motion (optional)")]
    [Tooltip("Assign to make the cards pop in (cascade) and the picker pop out when chosen/cancelled. Leave null for instant show/hide.")]
    [SerializeField] private MotionProfile motionProfile;

    private readonly List<SkillCard> pool = new List<SkillCard>();
    private Action<ItemData> onComplete;
    private Vector3 _cardsHome = Vector3.one;   // cards container's resting scale

    void Awake()
    {
        if (cardsContainer is RectTransform crt) _cardsHome = crt.localScale;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // Opens the picker over the bag's usable consumables. onChosen gets the picked
    // item, or null if they cancelled (tapped the backdrop).
    public void Show(List<InventorySystem.ItemStack> stacks, Action<ItemData> onChosen)
    {
        if (panelRoot == null || itemCardPrefab == null || cardsContainer == null)
        {
            Debug.LogError("[ItemPanel] panelRoot / itemCardPrefab / cardsContainer not assigned!");
            onChosen?.Invoke(null);
            return;
        }

        onComplete = onChosen;

        if (headerText) headerText.text = header;

        if (backdropButton)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Cancel);
        }

        int count = stacks?.Count ?? 0;
        if (emptyLabel) emptyLabel.SetActive(count == 0);

        EnsurePool(count);
        for (int i = 0; i < pool.Count; i++)
        {
            if (i < count)
            {
                var data    = stacks[i].Data;
                int held    = stacks[i].Count;
                // Corner shows the effect plus how many we have, e.g. "+20 HP  x3".
                string corner = string.IsNullOrEmpty(data.EffectLabel)
                    ? $"x{held}"
                    : $"{data.EffectLabel}  x{held}";

                pool[i].gameObject.SetActive(true);
                pool[i].Bind(data.Name, data.Icon, corner, true, () => Choose(data));
            }
            else
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        panelRoot.SetActive(true);

        // Pop the visible cards in one after another (scale + fade, layout-safe).
        if (motionProfile != null)
        {
            if (cardsContainer is RectTransform crt) { crt.DOKill(); crt.localScale = _cardsHome; }
            var cards = ActiveCardRects(count);
            foreach (var r in cards) { r.DOKill(); r.localScale = Vector3.one; }
            cards.ScaleCascade(motionProfile);
        }
    }

    // --- Internals (same shape as SkillPanel) ---

    private List<RectTransform> ActiveCardRects(int count)
    {
        var list = new List<RectTransform>(count);
        for (int i = 0; i < count && i < pool.Count; i++)
            if (pool[i] != null) list.Add((RectTransform)pool[i].transform);
        return list;
    }

    private void Choose(ItemData item)
    {
        var cb = onComplete;
        onComplete = null;
        Hide();
        cb?.Invoke(item);
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
        if (!panelRoot) return;

        if (motionProfile == null || !(cardsContainer is RectTransform crt))
        {
            panelRoot.SetActive(false);
            return;
        }

        crt.DOKill();
        crt.ScalePopOut(motionProfile, _cardsHome)
           .OnComplete(() =>
           {
               panelRoot.SetActive(false);
               crt.localScale = _cardsHome;
           });
    }

    private void EnsurePool(int needed)
    {
        while (pool.Count < needed)
        {
            var go   = Instantiate(itemCardPrefab, cardsContainer);
            var card = go.GetComponent<SkillCard>();
            if (card == null)
            {
                Debug.LogError("[ItemPanel] itemCardPrefab has no SkillCard component!");
                Destroy(go);
                return;
            }
            go.SetActive(false);
            pool.Add(card);
        }
    }
}
