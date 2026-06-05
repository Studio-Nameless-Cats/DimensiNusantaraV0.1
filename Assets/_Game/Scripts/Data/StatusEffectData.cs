using UnityEngine;

// A ScriptableObject describing ONE status effect (a buff or a debuff) that can sit on a
// battle unit for a few turns. It's fully data-driven: this one asset type covers every
// common status just by mixing a handful of simple behaviours:
//
//   - Poison / Burn (damage over time): damagePerTurn > 0  (lose HP each turn)
//   - Regen:                            damagePerTurn < 0  (heal HP each turn)
//   - Stun / Freeze:                    preventsAction = true (skips the unit's turn)
//   - Slow:                             speedMultiplier < 1 (lower initiative)
//   - Haste:                            speedMultiplier > 1 (higher initiative)
//   - Weaken / Rage:                    attackMultiplier  (scales the damage you deal)
//   - Guard / Break:                    defenseMultiplier (scales the damage you take)
//
// One status can mix several of these (say, a curse that poisons AND weakens). The
// per-turn HP tick happens at the START of the affected unit's turn; the stat multipliers
// just apply the whole time the status is active.
//
// Make one with: Right-click in Project -> RPG -> Status Effect Data.
// Apply it from a skill by setting the skill's Effect Type to "Apply Status" (or by giving
// a Damage/Heal skill a Status Effect rider).
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
    [Tooltip("Multiplier on the unit's Speed, which affects turn-order initiative (re-sorted each round) and run chance. <1 = Slow, >1 = Haste, 1 = no change.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Stacking")]
    [Tooltip("If true, re-applying this status to a unit that already has it refreshes the duration back to full instead of being ignored.")]
    [SerializeField] private bool refreshOnReapply = true;

    public string Name              => statusName;
    public Sprite Icon              => icon;
    public string Description       => description;
    public bool   IsBuff            => isBuff;
    public Color  Tint              => tint;
    public int    Duration          => Mathf.Max(1, duration);
    public int    DamagePerTurn     => damagePerTurn;
    public bool   PreventsAction    => preventsAction;
    public float  AttackMultiplier  => Mathf.Max(0f, attackMultiplier);
    public float  DefenseMultiplier => Mathf.Max(0.01f, defenseMultiplier); // keep it off 0 so the damage formula never divides by zero
    public float  SpeedMultiplier   => Mathf.Max(0f, speedMultiplier);
    public bool   RefreshOnReapply  => refreshOnReapply;

    // True if this status messes with HP every turn (poison, burn, or regen).
    public bool HasPerTurnTick => damagePerTurn != 0;

#if UNITY_EDITOR
    // Give this asset a stable GUID the first time it's made or inspected (editor-only).
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
