using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    internal enum ShieldAppearance { Protection, NorthernVengeance }

    // Exact prefab/list provenance from shield status effects, never name fragments.
    internal sealed class ShieldVisualCatalog
    {
        private ConditionalWeakTable<EffectList, object> lists = new ConditionalWeakTable<EffectList, object>();
        private readonly Dictionary<string, ShieldAppearance?> names = new Dictionary<string, ShieldAppearance?>(StringComparer.Ordinal);
        internal int Revision { get; private set; }
        public void Clear() { lists = new ConditionalWeakTable<EffectList, object>(); names.Clear(); Revision++; }
        public bool Contains(EffectList list) { return list != null && lists.TryGetValue(list, out _); }
        public bool TryGetAppearance(GameObject root, out ShieldAppearance appearance)
        {
            appearance = default;
            if (!root) return false;
            if (!names.TryGetValue(PrefabName(root.name), out var kind) || !kind.HasValue) return false;
            appearance = kind.Value;
            return true;
        }
        private static string PrefabName(string name)
        {
            return name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7) : name;
        }
        public void Register(StatusEffect effect)
        {
            if (!effect) return;
            if (effect is SE_Shield shield)
            {
                Add(shield.m_startEffects, ShieldAppearance.Protection);
                Add(shield.m_hitEffects, ShieldAppearance.Protection);
                Add(shield.m_breakEffects, ShieldAppearance.Protection);
                Add(shield.m_stopEffects, ShieldAppearance.Protection);
            }
            else if (PrefabName(effect.name) == "Staff_FrostOrbs" && effect.GetType().Name == "SE_React")
            {
                // Register the sphere, not the retaliatory projectile or its impact effects.
                Add(effect.m_startEffects, ShieldAppearance.NorthernVengeance);
            }
        }
        private void Add(EffectList list, ShieldAppearance appearance)
        {
            if (list == null || list.m_effectPrefabs == null) return;
            if (!Contains(list)) lists.Add(list, new object());
            foreach (var effect in list.m_effectPrefabs)
            {
                var prefab = effect == null ? null : effect.m_prefab;
                if (!Eligible(prefab)) continue;
                if (!names.TryGetValue(prefab.name, out var previous)) { names.Add(prefab.name, appearance); Revision++; }
                else if (previous.HasValue && previous != appearance) { names[prefab.name] = null; Revision++; }
            }
        }
        internal static bool Eligible(GameObject root)
        {
            return root && !root.GetComponentInChildren<Character>(true) && !root.GetComponentInChildren<Piece>(true);
        }
    }
}
