using UnityEngine;

/// <summary>Which resource a skill draws from / which command button surfaces it.</summary>
public enum SkillCategory { Normal, Special }

/// <summary>What a skill does when used.</summary>
public enum SkillEffectType { Damage, Heal, ApplyStatus }

/// <summary>Who a skill applies to.</summary>
public enum SkillTarget { SingleEnemy, AllEnemies, Self, SingleAlly, AllAllies }

/// <summary>
/// ScriptableObject describing one battle skill.
///
/// Two flavours, set by <see cref="category"/>:
///   - Normal  → surfaced by the SKILL button, costs MP.
///   - Special → surfaced by the SPECIAL SKILL button, costs the Special gauge
///               (which fills as the battle goes).
///
/// Create via: Right-click in Project → RPG → Skill Data.
/// Assign skills to a character on its CharacterData (Skills / Special Skills lists).
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "RPG/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id used by the loadout/save system to persist which skills a member has equipped. " +
             "Auto-assigned in the editor — do NOT change it once a build ships, or old saves can't resolve equipped skills.")]
    [SerializeField] private string id;
    [SerializeField] private string skillName = "New Skill";
    [SerializeField] private Sprite icon;
    [TextArea] [SerializeField] private string description;

    [Header("Classification")]
    [Tooltip("Normal skills cost MP and appear under the SKILL button. Special skills cost the Special gauge and appear under the SPECIAL SKILL button.")]
    [SerializeField] private SkillCategory category = SkillCategory.Normal;
    [Tooltip("Resource cost. MP for Normal skills, Special-gauge points for Special skills.")]
    [SerializeField] private int cost = 5;
    [Tooltip("Character level required before this skill can be used/equipped. 1 = available from the start.")]
    [SerializeField] private int unlockLevel = 1;

    [Header("Effect")]
    [SerializeField] private SkillEffectType effectType = SkillEffectType.Damage;
    [SerializeField] private SkillTarget     target     = SkillTarget.SingleEnemy;
    [Tooltip("DAMAGE only: multiplier on the user's Attack (uses the same formula as a basic attack). 1 = normal, 2 = double.")]
    [SerializeField] private float damageMultiplier = 1.5f;
    [Tooltip("HEAL only: flat HP restored to the target(s).")]
    [SerializeField] private int healAmount = 20;
    [Tooltip("Status applied to the target(s). REQUIRED when Effect Type = Apply Status. " +
             "Also acts as an optional RIDER on Damage/Heal skills (e.g. an attack that also poisons) — leave null for none. " +
             "Make sure the skill's Target matches the status: debuffs → enemies, buffs → allies/self.")]
    [SerializeField] private StatusEffectData statusEffect;

    // ── Properties ──────────────────────────────────────────────────────────
    public string          Id               => id;
    public string          Name             => skillName;
    public Sprite          Icon             => icon;
    public string          Description      => description;
    public SkillCategory   Category         => category;
    public int             Cost             => cost;
    public int             UnlockLevel      => Mathf.Max(1, unlockLevel);
    public SkillEffectType   EffectType       => effectType;
    public SkillTarget       Target           => target;
    public float             DamageMultiplier => damageMultiplier;
    public int               HealAmount       => healAmount;
    public StatusEffectData  StatusEffect     => statusEffect;

    /// <summary>True if this skill applies a status — either as its primary effect or as a rider on a Damage/Heal skill.</summary>
    public bool AppliesStatus => statusEffect != null
                                 && (effectType == SkillEffectType.ApplyStatus
                                     || effectType == SkillEffectType.Damage
                                     || effectType == SkillEffectType.Heal);

    /// <summary>True if this skill aims at an enemy (vs an ally/self), used to pick the target list.</summary>
    public bool TargetsEnemies => target == SkillTarget.SingleEnemy || target == SkillTarget.AllEnemies;

    /// <summary>True if this skill hits everyone on its side (no per-target picker needed).</summary>
    public bool TargetsAll => target == SkillTarget.AllEnemies || target == SkillTarget.AllAllies;

    /// <summary>True if this skill applies only to the user (no picker needed).</summary>
    public bool TargetsSelf => target == SkillTarget.Self;

#if UNITY_EDITOR
    // Auto-assign a stable GUID the first time this asset is created/inspected.
    // Editor-only: ids are baked into the asset and never regenerated at runtime.
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
