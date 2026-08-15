using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace LovenseRoR2;

internal enum ToyTarget { Toy1, Toy2, Both }
internal enum IntensityPreset { Chill, Normal, Masochist, Custom }

internal static class PluginConfig
{
    internal static ConfigEntry<string> Ip          = null!;
    internal static ConfigEntry<int>    Port        = null!;
    internal static ConfigEntry<bool>   AutoConnect = null!;

    private static ConfigEntry<int> IpOctet1 = null!;
    private static ConfigEntry<int> IpOctet2 = null!;
    private static ConfigEntry<int> IpOctet3 = null!;
    private static ConfigEntry<int> IpOctet4 = null!;
    private static bool _syncingIp;

    internal static ConfigEntry<bool> ShowOverlay = null!;

    internal static ConfigEntry<bool> EnableDamage         = null!;
    internal static ConfigEntry<bool> EnableKillChain      = null!;
    internal static ConfigEntry<bool> EnableLowHealth      = null!;
    internal static ConfigEntry<bool> EnableTeleporter     = null!;
    internal static ConfigEntry<bool> EnableDeath          = null!;
    internal static ConfigEntry<bool> EnableDiffScale      = null!;
    internal static ConfigEntry<bool> EnableEliteProximity = null!;
    internal static ConfigEntry<bool> EnableCrowdPanic     = null!;
    internal static ConfigEntry<bool> EnableItemPickup     = null!;
    internal static ConfigEntry<bool> EnableBossEngage     = null!;
    internal static ConfigEntry<bool> EnableVictory        = null!;

    internal static ConfigEntry<float> MultGlobal     = null!;
    internal static ConfigEntry<float> MultDamage     = null!;
    internal static ConfigEntry<float> MultKillChain  = null!;
    internal static ConfigEntry<float> MultLowHealth  = null!;
    internal static ConfigEntry<float> MultTeleporter = null!;
    internal static ConfigEntry<float> MultDeath      = null!;
    internal static ConfigEntry<float> MultElite      = null!;
    internal static ConfigEntry<float> MultCrowdPanic = null!;
    internal static ConfigEntry<float> MultItemPickup = null!;
    internal static ConfigEntry<float> MultBossEngage = null!;
    internal static ConfigEntry<float> MultVictory    = null!;
    internal static ConfigEntry<int>   KillsToMax     = null!;

    // Tuning
    internal static ConfigEntry<float> LowHealthThreshold  = null!;
    internal static ConfigEntry<float> KillChainWindow     = null!;
    internal static ConfigEntry<float> TaperCap            = null!;
    internal static ConfigEntry<float> EliteProximityRadius = null!;
    internal static ConfigEntry<float> CrowdPanicRadius     = null!;
    internal static ConfigEntry<float> HeartbeatSlowPeriod  = null!;
    internal static ConfigEntry<float> HeartbeatFastPeriod  = null!;

    // Overlay position
    internal static ConfigEntry<int> OverlayX = null!;
    internal static ConfigEntry<int> OverlayY = null!;

    // Toy routing
    internal static ConfigEntry<ToyTarget> ContinuousToyTarget = null!;
    internal static ConfigEntry<ToyTarget> EventToyTarget      = null!;

    // Presets
    internal static ConfigEntry<IntensityPreset> Preset = null!;
    private static bool _applyingPreset;

