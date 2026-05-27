using UnityEngine;

/// <summary>
/// ScriptableObject that stores all base data for a character (player or enemy).
/// Create via: Right-click in Project → RPG → Character Data
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "RPG/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string characterName;
    [SerializeField] private Sprite icon;

    [Header("Base Stats")]
    [SerializeField] private int maxHp = 50;
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int speed = 10;

    [Header("Overworld Visuals")]
    [SerializeField] private RuntimeAnimatorController overworldAnimator;

    [Header("Battle Visuals")]
    [SerializeField] private RuntimeAnimatorController battleAnimator;
    [SerializeField] private Sprite battleSprite; // Static fallback if no animator

    // ── Properties ──────────────────────────────────────────────────────────
    public string Name                                  => characterName;
    public Sprite Icon                                  => icon;
    public int MaxHp                                    => maxHp;
    public int Attack                                   => attack;
    public int Defense                                  => defense;
    public int Speed                                    => speed;
    public RuntimeAnimatorController OverworldAnimator  => overworldAnimator;
    public RuntimeAnimatorController BattleAnimator     => battleAnimator;
    public Sprite BattleSprite                          => battleSprite;
}
