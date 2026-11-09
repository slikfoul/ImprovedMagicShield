using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    // EffectsControl 1.0.1 captures and mutates its own copies of shield visuals.
    // Release those copies before Improved Magic Shield samples the native baseline.
    internal static class EffectsControlPriority
    {
        internal const string Guid = "Slikfoul.EffectsControl";
        internal static readonly System.Version SupportedVersion = new System.Version(1, 0, 1, 0);
        internal static bool Ready { get; private set; }
        private static FieldInfo activeField, pendingField, stageField;
        private static MethodInfo observe;
        private static BaseUnityPlugin plugin;
        private static Action<string> warn;
        private static int lastMask, lastRevision = -1;
        private static readonly Dictionary<GameObject, object> deferred = new Dictionary<GameObject, object>();
        private static readonly List<GameObject> scratch = new List<GameObject>();

        internal static void Install(Harmony harmony, Action<string> warning)
        {
            warn = warning;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || !info.Instance) return;
            if (info.Metadata.Version == null || info.Metadata.Version > SupportedVersion) return;
            MethodInfo lateUpdate = null;
            try
            {
                lateUpdate = Bind(info.Instance);
                if (lateUpdate == null) return;
                harmony.Patch(lateUpdate, prefix: new HarmonyMethod(typeof(EffectsControlPriority), nameof(BeforeLateUpdate)) { priority = Priority.First });
                harmony.Patch(observe, prefix: new HarmonyMethod(typeof(EffectsControlPriority), nameof(BeforeObserve)) { priority = Priority.First });
                Ready = true;
            }
            catch (Exception error)
            {
                if (lateUpdate != null)
                {
                    try
                    {
                        harmony.Unpatch(lateUpdate, HarmonyPatchType.Prefix, harmony.Id);
                        harmony.Unpatch(observe, HarmonyPatchType.Prefix, harmony.Id);
                    }
                    catch { /* Disabled prefixes pass through even if they could not be removed. */ }
                }
                Fail(error);
            }
        }

        private static MethodInfo Bind(BaseUnityPlugin instance)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type type = instance.GetType(), stage = type.Assembly.GetType("EffectsControl.EffectStage");
            Type state = type.Assembly.GetType("EffectsControl.VisualState");
            if (stage == null || !stage.IsEnum || state == null || !typeof(IDisposable).IsAssignableFrom(state)) return null;
            var lateUpdate = type.GetMethod("LateUpdate", flags, null, Type.EmptyTypes, null);
            observe = type.GetMethod("Observe", flags, null, new[] { typeof(GameObject), stage }, null);
            activeField = type.GetField("active", flags);
            pendingField = type.GetField("pending", flags);
            stageField = state.GetField("Stage", flags);
            if (lateUpdate?.ReturnType != typeof(void) || observe?.ReturnType != typeof(void) || stageField?.FieldType != stage ||
                activeField?.FieldType != typeof(Dictionary<,>).MakeGenericType(typeof(GameObject), state) ||
                pendingField?.FieldType != typeof(Dictionary<,>).MakeGenericType(typeof(GameObject), stage)) return null;
            plugin = instance;
            return lateUpdate;
        }

        private static bool BeforeObserve(object[] __args)
        {
            if (!Ready) return true;
            try
            {
                var root = __args[0] as GameObject;
                var controller = ShieldVisualController.Instance;
                if (controller == null || controller.CooperativePriority || !controller.Claims(root)) return true;
                deferred[root] = __args[1];
                controller.Observe(root);
                return false;
            }
            catch (Exception error) { Fail(error); return true; }
        }

        private static void BeforeLateUpdate(object __instance)
        {
            try
            {
                if (!Ready) { if (deferred.Count > 0) ResumeReleased(null); return; }
                var controller = ShieldVisualController.Instance;
                if (controller == null || controller.CooperativePriority) return;
                controller.RefreshCatalog();
                int mask = controller.PriorityMask;
                int revision = controller.Catalog.Revision;
                if (mask != 0 && (mask != lastMask || revision != lastRevision))
                {
                    // Sweep already captured visuals and observations queued before the toggle.
                    var active = (IDictionary)activeField.GetValue(__instance);
                    var pending = (IDictionary)pendingField.GetValue(__instance);
                    YieldClaimed(active, controller, true);
                    YieldClaimed(pending, controller, false);
                }
                lastMask = mask; lastRevision = revision;
                if (deferred.Count > 0) ResumeReleased(controller);
            }
            catch (Exception error) { Fail(error); }
        }

        private static void YieldClaimed(IDictionary states, ShieldVisualController controller, bool captured)
        {
            scratch.Clear();
            foreach (DictionaryEntry entry in states)
                if (entry.Key is GameObject root && controller.Claims(root)) scratch.Add(root);
            foreach (var root in scratch)
            {
                object state = states[root];
                deferred[root] = captured ? stageField.GetValue(state) : state;
                if (captured) ((IDisposable)state).Dispose();
                states.Remove(root);
                controller.Observe(root);
            }
            scratch.Clear();
        }

        private static void ResumeReleased(ShieldVisualController controller)
        {
            scratch.Clear();
            foreach (var pair in deferred)
                if (!pair.Key || controller?.Claims(pair.Key) != true) scratch.Add(pair.Key);
            foreach (var root in scratch)
            {
                object stage = deferred[root];
                deferred.Remove(root);
                if (root && plugin) observe.Invoke(plugin, new object[] { root, stage });
            }
            scratch.Clear();
        }

        private static void Fail(Exception error)
        {
            if (!Ready && warn == null) return;
            Ready = false;
            var log = warn; warn = null;
            log?.Invoke("EffectsControl priority bridge unavailable; shield conflict protection restored. " + error.Message);
        }

        internal static void Stop()
        {
            Ready = false;
            try { ResumeReleased(null); }
            catch (Exception error) { Fail(error); }
            finally { deferred.Clear(); scratch.Clear(); plugin = null; warn = null; lastMask = 0; lastRevision = -1; }
        }
    }
}
