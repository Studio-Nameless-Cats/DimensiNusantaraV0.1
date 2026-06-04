using UnityEngine;

/// <summary>
/// Central, tunable EXP curve for the whole game. One place to balance how fast
/// characters level. Pure math — no assets to wire.
///
/// <see cref="ExpToNext"/> returns the EXP needed to advance FROM a given level to
/// the next one (so the EXP bar = currentExp / ExpToNext(level)). The curve is a
/// gentle power growth: low early levels, steeper later.
///
/// To rebalance, tweak <see cref="BaseExp"/> / <see cref="Exponent"/> / <see cref="MaxLevel"/>.
/// (If you later want per-character curves, this can become a ScriptableObject; the
/// callers only touch ExpToNext / MaxLevel.)
/// </summary>
public static class LevelCurve
{
    /// <summary>Hard level cap. At this level a member stops gaining EXP.</summary>
    public const int MaxLevel = 99;

    // Curve constants. ExpToNext(level) = BaseExp * level^Exponent.
    //   L1→2 ≈ 50,  L2→3 ≈ 151,  L3→4 ≈ 290,  L5→6 ≈ 660, …
    private const float BaseExp  = 50f;
    private const float Exponent = 1.6f;

    /// <summary>EXP required to advance from <paramref name="level"/> to level+1.
    /// Returns int.MaxValue at/after the cap (effectively "never levels again").</summary>
    public static int ExpToNext(int level)
    {
        if (level < 1) level = 1;
        if (level >= MaxLevel) return int.MaxValue;
        return Mathf.RoundToInt(BaseExp * Mathf.Pow(level, Exponent));
    }
}
