using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A runtime instance of a character — tracks current HP and other mutable state.
/// Created from CharacterData at game start or when a character joins the party.
/// </summary>
public class PartyMember
{
    /// <summary>Max value of the per-battle Special gauge (fills as the fight goes).</summary>
    public const int SpecialMax = 100;

    /// <summary>How many NORMAL skills a member may bring into battle at once (loadout size).</summary>
    public const int MaxEquippedSkills = 4;

    private readonly CharacterData _base;
    private int currentHp;
    private int currentMp;
    private int currentSpecial; // 0..SpecialMax, battle-only — reset each battle
    private int level;          // 1..LevelCurve.MaxLevel
    private int currentExp;     // EXP accumulated TOWARD the next level (0..ExpToNextLevel)

    // Runtime "equipped loadout": which NORMAL skills (a subset of Base.Skills, max
    // MaxEquippedSkills) the SKILL button surfaces. Special skills stay fixed per
    // character, so they're NOT part of the loadout. Persisted via skill ids.
    private readonly List<SkillData> equippedSkills = new List<SkillData>();

    // Active status effects (buffs/debuffs). Battle-only: cleared at battle start
    // (same lifecycle as the Special gauge) and NOT serialized.
    private readonly List<StatusEffectInstance> statuses = new List<StatusEffectInstance>();

    /// <summary>Whether this member is selected to fight (vs sit in reserve). Default true.</summary>
    public bool IsActiveInBattle { get; set; } = true;

    public PartyMember(CharacterData characterData)
    {
        _base      = characterData;
        level      = Mathf.Clamp(characterData.StartingLevel, 1, LevelCurve.MaxLevel);
        currentExp = 0;
        currentHp  = MaxHp;   // MaxHp is level-scaled — level must be set first
        currentMp  = MaxMp;
        InitDefaultLoadout();
    }

    /// <summary>Rebuild a member from saved state — restores exact current HP (clamped). MP starts full.</summary>
    public PartyMember(CharacterData characterData, int savedHp)
        : this(characterData, savedHp, -1) { }

    /// <summary>
    /// Rebuild a member from saved state — restores current HP and MP (both clamped).
    /// Pass savedMp = -1 to restore MP to full (used for legacy v1 saves with no MP field).
    /// Level/EXP fall back to the character's starting level (legacy saves with no level).
    /// </summary>
    public PartyMember(CharacterData characterData, int savedHp, int savedMp)
        : this(characterData, savedHp, savedMp, 0, 0) { }

    /// <summary>
    /// Rebuild a member from saved state — restores HP, MP, level and EXP (all clamped).
    /// Pass savedMp = -1 to restore MP to full; pass savedLevel &lt;= 0 to fall back to the
    /// character's StartingLevel (legacy saves with no level field). Equipped loadout
    /// defaults to the first MaxEquippedSkills of the (unlocked) pool; call
    /// <see cref="RestoreLoadout"/> afterwards to apply a saved loadout.
    /// </summary>
    public PartyMember(CharacterData characterData, int savedHp, int savedMp, int savedLevel, int savedExp)
    {
        _base      = characterData;
        level      = savedLevel > 0
                       ? Mathf.Clamp(savedLevel, 1, LevelCurve.MaxLevel)
                       : Mathf.Clamp(characterData.StartingLevel, 1, LevelCurve.MaxLevel);
        currentExp = Mathf.Max(0, savedExp);
        currentHp  = Mathf.Clamp(savedHp, 0, MaxHp);   // MaxHp is level-scaled — level first
        currentMp  = savedMp < 0 ? MaxMp : Mathf.Clamp(savedMp, 0, MaxMp);
        InitDefaultLoadout();
    }

    // ── Stats (level-scaled) ──────────────────────────────────────────────────
    // Level 1 = the CharacterData base values; each level beyond adds the growth
    // amounts. Enemies (StartingLevel 1) are unaffected, so existing balance holds.
    private int Bonus(int perLevel) => perLevel * (level - 1);

    public CharacterData Base   => _base;
    public string Name          => _base.Name;
    public int MaxHp            => _base.MaxHp   + Bonus(_base.HpGrowth);   // status effects never change max pools
    public int MaxMp            => _base.MaxMp   + Bonus(_base.MpGrowth);
    // Attack/Defense/Speed are level-scaled AND modulated by active status multipliers
    // (Slow/Haste, Weaken/Rage, Guard/Break). With no statuses the multiplier is 1, so
    // these read exactly as before — fully additive.
    public int Attack           => Mathf.Max(0, Mathf.RoundToInt((_base.Attack  + Bonus(_base.AttackGrowth))  * StatusMult(StatModifier.Attack)));
    public int Defense          => Mathf.Max(1, Mathf.RoundToInt((_base.Defense + Bonus(_base.DefenseGrowth)) * StatusMult(StatModifier.Defense)));
    public int Speed            => Mathf.Max(0, Mathf.RoundToInt((_base.Speed   + Bonus(_base.SpeedGrowth))   * StatusMult(StatModifier.Speed)));

