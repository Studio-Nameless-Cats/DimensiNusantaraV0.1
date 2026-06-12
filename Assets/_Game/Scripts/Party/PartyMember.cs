using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The live, in-game version of a character. Tracks current HP and all the other stuff
// that changes during play. We build one from a CharacterData at game start, or when a
// character joins the party.
public class PartyMember
{
    // The top of the per-battle Special gauge (it fills up as the fight goes on).
    public const int SpecialMax = 100;

    // How many NORMAL skills a member can bring into battle at once (the loadout size).
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

    // Active status effects (buffs/debuffs). Battle-only: wiped at battle start (same
    // lifecycle as the Special gauge) and never saved.
    private readonly List<StatusEffectInstance> statuses = new List<StatusEffectInstance>();

    // Whether this member is picked to fight or sit on the bench. Defaults to true.
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

    // Rebuild a member from a save: restores their exact current HP (clamped). MP comes back full.
    public PartyMember(CharacterData characterData, int savedHp)
        : this(characterData, savedHp, -1) { }

    // Rebuild a member from a save: restores current HP and MP (both clamped). Pass
    // savedMp = -1 to fill MP up to full (that's what old v1 saves with no MP field do).
    // Level/EXP fall back to the character's starting level (for old saves with no level).
    public PartyMember(CharacterData characterData, int savedHp, int savedMp)
        : this(characterData, savedHp, savedMp, 0, 0) { }

    // Rebuild a member from a save: restores HP, MP, level and EXP (all clamped). Pass
    // savedMp = -1 to fill MP to full; pass savedLevel <= 0 to fall back to the character's
    // StartingLevel (for old saves with no level field). The equipped loadout starts as the
    // first MaxEquippedSkills of the unlocked pool; call RestoreLoadout afterwards to apply a saved one.
    public PartyMember(CharacterData characterData, int savedHp, int savedMp, int savedLevel, int savedExp)
    {
        _base      = characterData;
        level      = savedLevel > 0
                       ? Mathf.Clamp(savedLevel, 1, LevelCurve.MaxLevel)
                       : Mathf.Clamp(characterData.StartingLevel, 1, LevelCurve.MaxLevel);
        currentExp = Mathf.Max(0, savedExp);
        currentHp  = Mathf.Clamp(savedHp, 0, MaxHp);   // MaxHp is level-scaled, so set level first
        currentMp  = savedMp < 0 ? MaxMp : Mathf.Clamp(savedMp, 0, MaxMp);
        InitDefaultLoadout();
    }

    // --- Stats (scaled by level) ---
    // Level 1 = the raw CharacterData base values; every level past that adds the growth
    // amounts. Enemies (StartingLevel 1) aren't touched, so the existing balance holds.
    private int Bonus(int perLevel) => perLevel * (level - 1);

    public CharacterData Base   => _base;
    public string Name          => _base.Name;
    public int MaxHp            => _base.MaxHp   + Bonus(_base.HpGrowth);   // statuses never touch the max pools
    public int MaxMp            => _base.MaxMp   + Bonus(_base.MpGrowth);
    // Attack/Defense/Speed scale with level, add whatever the equipped gear gives
    // (InventorySystem tracks equipment by character id; returns 0 when nothing's worn),
    // and finally get bent by any active status multipliers (Slow/Haste, Weaken/Rage,
    // Guard/Break). With no statuses the multiplier is just 1, so these read like before.
    public int Attack           => Mathf.Max(0, Mathf.RoundToInt((_base.Attack  + Bonus(_base.AttackGrowth)  + InventorySystem.AttackBonusFor(_base.Id))  * StatusMult(StatModifier.Attack)));
    public int Defense          => Mathf.Max(1, Mathf.RoundToInt((_base.Defense + Bonus(_base.DefenseGrowth) + InventorySystem.DefenseBonusFor(_base.Id)) * StatusMult(StatModifier.Defense)));
    public int Speed            => Mathf.Max(0, Mathf.RoundToInt((_base.Speed   + Bonus(_base.SpeedGrowth)   + InventorySystem.SpeedBonusFor(_base.Id))   * StatusMult(StatModifier.Speed)));

