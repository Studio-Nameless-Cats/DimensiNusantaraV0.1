using UnityEngine;

// One fighter standing on the battlefield. Each BattleUnit wraps a PartyMember (its
// data and stats) and runs the visual model + HUD for that fighter.
//
// Setup:
//   - Drop BattleUnit prefabs at the spawn points in the Battle scene.
//   - Assign the BattleHud reference (a Canvas child sitting near the unit).
//   - The Animator on the model needs these triggers: Attack, Hit, Faint, Parry.
public class BattleUnit : MonoBehaviour
{
    // Heads up: don't set this in the Inspector. BattleSystem sets it at runtime.
    // Player and enemy units share the same prefab, so a hardcoded value here would
    // put everyone on the same team.
    private bool isPlayerUnit;

    [Header("References")]
    [SerializeField] private BattleHud hud;
    [Tooltip("Sprite for this unit's model. Auto-found in children if empty. Flipped so friendly units face right, enemies face left.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("HUD Positioning")]
    [Tooltip("World-space gap between the TOP of the sprite and the HUD. Increase to push the name/HP bar higher above the head.")]
    [SerializeField] private float hudGap = 0.25f;
    [Tooltip("If ON, the HUD re-aligns every frame (follows the sprite as the animation bobs). If OFF, it aligns once after spawn — usually what you want.")]
    [SerializeField] private bool continuousHudAlign = false;

    // True once the HUD has been parked above the sprite (used when continuousHudAlign is OFF).
    private bool hudAligned;

    // Pre-hashed animator trigger names (cheaper than passing strings every time).
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash    = Animator.StringToHash("Hit");
    private static readonly int FaintHash  = Animator.StringToHash("Faint");
    private static readonly int ParryHash  = Animator.StringToHash("Parry");

    private PartyMember member;
    private Animator    animator;

    public bool        IsPlayerUnit => isPlayerUnit;
    public PartyMember Member       => member;
    public BattleHud   Hud          => hud;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            Debug.LogWarning($"[BattleUnit] No Animator found in children of '{gameObject.name}'. Animations will be skipped. Add an Animator to your model child.");

        if (hud == null)
            Debug.LogWarning($"[BattleUnit] BattleHud is NOT assigned on '{gameObject.name}'. HP bar will not display. Assign the BattleHud reference in the prefab Inspector.");
    }

    // The animator swaps in the real sprite a frame after Setup(), so we don't actually
    // know the sprite's true size (it depends on Pixels-Per-Unit) until LateUpdate. So we
    // park the HUD just above the sprite's top edge here, where the bounds are valid.
    void LateUpdate()
    {
        if (continuousHudAlign)      AlignHudToSpriteTop();
        else if (!hudAligned)      { AlignHudToSpriteTop(); hudAligned = true; }
    }

    // Nudges the world-space HUD so it floats a fixed gap above the top of the sprite,
    // no matter how big the sprite is. Only touches the Y; the HUD keeps its sideways offset.
    private void AlignHudToSpriteTop()
    {
        if (hud == null || spriteRenderer == null || spriteRenderer.sprite == null) return;

        Vector3 pos = hud.transform.position;
        pos.y = spriteRenderer.bounds.max.y + hudGap;
        hud.transform.position = pos;
    }

    // --- Setup ---

    // Sets this unit up with its PartyMember. BattleSystem calls this when it spawns us.
    // isPlayer = true for party members, false for enemies. BattleSystem always passes
    // this in; don't ever rely on the Inspector for it.
    public void Setup(PartyMember partyMember, bool isPlayer)
    {
        isPlayerUnit = isPlayer;
        member       = partyMember;

        // We only have one left-facing set of battle clips (same model as the overworld).
        // Friendly units stand on the left and face right, so we mirror the clip (flipX=true).
        // Enemies stand on the right facing left, so they use the clip as-is (flipX=false).
        // Battle units don't walk around, so setting this once at spawn is all we need.
        if (spriteRenderer != null)
            spriteRenderer.flipX = isPlayer;
        Debug.Log($"[BattleUnit] Setting up '{partyMember.Name}' | HP:{partyMember.CurrentHp}/{partyMember.MaxHp} | ATK:{partyMember.Attack} | SPD:{partyMember.Speed} | IsPlayerUnit:{isPlayerUnit}");

        if (partyMember.Base.BattleAnimator != null && animator != null)
        {
            animator.runtimeAnimatorController = partyMember.Base.BattleAnimator;
            Debug.Log($"[BattleUnit] Animator controller set to '{partyMember.Base.BattleAnimator.name}' for '{partyMember.Name}'.");
        }
        else
        {
            if (partyMember.Base.BattleAnimator == null)
                Debug.LogWarning($"[BattleUnit] '{partyMember.Name}' has no BattleAnimator set in their CharacterData SO. No animations will play.");
            if (animator == null)
                Debug.LogWarning($"[BattleUnit] '{partyMember.Name}' has no Animator component in children. No animations will play.");
        }

        if (hud != null)
        {
            hud.SetData(member);
            // Players and enemies share one prefab, so the MP/Special sliders exist on
            // every unit. Show them for the whole team (any isPlayer unit), hide them on
            // enemies. This is per-unit, so EVERY party member gets the meters, not just
            // the main character.
            if (!isPlayer)
                hud.HideResources();
        }
        else
            Debug.LogWarning($"[BattleUnit] '{partyMember.Name}' has no BattleHud assigned, so the HP bar won't show.");
    }

    // --- Animations ---

    public void PlayAttackAnimation() => animator?.SetTrigger(AttackHash);
    public void PlayHitAnimation()    => animator?.SetTrigger(HitHash);
    public void PlayFaintAnimation()  => animator?.SetTrigger(FaintHash);
    // The block-and-hit-back clip, played when the player parries and counters.
    public void PlayParryAnimation()  => animator?.SetTrigger(ParryHash);

    // --- HUD ---

    // Redraws the HP bar after the member's HP changes.
    public void UpdateHud() => hud?.UpdateHP(member);

    // Redraws just the MP bar + Special gauge (e.g. after charging Special on a basic
    // attack or spending MP on a skill, when HP didn't change).
    public void RefreshResources() => hud?.UpdateResources(member);

    // Redraws the status-effect badges after a status gets applied or wears off.
    public void RefreshStatusIcons() => hud?.ShowStatuses(member.Statuses);

    // --- Showing / hiding ---

    // Yanks a fallen unit off the screen: hides its HUD (name + HP bar) and its
    // model/sprite so there's no leftover corpse lying around. The PartyMember data
    // underneath stays intact, so win/lose and turn-skip checks still work fine.
    // BattleSystem calls this after the faint animation finishes.
    public void Hide()
    {
        if (hud != null) hud.gameObject.SetActive(false);  // on purpose: the HUD may live on a separate canvas
        gameObject.SetActive(false);
    }
}
