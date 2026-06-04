using UnityEngine;

/// <summary>
/// Represents one fighter on the battle field.
/// Each BattleUnit wraps a PartyMember (its data/stats) and controls
/// the visual model + HUD for that fighter.
///
/// Setup:
///   - Place BattleUnit prefabs at the spawn point positions in the Battle scene.
///   - Assign the BattleHud reference (a Canvas child near the unit).
///   - The Animator on the model must have triggers: Attack, Hit, Faint, Parry.
/// </summary>
public class BattleUnit : MonoBehaviour
{
    // ⚠️ Do NOT set this in the Inspector — it is assigned at runtime by BattleSystem.
    // Both player and enemy units use the same prefab, so a hardcoded Inspector value
    // would make every unit the same team.
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

    // ── Animator parameter hashes ─────────────────────────────────────────────
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash    = Animator.StringToHash("Hit");
    private static readonly int FaintHash  = Animator.StringToHash("Faint");
    private static readonly int ParryHash  = Animator.StringToHash("Parry");

    // ── State ─────────────────────────────────────────────────────────────────
    private PartyMember member;
    private Animator    animator;

    // ── Properties ────────────────────────────────────────────────────────────
    public bool        IsPlayerUnit => isPlayerUnit;
    public PartyMember Member       => member;
    public BattleHud   Hud          => hud;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

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

    // The animator assigns the real sprite a frame after Setup(), so the sprite's
    // true bounds (which depend on its Pixels-Per-Unit) aren't known until LateUpdate.
    // Park the HUD just above the sprite's top edge here, where bounds are valid.
    void LateUpdate()
    {
        if (continuousHudAlign)      AlignHudToSpriteTop();
        else if (!hudAligned)      { AlignHudToSpriteTop(); hudAligned = true; }
    }

    /// <summary>
    /// Moves the world-space HUD so it sits a fixed gap above the TOP of the sprite,
    /// regardless of the sprite's size / Pixels-Per-Unit. Only the Y is changed —
    /// the HUD keeps its horizontal offset.
    /// </summary>
    private void AlignHudToSpriteTop()
    {
        if (hud == null || spriteRenderer == null || spriteRenderer.sprite == null) return;

        Vector3 pos = hud.transform.position;
        pos.y = spriteRenderer.bounds.max.y + hudGap;
        hud.transform.position = pos;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises this unit with a PartyMember. Called by BattleSystem at spawn time.
    /// </summary>
    /// <summary>
    /// Initialises this unit. isPlayer = true for party members, false for enemies.
    /// Always set by BattleSystem — never rely on the Inspector for this.
    /// </summary>
    public void Setup(PartyMember partyMember, bool isPlayer)
    {
        isPlayerUnit = isPlayer;
        member       = partyMember;

        // Single left-facing battle clip set (same model as the overworld).
        // Friendly units sit on the left and face RIGHT → mirror the left clip (flipX=true).
        // Enemies sit on the right and face LEFT → use the base clip un-flipped (flipX=false).
        // Battle units don't move directionally, so this one-time set at spawn is enough.
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
            hud.SetData(member);
        else
            Debug.LogWarning($"[BattleUnit] '{partyMember.Name}' has no BattleHud assigned — HP bar will not show.");
    }

    // ── Animations ────────────────────────────────────────────────────────────

    public void PlayAttackAnimation() => animator?.SetTrigger(AttackHash);
    public void PlayHitAnimation()    => animator?.SetTrigger(HitHash);
    public void PlayFaintAnimation()  => animator?.SetTrigger(FaintHash);
    /// <summary>Deflect-and-riposte clip, played when the player successfully parries and strikes back.</summary>
    public void PlayParryAnimation()  => animator?.SetTrigger(ParryHash);

    // ── HUD ───────────────────────────────────────────────────────────────────

    /// <summary>Refreshes the HP bar after the member's HP changes.</summary>
    public void UpdateHud() => hud?.UpdateHP(member);

    /// <summary>Refreshes only the MP bar + Special gauge (e.g. after charging the
    /// Special on a basic attack, or spending MP on a skill — no HP change).</summary>
    public void RefreshResources() => hud?.UpdateResources(member);

    /// <summary>Refreshes the status-effect badges after a status is applied or expires.</summary>
    public void RefreshStatusIcons() => hud?.ShowStatuses(member.Statuses);

    // ── Visibility ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a fallen unit from the battlefield view: hides its HUD (name + HP bar)
    /// and its model/sprite, so the player sees no lingering corpse. The underlying
    /// PartyMember data is left intact, so win/lose and turn-skip checks still work.
    /// Called by BattleSystem after the faint animation has played.
    /// </summary>
    public void Hide()
    {
        if (hud != null) hud.gameObject.SetActive(false);  // explicit: HUD may be a separate canvas
        gameObject.SetActive(false);
    }
}