    // --- Level & EXP ---
    public int  Level          => level;
    public int  CurrentExp     => currentExp;
    // EXP needed to go from the current level to the next one (int.MaxValue at the cap).
    public int  ExpToNextLevel => LevelCurve.ExpToNext(level);
    public bool IsMaxLevel     => level >= LevelCurve.MaxLevel;
    // 0..1 fill for an EXP bar. Always 1 at the cap.
    public float ExpNormalized => IsMaxLevel ? 1f : Mathf.Clamp01((float)currentExp / ExpToNextLevel);

    // --- Skills (loadout-aware + level-gated) ---
    // Battle reads these, NOT Base.Skills directly: Skills = the equipped subset (only the
    // ones unlocked at the current level); SpecialSkills = the character's fixed special
    // list, also filtered by unlock level.
    public IReadOnlyList<SkillData> Skills        => equippedSkills.Where(IsUnlocked).ToList();
    public IReadOnlyList<SkillData> SpecialSkills => _base.SpecialSkills.Where(IsUnlocked).ToList();

    // The pool of NORMAL skills this character is allowed to equip, gated by level.
    public IReadOnlyList<SkillData> SkillPool => _base.Skills.Where(IsUnlocked).ToList();

    // True if this member is high enough level for the skill's unlock requirement.
    public bool IsUnlocked(SkillData s) => s != null && level >= s.UnlockLevel;

    public int  EquippedCount         => equippedSkills.Count;
    public bool IsEquipped(SkillData s) => s != null && equippedSkills.Contains(s);
    public bool CanEquipMore           => equippedSkills.Count < MaxEquippedSkills;

    // --- The stuff that changes ---
    public int CurrentHp => currentHp;
    public int CurrentMp => currentMp;
    public int CurrentSpecial => currentSpecial;
    public bool IsFainted => currentHp <= 0;

    // --- Resources (MP + Special gauge) ---

    public bool CanAffordMp(int cost)      => currentMp >= cost;
    public bool CanAffordSpecial(int cost) => currentSpecial >= cost;

    // Spend MP if we can afford it. Returns false (and spends nothing) if we're too broke.
    public bool SpendMp(int cost)
    {
        if (cost <= 0) return true;
        if (currentMp < cost) return false;
        currentMp -= cost;
        return true;
    }

    // Spend Special-gauge points if we have enough. Returns false if it's not charged enough.
    public bool SpendSpecial(int cost)
    {
        if (cost <= 0) return true;
        if (currentSpecial < cost) return false;
        currentSpecial -= cost;
        return true;
    }

    // Charge up the Special gauge (capped at SpecialMax). Call this as the fight goes on.
    public void AddSpecial(int amount)
    {
        currentSpecial = Mathf.Clamp(currentSpecial + Mathf.Abs(amount), 0, SpecialMax);
    }

    // Empty the Special gauge. Call this at the start of every battle.
    public void ResetSpecial() => currentSpecial = 0;

    public void RestoreMp(int amount)
    {
        currentMp = Mathf.Min(MaxMp, currentMp + Mathf.Abs(amount));
    }

    // --- Combat ---

    // Works out and applies damage from an attacker, and returns how much it dealt.
    // Formula: damage = (attacker.Attack * 2) / max(1, this.Defense) * multiplier
    // Pass multiplier = 2f for a critical hit.
    public int TakeDamage(int attackerAttack, float multiplier = 1f)
    {
        float raw    = (attackerAttack * 2f) / Mathf.Max(1f, Defense);
        int   damage = Mathf.Max(1, Mathf.RoundToInt(raw * multiplier));

        currentHp = Mathf.Max(0, currentHp - damage);
        return damage;
    }

    // --- Healing ---

    public void HealFull()
    {
        currentHp = MaxHp;
        currentMp = MaxMp; // resting tops up MP too
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(MaxHp, currentHp + Mathf.Abs(amount));
    }

    // --- EXP / leveling up ---

    // Hand over some EXP and sort out any level-ups. Returns the list of NEW levels hit
    // (empty if none) so the caller can shout "naik ke Level X!". Each level-up grows the
    // stat pools, and the gained HP/MP gets added to the current values too (a little heal),
    // so leveling mid-fight feels good without being a full restore. Does nothing at the cap.
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

