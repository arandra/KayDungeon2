using System;
using System.Collections.Generic;

public static class SkillRange
{
    public const int BaseSize = 2;

    public static int RangeFromStep(int step)
    {
        return Math.Max(0, step);
    }

    public static IEnumerable<(int dx, int dy)> CircleOffsets(int rangeStep)
    {
        var radius = RangeFromStep(rangeStep);
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var cost = DistanceCost(dx, dy);
                if (cost <= radius)
                {
                    yield return (dx, dy);
                }
            }
        }
    }

    public static float DistanceCost(int dx, int dy)
    {
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);
        var diag = Math.Min(adx, ady);
        var straight = Math.Max(adx, ady) - diag;
        return diag * 1.5f + straight;
    }
}
