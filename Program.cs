using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LovenseRoR2;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.rune580.riskofoptions")]
public class LovensePlugin : BaseUnityPlugin
{
    public const string PluginGUID    = "com.mordraga.lovenserisk";
    public const string PluginName    = "LovenseRoR2";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger = null!;
    internal static string? ToyId;

    // Vibration sources — 0-20 scale, combined each frame
    internal static float DamageSource;
    internal static float DeathSource;

    // Current intensity exposed for overlay
    internal static int CurrentPercent;

    private int   _lastSentIntensity = -1;
    private bool  _wasAlive;

    internal static float KillChainSource;

    private static int   _killChainCount;
    private static float _killChainExpiry;
    private static float _patternEndTime    = -1f;
    private static float _taperStartTime;
    private static int   _taperStartPercent;
    private const  float KillChainWindow = 3f;

    private static readonly HttpClient Http = null!;
    private static string BaseUrl = "";

    private const float DamageDecay = 15f;
    private const float DeathDecay  =  4f;

    // ── Config ────────────────────────────────────────────────────────────────
    private static ConfigEntry<string> _cfgIp          = null!;
    private static ConfigEntry<int>    _cfgPort        = null!;
    private static ConfigEntry<bool>   _cfgAutoConnect = null!;

    // Display
    private static ConfigEntry<bool> _cfgShowOverlay = null!;

    // Feature toggles
    private static ConfigEntry<bool> _cfgEnableDamage     = null!;
    private static ConfigEntry<bool> _cfgEnableKillChain  = null!;
    private static ConfigEntry<bool> _cfgEnableLowHealth  = null!;
    private static ConfigEntry<bool> _cfgEnableTeleporter = null!;
    private static ConfigEntry<bool> _cfgEnableDeath      = null!;
    private static ConfigEntry<bool> _cfgEnableDiffScale  = null!;

    // Intensity multipliers
    private static ConfigEntry<float> _cfgMultGlobal     = null!;
    private static ConfigEntry<float> _cfgMultDamage     = null!;
    private static ConfigEntry<float> _cfgMultKillChain  = null!;
    private static ConfigEntry<float> _cfgMultLowHealth  = null!;
    private static ConfigEntry<float> _cfgMultTeleporter = null!;
    private static ConfigEntry<float> _cfgMultDeath      = null!;

    static LovensePlugin()
    {
        System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-platform", "LovenseRoR2");
        Http = http;
    }

