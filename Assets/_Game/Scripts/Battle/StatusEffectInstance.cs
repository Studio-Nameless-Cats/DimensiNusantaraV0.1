// A live, battle-only copy of a StatusEffectData stuck on one PartyMember. It just keeps
// count of how many of that unit's turns are left. Plain C# (no MonoBehaviour or SO):
// we make a new one each time a status gets applied and throw it away when it expires or
// the fight ends. Nothing gets saved; statuses are wiped at the start of each battle,
// same as the per-battle Special gauge.
public class StatusEffectInstance
{
    public StatusEffectData Data { get; }

    // Turns left before this wears off. Counts down at the start of the unit's turn.
    public int TurnsRemaining { get; private set; }

    public StatusEffectInstance(StatusEffectData data)
    {
        Data           = data;
        TurnsRemaining = data != null ? data.Duration : 1;
    }

    // Knock one turn off the timer and return what's left.
    public int Tick() => --TurnsRemaining;

    // True once it's run out of turns.
    public bool IsExpired => TurnsRemaining <= 0;

    // Bump the timer back to full. Used when you re-apply a status that's allowed to refresh.
    public void Refresh()
    {
        if (Data != null) TurnsRemaining = Data.Duration;
    }
}

// One turn's worth of HP change from a status. Negative = poison/burn hurting you,
// positive = regen healing you.
public struct StatusTick
{
    public readonly StatusEffectData Data;
    // The HP change. Negative means damage taken, positive means HP healed.
    public readonly int HpDelta;
    public StatusTick(StatusEffectData data, int hpDelta) { Data = data; HpDelta = hpDelta; }
}

// A little rundown of what happened to a unit's statuses at the start of its turn.
// PartyMember.ProcessTurnStart() hands this back so BattleSystem can narrate it without
// caring about any specific UI.
public class StatusTurnReport
{
    // True if the unit got stunned this turn (so its action should be skipped).
    public bool WasStunned;
    // The HP changes that hit this turn (poison/regen), in order.
    public readonly System.Collections.Generic.List<StatusTick> Ticks = new System.Collections.Generic.List<StatusTick>();
    // Statuses that ran out of turns and got removed this turn.
    public readonly System.Collections.Generic.List<StatusEffectData> Expired = new System.Collections.Generic.List<StatusEffectData>();

    public bool HasTicks => Ticks.Count > 0;
}
