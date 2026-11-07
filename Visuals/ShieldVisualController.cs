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
        private readonly VisualSettings protection, vengeance;
        private readonly Action<string> warn;
        private readonly HashSet<GameObject> pending = new HashSet<GameObject>();
        private readonly HashSet<GameObject> pendingNetwork = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, ActiveEffect> active = new Dictionary<GameObject, ActiveEffect>();
        private readonly List<GameObject> expired = new List<GameObject>();
        private bool rebuild = true, protectionApplied, vengeanceApplied, faulted, warnedProtection, warnedVengeance;
        private bool catalogChanged;
        private int failures;
        private sealed class ActiveEffect
        {
            internal readonly ShieldAppearance Appearance;
            internal readonly VisualState State;
            internal ActiveEffect(ShieldAppearance appearance, VisualState state) { Appearance = appearance; State = state; }
        }

        public ShieldVisualController(VisualSettings protection, VisualSettings vengeance, Action<string> warn)
        {
            this.protection = protection; this.vengeance = vengeance; this.warn = warn; Instance = this;
        }
        internal void RequestCatalog() { rebuild = true; }
        internal void ReleaseLightWrites()
        {
            foreach (var effect in active.Values) effect.State.ReleaseLightWrites();
        }
        internal void Register(StatusEffect effect)
        {
            RefreshCatalog();
            Catalog.Register(effect);
        }
        internal void Observe(GameObject root)
        {
            if (ShieldVisualCatalog.Eligible(root)) pending.Add(root);
        }
        internal void ObserveNetwork(GameObject root)
        {
            if (!root) return;
            if (rebuild) pendingNetwork.Add(root);
            else if (Catalog.TryGetAppearance(root, out _)) Observe(root);
        }

        private static bool IsSubtleShield(string guid, string name) =>
            guid.IndexOf("subtleshield", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(name, "SubtleShield", StringComparison.OrdinalIgnoreCase);

        internal int PriorityMask
        {
            get
            {
                if (!Plugin.IsEnabled || faulted || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return 0;
                int mask = (protection.Enabled.Value ? 1 : 0) | (vengeance.Enabled.Value ? 2 : 0);
                if (mask != 0)
                    foreach (var info in Chainloader.PluginInfos.Values)
                        if (IsSubtleShield(info.Metadata.GUID, info.Metadata.Name)) return 0;
                return mask;
            }
        }

        internal bool Claims(GameObject root)
        {
            if (!root) return false;
            // Do not let EffectsControl recapture the object until Tick has restored it.
            if (active.ContainsKey(root)) return true;
            RefreshCatalog();
            return Catalog.TryGetAppearance(root, out var appearance)
                && (PriorityMask & (appearance == ShieldAppearance.Protection ? 1 : 2)) != 0;
        }

        internal void RefreshCatalog()
        {
            if (!rebuild) return;
            Catalog.Clear();
            if (ObjectDB.instance)
                foreach (var effect in ObjectDB.instance.m_StatusEffects)
                    Catalog.Register(effect);
            rebuild = false;
            catalogChanged = true;
        }

        private static void GetConflicts(out bool blockProtection, out bool blockVengeance)
        {
            blockProtection = blockVengeance = false;
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (IsSubtleShield(info.Metadata.GUID, info.Metadata.Name))
                {
                    blockProtection = blockVengeance = true;
                    return;
                }
                if (!string.Equals(info.Metadata.GUID, EffectsControlPriority.Guid, StringComparison.OrdinalIgnoreCase) ||
                    EffectsControlPriority.Ready) continue;
                ConfigEntry<bool> enabled, shield;
                // Missing/unknown settings are treated conservatively; never rewrite the other mod's config.
                if (info.Instance && info.Instance.Config.TryGetEntry("1. General", "Enable mod", out enabled)
                    && info.Instance.Config.TryGetEntry("1. General", "Magic shield", out shield)
                    && (!enabled.Value || !shield.Value)) continue;
                blockProtection = true;
                // EffectsControl through 1.0.1 registers SE_Shield only; the Vengeance
                // Sphere is a separate SE_React start effect, not a protection bubble.
                var version = info.Metadata.Version;
                blockVengeance |= version == null || version > EffectsControlPriority.SupportedVersion;
            }
        }

        public void Tick(bool modEnabled)
        {
            try
            {
                RefreshCatalog();
                bool rescan = catalogChanged;
                catalogChanged = false;
                foreach (var root in pendingNetwork) if (Catalog.TryGetAppearance(root, out _)) Observe(root);
                pendingNetwork.Clear();
                bool requested = modEnabled && (protection.Enabled.Value || vengeance.Enabled.Value)
                    && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
                bool blockProtection = false, blockVengeance = false;
                if (requested) GetConflicts(out blockProtection, out blockVengeance);
                bool protectionConflict = blockProtection && protection.Enabled.Value;
                bool vengeanceConflict = blockVengeance && vengeance.Enabled.Value;
                if (protectionConflict && !warnedProtection) warn("Magic Shield Effect suspended: disable Magic shield in EffectsControl or disable SubtleShield to use it.");
                if (vengeanceConflict && !warnedVengeance) warn("Northern Vengeance Effect suspended: disable conflicting shield controls in EffectsControl or SubtleShield to use it.");
                warnedProtection = protectionConflict;
                warnedVengeance = vengeanceConflict;
                if (!requested) faulted = false;
                bool showProtection = requested && !faulted && protection.Enabled.Value && !blockProtection;
                bool showVengeance = requested && !faulted && vengeance.Enabled.Value && !blockVengeance;
                if (!showProtection && !showVengeance)
                {
                    if (active.Count != 0) Restore();
                    pending.Clear();
                    protectionApplied = vengeanceApplied = false;
                    return;
                }
                var protectionOptions = showProtection ? protection.Get() : null;
                var vengeanceOptions = showVengeance ? vengeance.Get() : null;
                VisualOptions Options(ShieldAppearance appearance) =>
                    appearance == ShieldAppearance.Protection ? protectionOptions : vengeanceOptions;
                if ((showProtection && !protectionApplied) || (showVengeance && !vengeanceApplied) || rescan)
                {
                    // One discovery pass when enabled, including already-present remote bubbles.
                    // Prefabs/assets have no loaded scene and must never be modified.
                    foreach (var root in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (root && root.scene.IsValid() && root.scene.isLoaded && root.activeInHierarchy
                            && Catalog.TryGetAppearance(root, out var appearance) && Options(appearance) != null) Observe(root);
                }
                protectionApplied = showProtection;
                vengeanceApplied = showVengeance;
                // Run after native Start and EffectsControl's LateUpdate, preserving native baselines.
                foreach (var root in pending)
                {
                    if (!root || active.ContainsKey(root) || !Catalog.TryGetAppearance(root, out var appearance)
                        || Options(appearance) == null) continue;
                    var state = new VisualState(root, EffectStage.Shield);
                    if (state.Empty) state.Dispose(); else active.Add(root, new ActiveEffect(appearance, state));
                }
                pending.Clear();
                expired.Clear();
                foreach (var pair in active)
                {
                    var options = Options(pair.Value.Appearance);
                    if (!pair.Key || options == null || (rescan &&
                        (!Catalog.TryGetAppearance(pair.Key, out var appearance) || appearance != pair.Value.Appearance)))
                    { pair.Value.State.Dispose(); expired.Add(pair.Key); }
                    else pair.Value.State.Apply(options);
                }
                foreach (var root in expired) active.Remove(root);
            }
            catch (Exception error)
            {
                Restore(); protectionApplied = vengeanceApplied = false; faulted = true;
                if (failures++ < 5) warn("Shield visual controls suspended after an error; gameplay continues. " + error);
            }
        }
        private void Restore()
        {
            foreach (var effect in active.Values) effect.State.Dispose();
            active.Clear(); pending.Clear();
        }
        public void Dispose()
        {
            Restore(); pendingNetwork.Clear(); Catalog.Clear();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
