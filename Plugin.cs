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
        public const string PluginVersion = "0.1.0";
        private Harmony harmony;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> showProtection;
        private static ConfigEntry<bool> showHud;
        private static ConfigEntry<int> labelScale;
        private static bool hooksReady;
        internal static bool IsEnabled { get { return hooksReady && modEnabled != null && modEnabled.Value; } }
        private ShieldLabels labels;
        private ShieldHud shieldHud;
        private ShieldVisualController shieldVisuals;

        private void Awake()
        {
            modEnabled = Config.Bind("1 - General", "Enabled", true, "Enable improved shield refreshes, displays and optional appearance controls. Disabling restores vanilla behavior for subsequent casts without removing active shields.");
            showProtection = Config.Bind("2 - Display", "Show remaining protection", false, "Show remaining shield protection above other eligible characters, never your own character. Other characters need this mod on their owning peer to supply current protection.");
            showHud = Config.Bind("2 - Display", "Show local shield bar", true, "Show your own remaining / full shield protection in the lower resource HUD. Independent of the overhead labels.");
            labelScale = Config.Bind("2 - Display", "Label scale (%)", 100, new ConfigDescription("Size of the remaining-protection label, in percent.", new AcceptableValueRange<int>(50, 300)));
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
            if (labels != null) labels.Tick(IsEnabled, showProtection.Value);
        }

        private void OnGUI()
        {
            if (labels != null && IsEnabled && showProtection.Value) labels.Draw(labelScale.Value);
        }

        private void PrepareLabels(Camera camera)
        {
            if (labels != null) labels.PrepareFrame(camera, IsEnabled && showProtection.Value);
        }

        private void LateUpdate()
        {
            if (shieldHud != null) shieldHud.Tick(IsEnabled && showHud.Value);
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
