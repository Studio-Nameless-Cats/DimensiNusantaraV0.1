using UnityEngine;
using TMPro;
using Nusantara.UI.Motion;

// The little "press E" chip that pops up at the bottom of the overworld when you're
// standing next to something you can poke. One of these lives in the overworld HUD;
// interactables just call InteractionPrompt.Instance.Show("BICARA") when the player's
// close and Hide() when they walk off.
//
// The pop in/out is handled by a UIAnimator - drop one on this object (or a child)
// and tune the slide there. No animator wired? We just SetActive on/off, no slide.
//
// The verb is whatever the interactable wants on the chip: BICARA for talking to an
// NPC, PERIKSA for examining a campfire / object, and so on.
//
// Reskin notes (scene, per UI_REWORK_PLAN section 2): sheared chip, gold key square
// holding "E" (OnField letter), verb in italic OnDark beside it, anchored bottom-center.
public class InteractionPrompt : MonoBehaviour
{
    // Interactables grab this without needing a scene reference. There should only be
    // one prompt per scene; last one to wake up wins.
    public static InteractionPrompt Instance { get; private set; }

    [Header("Refs")]
    [Tooltip("The label that shows the verb (PERIKSA / BICARA). Italic TMP per the house style.")]
    [SerializeField] private TMP_Text verbLabel;
    [Tooltip("Pops the chip in and out. Leave blank to just SetActive on/off with no slide.")]
    [SerializeField] private UIAnimator animator;
    [Tooltip("The chip root we show/hide when there's no animator. Defaults to this GameObject if blank.")]
    [SerializeField] private GameObject chipRoot;

    [Header("Defaults")]
    [Tooltip("Shown when an interactable asks for the prompt but doesn't pass its own verb.")]
    [SerializeField] private string defaultVerb = "PERIKSA";

    // Who asked for the prompt last. We track it so a second interactable taking over
    // doesn't get its prompt yanked when the FIRST one (that we just walked away from)
    // tells us to hide.
    private object _owner;
    private bool _shown;

    void Awake()
    {
        Instance = this;
        if (chipRoot == null) chipRoot = (animator != null) ? animator.gameObject : gameObject;
        HideInstant();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Pop the chip up with the given verb. Pass an owner (usually 'this' from the
    // caller) so we only hide when that same owner says so - stops an NPC you just
    // left from hiding the prompt for a campfire you just reached. Calling Show again
    // with a new verb while already up just swaps the text, no re-pop.
    public void Show(string verb, object owner = null)
    {
        _owner = owner;
        if (verbLabel != null) verbLabel.text = string.IsNullOrEmpty(verb) ? defaultVerb : verb;

        if (_shown) return;
        _shown = true;

        if (animator != null) animator.Show();
        else if (chipRoot != null) chipRoot.SetActive(true);
    }

    // Hide the chip. If you passed an owner to Show, pass the same one here - a Hide
    // from anyone else is ignored so two nearby interactables don't fight over it.
    public void Hide(object owner = null)
    {
        // someone else owns the prompt right now - not yours to hide.
        if (owner != null && _owner != null && !ReferenceEquals(owner, _owner)) return;

        _owner = null;
        if (!_shown) return;
        _shown = false;

        if (animator != null) animator.Hide();
        else if (chipRoot != null) chipRoot.SetActive(false);
    }

    // Snap hidden with no slide - used on Awake so the chip starts out of sight.
    private void HideInstant()
    {
        _shown = false;
        _owner = null;
        if (chipRoot != null) chipRoot.SetActive(false);
    }
}