    private void Awake()
    {
        Logger = base.Logger;

        // Connection
        _cfgIp          = Config.Bind("Connection", "IP",          "192.168.1.4", "Lovense Connect Game Mode IP address");
        _cfgPort        = Config.Bind("Connection", "Port",         30010,         "Lovense Connect Game Mode port");
        _cfgAutoConnect = Config.Bind("Connection", "Auto-Connect", false,         "Automatically connect on game launch");
        BaseUrl         = $"https://{_cfgIp.Value}:{_cfgPort.Value}/command";
        _cfgIp.SettingChanged   += (_, _) => BaseUrl = $"https://{_cfgIp.Value}:{_cfgPort.Value}/command";
        _cfgPort.SettingChanged += (_, _) => BaseUrl = $"https://{_cfgIp.Value}:{_cfgPort.Value}/command";

        // Display
        _cfgShowOverlay = Config.Bind("Display", "Show Intensity Overlay", true, "Show current intensity % on screen");

        // Feature toggles
        _cfgEnableDamage     = Config.Bind("Features", "Damage",              true, "Vibrate on taking damage");
        _cfgEnableKillChain  = Config.Bind("Features", "Kill Chain",          true, "Vibrate on kills (chains with rapid kills)");
        _cfgEnableLowHealth  = Config.Bind("Features", "Low Health Hum",      true, "Hum when below 25% HP");
        _cfgEnableTeleporter = Config.Bind("Features", "Teleporter Charge",   true, "Ramp up during teleporter charge");
        _cfgEnableDeath      = Config.Bind("Features", "Death Burst",         true, "Max burst on death");
        _cfgEnableDiffScale  = Config.Bind("Features", "Difficulty Scaling",  true, "Scale intensity with difficulty coefficient");

        // Multipliers
        var multCfg = new SliderConfig { min = 0f, max = 2f, FormatString = "{0:0.0}x" };
        _cfgMultGlobal     = Config.Bind("Intensity", "Global Multiplier",     1.0f, "Overall intensity scale");
        _cfgMultDamage     = Config.Bind("Intensity", "Damage Multiplier",     1.0f, "Damage haptic intensity");
        _cfgMultKillChain  = Config.Bind("Intensity", "Kill Chain Multiplier", 1.0f, "Kill chain haptic intensity");
        _cfgMultLowHealth  = Config.Bind("Intensity", "Low Health Multiplier", 1.0f, "Low health hum intensity");
        _cfgMultTeleporter = Config.Bind("Intensity", "Teleporter Multiplier", 1.0f, "Teleporter charge intensity");
        _cfgMultDeath      = Config.Bind("Intensity", "Death Multiplier",      1.0f, "Death burst intensity");

        // RiskOfOptions — Connection
        ModSettingsManager.AddOption(new StringInputFieldOption(_cfgIp));
        ModSettingsManager.AddOption(new IntSliderOption(_cfgPort, new IntSliderConfig { min = 1024, max = 65535 }));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgAutoConnect));
        ModSettingsManager.AddOption(new GenericButtonOption("Connect",    "Connection", "Connect to the toy", "Connect",    Connect));
        ModSettingsManager.AddOption(new GenericButtonOption("Disconnect", "Connection", "Disconnect the toy", "Disconnect", Disconnect));

        // RiskOfOptions — Display
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgShowOverlay));

        // RiskOfOptions — Features
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableDamage));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableKillChain));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableLowHealth));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableTeleporter));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableDeath));
        ModSettingsManager.AddOption(new CheckBoxOption(_cfgEnableDiffScale));

        // RiskOfOptions — Multipliers
        ModSettingsManager.AddOption(new SliderOption(_cfgMultGlobal,     multCfg));
        ModSettingsManager.AddOption(new SliderOption(_cfgMultDamage,     multCfg));
        ModSettingsManager.AddOption(new SliderOption(_cfgMultKillChain,  multCfg));
        ModSettingsManager.AddOption(new SliderOption(_cfgMultLowHealth,  multCfg));
        ModSettingsManager.AddOption(new SliderOption(_cfgMultTeleporter, multCfg));
        ModSettingsManager.AddOption(new SliderOption(_cfgMultDeath,      multCfg));

        new Harmony(PluginGUID).PatchAll();
        if (_cfgAutoConnect.Value) _ = InitAsync();
    }

    private void Update()
    {
        if (ToyId == null) return;

        float dt = Time.deltaTime;

        DamageSource = Mathf.Max(0, DamageSource - dt * DamageDecay);
        DeathSource  = Mathf.Max(0, DeathSource  - dt * DeathDecay);

        // when chain expires, fire a PatternV2 taper lasting the same duration as the chain
        if (_killChainCount > 0 && Time.time > _killChainExpiry)
        {
            int   peakPos        = Mathf.Min(_killChainCount * 10, 100);
            float taperDurationS = Mathf.Min(_killChainCount * KillChainWindow / 2f, 6f);
            int   taperDurationMs = Mathf.RoundToInt(taperDurationS * 1000f);

            _taperStartTime    = Time.time;
            _taperStartPercent = peakPos;
            _patternEndTime    = Time.time + taperDurationS;

            _ = TrySendPatternV2(new object[]
            {
                new { ts = 0,              pos = peakPos },
                new { ts = taperDurationMs, pos = 0 },
            });

            _killChainCount = 0;
            KillChainSource = 0f;
        }

        float lowHealthSource = 0f;
        float teleSource      = 0f;

        if (Run.instance != null)
        {
            var localBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;

            bool isAlive = localBody != null && localBody.healthComponent?.alive == true;
            if (_wasAlive && !isAlive && _cfgEnableDeath.Value)
                DeathSource = 20f * _cfgMultDeath.Value;
            _wasAlive = isAlive;

            if (_cfgEnableLowHealth.Value && localBody?.healthComponent != null)
            {
                float hpFrac = localBody.healthComponent.combinedHealthFraction;
                if (hpFrac < 0.25f)
                    lowHealthSource = Mathf.Lerp(0f, 8f, 1f - hpFrac / 0.25f) * _cfgMultLowHealth.Value;
            }

            if (_cfgEnableTeleporter.Value)
            {
                var tele = TeleporterInteraction.instance;
                if (tele != null && !tele.isCharged && tele.chargeFraction > 0f)
                    teleSource = tele.chargeFraction * 15f * _cfgMultTeleporter.Value;
            }
        }
        else
        {
            _wasAlive = false;
        }

        float diffScale = (_cfgEnableDiffScale.Value && Run.instance != null)
            ? 1f + Mathf.Log(Mathf.Max(1f, Run.instance.difficultyCoefficient)) * 0.2f
            : 1f;

        if (Time.time < _patternEndTime)
        {
            float t = (Time.time - _taperStartTime) / (_patternEndTime - _taperStartTime);
            CurrentPercent = Mathf.RoundToInt(Mathf.Lerp(_taperStartPercent, 0f, Mathf.Clamp01(t)));
            return;
        }
        if (_patternEndTime > 0f)
        {
            _patternEndTime    = -1f;
            _lastSentIntensity = -1;
        }

        float damage    = _cfgEnableDamage.Value    ? DamageSource    * _cfgMultDamage.Value    : 0f;
        float death     = _cfgEnableDeath.Value     ? DeathSource                               : 0f;
        float killChain = _cfgEnableKillChain.Value ? KillChainSource * _cfgMultKillChain.Value : 0f;

        int intensity = Mathf.RoundToInt(
            Mathf.Clamp((damage + death + killChain + lowHealthSource + teleSource) * diffScale * _cfgMultGlobal.Value, 0f, 20f)
        );

        CurrentPercent = Mathf.RoundToInt(intensity / 20f * 100f);

        if (intensity != _lastSentIntensity)
        {
            _lastSentIntensity = intensity;
            _ = TrySendVibrate(intensity);
        }
    }

    private void OnGUI()
    {
        if (!_cfgShowOverlay.Value || ToyId == null) return;
        GUI.Label(new Rect(10, 10, 160, 24), $"Lovense: {CurrentPercent}%");
    }

    internal static void OnDamage(float damage, float maxHp, bool isDot)
    {
        if (!_cfgEnableDamage.Value) return;
        float add = Mathf.Clamp01(damage / maxHp) * 20f;
        DamageSource = Mathf.Clamp(DamageSource + (isDot ? add * 0.4f : add), 0f, 20f);
    }

    internal static void OnKill()
    {
        if (ToyId == null || !_cfgEnableKillChain.Value) return;

        if (Time.time > _killChainExpiry)
        {
            _killChainCount  = 0;
            _killChainExpiry = Time.time; // normalize so +=  starts from now
        }

        _killChainCount++;
        _killChainExpiry += KillChainWindow; // each kill adds 3s (4 kills = 12s total)

        // +10% per kill, caps at 100% (kill 10); holds for KillChainWindow seconds
        KillChainSource = Mathf.Min(_killChainCount * 2f, 20f);
    }

    private static void Connect() => _ = InitAsync();

    private static void Disconnect()
    {
        ToyId = null;
        DamageSource = DeathSource = KillChainSource = 0f;
        _killChainCount = 0;
        _patternEndTime = -1f;
        CurrentPercent  = 0;
        Logger.LogInfo("Lovense disconnected.");
    }

    private static async Task InitAsync()
    {
        // Block Update from sending Vibrate:0 during the connection ramp.
        // Must be set before the first await while we're still on the main thread.
        _patternEndTime = Time.time + 1.1f;
        try
        {
            var resp = await SendCommand(new { command = "GetToys" });
            var toysStr = resp["data"]?["toys"]?.ToString();
            if (toysStr == null)
            {
                Logger.LogError($"Lovense init failed: unexpected response: {resp}");
                return;
            }
            var toys = JObject.Parse(toysStr);
            if (!toys.Properties().Any())
            {
                Logger.LogWarning("Lovense: no toys found. Is the toy connected?");
                return;
            }
            ToyId = toys.Properties().First().Name;
            Logger.LogInfo($"Lovense connected: {ToyId}");

            await TrySendPatternV2(new object[]
            {
                new { ts =   0, pos =   0 },
                new { ts = 200, pos =  30 },
                new { ts = 400, pos =  60 },
                new { ts = 600, pos = 100 },
                new { ts = 900, pos =   0 },
            });
        }
        catch (HttpRequestException e)
        {
            _patternEndTime = -1f;
            Logger.LogError($"Lovense: could not reach {BaseUrl} — is Lovense Connect running with Game Mode on? ({e.Message})");
        }
        catch (Exception e)
        {
            _patternEndTime = -1f;
            Logger.LogError($"Lovense init failed: {e}");
        }
    }

    internal static async Task TrySendPatternV2(object[] actions)
    {
        try
        {
            await SendCommand(new { command = "PatternV2", type = "Setup", actions, apiVer = 1 });
            await SendCommand(new { command = "PatternV2", type = "Play",  apiVer = 1 });
        }
        catch (Exception e) { Logger.LogError($"Lovense PatternV2 failed: {e}"); }
    }

    private static async Task TrySendVibrate(int intensity)
    {
        try { await SendCommand(new
        {
            toy = ToyId, command = "Function", action = $"Vibrate:{intensity}",
            timeSec = 0, loopRunningSec = 0, loopPauseSec = 0, apiVer = 1
        }); }
        catch (Exception e) { Logger.LogWarning($"Lovense vibrate failed: {e.Message}"); }
    }

    private static async Task<JObject> SendCommand(object payload)
    {
        var json    = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var result  = await Http.PostAsync(BaseUrl, content);
        var body    = await result.Content.ReadAsStringAsync();
        return JObject.Parse(body);
    }
}

[HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamage))]
class PatchTakeDamage
{
    static void Postfix(HealthComponent __instance, DamageInfo damageInfo)
    {
        var localUser = LocalUserManager.GetFirstLocalUser();
        if (localUser?.cachedBody != __instance.body) return;

        bool isDot = damageInfo.dotIndex != DotController.DotIndex.None;
        LovensePlugin.OnDamage(damageInfo.damage, __instance.fullCombinedHealth, isDot);
    }
}

[HarmonyPatch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterDeath))]
class PatchOnKill
{
    static void Postfix(DamageReport damageReport)
    {
        if (damageReport == null) return;
        if (damageReport.attackerTeamIndex != TeamIndex.Player) return;
        if (damageReport.victimTeamIndex   == TeamIndex.Player) return;

        LovensePlugin.OnKill();
    }
}
