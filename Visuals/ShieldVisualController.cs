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
        private bool disposed, warnedPeerRead;
        private Action<int> releaseEffectsControl;
        private Func<int> readEffectsControl;
        private int peerReleased;
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

        private int RequestedMask(bool modEnabled)
        {
            if (disposed || !modEnabled || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return 0;
            return (protection.Enabled.Value ? 1 : 0) | (vengeance.Enabled.Value ? 2 : 0);
        }

        internal bool CooperativePriority => releaseEffectsControl != null;
        internal int PriorityMask
        {
            get
            {
                int requested = RequestedMask(Plugin.IsEnabled);
                return requested == 0 || faulted ? 0 : requested & ~ConflictMask();
            }
        }
        private int ReadPriorityMask() => PriorityMask | (protectionApplied ? 1 : 0) | (vengeanceApplied ? 2 : 0);

        internal Func<int> ConnectPriority(int protocolVersion, Action<int> release, Func<int> read)
        {
            if (disposed || protocolVersion != 1 || release == null || read == null) return null;
            releaseEffectsControl = release;
            readEffectsControl = read;
            peerReleased = 0;
            warnedPeerRead = false;
            return ReadPriorityMask;
        }

        internal bool ControlledByEffectsControl(ShieldAppearance appearance)
        {
            if (disposed || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
            int bit = appearance == ShieldAppearance.Protection ? 1 : 2;
            if ((ReadPriorityMask() & bit) != 0) return false;
            foreach (var info in Chainloader.PluginInfos.Values)
                if (IsSubtleShield(info.Metadata.GUID, info.Metadata.Name)) return false;
            if (readEffectsControl != null)
            {
                try { return (readEffectsControl() & bit) != 0; }
                catch (Exception error)
                {
                    if (!warnedPeerRead) warn("Cannot read EffectsControl's shield appearance ownership: " + error.Message);
                    warnedPeerRead = true;
                    return false;
                }
            }
            // Only the ordinary shield is known to be handled by the legacy release.
            return bit == 1 && Chainloader.PluginInfos.TryGetValue(EffectsControlPriority.Guid, out var legacy)
                && legacy.Metadata.Version != null && legacy.Metadata.Version <= EffectsControlPriority.SupportedVersion
                && legacy.Instance
                && legacy.Instance.Config.TryGetEntry("1. General", "Enable mod", out ConfigEntry<bool> enabled)
                && legacy.Instance.Config.TryGetEntry("1. General", "Magic shield", out ConfigEntry<bool> shield)
                && enabled.Value && shield.Value;
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

        private int ConflictMask()
        {
            int blocked = 0;
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (IsSubtleShield(info.Metadata.GUID, info.Metadata.Name))
                {
                    return 3;
                }
                if (!string.Equals(info.Metadata.GUID, EffectsControlPriority.Guid, StringComparison.OrdinalIgnoreCase) ||
                    CooperativePriority || EffectsControlPriority.Ready) continue;
                ConfigEntry<bool> enabled, shield;
                // Missing/unknown settings are treated conservatively; never rewrite the other mod's config.
                if (info.Instance && info.Instance.Config.TryGetEntry("1. General", "Enable mod", out enabled)
                    && info.Instance.Config.TryGetEntry("1. General", "Magic shield", out shield)
                    && (!enabled.Value || !shield.Value)) continue;
                blocked |= 1;
                // EffectsControl through 1.0.1 registers SE_Shield only; the Vengeance
                // Sphere is a separate SE_React start effect, not a protection bubble.
                var version = info.Metadata.Version;
                if (version == null || version > EffectsControlPriority.SupportedVersion) blocked |= 2;
            }
            return blocked;
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
                int requested = RequestedMask(modEnabled);
                int conflicts = requested == 0 ? 0 : requested & ConflictMask();
                bool protectionConflict = (conflicts & 1) != 0;
                bool vengeanceConflict = (conflicts & 2) != 0;
                if (protectionConflict && !warnedProtection) warn("Magic Shield Effect suspended: disable Magic shield in EffectsControl or disable SubtleShield to use it.");
                if (vengeanceConflict && !warnedVengeance) warn("Northern Vengeance Effect suspended: disable conflicting shield controls in EffectsControl or SubtleShield to use it.");
                warnedProtection = protectionConflict;
                warnedVengeance = vengeanceConflict;
                if (requested == 0) faulted = false;
                int desired = faulted ? 0 : requested & ~conflicts;
                bool showProtection = (desired & 1) != 0, showVengeance = (desired & 2) != 0;
                if (!showProtection && !showVengeance)
                {
                    Restore();
                    return;
                }
                // EffectsControl must restore its own captured state before we sample
                // the native baseline. On disable keep publishing our old applied bit
                // until all affected instances have been restored below.
                int takeOver = desired & ~peerReleased;
                if (takeOver != 0 && releaseEffectsControl != null)
                {
                    releaseEffectsControl(takeOver);
                    peerReleased |= takeOver;
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
                protectionApplied |= showProtection;
                vengeanceApplied |= showVengeance;
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
                protectionApplied = showProtection;
                vengeanceApplied = showVengeance;
                peerReleased &= desired;
            }
            catch (Exception error)
            {
                faulted = true;
                Restore();
                if (failures++ < 5) warn("Shield visual controls suspended after an error; gameplay continues. " + error);
            }
        }
        private void Restore()
        {
            foreach (var effect in active.Values) effect.State.Dispose();
            active.Clear(); pending.Clear();
            protectionApplied = vengeanceApplied = false;
            peerReleased = 0;
        }
        public void Dispose()
        {
            disposed = true;
            Restore(); pendingNetwork.Clear(); Catalog.Clear();
            releaseEffectsControl = null; readEffectsControl = null;
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
