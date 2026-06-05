using UnityEngine;

// The one EXP curve for the whole game, all in one tunable spot so you can balance how
// fast characters level. It's pure math, nothing to wire up.
//
// ExpToNext returns the EXP needed to go FROM a given level to the next one (so the EXP
// bar is just currentExp / ExpToNext(level)). The curve is a gentle power climb: cheap
// early levels, pricier later ones.
//
// Want to rebalance? Tweak BaseExp / Exponent / MaxLevel. (If you ever want per-character
// curves, this could become a ScriptableObject; callers only touch ExpToNext / MaxLevel.)
public static class LevelCurve
{
    // The hard level cap. Once a member hits this, they stop earning EXP.
    public const int MaxLevel = 99;

    // The curve constants. ExpToNext(level) = BaseExp * level^Exponent.
    //   L1->2 about 50, L2->3 about 151, L3->4 about 290, L5->6 about 660, and so on.
    private const float BaseExp  = 50f;
    private const float Exponent = 1.6f;

    // EXP needed to go from 'level' to level+1. Returns int.MaxValue at or past the cap,
    // which basically means "never levels up again".
    public static int ExpToNext(int level)
    {
        if (level < 1) level = 1;
        if (level >= MaxLevel) return int.MaxValue;
        return Mathf.RoundToInt(BaseExp * Mathf.Pow(level, Exponent));
    }
}
