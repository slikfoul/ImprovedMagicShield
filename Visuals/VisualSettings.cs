using BepInEx.Configuration;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    public enum EffectStage { Shield }

    // Kept separate from gameplay protection, duration and network state.
    public sealed class VisualOptions
    {
        public float Light = 1, Glow = 1, Opacity = 1, Size = 1, Density = 1;
        public float ShieldOvality;
        public bool TintEnabled;
        public Color Tint = Color.white;
        public static VisualOptions Original() { return new VisualOptions(); }
    }

    internal sealed class VisualSettings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<int> Color, Glow, Light, Opacity, Ovality;

        public VisualSettings(ConfigFile config)
        {
            const string section = "3 - Magic Shield";
            Enabled = config.Bind(section, "Magic Shield Effect", false,
                new ConfigDescription("Customize shield appearance on this client. Off by default. Disable Magic shield in EffectsControl before enabling this setting. Controls are suspended while EffectsControl shield controls or SubtleShield are active. Turning this off restores the captured appearance; shield protection and the HUD are unaffected.", null,
                    new ConfigurationManagerAttributes { Order = 100 }));
            Color = Integer(config, section, "Color", 269, 360, "Hue: 0/360 red, 60 yellow, 120 green, 180 cyan, 240 blue, 300 magenta. Applied whenever Magic Shield Effect is enabled.");
            Glow = Integer(config, section, "Glow", 72, 100, "Bubble and particle glow. 100 is original brightness.");
            Light = Integer(config, section, "Light brightness", 35, 100, "Shield light intensity. 100 is original brightness; light range is unchanged.");
            Opacity = Integer(config, section, "Opacity", 30, 100, "Bubble and particle opacity. 100 is original opacity; 0 hides the visual shield without changing protection.");
            Ovality = Integer(config, section, "Ovality", 50, 100, "Bubble shape: 0 round, 50 oval, 100 narrow oval. Height and colliders are unchanged.");
        }

        private static ConfigEntry<int> Integer(ConfigFile config, string section, string key, int value, int maximum, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description + " Whole numbers only.", new AcceptableValueRange<int>(0, maximum)));
        }

        public VisualOptions Get()
        {
            return new VisualOptions {
                TintEnabled = true, Tint = UnityEngine.Color.HSVToRGB(Color.Value / 360f, 1, 1),
                Glow = Glow.Value / 100f, Light = Light.Value / 100f, Opacity = Opacity.Value / 100f,
                ShieldOvality = Ovality.Value / 100f
            };
        }
    }

    // ConfigurationManager reads public tag fields by name; no runtime dependency is needed.
    internal sealed class ConfigurationManagerAttributes
    {
        public int Order;
    }
}
