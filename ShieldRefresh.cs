using System;
using System.Reflection;

namespace ImprovedMagicShield
{
    internal static class ShieldRefresh
    {
        private static readonly FieldInfo total = typeof(SE_Shield).GetField("m_totalAbsorbDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo damage = typeof(SE_Shield).GetField("m_damage", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void VerifyFields()
        {
            if (total == null || damage == null || total.FieldType != typeof(float) || damage.FieldType != typeof(float))
                throw new MissingFieldException("SE_Shield protection fields are incompatible.");
        }

        // Same calculation and operation order as the installed SE_Shield.SetLevel.
        // Item level changes duration, while skill and world level change capacity.
        internal static float FullProtection(SE_Shield shield, float skillLevel)
        {
            float value = shield.m_absorbDamage + shield.m_absorbDamagePerSkillLevel * skillLevel;
            if (Game.m_worldLevel > 0)
                value += shield.m_absorbDamageWorldLevel * (float)Game.m_worldLevel;
            return value;
        }

        internal static bool TryRefresh(SE_Shield shield, int itemLevel, float skillLevel)
        {
            float incoming = FullProtection(shield, skillLevel);
            float remaining = (float)total.GetValue(shield) - (float)damage.GetValue(shield);
            if (!(incoming >= remaining)) return false;

            shield.ResetTime();
            shield.SetLevel(itemLevel, skillLevel);
            damage.SetValue(shield, 0f);
            return true;
        }

        internal static float Remaining(SE_Shield shield)
        {
            return Math.Max(0f, (float)total.GetValue(shield) - (float)damage.GetValue(shield));
        }

        internal static float Capacity(SE_Shield shield) { return (float)total.GetValue(shield); }

        internal static SE_Shield FindShield(Character character)
        {
            foreach (StatusEffect effect in character.GetSEMan().GetStatusEffects())
                if (effect is SE_Shield shield) return shield;
            return null;
        }

        // A rejected update returns null only to the caller's local variable. The active
        // effect stays in SEMan; vanilla's following SetAttacker is thereby skipped.
        internal static StatusEffect RefreshFromHit(StatusEffect effect, HitData hit)
        {
            if (Plugin.IsEnabled && effect is SE_Shield shield)
                return TryRefresh(shield, hit.m_itemLevel, hit.m_skillLevel) ? effect : null;

            effect.ResetTime();
            effect.SetLevel(hit.m_itemLevel, hit.m_skillLevel);
            return effect;
        }

        internal static bool InterceptManagerRefresh(SEMan manager, int hash, bool resetTime,
            int itemLevel, float skillLevel, short variant, ref StatusEffect result)
        {
            if (!Plugin.IsEnabled || !resetTime) return true;
            SE_Shield shield = manager.GetStatusEffect(hash) as SE_Shield;
            if (shield == null) return true;
            if (TryRefresh(shield, itemLevel, skillLevel)) shield.m_hitVariant = variant;
            // Both vanilla existing-effect branches return null, including a successful refresh.
            result = null;
            return false;
        }
    }
}