            // Bump the live pools up by this level's growth (capped at the new maxima).
            currentHp = Mathf.Min(MaxHp, currentHp + _base.HpGrowth);
            currentMp = Mathf.Min(MaxMp, currentMp + _base.MpGrowth);
        }

        if (IsMaxLevel) currentExp = 0;           // no bar to show past the cap
        return gained;
    }

    // --- Editing the loadout ---

    // The default loadout is just the first MaxEquippedSkills of the character's pool.
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

    // Equip a normal skill. It has to belong to this character's pool, and the loadout
    // can't already be full. Returns true if it ends up equipped.
    public bool Equip(SkillData skill)
    {
        if (skill == null) return false;
        if (equippedSkills.Contains(skill)) return true;
        if (!_base.Skills.Contains(skill)) return false;   // not one of this character's skills
        if (!IsUnlocked(skill)) return false;              // level-locked, not learned yet
        if (equippedSkills.Count >= MaxEquippedSkills) return false;
        equippedSkills.Add(skill);
        return true;
    }

    // Take a skill out of the loadout. Returns true if it was actually equipped.
    public bool Unequip(SkillData skill) => equippedSkills.Remove(skill);

    // Flip a skill on or off, respecting the cap. Returns the new equipped state.
    public bool ToggleEquip(SkillData skill)
    {
        if (skill == null) return false;
        if (equippedSkills.Contains(skill)) { equippedSkills.Remove(skill); return false; }
        return Equip(skill);
    }

    // The ids of the currently-equipped skills, in order. Used when saving.
    public List<string> GetEquippedIds()
        => equippedSkills.Where(s => s != null && !string.IsNullOrEmpty(s.Id))
                         .Select(s => s.Id).ToList();

    // Rebuild the equipped loadout from saved skill ids, matched against this character's
    // own pool. Ids we don't recognise just get dropped; a null/empty list (or one that
    // matches nothing) falls back to the default loadout. Respects the cap.
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

        if (equippedSkills.Count == 0) InitDefaultLoadout();   // none of the saved ones matched
    }

    // --- Status effects (battle-only) ---

    private enum StatModifier { Attack, Defense, Speed }

    // Multiplies together every active status's multiplier for one stat (1 if there are none).
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

    // Read-only peek at the active status effects (the HUD icons use this).
    public IReadOnlyList<StatusEffectInstance> Statuses => statuses;
    public bool HasStatuses => statuses.Count > 0;

    // True if any active status is stopping this unit from acting (Stun / Freeze).
    public bool IsStunned => statuses.Any(s => s.Data != null && s.Data.PreventsAction);

    // Wipe all status effects. Call at battle start (statuses don't carry between fights).
    public void ClearStatuses() => statuses.Clear();

    // Slap a status onto this member. If they already have that exact status, we refresh
    // its duration back to full (when the data allows it) instead of stacking a duplicate.
    // Returns true if it was freshly added, false if it just refreshed an existing one.
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

    // Handle all the start-of-turn status bookkeeping for this member, in this order:
    //   1. apply each status's per-turn HP change (poison/regen), clamped to 0..MaxHp;
    //   2. note whether the unit is stunned THIS turn (before we count down, so a stun
    //      actually spends its last turn);
    //   3. tick every status down by one turn and drop the ones that ran out.
    // Returns a StatusTurnReport for the caller to narrate. Doesn't care about any UI.
    public StatusTurnReport ProcessTurnStart()
    {
        var report = new StatusTurnReport();
        if (statuses.Count == 0) return report;

        report.WasStunned = IsStunned;

        // 1) the per-turn HP tick
        foreach (var s in statuses)
        {
            int delta = s.Data != null ? s.Data.DamagePerTurn : 0;
            if (delta == 0) continue;

            int before = currentHp;
            currentHp = delta > 0
                ? Mathf.Max(0, currentHp - delta)        // poison/burn hurts
                : Mathf.Min(MaxHp, currentHp - delta);   // regen heals (delta is negative, so this adds)

            int applied = currentHp - before;            // negative = damage, positive = heal
            if (applied != 0) report.Ticks.Add(new StatusTick(s.Data, applied));
        }

        // 2 & 3) count everyone down and drop the expired ones
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
