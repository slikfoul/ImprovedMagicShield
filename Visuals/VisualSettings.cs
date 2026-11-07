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
        private readonly bool vengeance;

        public VisualSettings(ConfigFile config)
            : this(config, ShieldAppearance.Protection)
        {
        }

        internal VisualSettings(ConfigFile config, ShieldAppearance appearance)
        {
            vengeance = appearance == ShieldAppearance.NorthernVengeance;
            string section = vengeance ? "4 - Northern Vengeance" : "3 - Magic Shield";
            string toggle = vengeance ? "Northern Vengeance Effect" : "Magic Shield Effect";
            string compatibility = vengeance
                ? "Takes priority over compatible EffectsControl versions. SubtleShield or an unsupported EffectsControl version can suspend this setting."
                : "Takes priority over compatible EffectsControl versions. Turning this off returns the effect to EffectsControl if its Magic shield setting is enabled. SubtleShield or an unsupported EffectsControl version can suspend this setting.";
            Enabled = config.Bind(section, toggle, false,
                new ConfigDescription("Customize shield appearance on this client. Off by default. " + compatibility + " Turning this off restores the captured appearance; shield protection and the HUD are unaffected.", null,
                    new ConfigurationManagerAttributes { Order = 100 }));
            Color = Integer(config, section, "Color", vengeance ? 200 : 269, 360, "Hue: 0/360 red, 60 yellow, 120 green, 180 cyan, 240 blue, 300 magenta. Applied whenever this appearance is enabled.");
            Glow = Integer(config, section, "Glow", 72, 100, "Bubble and particle glow. 100 is original brightness.");
            Light = Integer(config, section, "Light brightness", 35, 100, "Shield light intensity. 100 is original brightness; light range is unchanged.");
            Opacity = Integer(config, section, "Opacity", 30, 100, "Bubble and particle opacity. 100 is original opacity; 0 hides the visual shield without changing its gameplay effect.");
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
                // Both vanilla prefabs use the same mesh: 2.5 for Protection, 3.0 for
                // Vengeance. A factor of 0.90 brings Vengeance to 2.7 (8% larger).
                Size = vengeance ? 0.90f : 1f,
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
