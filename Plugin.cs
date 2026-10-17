using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ImprovedMagicShield
{
    [BepInPlugin(PluginGuid, "Improved Magic Shield", PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Slikfoul.ImprovedMagicShield";
        public const string PluginVersion = "0.0.1";
        private Harmony harmony;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> showProtection;
        private static ConfigEntry<bool> showHud;
        private static ConfigEntry<int> labelScale;
        private static bool hooksReady;
        internal static bool IsEnabled { get { return hooksReady && modEnabled != null && modEnabled.Value; } }
        private ShieldLabels labels;
        private ShieldHud shieldHud;

        private void Awake()
        {
            modEnabled = Config.Bind("1 - General", "Enabled", true, "Enable improved shield refreshes and displays. Disabling restores vanilla behavior for subsequent casts without removing active shields.");
            showProtection = Config.Bind("2 - Display", "Show remaining protection", false, "Show remaining shield protection above eligible characters. Other characters need this mod on their owning peer to supply current protection.");
            showHud = Config.Bind("2 - Display", "Show local shield bar", true, "Show your own remaining / full shield protection in the lower resource HUD. Independent of the overhead labels.");
            labelScale = Config.Bind("2 - Display", "Label scale (%)", 100, new ConfigDescription("Size of the remaining-protection label, in percent.", new AcceptableValueRange<int>(50, 300)));
            try
            {
                ShieldRefresh.VerifyFields();
                harmony = new Harmony(PluginGuid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                labels = new ShieldLabels();
                shieldHud = new ShieldHud();
                hooksReady = true;
                Logger.LogInfo("Improved Magic Shield " + PluginVersion + " loaded. Shield refreshes compare new full protection with current remaining protection.");
            }
            catch (Exception error)
            {
                if (harmony != null) harmony.UnpatchSelf();
                hooksReady = false;
                Logger.LogError("Improved Magic Shield disabled: incompatible status-effect hooks. " + error);
            }
        }

        private void Update()
        {
            if (labels != null) labels.Tick(IsEnabled, showProtection.Value);
        }

        private void OnGUI()
        {
            if (labels != null && IsEnabled && showProtection.Value) labels.Draw(labelScale.Value);
        }

        private void LateUpdate()
        {
            if (shieldHud != null) shieldHud.Tick(IsEnabled && showHud.Value);
        }

        private void OnDestroy()
        {
            hooksReady = false;
            if (labels != null) labels.Clear();
            if (shieldHud != null) shieldHud.Destroy();
            if (harmony != null) harmony.UnpatchSelf();
        }
    }
}
