using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ImprovedMagicShield
{
    // Observe the owner's vanilla notification. Only an alerted enemy's target
    // report counts; sensing, attack inputs and block stance do not change the HUD.
    [HarmonyPatch(typeof(Player), "RPC_OnTargeted")]
    internal static class ShieldThreatHook
    {
        internal static void Postfix(Player __instance, bool alerted)
        {
            if (Plugin.IsEnabled) ShieldActivity.ObserveThreat(__instance, alerted);
        }
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class HitShieldHook
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var reset = typeof(StatusEffect).GetMethod(nameof(StatusEffect.ResetTime));
            var level = typeof(StatusEffect).GetMethod(nameof(StatusEffect.SetLevel));
            var item = typeof(HitData).GetField(nameof(HitData.m_itemLevel));
            var skill = typeof(HitData).GetField(nameof(HitData.m_skillLevel));
            var matches = new List<int>();
            for (int i = 0; i + 7 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldloc_S || !(code[i].operand is LocalBuilder local) || local.LocalType != typeof(StatusEffect)) continue;
                if (code[i + 1].Calls(reset) && code[i + 2].opcode == code[i].opcode && Equals(code[i + 2].operand, local)
                    && code[i + 3].opcode == OpCodes.Ldarg_2 && code[i + 4].LoadsField(item)
                    && code[i + 5].opcode == OpCodes.Ldarg_2 && code[i + 6].LoadsField(skill) && code[i + 7].Calls(level))
                    matches.Add(i);
            }
            if (matches.Count != 1) throw new InvalidOperationException("Expected exactly one Character.RPC_Damage existing-effect refresh block; found " + matches.Count + ".");
            int start = matches[0];
            // Never silently remove a branch target or exception boundary introduced by another patch.
            if (code.Skip(start + 1).Take(7).Any(c => c.labels.Count != 0 || c.blocks.Count != 0) || code[start].blocks.Count != 0)
                throw new InvalidOperationException("Unexpected control flow inside the shield refresh block.");
            var first = new CodeInstruction(code[start]);
            var replacement = new[]
            {
                first,
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(ShieldRefresh).GetMethod(nameof(ShieldRefresh.RefreshFromHit), BindingFlags.Static | BindingFlags.NonPublic)),
                new CodeInstruction(OpCodes.Stloc_S, code[start].operand)
            };
            code.RemoveRange(start, 8);
            code.InsertRange(start, replacement);
            return code;
        }
    }

    // Public hash calls and their RPC converge here only after vanilla's ownership check.
    [HarmonyPatch(typeof(SEMan), "Internal_AddStatusEffect")]
    internal static class ManagerHashShieldHook
    {
        internal static bool Prefix(SEMan __instance, int nameHash, bool resetTime, int itemLevel,
            float skillLevel, short variant, ref StatusEffect __result)
        {
            return ShieldRefresh.InterceptManagerRefresh(__instance, nameHash, resetTime, itemLevel, skillLevel, variant, ref __result);
        }
    }

    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
    internal static class ManagerObjectShieldHook
    {
        internal static bool Prefix(SEMan __instance, StatusEffect statusEffect, bool resetTime,
            int itemLevel, float skillLevel, short variant, ref StatusEffect __result)
        {
            return ShieldRefresh.InterceptManagerRefresh(__instance, statusEffect.NameHash(), resetTime, itemLevel, skillLevel, variant, ref __result);
        }
    }
}
