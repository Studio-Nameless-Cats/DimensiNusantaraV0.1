/// <summary>
/// A runtime, battle-only instance of a <see cref="StatusEffectData"/> sitting on one
/// PartyMember. Tracks how many of the affected unit's turns remain. Plain C# (no
/// MonoBehaviour / SO) — created fresh each time a status is applied, discarded when it
/// expires or the battle ends. Not serialized: statuses are cleared at battle start
/// (same lifecycle as the per-battle Special gauge).
/// </summary>
public class StatusEffectInstance
{
    public StatusEffectData Data { get; }

    /// <summary>Turns left before this status expires. Counts down at the affected unit's turn start.</summary>
    public int TurnsRemaining { get; private set; }

    public StatusEffectInstance(StatusEffectData data)
    {
        Data           = data;
        TurnsRemaining = data != null ? data.Duration : 1;
    }

    /// <summary>Decrement the remaining duration by one turn and return the new value.</summary>
    public int Tick() => --TurnsRemaining;

    /// <summary>True once the status has run out of turns.</summary>
    public bool IsExpired => TurnsRemaining <= 0;

    /// <summary>Reset the timer back to the data's full duration (used when re-applying a refreshable status).</summary>
    public void Refresh()
    {
        if (Data != null) TurnsRemaining = Data.Duration;
    }
}

/// <summary>One per-turn HP change from a status (poison/burn = negative, regen = positive).</summary>
public struct StatusTick
{
    public readonly StatusEffectData Data;
    /// <summary>HP delta applied. NEGATIVE = damage taken, POSITIVE = HP healed.</summary>
    public readonly int HpDelta;
    public StatusTick(StatusEffectData data, int hpDelta) { Data = data; HpDelta = hpDelta; }
}

/// <summary>
/// Summary of what happened to a unit's statuses at the start of its turn — returned by
/// <see cref="PartyMember.ProcessTurnStart"/> so the BattleSystem can narrate it (UI-agnostic).
/// </summary>
public class StatusTurnReport
{
    /// <summary>True if the unit was stunned this turn (its action should be skipped).</summary>
    public bool WasStunned;
    /// <summary>Per-turn HP changes applied this turn (DoT / regen), in order.</summary>
    public readonly System.Collections.Generic.List<StatusTick> Ticks = new System.Collections.Generic.List<StatusTick>();
    /// <summary>Statuses that ran out of turns and were removed this turn.</summary>
    public readonly System.Collections.Generic.List<StatusEffectData> Expired = new System.Collections.Generic.List<StatusEffectData>();

    public bool HasTicks => Ticks.Count > 0;
}
