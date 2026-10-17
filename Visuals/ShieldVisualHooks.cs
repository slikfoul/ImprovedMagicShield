using HarmonyLib;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class VisualDatabaseAwakeHook { private static void Postfix() { ShieldVisualController.Instance?.RequestCatalog(); } }
    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    internal static class VisualDatabaseCopyHook { private static void Postfix() { ShieldVisualController.Instance?.RequestCatalog(); } }
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class VisualSceneHook { private static void Postfix() { ShieldVisualController.Instance?.RequestCatalog(); } }
    [HarmonyPatch(typeof(SE_Shield), "Setup")]
    internal static class VisualShieldSetupHook { private static void Prefix(SE_Shield __instance) { ShieldVisualController.Instance?.Register(__instance); } }
    [HarmonyPatch(typeof(EffectList), "Create")]
    internal static class VisualEffectCreateHook
    {
        private static void Postfix(EffectList __instance, GameObject[] __result)
        {
            var controller = ShieldVisualController.Instance;
            if (controller == null || __result == null || !controller.Catalog.Contains(__instance)) return;
            foreach (var root in __result) controller.Observe(root);
        }
    }
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    internal static class VisualNetworkCreateHook { private static void Postfix(GameObject __result) { ShieldVisualController.Instance?.ObserveNetwork(__result); } }
}
