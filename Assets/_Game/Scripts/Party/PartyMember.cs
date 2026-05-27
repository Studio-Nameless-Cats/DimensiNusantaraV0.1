using UnityEngine;

/// <summary>
/// A runtime instance of a character — tracks current HP and other mutable state.
/// Created from CharacterData at game start or when a character joins the party.
/// </summary>
public class PartyMember
{
    private readonly CharacterData _base;
    private int currentHp;

    public PartyMember(CharacterData characterData)
    {
        _base     = characterData;
        currentHp = characterData.MaxHp;
    }

    // ── Read-only references to base stats ──────────────────────────────────
    public CharacterData Base   => _base;
    public string Name          => _base.Name;
    public int MaxHp            => _base.MaxHp;
    public int Attack           => _base.Attack;
    public int Defense          => _base.Defense;
    public int Speed            => _base.Speed;

    // ── Mutable state ────────────────────────────────────────────────────────
    public int CurrentHp => currentHp;
    public bool IsFainted => currentHp <= 0;

    // ── Combat ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates and applies damage from an attacker.
    /// Returns the final damage dealt.
    /// Formula: damage = (attacker.Attack * 2) / max(1, this.Defense) * multiplier
    /// Pass multiplier = 2f for a critical hit.
    /// </summary>
    public int TakeDamage(int attackerAttack, float multiplier = 1f)
    {
        float raw    = (attackerAttack * 2f) / Mathf.Max(1f, Defense);
        int   damage = Mathf.Max(1, Mathf.RoundToInt(raw * multiplier));

        currentHp = Mathf.Max(0, currentHp - damage);
        return damage;
    }

    // ── Healing ──────────────────────────────────────────────────────────────

    public void HealFull()
    {
        currentHp = MaxHp;
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(MaxHp, currentHp + Mathf.Abs(amount));
    }
}
