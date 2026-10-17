using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    // Exact prefab/list provenance from shield status effects, never name fragments.
    internal sealed class ShieldVisualCatalog
    {
        private ConditionalWeakTable<EffectList, object> lists = new ConditionalWeakTable<EffectList, object>();
        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        public void Clear() { lists = new ConditionalWeakTable<EffectList, object>(); names.Clear(); }
        public bool Contains(EffectList list) { return list != null && lists.TryGetValue(list, out _); }
        public bool Contains(GameObject root)
        {
            if (!root) return false;
            string name = root.name;
            if (name.EndsWith("(Clone)", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 7);
            return names.Contains(name);
        }
        public void Register(SE_Shield shield)
        {
            if (!shield) return;
            Add(shield.m_startEffects); Add(shield.m_hitEffects); Add(shield.m_breakEffects); Add(shield.m_stopEffects);
        }
        private void Add(EffectList list)
        {
            if (list == null || list.m_effectPrefabs == null) return;
            if (!Contains(list)) lists.Add(list, new object());
            foreach (var effect in list.m_effectPrefabs)
            {
                var prefab = effect == null ? null : effect.m_prefab;
                if (!Eligible(prefab)) continue;
                names.Add(prefab.name);
            }
        }
        internal static bool Eligible(GameObject root)
        {
            return root && !root.GetComponentInChildren<Character>(true) && !root.GetComponentInChildren<Piece>(true);
        }
    }
}
