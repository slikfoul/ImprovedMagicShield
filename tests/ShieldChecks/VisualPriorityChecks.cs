using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using ImprovedMagicShield.Visuals;
using UnityEngine;

internal static class VisualPriorityChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var protection = new VisualSettings(0.35f);
        var vengeance = new VisualSettings(0.48f);
        var warnings = new List<string>();
        using var controller = new ShieldVisualController(protection, vengeance, warnings.Add);
        var effects = new EffectsControl.Plugin();
        effects.Config.Set("Enable mod", true);
        effects.Config.Set("Magic shield", true);
        Chainloader.PluginInfos.Add(EffectsControlPriority.Guid, new PluginInfo
        {
            Instance = effects, Metadata = new BepInPlugin { GUID = EffectsControlPriority.Guid, Name = "EffectsControl", Version = new Version(1, 0, 1) }
        });
        var ordinary = new GameObject("vfx_StaffShield(Clone)");
        var frost = new GameObject("vfx_FrostOrbs(Clone)");
        var weapon = new GameObject("WeaponEffect(Clone)");
        EffectList Effects(string name) => new EffectList { m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = new GameObject(name) } } };
        ObjectDB.instance = new ObjectDB { m_StatusEffects = new List<StatusEffect> {
            new SE_Shield(700, 0) { m_startEffects = Effects("vfx_StaffShield") },
            new SE_React { name = "Staff_FrostOrbs", m_startEffects = Effects("vfx_FrostOrbs") }
        }};
        Resources.Objects.AddRange(new[] { ordinary, frost, weapon });
        void Frame()
        {
            controller.ReleaseLightWrites();
            effects.LateUpdate();
            controller.Tick(true);
        }
        void Near(GameObject target, float expected, string why) => check(Math.Abs(VisualSamples.Get(target) - expected) < 0.001f, why);
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type bridge = typeof(EffectsControlPriority);
            check(bridge.GetMethod("Bind", flags).Invoke(null, new object[] { effects }) is MethodInfo,
                "Priority bridge binds to the EffectsControl fixture's layout.");
            bridge.GetProperty("Ready", flags).SetValue(null, true);
            effects.BeforeObserve = (Func<object[], bool>)bridge.GetMethod("BeforeObserve", flags).CreateDelegate(typeof(Func<object[], bool>));
            effects.BeforeLateUpdate = (Action<object>)bridge.GetMethod("BeforeLateUpdate", flags).CreateDelegate(typeof(Action<object>));
            effects.Observe(ordinary, EffectsControl.EffectStage.Shield);
            effects.Observe(weapon, EffectsControl.EffectStage.Impact);
            Frame();
            Near(ordinary, 25, "EffectsControl can manage the ordinary shield while this profile is off.");
            protection.Enabled.Value = true;
            vengeance.Enabled.Value = true;
            effects.Observe(frost, EffectsControl.EffectStage.Impact);
            Frame();
            Near(ordinary, 35, "Improved Magic Shield takes over from EffectsControl on an already customized sphere.");
            Near(frost, 48, "The Northern Vengeance profile is active alongside EffectsControl.");
            Near(weapon, 25, "Priority does not affect unrelated weapon visuals.");
            vengeance.Enabled.Value = false;
            Frame();
            Frame();
            Near(frost, 25, "A released effect is handed back without capturing the scaled baseline.");
            Near(ordinary, 35, "Disabling one profile does not release the other.");
            protection.Enabled.Value = false;
            Frame();
            Frame();
            Near(ordinary, 25, "EffectsControl takes the ordinary sphere back after restoration.");
            check(effects.Config.TryGetEntry("1. General", "Magic shield", out BepInEx.Configuration.ConfigEntry<bool> saved)
                && saved.Value, "Priority never changes the other mod's setting.");

            EffectsControlPriority.Stop();
            effects.BeforeObserve = null;
            effects.BeforeLateUpdate = null;
            Chainloader.PluginInfos[EffectsControlPriority.Guid].Metadata.Version = new Version(1, 1, 0);
            check(controller.ConnectPriority(2, _ => { }, () => 3) == null, "An unsupported protocol is rejected.");
            check(controller.ConnectPriority(1, null, () => 3) == null, "An incomplete handshake is rejected.");
            int peerMask = 3, releaseMask = 0, releaseCalls = 0;
            bool failRelease = false, failRead = false;
            Func<int> owned = controller.ConnectPriority(1, mask =>
            {
                if (failRelease) throw new InvalidOperationException("simulated release failure");
                releaseMask = mask;
                releaseCalls++;
                effects.ReleaseShields(mask);
            }, () => failRead ? throw new InvalidOperationException("simulated read failure") : peerMask);
            bool Owner(ShieldAppearance appearance) => controller.ControlledByEffectsControl(appearance);
            void CooperativeFrame()
            {
                controller.ReleaseLightWrites();
                if ((peerMask & 1 & ~owned()) != 0) effects.Observe(ordinary, EffectsControl.EffectStage.Shield);
                if ((peerMask & 2 & ~owned()) != 0) effects.Observe(frost, EffectsControl.EffectStage.Impact);
                effects.LateUpdate();
                controller.Tick(ImprovedMagicShield.Plugin.IsEnabled);
            }
            check(owned() == 0 && Owner(ShieldAppearance.Protection) && Owner(ShieldAppearance.NorthernVengeance),
                "F1 shows the peer only in its two enabled sections.");
            protection.Enabled.Value = true;
            check(owned() == 1 && !Owner(ShieldAppearance.Protection) && Owner(ShieldAppearance.NorthernVengeance),
                "The first desired profile already reserves only its own shield.");
            CooperativeFrame();
            Near(ordinary, 35, "The synchronous handoff releases EffectsControl before capture.");
            Near(frost, 25, "The peer retains the other sphere.");
            check(releaseCalls == 1 && releaseMask == 1, "Only Protection was released on its transition.");
            vengeance.Enabled.Value = true;
            CooperativeFrame();
            Near(frost, 48, "Northern Vengeance takes over from its peer independently.");
            check(owned() == 3 && releaseCalls == 2 && releaseMask == 2, "Only newly enabled Northern Vengeance was released.");
            protection.Enabled.Value = false;
            check(owned() == 3 && !Owner(ShieldAppearance.Protection), "An applied profile still holds priority until restoration.");
            CooperativeFrame();
            Near(ordinary, 100, "The original sphere is restored before giving ownership back.");
            check(owned() == 2 && Owner(ShieldAppearance.Protection) && !Owner(ShieldAppearance.NorthernVengeance),
                "Only the restored shield gets the EffectsControl hint.");
            CooperativeFrame();
            Near(ordinary, 25, "The peer captures the original state after the handoff.");
            Near(weapon, 25, "The cooperative protocol leaves weapon effects alone.");
            check(releaseCalls == 2, "Unchanged profiles are not released every frame.");
            ImprovedMagicShield.Plugin.IsEnabled = false;
            check(owned() == 2, "The applied bit also survives a global disable until restoration.");
            CooperativeFrame();
            check(owned() == 0 && Owner(ShieldAppearance.NorthernVengeance), "Global disable returns ownership to the enabled peer.");
            ImprovedMagicShield.Plugin.IsEnabled = true;
            failRelease = true;
            CooperativeFrame();
            check(owned() == 0 && Owner(ShieldAppearance.NorthernVengeance), "A failed release yields safely without visual writes.");
            vengeance.Enabled.Value = false;
            CooperativeFrame();
            failRelease = false;
            vengeance.Enabled.Value = true;
            CooperativeFrame();
            Near(frost, 48, "A deliberate disable/re-enable recovers from a failed handoff.");
            failRead = true;
            check(!Owner(ShieldAppearance.Protection), "A failed peer read does not mislabel the original.");
            failRead = false;
            peerMask = 2;
            check(!Owner(ShieldAppearance.Protection), "The original appearance does not get a peer hint.");
            Chainloader.PluginInfos["subtleshield"] = new PluginInfo { Metadata = new BepInPlugin { GUID = "subtleshield", Name = "SubtleShield" } };
            CooperativeFrame();
            check(owned() == 0 && !Owner(ShieldAppearance.NorthernVengeance), "SubtleShield restores the visuals and hides peer hints.");
            Chainloader.PluginInfos.Remove("subtleshield");
        }
        finally
        {
            controller.Dispose();
            ImprovedMagicShield.Plugin.IsEnabled = true;
            EffectsControlPriority.Stop();
            Chainloader.PluginInfos.Clear();
            Resources.Objects.Clear();
            VisualSamples.Values.Clear();
            ObjectDB.instance = null;
        }
    }
}

