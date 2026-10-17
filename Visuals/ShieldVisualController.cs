using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImprovedMagicShield.Visuals
{
    internal sealed class ShieldVisualController : IDisposable
    {
        internal static ShieldVisualController Instance;
        internal readonly ShieldVisualCatalog Catalog = new ShieldVisualCatalog();
        private readonly VisualSettings settings;
        private readonly Action<string> warn;
        private readonly HashSet<GameObject> pending = new HashSet<GameObject>();
        private readonly HashSet<GameObject> pendingNetwork = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, VisualState> active = new Dictionary<GameObject, VisualState>();
        private readonly List<GameObject> expired = new List<GameObject>();
        private bool rebuild = true, applied, faulted, warned;
        private int failures;

        public ShieldVisualController(VisualSettings settings, Action<string> warn)
        {
            this.settings = settings; this.warn = warn; Instance = this;
        }
        internal void RequestCatalog() { rebuild = true; }
        internal void ReleaseLightWrites()
        {
            foreach (var state in active.Values) state.ReleaseLightWrites();
        }
        internal void Register(SE_Shield shield) { Catalog.Register(shield); }
        internal void Observe(GameObject root)
        {
            if (ShieldVisualCatalog.Eligible(root)) pending.Add(root);
        }
        internal void ObserveNetwork(GameObject root)
        {
            if (!root) return;
            if (rebuild) pendingNetwork.Add(root);
            else if (Catalog.Contains(root)) Observe(root);
        }

        internal static bool ConflictingControlsEnabled()
        {
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (info.Metadata.GUID.IndexOf("subtleshield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(info.Metadata.Name, "SubtleShield", StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.Equals(info.Metadata.GUID, "Slikfoul.EffectsControl", StringComparison.OrdinalIgnoreCase)) continue;
                ConfigEntry<bool> enabled, shield;
                // Missing/unknown settings are treated conservatively; never rewrite the other mod's config.
                if (!info.Instance ||
                    !info.Instance.Config.TryGetEntry("1. General", "Enable mod", out enabled) ||
                    !info.Instance.Config.TryGetEntry("1. General", "Magic shield", out shield)) return true;
                if (enabled.Value && shield.Value) return true;
            }
            return false;
        }

        public void Tick(bool modEnabled)
        {
            try
            {
                bool catalogChanged = rebuild;
                if (rebuild)
                {
                    Catalog.Clear();
                    if (ObjectDB.instance)
                        foreach (var effect in ObjectDB.instance.m_StatusEffects)
                            if (effect is SE_Shield shield) Catalog.Register(shield);
                    rebuild = false;
                }
                foreach (var root in pendingNetwork) if (Catalog.Contains(root)) Observe(root);
                pendingNetwork.Clear();
                bool requested = modEnabled && settings.Enabled.Value && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
                bool conflict = requested && ConflictingControlsEnabled();
                if (conflict && !warned) warn("Magic Shield Effect suspended: disable Magic shield in EffectsControl or disable SubtleShield to use these visual settings.");
                warned = conflict;
                if (!requested) faulted = false;
                bool enabled = requested && !conflict && !faulted;
                if (!enabled)
                {
                    if (applied) Restore();
                    pending.Clear();
                    applied = false;
                    return;
                }
                if (!applied || catalogChanged)
                {
                    // One discovery pass when enabled, including already-present remote bubbles.
                    // Prefabs/assets have no loaded scene and must never be modified.
                    foreach (var root in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (root && root.scene.IsValid() && root.scene.isLoaded && root.activeInHierarchy && Catalog.Contains(root)) Observe(root);
                }
                applied = true;
                // Run after native Start and EffectsControl's LateUpdate, preserving native baselines.
                foreach (var root in pending)
                {
                    if (!root || active.ContainsKey(root)) continue;
                    var state = new VisualState(root, EffectStage.Shield);
                    if (state.Empty) state.Dispose(); else active.Add(root, state);
                }
                pending.Clear();
                var options = settings.Get();
                expired.Clear();
                foreach (var pair in active)
                {
                    if (!pair.Key) { pair.Value.Dispose(); expired.Add(pair.Key); }
                    else pair.Value.Apply(options);
                }
                foreach (var root in expired) active.Remove(root);
            }
            catch (Exception error)
            {
                Restore(); applied = false; faulted = true;
                if (failures++ < 5) warn("Shield visual controls suspended after an error; gameplay continues. " + error);
            }
        }
        private void Restore()
        {
            foreach (var state in active.Values) state.Dispose();
            active.Clear(); pending.Clear();
        }
        public void Dispose()
        {
            Restore(); pendingNetwork.Clear(); Catalog.Clear();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
