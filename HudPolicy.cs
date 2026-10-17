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

        internal static float Width(float capacity, int protectionPerSegment)
        {
            // Native stamina/Eitr artwork repeats every 32 HUD units. Use the actual
            // shield capacity, never the recipient's skill or another resource's width.
            return Math.Max(0f, capacity) / Math.Max(1, protectionPerSegment) * 32f;
        }

        internal static float Alpha(float quietTime)
        {
            return Math.Max(0f, Math.Min(1f, 1f - (Math.Max(0f, quietTime) - 5f) / 0.5f));
        }

        internal static string Text(float remaining, float capacity)
        {
            return DisplayPolicy.WholeProtection(remaining).ToString() + " / " + DisplayPolicy.WholeProtection(capacity);
        }
    }
}
