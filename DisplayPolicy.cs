using System;

namespace ImprovedMagicShield
{
    internal static class DisplayPolicy
    {
        internal const double FreshSeconds = 2.0;
        internal static float ProtectionFraction(float remaining, float capacity)
        {
            if (!(remaining > 0f) || !(capacity > 0f) || float.IsInfinity(remaining) || float.IsInfinity(capacity)) return 0f;
            return Math.Min(1f, remaining / capacity);
        }
        internal static float SnapshotAlpha(double activityAt, double now)
        {
            // Missing activity on older clients is not evidence of combat.
            if (activityAt < 0.0 || activityAt > now + FreshSeconds || double.IsNaN(activityAt)) return 0f;
            return HudPolicy.Alpha((float)System.Math.Max(0.0, now - activityAt));
        }
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