// Drive the production prefixes explicitly: the game's Mono Harmony build does not
// install detours on this CoreCLR test host. Test the real handoff, not Unity rendering.
internal static class VisualSamples
{
    internal static readonly Dictionary<GameObject, float> Values = new();
    internal static float Get(GameObject root) => Values.TryGetValue(root, out var value) ? value : 100f;
}
public class ObjectDB : UnityEngine.Object
{
    public static ObjectDB instance;
    public List<StatusEffect> m_StatusEffects;
}
namespace UnityEngine
{
    public static class Resources
    {
        internal static readonly List<GameObject> Objects = new();
        public static T[] FindObjectsOfTypeAll<T>() => Objects.FindAll(item => item is T).ConvertAll(item => (T)(object)item).ToArray();
    }
    public static class SystemInfo { public static Rendering.GraphicsDeviceType graphicsDeviceType = Rendering.GraphicsDeviceType.Test; }
}
namespace UnityEngine.Rendering { public enum GraphicsDeviceType { Null, Test } }
namespace UnityEngine.SceneManagement { public struct Scene { public bool IsValid() => true; public bool isLoaded => true; } }
namespace BepInEx
{
    public class BaseUnityPlugin : UnityEngine.Object { public Configuration.ConfigFile Config { get; } = new(); }
    public class BepInPlugin { public string GUID, Name; public Version Version; }
    public class PluginInfo { public BaseUnityPlugin Instance; public BepInPlugin Metadata; }
}
namespace BepInEx.Bootstrap
{
    public static class Chainloader { public static readonly Dictionary<string, PluginInfo> PluginInfos = new(); }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; }
    public class ConfigFile
    {
        private readonly Dictionary<string, object> entries = new();
        public void Set(string key, bool value) => entries[key] = new ConfigEntry<bool> { Value = value };
        public bool TryGetEntry<T>(string section, string key, out ConfigEntry<T> entry)
        {
            entry = entries.TryGetValue(key, out var value) ? value as ConfigEntry<T> : null;
            return entry != null;
        }
    }
}
namespace ImprovedMagicShield.Visuals
{
    public enum EffectStage { Shield }
    internal sealed class VisualSettings
    {
        internal readonly BepInEx.Configuration.ConfigEntry<bool> Enabled = new();
        private readonly float multiplier;
        internal VisualSettings(float multiplier) { this.multiplier = multiplier; }
        internal VisualOptions Get() => new VisualOptions { Multiplier = multiplier };
    }
    internal sealed class VisualOptions { internal float Multiplier; }
    internal sealed class VisualState : IDisposable
    {
        private readonly GameObject root;
        private readonly float original;
        internal VisualState(GameObject root, EffectStage stage) { this.root = root; original = VisualSamples.Get(root); }
        internal bool Empty => false;
        internal void Apply(VisualOptions options) => VisualSamples.Values[root] = original * options.Multiplier;
        internal void ReleaseLightWrites() => Dispose();
        public void Dispose() => VisualSamples.Values[root] = original;
    }
}
namespace EffectsControl
{
    public enum EffectStage { Impact, Shield }
    public sealed class VisualState : IDisposable
    {
        public readonly EffectStage Stage;
        private readonly GameObject root;
        private readonly float original;
        public VisualState(GameObject root, EffectStage stage) { this.root = root; Stage = stage; original = VisualSamples.Get(root); }
        public void Apply() => VisualSamples.Values[root] = original * 0.25f;
        public void Dispose() => VisualSamples.Values[root] = original;
    }
    public sealed class Plugin : BaseUnityPlugin
    {
        private readonly Dictionary<GameObject, VisualState> active = new();
        private readonly Dictionary<GameObject, EffectStage> pending = new();
        internal Func<object[], bool> BeforeObserve;
        internal Action<object> BeforeLateUpdate;
        public void Observe(GameObject root, EffectStage stage)
        {
            if (BeforeObserve?.Invoke(new object[] { root, stage }) == false) return;
            if (!active.ContainsKey(root)) pending[root] = stage;
        }
        public void LateUpdate()
        {
            BeforeLateUpdate?.Invoke(this);
            foreach (var pair in pending) active[pair.Key] = new VisualState(pair.Key, pair.Value);
            pending.Clear();
            foreach (var state in active.Values) state.Apply();
        }
        internal void ReleaseShields(int mask)
        {
            int Bit(GameObject root) => root.name.StartsWith("vfx_StaffShield", StringComparison.Ordinal) ? 1
                : root.name.StartsWith("vfx_FrostOrbs", StringComparison.Ordinal) ? 2 : 0;
            foreach (var root in new List<GameObject>(active.Keys))
            {
                if ((mask & Bit(root)) == 0) continue;
                active[root].Dispose();
                active.Remove(root);
            }
            foreach (var root in new List<GameObject>(pending.Keys))
                if ((mask & Bit(root)) != 0) pending.Remove(root);
        }
    }
}
