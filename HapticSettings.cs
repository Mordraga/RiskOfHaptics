using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace LovenseRoR2;

internal static class HapticSettings
{
    internal static ConfigEntry<PriorityProfile> Profile = null!;
    internal static ConfigEntry<int> MaximumPercent = null!;
    internal static ConfigEntry<bool> PreviewOnly = null!;
    internal static ConfigEntry<HapticEffect> PreviewEffect = null!;
    private static readonly Dictionary<HapticEffect, ConfigEntry<int>> Priorities = new();
    private static readonly Dictionary<HapticEffect, ConfigEntry<int>> CustomPriorities = new();
    private static bool _applying;

    internal static string Label(HapticEffect effect) => effect switch
    {
        HapticEffect.KillChain => "Kill chain", HapticEffect.LowHealth => "Low health",
        HapticEffect.EliteProximity => "Elite proximity", HapticEffect.CrowdPanic => "Crowd panic",
        HapticEffect.ItemPickup => "Item pickup", HapticEffect.BossEngage => "Boss engagement",
        _ => effect.ToString(),
    };

    internal static void Initialize(ConfigFile config)
    {
        Profile = config.Bind("Priorities", "Playstyle Preset", PriorityProfile.Balanced,
            "Choose by build and combat loop, not survivor identity. RapidFire: Commando, nailgun MUL-T. MobileKiting: Huntress, evasive builds. PrecisionBurst: Railgunner, Bandit, rebar MUL-T. AbilityBurst: Artificer, caster Void Fiend. MeleeMomentum: Mercenary, Loader, saw MUL-T. ZoneControl: Engineer, Captain. Attrition: Acrid, damage-over-time builds. HealthTrading: REX, health-spending Void Fiend. Changes priorities only; strength and timing remain yours. Custom recalls saved priorities.");
        MaximumPercent = config.Bind("Controls", "Maximum Intensity Percent", 100,
            new ConfigDescription("Hard limit for every effect and preview. Rounded down to a supported 5% step.", new AcceptableValueRange<int>(0, 100)));
        PreviewOnly = config.Bind("Controls", "Preview Only", false,
            "Stop device output and show effects on two virtual devices in the overlay. Works without a connection. Turn off to resume hardware output.");
        PreviewEffect = config.Bind("Preview", "Effect", HapticEffect.ItemPickup, "Effect to audition using current routing, priorities, toggles, and intensity settings.");
        foreach (HapticEffect effect in Enum.GetValues(typeof(HapticEffect)))
        {
            int initial = PriorityProfiles.Get(Profile.Value, effect);
            var entry = config.Bind("Priorities", Label(effect), initial,
                new ConfigDescription("Higher numbers override lower numbers on the same device. Equal priorities keep the current effect. Suppressed one-shots are discarded.", new AcceptableValueRange<int>(0, 100)));
            Priorities.Add(effect, entry);
            CustomPriorities.Add(effect, config.Bind("Priorities", "Custom." + effect, initial,
                new ConfigDescription("Saved custom priority", new AcceptableValueRange<int>(0, 100), "HideFromConfigManager")));
            entry.SettingChanged += (_, _) =>
            {
                if (_applying) return;
                _applying = true;
                Profile.Value = PriorityProfile.Custom;
                _applying = false;
            };
        }
        Profile.SettingChanged += (_, _) => { if (!_applying) ApplyProfile(); };
        PreviewOnly.SettingChanged += (_, _) => LovensePlugin.StopAndReset();
        ModSettingsManager.AddOption(new ChoiceOption(Profile));
        foreach (var entry in Priorities.Values)
            ModSettingsManager.AddOption(new IntSliderOption(entry, new IntSliderConfig { min = 0, max = 100 }));
        ModSettingsManager.AddOption(new GenericButtonOption("Save Custom Priorities", "Priorities", "Save all current priorities to the Custom slot", "Save", SaveCustom));
        ModSettingsManager.AddOption(new IntSliderOption(MaximumPercent, new IntSliderConfig { min = 0, max = 100 }));
        ModSettingsManager.AddOption(new CheckBoxOption(PreviewOnly));
        ModSettingsManager.AddOption(new GenericButtonOption("Pause or Resume", "Controls", "Pause or resume all feedback. F8 also toggles feedback; Escape only navigates game menus.", "Toggle", LovensePlugin.TogglePause));
        ModSettingsManager.AddOption(new ChoiceOption(PreviewEffect));
        ModSettingsManager.AddOption(new GenericButtonOption("Preview Effect", "Preview", "Play the selected effect. Enable Preview Only for an overlay audition without device output.", "Play", LovensePlugin.Preview));
    }

    private static void ApplyProfile()
    {
        _applying = true;
        try
        {
            foreach (var pair in Priorities)
                pair.Value.Value = Profile.Value == PriorityProfile.Custom
                    ? CustomPriorities[pair.Key].Value : PriorityProfiles.Get(Profile.Value, pair.Key);
        }
        finally { _applying = false; }
    }

    private static void SaveCustom()
    {
        foreach (var pair in Priorities) CustomPriorities[pair.Key].Value = pair.Value.Value;
        LovensePlugin.Logger.LogInfo("Current priorities saved to Custom.");
    }

    internal static int Priority(HapticEffect effect) => Priorities[effect].Value;

    internal static float Multiplier(HapticEffect effect) => Math.Max(0, effect switch
    {
        HapticEffect.Damage => PluginConfig.MultDamage.Value,
        HapticEffect.KillChain => PluginConfig.MultKillChain.Value,
        HapticEffect.LowHealth => PluginConfig.MultLowHealth.Value,
        HapticEffect.Teleporter => PluginConfig.MultTeleporter.Value,
        HapticEffect.Death => PluginConfig.MultDeath.Value,
        HapticEffect.EliteProximity => PluginConfig.MultElite.Value,
        HapticEffect.CrowdPanic => PluginConfig.MultCrowdPanic.Value,
        HapticEffect.ItemPickup => PluginConfig.MultItemPickup.Value,
        HapticEffect.BossEngage => PluginConfig.MultBossEngage.Value,
        HapticEffect.Victory => PluginConfig.MultVictory.Value,
        _ => 0,
    });

    internal static bool Enabled(HapticEffect effect) => Multiplier(effect) > 0 && (effect switch
    {
        HapticEffect.Damage => PluginConfig.EnableDamage.Value,
        HapticEffect.KillChain => PluginConfig.EnableKillChain.Value,
        HapticEffect.LowHealth => PluginConfig.EnableLowHealth.Value,
        HapticEffect.Teleporter => PluginConfig.EnableTeleporter.Value,
        HapticEffect.Death => PluginConfig.EnableDeath.Value,
        HapticEffect.EliteProximity => PluginConfig.EnableEliteProximity.Value,
        HapticEffect.CrowdPanic => PluginConfig.EnableCrowdPanic.Value,
        HapticEffect.ItemPickup => PluginConfig.EnableItemPickup.Value,
        HapticEffect.BossEngage => PluginConfig.EnableBossEngage.Value,
        HapticEffect.Victory => PluginConfig.EnableVictory.Value,
        _ => false,
    });
}
