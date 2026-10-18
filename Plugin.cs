using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ImprovedMagicShield.Visuals;

namespace ImprovedMagicShield
{
    [BepInPlugin(PluginGuid, "Improved Magic Shield", PluginVersion)]
    [BepInDependency("Slikfoul.EffectsControl", BepInDependency.DependencyFlags.SoftDependency)]
    [DefaultExecutionOrder(11000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Slikfoul.ImprovedMagicShield";
        public const string PluginVersion = "1.2.2";
        private Harmony harmony;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> showProtection;
        private static ConfigEntry<bool> showHud;
        private static ConfigEntry<bool> showHealthOutline;
        private static ConfigEntry<int> labelScale;
        private static ConfigEntry<int> protectionPerSegment;
        private static ConfigEntry<int> labelOffset;
        private static bool hooksReady;
        internal static bool IsEnabled { get { return hooksReady && modEnabled != null && modEnabled.Value; } }
        private ShieldLabels labels;
        private ShieldHud shieldHud;
        private ShieldVisualController shieldVisuals;

        private void Awake()
        {
            modEnabled = Config.Bind("1 - General", "Enabled", true, "Enable improved shield refreshes, displays and optional appearance controls. Disabling restores vanilla behavior for subsequent casts without removing active shields.");
            showProtection = Config.Bind("2 - Display", "Show remaining protection", false, "Show remaining shield protection above other eligible characters, never your own character. Use the shielded character's cast, damage and combat activity to fade after five quiet seconds, independently of your combat. The owning peer needs this version to supply current protection and activity.");
            showHealthOutline = Config.Bind("2 - Display", "Show shield health outline", false, "Draw a rounded, translucent purple contour outside other shielded characters' visible health bars. Its visible length follows remaining / full shield protection without a vertical end cap through health. Independent of protection numbers and the local shield bar; native health stays unchanged. The owning peer needs version 1.2.0 or newer to supply the actual shield capacity.");
            showHud = Config.Bind("2 - Display", "Show local shield bar", true, "Show your own remaining / full shield protection in a fixed lower HUD row. Show for five seconds after a cast, shield damage or a vanilla notification that an alerted enemy targets you. Attack, block and bow inputs alone do not reveal the bar. Fade out after five quiet seconds, including damaged shields. Independent of the overhead labels.");
            labelScale = Config.Bind("2 - Display", "Label scale (%)", 80, new ConfigDescription("Size of the remaining-protection label, in percent.", new AcceptableValueRange<int>(50, 300)));
            protectionPerSegment = Config.Bind("2 - Display", "Protection per segment", 50, new ConfigDescription("Full shield protection represented by one native 32-unit HUD segment. Lower values make the local bar longer. Width follows the currently applied shield, including another caster's shield. The frame keeps shrinking at higher values; its readable caption may extend beyond a short frame. Damage changes only the fill. Whole numbers only.", new AcceptableValueRange<int>(10, 500)));
            labelOffset = Config.Bind("2 - Display", "Label vertical offset", -20, new ConfigDescription("Move remaining-protection labels relative to their automatic position: above player and summoned skeleton names, below other visible name/health blocks. Positive values move up; negative values move down. Uses HUD units and follows the native header canvas scale. Does not move the local shield bar. Whole numbers only.", new AcceptableValueRange<int>(-200, 200)));
            var visualSettings = new VisualSettings(Config);
            try
            {
                ShieldRefresh.VerifyFields();
                shieldVisuals = new ShieldVisualController(visualSettings, message => Logger.LogWarning(message));
                harmony = new Harmony(PluginGuid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                labels = new ShieldLabels();
                shieldHud = new ShieldHud();
                Camera.onPreCull += PrepareLabels;
                hooksReady = true;
                Logger.LogInfo("Improved Magic Shield " + PluginVersion + " loaded. Shield refreshes compare new full protection with current remaining protection.");
            }
            catch (Exception error)
            {
                if (harmony != null) harmony.UnpatchSelf();
                hooksReady = false;
                if (shieldVisuals != null) shieldVisuals.Dispose();
                Logger.LogError("Improved Magic Shield disabled: incompatible status-effect hooks. " + error);
            }
        }

        private void Update()
        {
            if (shieldVisuals != null) shieldVisuals.ReleaseLightWrites();
            if (labels != null) labels.Tick(IsEnabled, showProtection.Value, showHealthOutline.Value);
        }

        private void OnGUI()
        {
            if (labels != null && IsEnabled && showProtection.Value) labels.Draw(labelScale.Value, labelOffset.Value);
        }

        private void PrepareLabels(Camera camera)
        {
            if (labels != null) labels.PrepareFrame(camera, IsEnabled && showProtection.Value, labelScale.Value, labelOffset.Value, IsEnabled && showHealthOutline.Value);
        }

        private void LateUpdate()
        {
            if (shieldHud != null) shieldHud.Tick(IsEnabled && showHud.Value, protectionPerSegment.Value);
            if (shieldVisuals != null) shieldVisuals.Tick(IsEnabled);
        }

        private void OnDestroy()
        {
            hooksReady = false;
            Camera.onPreCull -= PrepareLabels;
            if (labels != null) labels.Clear();
            if (shieldHud != null) shieldHud.Destroy();
            if (shieldVisuals != null) shieldVisuals.Dispose();
            if (harmony != null) harmony.UnpatchSelf();
        }
    }
}
