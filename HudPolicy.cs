using System;

namespace ImprovedMagicShield
{
    internal static class HudPolicy
    {
        internal static bool TryRead(SE_Shield shield, out float remaining, out float capacity)
        {
            remaining = capacity = 0f;
            if (shield == null || (shield.m_ttl > 0f && shield.GetDuration() > shield.m_ttl)) return false;
            remaining = ShieldRefresh.Remaining(shield);
            capacity = ShieldRefresh.Capacity(shield);
            return remaining > 0f && capacity > 0f && !float.IsInfinity(capacity) && !float.IsNaN(capacity);
        }

        internal static float Fraction(float remaining, float capacity)
        {
            return capacity > 0f ? Math.Max(0f, Math.Min(1f, remaining / capacity)) : 0f;
        }

        internal static string Text(float remaining, float capacity)
        {
            return "Shield " + DisplayPolicy.WholeProtection(remaining) + " / " + DisplayPolicy.WholeProtection(capacity);
        }
    }
}