    // ── Level & EXP ───────────────────────────────────────────────────────────
    public int  Level          => level;
    public int  CurrentExp     => currentExp;
    /// <summary>EXP needed to advance from the current level to the next (int.MaxValue at cap).</summary>
    public int  ExpToNextLevel => LevelCurve.ExpToNext(level);
    public bool IsMaxLevel     => level >= LevelCurve.MaxLevel;
    /// <summary>0..1 fill for an EXP bar. Always 1 at the cap.</summary>
    public float ExpNormalized => IsMaxLevel ? 1f : Mathf.Clamp01((float)currentExp / ExpToNextLevel);

    // ── Skills (loadout-aware + level-gated) ──────────────────────────────────
    // Battle reads these, NOT Base.Skills directly: Skills = the equipped subset
    // (only those unlocked at the current level); SpecialSkills = the character's
    // fixed special list, filtered by unlock level.
    public IReadOnlyList<SkillData> Skills        => equippedSkills.Where(IsUnlocked).ToList();
    public IReadOnlyList<SkillData> SpecialSkills => _base.SpecialSkills.Where(IsUnlocked).ToList();

    /// <summary>The pool of NORMAL skills this character can choose to equip, gated by level.</summary>
    public IReadOnlyList<SkillData> SkillPool => _base.Skills.Where(IsUnlocked).ToList();

    /// <summary>True if this member's level meets the skill's unlock requirement.</summary>
    public bool IsUnlocked(SkillData s) => s != null && level >= s.UnlockLevel;

    public int  EquippedCount         => equippedSkills.Count;
    public bool IsEquipped(SkillData s) => s != null && equippedSkills.Contains(s);
    public bool CanEquipMore           => equippedSkills.Count < MaxEquippedSkills;

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

    // ── Experience / leveling ──────────────────────────────────────────────────

    /// <summary>
    /// Grant EXP and resolve any level-ups. Returns the list of NEW levels reached
    /// (empty if none) so the caller can announce "naik ke Level X!". Each level-up
    /// grows the stat pools; the gained HP/MP is added to the current values too (a
    /// small heal), so leveling mid-battle feels rewarding without a full restore.
    /// No-op at the level cap.
    /// </summary>
    public List<int> AddExp(int amount)
    {
        var gained = new List<int>();
        if (amount <= 0 || IsMaxLevel) return gained;

        currentExp += amount;

        while (!IsMaxLevel && currentExp >= ExpToNextLevel)
        {
            currentExp -= ExpToNextLevel;
            level++;                              // MaxHp/MaxMp now reflect the new level
            gained.Add(level);

            // Grow the live pools by this level's gain (clamped to the new maxima).
            currentHp = Mathf.Min(MaxHp, currentHp + _base.HpGrowth);
            currentMp = Mathf.Min(MaxMp, currentMp + _base.MpGrowth);
        }

        if (IsMaxLevel) currentExp = 0;           // no bar past the cap
        return gained;
    }

    // ── Loadout editing ────────────────────────────────────────────────────────

    /// <summary>Default loadout = the first MaxEquippedSkills of the character's pool.</summary>
    private void InitDefaultLoadout()
    {
        equippedSkills.Clear();
        if (_base?.Skills == null) return;
        foreach (var s in _base.Skills)
        {
            if (s == null || !IsUnlocked(s) || equippedSkills.Contains(s)) continue;
            equippedSkills.Add(s);
            if (equippedSkills.Count >= MaxEquippedSkills) break;
        }
    }

    /// <summary>
    /// Equip a normal skill (must belong to this character's pool, and the loadout
    /// must not be full). Returns true if it's now equipped.
    /// </summary>
    public bool Equip(SkillData skill)
    {
        if (skill == null) return false;
        if (equippedSkills.Contains(skill)) return true;
        if (!_base.Skills.Contains(skill)) return false;   // not one of this character's skills
        if (!IsUnlocked(skill)) return false;              // level-gated — not learned yet
        if (equippedSkills.Count >= MaxEquippedSkills) return false;
        equippedSkills.Add(skill);
        return true;
    }

    /// <summary>Remove a skill from the equipped loadout. Returns true if it was equipped.</summary>
    public bool Unequip(SkillData skill) => equippedSkills.Remove(skill);

