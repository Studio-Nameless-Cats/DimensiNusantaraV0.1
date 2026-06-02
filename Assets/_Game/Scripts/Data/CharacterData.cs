using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that stores all base data for a character (player or enemy).
/// Create via: Right-click in Project → RPG → Character Data
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "RPG/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id used by the save system to reconstruct this character on load. " +
             "Auto-assigned in the editor — do NOT change it once a build ships, or old saves can't find this character.")]
    [SerializeField] private string id;

    [Header("Basic Info")]
    [SerializeField] private string characterName;
    [SerializeField] private Sprite icon;

    [Header("Base Stats")]
    [SerializeField] private int maxHp = 50;
    [SerializeField] private int maxMp = 20;
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int speed = 10;

    [Header("Skills")]
    [Tooltip("Normal skills (cost MP) — shown under the SKILL command button.")]
    [SerializeField] private List<SkillData> skills = new List<SkillData>();
    [Tooltip("Special skills (cost the Special gauge) — shown under the SPECIAL SKILL command button.")]
    [SerializeField] private List<SkillData> specialSkills = new List<SkillData>();

    [Header("Overworld Visuals")]
    [SerializeField] private RuntimeAnimatorController overworldAnimator;

    [Header("Battle Visuals")]
    [SerializeField] private RuntimeAnimatorController battleAnimator;
    [SerializeField] private Sprite battleSprite; // Static fallback if no animator

    // ── Properties ──────────────────────────────────────────────────────────
    public string Id                                    => id;
    public string Name                                  => characterName;
    public Sprite Icon                                  => icon;
    public int MaxHp                                    => maxHp;
    public int MaxMp                                    => maxMp;
    public IReadOnlyList<SkillData> Skills              => skills;
    public IReadOnlyList<SkillData> SpecialSkills       => specialSkills;
    public int Attack                                   => attack;
    public int Defense                                  => defense;
    public int Speed                                    => speed;
    public RuntimeAnimatorController OverworldAnimator  => overworldAnimator;
    public RuntimeAnimatorController BattleAnimator     => battleAnimator;
    public Sprite BattleSprite                          => battleSprite;

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
