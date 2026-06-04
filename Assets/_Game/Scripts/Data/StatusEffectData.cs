using UnityEngine;

/// <summary>
/// ScriptableObject describing ONE status effect (buff or debuff) that can sit on a
/// battle unit for a number of turns. Fully data-driven — one asset type models
/// every common status by combining a few simple behaviours:
///
///   • Poison / Burn (DoT) → <see cref="damagePerTurn"/> &gt; 0  (HP lost each turn)
///   • Regen             → <see cref="damagePerTurn"/> &lt; 0  (HP healed each turn)
///   • Stun / Freeze     → <see cref="preventsAction"/> = true (skips the unit's turn)
///   • Slow              → <see cref="speedMultiplier"/> &lt; 1 (drops initiative)
///   • Haste             → <see cref="speedMultiplier"/> &gt; 1 (raises initiative)
///   • Weaken / Rage     → <see cref="attackMultiplier"/>  (scales outgoing damage)
///   • Guard / Break     → <see cref="defenseMultiplier"/> (scales incoming damage)
///
/// A single status may combine several of these (e.g. a curse that both poisons and
/// weakens). The per-turn HP tick happens at the START of the affected unit's turn;
/// the stat multipliers apply continuously while the status is active.
///
/// Create via: Right-click in Project → RPG → Status Effect Data.
/// Apply one from a skill by setting the skill's Effect Type to "Apply Status" (or by
/// assigning a Status Effect rider on a Damage/Heal skill).
/// </summary>
[CreateAssetMenu(fileName = "New Status", menuName = "RPG/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id (auto-assigned in the editor). Reserved for future save persistence of mid-battle statuses; harmless to leave alone.")]
    [SerializeField] private string id;
    [SerializeField] private string statusName = "New Status";
    [SerializeField] private Sprite icon;
    [TextArea] [SerializeField] private string description;
    [Tooltip("True = a beneficial buff (tinted positively in UI). False = a harmful debuff.")]
    [SerializeField] private bool isBuff = false;
    [Tooltip("Optional tint for the status icon (e.g. green poison, blue freeze). Used by the optional HUD icon display.")]
    [SerializeField] private Color tint = Color.white;

    [Header("Duration")]
    [Tooltip("How many of the affected unit's turns this status lasts. It ticks (and counts down) at the start of each of that unit's turns.")]
    [Min(1)] [SerializeField] private int duration = 3;

    [Header("Per-turn HP change")]
    [Tooltip("HP changed at the START of the affected unit's turn. POSITIVE = damage over time (poison/burn). NEGATIVE = heal over time (regen). 0 = none.")]
    [SerializeField] private int damagePerTurn = 0;

    [Header("Action")]
    [Tooltip("If true, the affected unit cannot act on its turn (Stun / Freeze).")]
    [SerializeField] private bool preventsAction = false;

    [Header("Stat multipliers (applied while active)")]
    [Tooltip("Multiplier on the unit's Attack. <1 weakens, >1 enrages, 1 = no change.")]
    [SerializeField] private float attackMultiplier = 1f;
    [Tooltip("Multiplier on the unit's Defense. <1 = takes more damage (armor break), >1 = takes less (guard), 1 = no change.")]
    [SerializeField] private float defenseMultiplier = 1f;
    [Tooltip("Multiplier on the unit's Speed → affects turn-order initiative (re-sorted each round) and run chance. <1 = Slow, >1 = Haste, 1 = no change.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Stacking")]
    [Tooltip("If true, re-applying this status to a unit that already has it refreshes the duration back to full instead of being ignored.")]
    [SerializeField] private bool refreshOnReapply = true;

    // ── Properties ──────────────────────────────────────────────────────────
    public string Name              => statusName;
    public Sprite Icon              => icon;
    public string Description       => description;
    public bool   IsBuff            => isBuff;
    public Color  Tint              => tint;
    public int    Duration          => Mathf.Max(1, duration);
    public int    DamagePerTurn     => damagePerTurn;
    public bool   PreventsAction    => preventsAction;
    public float  AttackMultiplier  => Mathf.Max(0f, attackMultiplier);
    public float  DefenseMultiplier => Mathf.Max(0.01f, defenseMultiplier); // never 0 → avoids divide-by-zero in the damage formula
    public float  SpeedMultiplier   => Mathf.Max(0f, speedMultiplier);
    public bool   RefreshOnReapply  => refreshOnReapply;

    /// <summary>True if this status changes HP each turn (poison, burn, or regen).</summary>
    public bool HasPerTurnTick => damagePerTurn != 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
