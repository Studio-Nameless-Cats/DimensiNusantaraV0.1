using UnityEngine;

/// <summary>
/// A runtime instance of a character — tracks current HP and other mutable state.
/// Created from CharacterData at game start or when a character joins the party.
/// </summary>
public class PartyMember
{
    /// <summary>Max value of the per-battle Special gauge (fills as the fight goes).</summary>
    public const int SpecialMax = 100;

    private readonly CharacterData _base;
    private int currentHp;
    private int currentMp;
    private int currentSpecial; // 0..SpecialMax, battle-only — reset each battle

    public PartyMember(CharacterData characterData)
    {
        _base     = characterData;
        currentHp = characterData.MaxHp;
        currentMp = characterData.MaxMp;
    }

    /// <summary>Rebuild a member from saved state — restores exact current HP (clamped). MP starts full.</summary>
    public PartyMember(CharacterData characterData, int savedHp)
        : this(characterData, savedHp, -1) { }

    /// <summary>
    /// Rebuild a member from saved state — restores current HP and MP (both clamped).
    /// Pass savedMp = -1 to restore MP to full (used for legacy v1 saves with no MP field).
    /// </summary>
    public PartyMember(CharacterData characterData, int savedHp, int savedMp)
    {
        _base     = characterData;
        currentHp = Mathf.Clamp(savedHp, 0, characterData.MaxHp);
        currentMp = savedMp < 0 ? characterData.MaxMp : Mathf.Clamp(savedMp, 0, characterData.MaxMp);
    }

    // ── Read-only references to base stats ──────────────────────────────────
    public CharacterData Base   => _base;
    public string Name          => _base.Name;
    public int MaxHp            => _base.MaxHp;
    public int MaxMp            => _base.MaxMp;
    public int Attack           => _base.Attack;
    public int Defense          => _base.Defense;
    public int Speed            => _base.Speed;

    // ── Mutable state ────────────────────────────────────────────────────────
    public int CurrentHp => currentHp;
    public int CurrentMp => currentMp;
    public int CurrentSpecial => currentSpecial;
    public bool IsFainted => currentHp <= 0;

    // ── Resources (MP + Special gauge) ────────────────────────────────────────

    public bool CanAffordMp(int cost)      => currentMp >= cost;
    public bool CanAffordSpecial(int cost) => currentSpecial >= cost;

    /// <summary>Spend MP if affordable. Returns false (and spends nothing) if too poor.</summary>
    public bool SpendMp(int cost)
    {
        if (cost <= 0) return true;
        if (currentMp < cost) return false;
        currentMp -= cost;
        return true;
    }

    /// <summary>Spend Special-gauge points if affordable. Returns false if not charged enough.</summary>
    public bool SpendSpecial(int cost)
    {
        if (cost <= 0) return true;
        if (currentSpecial < cost) return false;
        currentSpecial -= cost;
        return true;
    }

    /// <summary>Add to the Special gauge (clamped to SpecialMax). Call as the battle progresses.</summary>
    public void AddSpecial(int amount)
    {
        currentSpecial = Mathf.Clamp(currentSpecial + Mathf.Abs(amount), 0, SpecialMax);
    }

    /// <summary>Reset the Special gauge to empty — call at the start of each battle.</summary>
    public void ResetSpecial() => currentSpecial = 0;

    public void RestoreMp(int amount)
    {
        currentMp = Mathf.Min(MaxMp, currentMp + Mathf.Abs(amount));
    }

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
        currentMp = MaxMp; // rest restores MP too
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(MaxHp, currentHp + Mathf.Abs(amount));
    }
}
