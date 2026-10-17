using System;

namespace ImprovedMagicShield
{
    internal static class DisplayPolicy
    {
        internal const double FreshSeconds = 2.0;
        internal static bool IsFresh(float remaining, double sampledAt, double now, long publisher, long owner)
        {
            double age = now - sampledAt;
            return remaining > 0f && !float.IsInfinity(remaining) && publisher == owner
                && age >= -FreshSeconds && age <= FreshSeconds;
        }

        // Round down: the label must not promise protection beyond the actual remainder.
        internal static int WholeProtection(float remaining)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0.0, Math.Floor(remaining)));
        }
    }
}