    private static ConfigEntry<float> CustomMultGlobal          = null!;
    private static ConfigEntry<float> CustomMultDamage          = null!;
    private static ConfigEntry<float> CustomMultKillChain       = null!;
    private static ConfigEntry<float> CustomMultLowHealth       = null!;
    private static ConfigEntry<float> CustomMultTeleporter      = null!;
    private static ConfigEntry<float> CustomMultDeath           = null!;
    private static ConfigEntry<float> CustomMultElite           = null!;
    private static ConfigEntry<float> CustomMultCrowdPanic      = null!;
    private static ConfigEntry<float> CustomMultItemPickup      = null!;
    private static ConfigEntry<float> CustomMultBossEngage      = null!;
    private static ConfigEntry<float> CustomMultVictory         = null!;
    private static ConfigEntry<int>   CustomKillsToMax          = null!;
    private static ConfigEntry<float> CustomLowHealthThreshold  = null!;
    private static ConfigEntry<float> CustomKillChainWindow     = null!;
    private static ConfigEntry<float> CustomTaperCap            = null!;
    private static ConfigEntry<float> CustomEliteProximityRadius = null!;
    private static ConfigEntry<float> CustomCrowdPanicRadius     = null!;
    private static ConfigEntry<float> CustomHeartbeatSlowPeriod  = null!;
    private static ConfigEntry<float> CustomHeartbeatFastPeriod  = null!;

    private struct PresetValues
    {
        public float MultGlobal, MultDamage, MultKillChain, MultLowHealth, MultTeleporter, MultDeath,
                      MultElite, MultCrowdPanic, MultItemPickup, MultBossEngage, MultVictory;
        public int   KillsToMax;
        public float LowHealthThreshold, KillChainWindow, TaperCap,
                      EliteProximityRadius, CrowdPanicRadius, HeartbeatSlowPeriod, HeartbeatFastPeriod;
    }

    private static readonly PresetValues ChillValues = new()
    {
        MultGlobal = 0.6f, MultDamage = 0.6f, MultKillChain = 0.6f, MultLowHealth = 0.6f, MultTeleporter = 0.6f,
        MultDeath = 0.6f, MultElite = 0.6f, MultCrowdPanic = 0.6f, MultItemPickup = 0.6f, MultBossEngage = 0.6f,
        MultVictory = 0.8f, KillsToMax = 20, LowHealthThreshold = 0.25f, KillChainWindow = 2f, TaperCap = 3f,
        EliteProximityRadius = 25f, CrowdPanicRadius = 12f, HeartbeatSlowPeriod = 1.2f, HeartbeatFastPeriod = 0.4f,
    };

    private static readonly PresetValues NormalValues = new()
    {
        MultGlobal = 1f, MultDamage = 1f, MultKillChain = 1f, MultLowHealth = 1f, MultTeleporter = 1f,
        MultDeath = 1f, MultElite = 1f, MultCrowdPanic = 1f, MultItemPickup = 1f, MultBossEngage = 1f,
        MultVictory = 1f, KillsToMax = 15, LowHealthThreshold = 0.25f, KillChainWindow = 2f, TaperCap = 4f,
        EliteProximityRadius = 30f, CrowdPanicRadius = 15f, HeartbeatSlowPeriod = 1.0f, HeartbeatFastPeriod = 0.25f,
    };

    private static readonly PresetValues MasochistValues = new()
    {
        MultGlobal = 1.5f, MultDamage = 1.6f, MultKillChain = 1.4f, MultLowHealth = 1.5f, MultTeleporter = 1.4f,
        MultDeath = 1.6f, MultElite = 1.6f, MultCrowdPanic = 1.5f, MultItemPickup = 1.3f, MultBossEngage = 1.6f,
        MultVictory = 1.6f, KillsToMax = 10, LowHealthThreshold = 0.35f, KillChainWindow = 2.5f, TaperCap = 6f,
        EliteProximityRadius = 40f, CrowdPanicRadius = 20f, HeartbeatSlowPeriod = 0.8f, HeartbeatFastPeriod = 0.15f,
    };

