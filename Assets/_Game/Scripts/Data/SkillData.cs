using UnityEngine;

// Which resource a skill spends, and which command button it shows up under.
public enum SkillCategory { Normal, Special }

// What a skill actually does when you use it.
public enum SkillEffectType { Damage, Heal, ApplyStatus }

// Who a skill hits.
public enum SkillTarget { SingleEnemy, AllEnemies, Self, SingleAlly, AllAllies }

// A ScriptableObject describing one battle skill.
//
// There are two flavours, picked by 'category':
//   - Normal  -> shows up under the SKILL button, costs MP.
//   - Special -> shows up under the SPECIAL SKILL button, costs the Special gauge
//                (which fills up as the fight goes on).
//
// Make one with: Right-click in Project -> RPG -> Skill Data.
// Then add skills to a character on its CharacterData (the Skills / Special Skills lists).
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
    [Tooltip("Status slapped on the target(s). REQUIRED when Effect Type = Apply Status. " +
             "Can also ride along on a Damage/Heal skill (like an attack that also poisons); leave it null if you don't want that. " +
             "Make sure the skill's Target matches the status: debuffs go on enemies, buffs go on allies/self.")]
    [SerializeField] private StatusEffectData statusEffect;

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

    // True if this skill puts a status on the target, whether that's the main effect or
    // just a rider on a Damage/Heal skill.
    public bool AppliesStatus => statusEffect != null
                                 && (effectType == SkillEffectType.ApplyStatus
                                     || effectType == SkillEffectType.Damage
                                     || effectType == SkillEffectType.Heal);

    // True if this skill is aimed at enemies (not allies/self). Used to pick the target list.
    public bool TargetsEnemies => target == SkillTarget.SingleEnemy || target == SkillTarget.AllEnemies;

    // True if this skill hits everyone on its side, so no per-target picker is needed.
    public bool TargetsAll => target == SkillTarget.AllEnemies || target == SkillTarget.AllAllies;

    // True if this skill only ever hits the user, so no picker is needed.
    public bool TargetsSelf => target == SkillTarget.Self;

#if UNITY_EDITOR
    // Hand this asset a stable GUID the first time it's made or inspected. Editor-only:
    // the id gets baked in and never gets regenerated at runtime.
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
