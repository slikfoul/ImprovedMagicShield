using System;
using System.Runtime.CompilerServices;

namespace ImprovedMagicShield
{
    // Shared by the owner's lower bar and published overhead display. Activity
    // belongs to the shielded character; the viewer's combat state is irrelevant.
    internal static class ShieldActivity
    {
        private sealed class State
        {
            internal SE_Shield Shield;
            internal float Age, Protection, ActivityAge;
            internal uint Threat, SeenThreat;
        }
        private static ConditionalWeakTable<Character, State> states = new ConditionalWeakTable<Character, State>();
        private static State Get(Character character) { return states.GetValue(character, ignored => new State()); }

        internal static void ObserveThreat(Player player, bool alerted)
        {
            if (!alerted || player != Player.m_localPlayer) return;
            unchecked { Get(player).Threat++; }
        }

        internal static float QuietTime(Character character, SE_Shield shield, float remaining)
        {
            State state = Get(character);
            float age = shield.GetDuration();
            BaseAI ai = character is Player ? null : character.GetBaseAI();
            bool fighting = ai != null && ai.IsAlerted() && ai.HaveTarget();
            if (!ReferenceEquals(state.Shield, shield) || age < state.Age
                || remaining < state.Protection || state.Threat != state.SeenThreat || fighting)
                state.ActivityAge = age;
            state.Shield = shield;
            state.Age = age;
            state.Protection = remaining;
            state.SeenThreat = state.Threat;
            return Math.Max(0f, age - state.ActivityAge);
        }

        internal static void Clear() { states = new ConditionalWeakTable<Character, State>(); }
    }
}