    internal static void Initialize(ConfigFile config)
    {
        Ip          = config.Bind("Connection", "IP",          "192.168.1.4", "Lovense Connect Game Mode IP address. If typing here doesn't work, use the octet sliders below instead.");
        Port        = config.Bind("Connection", "Port",         30010,         "Lovense Connect Game Mode port");
        AutoConnect = config.Bind("Connection", "Auto-Connect", false,         "Automatically connect on game launch");

        var startOctets = ParseOctets(Ip.Value);
        IpOctet1 = config.Bind("Connection", "IP Octet 1", startOctets[0], new ConfigDescription("First number of the IP address, e.g. the 192 in 192.168.1.4. Use this if the IP text field above won't accept typing.", new AcceptableValueRange<int>(0, 255)));
        IpOctet2 = config.Bind("Connection", "IP Octet 2", startOctets[1], new ConfigDescription("Second number of the IP address", new AcceptableValueRange<int>(0, 255)));
        IpOctet3 = config.Bind("Connection", "IP Octet 3", startOctets[2], new ConfigDescription("Third number of the IP address", new AcceptableValueRange<int>(0, 255)));
        IpOctet4 = config.Bind("Connection", "IP Octet 4", startOctets[3], new ConfigDescription("Fourth number of the IP address", new AcceptableValueRange<int>(0, 255)));
        IpOctet1.SettingChanged += (_, _) => RebuildIpFromOctets();
        IpOctet2.SettingChanged += (_, _) => RebuildIpFromOctets();
        IpOctet3.SettingChanged += (_, _) => RebuildIpFromOctets();
        IpOctet4.SettingChanged += (_, _) => RebuildIpFromOctets();
        Ip.SettingChanged += (_, _) => SyncOctetsFromIp();

        ShowOverlay = config.Bind("Display", "Show Intensity Overlay", true, "Show current intensity % on screen");

        EnableDamage         = config.Bind("Features", "Damage",             true, "Vibrate on taking damage");
        EnableKillChain      = config.Bind("Features", "Kill Chain",         true, "Vibrate on kills (chains with rapid kills)");
        EnableLowHealth      = config.Bind("Features", "Low Health Heartbeat", true, "Rhythmic heartbeat pulse below the HP threshold, speeding up as HP drops");
        EnableTeleporter     = config.Bind("Features", "Teleporter Charge",  true, "Ramp up during teleporter charge");
        EnableDeath          = config.Bind("Features", "Death Burst",        true, "Max burst on death");
        EnableDiffScale      = config.Bind("Features", "Difficulty Scaling", true, "Scale intensity with difficulty coefficient");
        EnableEliteProximity = config.Bind("Features", "Elite Proximity",    true, "Hum rises as a nearby elite enemy gets closer");
        EnableCrowdPanic     = config.Bind("Features", "Crowd Panic",        true, "Intensity scales with the number of enemies swarming you");
        EnableItemPickup     = config.Bind("Features", "Item Pickup Buzz",   true, "Short buzz on item pickup, bigger for rarer items");
        EnableBossEngage     = config.Bind("Features", "Boss Engage Pulse",  true, "One-shot pulse when a teleporter boss fight begins");
        EnableVictory        = config.Bind("Features", "Victory Finale",     true, "Celebratory pulse when you win the run");

        var multCfg = new SliderConfig { min = 0f, max = 2f, FormatString = "{0:0.0}x" };
        MultGlobal     = config.Bind("Intensity", "Global Multiplier",               1.0f, "Overall intensity scale");
        MultDamage     = config.Bind("Intensity", "Damage Multiplier",               1.0f, "Damage haptic intensity");
        MultKillChain  = config.Bind("Intensity", "Kill Chain Multiplier",           1.0f, "Kill chain haptic intensity");
        MultLowHealth  = config.Bind("Intensity", "Low Health Multiplier",           1.0f, "Low health heartbeat intensity");
        MultTeleporter = config.Bind("Intensity", "Teleporter Multiplier",           1.0f, "Teleporter charge intensity");
        MultDeath      = config.Bind("Intensity", "Death Multiplier",                1.0f, "Death burst intensity");
        MultElite      = config.Bind("Intensity", "Elite Proximity Multiplier",      1.0f, "Elite proximity intensity");
        MultCrowdPanic = config.Bind("Intensity", "Crowd Panic Multiplier",          1.0f, "Crowd panic intensity");
        MultItemPickup = config.Bind("Intensity", "Item Pickup Multiplier",          1.0f, "Item pickup buzz intensity");
        MultBossEngage = config.Bind("Intensity", "Boss Engage Multiplier",          1.0f, "Boss engage pulse intensity");
        MultVictory    = config.Bind("Intensity", "Victory Multiplier",              1.0f, "Victory finale intensity");
        KillsToMax     = config.Bind("Intensity", "Kill Chain Ramp (kills to 100%)", 15,  "How many kills in a chain to reach 100% intensity");

        LowHealthThreshold   = config.Bind("Tuning", "Low Health Threshold", 0.25f, "HP fraction below which the heartbeat activates (0.0 - 1.0)");
        KillChainWindow      = config.Bind("Tuning", "Kill Chain Window (seconds)", 2f, "Time between kills to maintain a chain");
        TaperCap             = config.Bind("Tuning", "Kill Chain Taper Cap (seconds)", 4f, "Maximum duration of the kill chain taper pattern");
        EliteProximityRadius = config.Bind("Tuning", "Elite Proximity Radius (m)", 30f, "Distance at which a nearby elite starts to register");
        CrowdPanicRadius     = config.Bind("Tuning", "Crowd Panic Radius (m)", 15f, "Distance within which enemies count toward the crowd panic meter");
        HeartbeatSlowPeriod  = config.Bind("Tuning", "Heartbeat Period at Threshold (s)", 1.0f, "Seconds between beats right as the heartbeat kicks in");
        HeartbeatFastPeriod  = config.Bind("Tuning", "Heartbeat Period at 0 HP (s)", 0.25f, "Seconds between beats as HP approaches zero");

        OverlayX = config.Bind("Overlay", "Position X", 10, "Horizontal position of the intensity overlay");
        OverlayY = config.Bind("Overlay", "Position Y", 10, "Vertical position of the intensity overlay");

        ContinuousToyTarget = config.Bind("Toy Routing", "Continuous Signal Toy", ToyTarget.Toy1,
            "Which toy receives the continuous signal (damage, death, heartbeat, teleporter, elite proximity, crowd panic). Falls back to Toy 1 if the chosen toy isn't connected.");
        EventToyTarget = config.Bind("Toy Routing", "Event Pattern Toy", ToyTarget.Toy1,
            "Which toy receives one-shot event patterns (kill chain taper, boss engage, item pickup, victory). Falls back to Toy 1 if the chosen toy isn't connected.");

        Preset = config.Bind("Presets", "Intensity Preset", IntensityPreset.Normal,
            "Chill / Normal / Masochist overwrite the sliders below with a built-in tuning. Custom recalls whatever you last saved with the button below.");

        CustomMultGlobal           = config.Bind("Presets", "Custom.MultGlobal", NormalValues.MultGlobal, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultDamage           = config.Bind("Presets", "Custom.MultDamage", NormalValues.MultDamage, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultKillChain        = config.Bind("Presets", "Custom.MultKillChain", NormalValues.MultKillChain, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultLowHealth        = config.Bind("Presets", "Custom.MultLowHealth", NormalValues.MultLowHealth, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultTeleporter       = config.Bind("Presets", "Custom.MultTeleporter", NormalValues.MultTeleporter, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultDeath            = config.Bind("Presets", "Custom.MultDeath", NormalValues.MultDeath, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultElite            = config.Bind("Presets", "Custom.MultElite", NormalValues.MultElite, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultCrowdPanic       = config.Bind("Presets", "Custom.MultCrowdPanic", NormalValues.MultCrowdPanic, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultItemPickup       = config.Bind("Presets", "Custom.MultItemPickup", NormalValues.MultItemPickup, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultBossEngage       = config.Bind("Presets", "Custom.MultBossEngage", NormalValues.MultBossEngage, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomMultVictory          = config.Bind("Presets", "Custom.MultVictory", NormalValues.MultVictory, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomKillsToMax           = config.Bind("Presets", "Custom.KillsToMax", NormalValues.KillsToMax, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomLowHealthThreshold   = config.Bind("Presets", "Custom.LowHealthThreshold", NormalValues.LowHealthThreshold, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomKillChainWindow      = config.Bind("Presets", "Custom.KillChainWindow", NormalValues.KillChainWindow, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomTaperCap             = config.Bind("Presets", "Custom.TaperCap", NormalValues.TaperCap, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomEliteProximityRadius = config.Bind("Presets", "Custom.EliteProximityRadius", NormalValues.EliteProximityRadius, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomCrowdPanicRadius     = config.Bind("Presets", "Custom.CrowdPanicRadius", NormalValues.CrowdPanicRadius, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomHeartbeatSlowPeriod  = config.Bind("Presets", "Custom.HeartbeatSlowPeriod", NormalValues.HeartbeatSlowPeriod, new ConfigDescription("", null, "HideFromConfigManager"));
        CustomHeartbeatFastPeriod  = config.Bind("Presets", "Custom.HeartbeatFastPeriod", NormalValues.HeartbeatFastPeriod, new ConfigDescription("", null, "HideFromConfigManager"));

        RegisterOptions(multCfg);

        MultGlobal.SettingChanged           += MarkCustom;
        MultDamage.SettingChanged           += MarkCustom;
        MultKillChain.SettingChanged        += MarkCustom;
        MultLowHealth.SettingChanged        += MarkCustom;
        MultTeleporter.SettingChanged       += MarkCustom;
        MultDeath.SettingChanged            += MarkCustom;
        MultElite.SettingChanged            += MarkCustom;
        MultCrowdPanic.SettingChanged       += MarkCustom;
        MultItemPickup.SettingChanged       += MarkCustom;
        MultBossEngage.SettingChanged       += MarkCustom;
        MultVictory.SettingChanged          += MarkCustom;
        KillsToMax.SettingChanged           += MarkCustom;
        LowHealthThreshold.SettingChanged   += MarkCustom;
        KillChainWindow.SettingChanged      += MarkCustom;
        TaperCap.SettingChanged             += MarkCustom;
        EliteProximityRadius.SettingChanged += MarkCustom;
        CrowdPanicRadius.SettingChanged     += MarkCustom;
        HeartbeatSlowPeriod.SettingChanged  += MarkCustom;
        HeartbeatFastPeriod.SettingChanged  += MarkCustom;

        Preset.SettingChanged += (_, _) =>
        {
            if (_applyingPreset) return;
            ApplyPreset(Preset.Value);
        };
    }

    private static void MarkCustom(object sender, EventArgs e)
    {
        if (_applyingPreset || Preset.Value == IntensityPreset.Custom) return;
        _applyingPreset = true;
        Preset.Value = IntensityPreset.Custom;
        _applyingPreset = false;
    }

    private static void ApplyPreset(IntensityPreset preset)
    {
        PresetValues v = preset switch
        {
            IntensityPreset.Chill     => ChillValues,
            IntensityPreset.Masochist => MasochistValues,
            IntensityPreset.Custom    => LoadCustomValues(),
            _                         => NormalValues,
        };

        _applyingPreset = true;
        MultGlobal.Value = v.MultGlobal;
        MultDamage.Value = v.MultDamage;
        MultKillChain.Value = v.MultKillChain;
        MultLowHealth.Value = v.MultLowHealth;
        MultTeleporter.Value = v.MultTeleporter;
        MultDeath.Value = v.MultDeath;
        MultElite.Value = v.MultElite;
        MultCrowdPanic.Value = v.MultCrowdPanic;
        MultItemPickup.Value = v.MultItemPickup;
        MultBossEngage.Value = v.MultBossEngage;
        MultVictory.Value = v.MultVictory;
        KillsToMax.Value = v.KillsToMax;
        LowHealthThreshold.Value = v.LowHealthThreshold;
        KillChainWindow.Value = v.KillChainWindow;
        TaperCap.Value = v.TaperCap;
        EliteProximityRadius.Value = v.EliteProximityRadius;
        CrowdPanicRadius.Value = v.CrowdPanicRadius;
        HeartbeatSlowPeriod.Value = v.HeartbeatSlowPeriod;
        HeartbeatFastPeriod.Value = v.HeartbeatFastPeriod;
        _applyingPreset = false;
    }

    private static PresetValues LoadCustomValues() => new()
    {
        MultGlobal = CustomMultGlobal.Value, MultDamage = CustomMultDamage.Value, MultKillChain = CustomMultKillChain.Value,
        MultLowHealth = CustomMultLowHealth.Value, MultTeleporter = CustomMultTeleporter.Value, MultDeath = CustomMultDeath.Value,
        MultElite = CustomMultElite.Value, MultCrowdPanic = CustomMultCrowdPanic.Value, MultItemPickup = CustomMultItemPickup.Value,
        MultBossEngage = CustomMultBossEngage.Value, MultVictory = CustomMultVictory.Value, KillsToMax = CustomKillsToMax.Value,
        LowHealthThreshold = CustomLowHealthThreshold.Value, KillChainWindow = CustomKillChainWindow.Value, TaperCap = CustomTaperCap.Value,
        EliteProximityRadius = CustomEliteProximityRadius.Value, CrowdPanicRadius = CustomCrowdPanicRadius.Value,
        HeartbeatSlowPeriod = CustomHeartbeatSlowPeriod.Value, HeartbeatFastPeriod = CustomHeartbeatFastPeriod.Value,
    };

    internal static void SaveCustomPreset()
    {
        CustomMultGlobal.Value = MultGlobal.Value;
        CustomMultDamage.Value = MultDamage.Value;
        CustomMultKillChain.Value = MultKillChain.Value;
        CustomMultLowHealth.Value = MultLowHealth.Value;
        CustomMultTeleporter.Value = MultTeleporter.Value;
        CustomMultDeath.Value = MultDeath.Value;
        CustomMultElite.Value = MultElite.Value;
        CustomMultCrowdPanic.Value = MultCrowdPanic.Value;
        CustomMultItemPickup.Value = MultItemPickup.Value;
        CustomMultBossEngage.Value = MultBossEngage.Value;
        CustomMultVictory.Value = MultVictory.Value;
        CustomKillsToMax.Value = KillsToMax.Value;
        CustomLowHealthThreshold.Value = LowHealthThreshold.Value;
        CustomKillChainWindow.Value = KillChainWindow.Value;
        CustomTaperCap.Value = TaperCap.Value;
        CustomEliteProximityRadius.Value = EliteProximityRadius.Value;
        CustomCrowdPanicRadius.Value = CrowdPanicRadius.Value;
        CustomHeartbeatSlowPeriod.Value = HeartbeatSlowPeriod.Value;
        CustomHeartbeatFastPeriod.Value = HeartbeatFastPeriod.Value;
        LovensePlugin.Logger.LogInfo("Lovense: current tuning saved as the Custom preset.");
    }

    private static int[] ParseOctets(string ip)
    {
        var parts = ip.Split('.');
        var result = new int[4];
        for (int i = 0; i < 4; i++)
        {
            int v = 0;
            if (i < parts.Length) int.TryParse(parts[i], out v);
            result[i] = Math.Max(0, Math.Min(255, v));
        }
        return result;
    }

    private static void RebuildIpFromOctets()
    {
        if (_syncingIp) return;
        _syncingIp = true;
        Ip.Value = $"{IpOctet1.Value}.{IpOctet2.Value}.{IpOctet3.Value}.{IpOctet4.Value}";
        _syncingIp = false;
    }

    private static void SyncOctetsFromIp()
    {
        if (_syncingIp) return;
        _syncingIp = true;
        var o = ParseOctets(Ip.Value);
        IpOctet1.Value = o[0];
        IpOctet2.Value = o[1];
        IpOctet3.Value = o[2];
        IpOctet4.Value = o[3];
        _syncingIp = false;
    }

    private static void RegisterOptions(SliderConfig multCfg)
    {
        ModSettingsManager.AddOption(new StringInputFieldOption(Ip));
        ModSettingsManager.AddOption(new IntSliderOption(IpOctet1, new IntSliderConfig { min = 0, max = 255 }));
        ModSettingsManager.AddOption(new IntSliderOption(IpOctet2, new IntSliderConfig { min = 0, max = 255 }));
        ModSettingsManager.AddOption(new IntSliderOption(IpOctet3, new IntSliderConfig { min = 0, max = 255 }));
        ModSettingsManager.AddOption(new IntSliderOption(IpOctet4, new IntSliderConfig { min = 0, max = 255 }));
        ModSettingsManager.AddOption(new IntSliderOption(Port, new IntSliderConfig { min = 1024, max = 65535 }));
        ModSettingsManager.AddOption(new CheckBoxOption(AutoConnect));
        ModSettingsManager.AddOption(new GenericButtonOption("Connect",    "Connection", "Connect to the toy(s)", "Connect",    LovensePlugin.Connect));
        ModSettingsManager.AddOption(new GenericButtonOption("Disconnect", "Connection", "Disconnect the toy(s)", "Disconnect", LovensePlugin.Disconnect));

        ModSettingsManager.AddOption(new CheckBoxOption(ShowOverlay));

        ModSettingsManager.AddOption(new CheckBoxOption(EnableDamage));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableKillChain));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableLowHealth));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableTeleporter));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableDeath));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableDiffScale));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableEliteProximity));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableCrowdPanic));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableItemPickup));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableBossEngage));
        ModSettingsManager.AddOption(new CheckBoxOption(EnableVictory));