    /// <summary>
    /// Toggle a skill's equipped state, honoring the cap. Returns the new equipped state.
    /// </summary>
    public bool ToggleEquip(SkillData skill)
    {
        if (skill == null) return false;
        if (equippedSkills.Contains(skill)) { equippedSkills.Remove(skill); return false; }
        return Equip(skill);
    }

    /// <summary>Stable ids of the currently-equipped skills, in order — for saving.</summary>
    public List<string> GetEquippedIds()
        => equippedSkills.Where(s => s != null && !string.IsNullOrEmpty(s.Id))
                         .Select(s => s.Id).ToList();

    /// <summary>
    /// Restore the equipped loadout from saved skill ids, resolved against this
    /// character's own pool. Unknown ids are dropped; a null/empty list (or one that
    /// resolves to nothing) falls back to the default loadout. Honors the cap.
    /// </summary>
    public void RestoreLoadout(List<string> equippedIds)
    {
        if (equippedIds == null || equippedIds.Count == 0) { InitDefaultLoadout(); return; }

        equippedSkills.Clear();
        foreach (var id in equippedIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var match = _base.Skills.FirstOrDefault(s => s != null && s.Id == id);
            if (match != null && !equippedSkills.Contains(match))
            {
                equippedSkills.Add(match);
                if (equippedSkills.Count >= MaxEquippedSkills) break;
            }
        }

        if (equippedSkills.Count == 0) InitDefaultLoadout();   // saved set no longer resolves
    }

    // ── Status effects (battle-only) ────────────────────────────────────────────

    private enum StatModifier { Attack, Defense, Speed }

    /// <summary>Product of every active status's multiplier for one stat (1 if none).</summary>
    private float StatusMult(StatModifier which)
    {
        float m = 1f;
        for (int i = 0; i < statuses.Count; i++)
        {
            var d = statuses[i].Data;
            if (d == null) continue;
            switch (which)
            {
                case StatModifier.Attack:  m *= d.AttackMultiplier;  break;
                case StatModifier.Defense: m *= d.DefenseMultiplier; break;
                case StatModifier.Speed:   m *= d.SpeedMultiplier;   break;
            }
        }
        return m;
    }

    /// <summary>Read-only view of the active status effects (for HUD icons).</summary>
    public IReadOnlyList<StatusEffectInstance> Statuses => statuses;
    public bool HasStatuses => statuses.Count > 0;

    /// <summary>True if any active status prevents this unit from acting (Stun / Freeze).</summary>
    public bool IsStunned => statuses.Any(s => s.Data != null && s.Data.PreventsAction);

    /// <summary>Remove all status effects. Call at battle start (statuses don't persist between fights).</summary>
    public void ClearStatuses() => statuses.Clear();

    /// <summary>
    /// Apply a status to this member. If the same status is already active it is
    /// refreshed to full duration (when the data allows) rather than duplicated.
    /// Returns true if it was NEWLY added (false if it merely refreshed an existing one).
    /// </summary>
    public bool ApplyStatus(StatusEffectData data)
    {
        if (data == null) return false;

        var existing = statuses.FirstOrDefault(s => s.Data == data);
        if (existing != null)
        {
            if (data.RefreshOnReapply) existing.Refresh();
            return false;
        }

        statuses.Add(new StatusEffectInstance(data));
        return true;
    }

    /// <summary>
    /// Resolve start-of-turn status bookkeeping for this member, in order:
    ///   1. apply each status's per-turn HP change (DoT / regen) — clamped to 0..MaxHp;
    ///   2. capture whether the unit is stunned THIS turn (before counting down, so a
    ///      stun spends its final turn);
    ///   3. count every status down by one turn and drop the expired ones.
    /// Returns a <see cref="StatusTurnReport"/> the caller narrates. UI-agnostic.
    /// </summary>
    public StatusTurnReport ProcessTurnStart()
    {
        var report = new StatusTurnReport();
        if (statuses.Count == 0) return report;

        report.WasStunned = IsStunned;

        // 1) per-turn HP tick
        foreach (var s in statuses)
        {
            int delta = s.Data != null ? s.Data.DamagePerTurn : 0;
            if (delta == 0) continue;

            int before = currentHp;
            currentHp = delta > 0
                ? Mathf.Max(0, currentHp - delta)        // DoT
                : Mathf.Min(MaxHp, currentHp - delta);   // regen (delta < 0 → adds)

            int applied = currentHp - before;            // <0 damage, >0 heal
            if (applied != 0) report.Ticks.Add(new StatusTick(s.Data, applied));
        }

        // 2 & 3) count down and expire
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            if (statuses[i].Tick() <= 0)
            {
                report.Expired.Add(statuses[i].Data);
                statuses.RemoveAt(i);
            }
        }

        return report;
    }
}