        ModSettingsManager.AddOption(new SliderOption(MultGlobal,     multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultDamage,     multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultKillChain,  multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultLowHealth,  multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultTeleporter, multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultDeath,      multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultElite,      multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultCrowdPanic, multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultItemPickup, multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultBossEngage, multCfg));
        ModSettingsManager.AddOption(new SliderOption(MultVictory,    multCfg));
        ModSettingsManager.AddOption(new IntSliderOption(KillsToMax, new IntSliderConfig { min = 2, max = 30 }));

        ModSettingsManager.AddOption(new SliderOption(LowHealthThreshold, new SliderConfig { min = 0.05f, max = 0.5f,  FormatString = "{0:P0}" }));
        ModSettingsManager.AddOption(new SliderOption(KillChainWindow,    new SliderConfig { min = 0.5f,  max = 5f,    FormatString = "{0:0.0}s" }));
        ModSettingsManager.AddOption(new SliderOption(TaperCap,           new SliderConfig { min = 1f,    max = 10f,   FormatString = "{0:0.0}s" }));
        ModSettingsManager.AddOption(new SliderOption(EliteProximityRadius, new SliderConfig { min = 5f,  max = 60f,   FormatString = "{0:0}m" }));
        ModSettingsManager.AddOption(new SliderOption(CrowdPanicRadius,     new SliderConfig { min = 5f,  max = 40f,   FormatString = "{0:0}m" }));
        ModSettingsManager.AddOption(new SliderOption(HeartbeatSlowPeriod,  new SliderConfig { min = 0.4f, max = 2.0f, FormatString = "{0:0.00}s" }));
        ModSettingsManager.AddOption(new SliderOption(HeartbeatFastPeriod,  new SliderConfig { min = 0.1f, max = 0.6f, FormatString = "{0:0.00}s" }));

        ModSettingsManager.AddOption(new IntSliderOption(OverlayX, new IntSliderConfig { min = 0, max = 1920 }));
        ModSettingsManager.AddOption(new IntSliderOption(OverlayY, new IntSliderConfig { min = 0, max = 1080 }));

        ModSettingsManager.AddOption(new ChoiceOption(ContinuousToyTarget));
        ModSettingsManager.AddOption(new ChoiceOption(EventToyTarget));

        ModSettingsManager.AddOption(new ChoiceOption(Preset));
        ModSettingsManager.AddOption(new GenericButtonOption("Save Custom Preset", "Presets", "Save the current sliders into the Custom preset slot", "Save", SaveCustomPreset));
    }
}
