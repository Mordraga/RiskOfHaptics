using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RoR2;
using System;
using System.Collections.Generic;
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
    public const string PluginGUID = "com.mordraga.lovenserisk";
    public const string PluginName = "LovenseRoR2";
    public const string PluginVersion = "1.1.3";

    internal static new ManualLogSource Logger = null!;
    internal static string? ToyId;
    internal static string? ToyId2;
    internal static bool Paused;
    internal static string ConnectionStatus = "Disconnected";
    internal static readonly int[] DevicePercent = new int[2];
    private static readonly HapticEngine[] Engines = { new HapticEngine(), new HapticEngine() };
    private static readonly Dictionary<string, HapticOutput> Outputs = new();
    private static Task _stopTask = Task.CompletedTask;
    private static bool _connectionBusy;
    private static int _connectionVersion;
    private static float _damageSource;
    private static bool _wasAlive;
    private static int _killChainCount;
    private static float _killChainExpiry;
    private static float _killChainSource;
    private static string BaseUrl = "";
    private static readonly HttpClient Http;
    private Harmony? _harmony;
    private static bool CanPlay => !Paused && !_connectionBusy && _stopTask.IsCompleted
        && (ToyId != null || HapticSettings.PreviewOnly.Value);
    private static float Now => Time.unscaledTime;
    private static float OutputScale => PluginConfig.MultGlobal.Value *
        (PluginConfig.EnableDiffScale.Value && Run.instance != null
            ? 1 + Mathf.Log(Mathf.Max(1, Run.instance.difficultyCoefficient)) * 0.2f : 1);

    static LovensePlugin()
    {
        // Preserve the original working transport setup. The game's bundled Mono
        // HttpClient handler does not forward its per-handler certificate callback.
        System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-platform", "LovenseRoR2");
        Http = http;
    }

    private void Awake()
    {
        Logger = base.Logger;
        PluginConfig.Initialize(Config);
        HapticOverlay.Initialize();
        BaseUrl = Endpoint();
        PluginConfig.Ip.SettingChanged += (_, _) => EndpointChanged();
        PluginConfig.Port.SettingChanged += (_, _) => EndpointChanged();
        PluginConfig.ContinuousToyTarget.SettingChanged += (_, _) => StopAndReset();
        PluginConfig.EventToyTarget.SettingChanged += (_, _) => StopAndReset();
        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();
        TeleporterInteraction.onTeleporterBeginChargingGlobal += OnBossEngageGlobal;
        Run.onServerGameOver += OnGameOverGlobal;
        if (PluginConfig.AutoConnect.Value) Connect();
    }

    private static string Endpoint() => $"https://{PluginConfig.Ip.Value}:{PluginConfig.Port.Value}/command";
    private static void EndpointChanged()
    {
        Disconnect(); // Existing outputs retain the old endpoint for their final stop.
        BaseUrl = Endpoint();
    }

    private void Update()
    {
        // Escape belongs to menu navigation, including the priority settings UI.
        if (Input.GetKeyDown(KeyCode.F8)) TogglePause();
        if (!CanPlay) return;
        foreach (var engine in Engines) engine.BeginFrame();
        _damageSource = Mathf.Max(0, _damageSource - Time.unscaledDeltaTime * 75);
        if (_damageSource > 0) Continuous(HapticEffect.Damage, _damageSource);

        if (!HapticSettings.Enabled(HapticEffect.KillChain))
            _killChainCount = 0;
        if (_killChainCount > 0 && Time.time > _killChainExpiry)
        {
            float duration = Mathf.Min(_killChainCount * PluginConfig.KillChainWindow.Value / 2, PluginConfig.TaperCap.Value);
            Trigger(HapticEffect.KillChain, HapticPattern.Taper(_killChainSource, duration), PluginConfig.EventToyTarget.Value);
            _killChainCount = 0;
        }
        if (_killChainCount > 0) Continuous(HapticEffect.KillChain, _killChainSource);

        if (Run.instance != null) CollectGameSources();
        else _wasAlive = false;

        float scale = OutputScale;
        for (int slot = 0; slot < Engines.Length; slot++)
        {
            var selected = Engines[slot].Select(Now, HapticSettings.Priority, HapticSettings.Enabled);
            int intensity = selected.Effect.HasValue ? HapticEngine.ToIntensity(selected.Percent,
                HapticSettings.Multiplier(selected.Effect.Value), scale,
                HapticSettings.MaximumPercent.Value) : 0;
            DevicePercent[slot] = intensity * 5;
            string? toy = slot == 0 ? ToyId : ToyId2;
            if (!HapticSettings.PreviewOnly.Value && toy != null) Output(toy).Tick(intensity, Now);
        }
    }

    private static void CollectGameSources()
    {
        var localBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;
        bool alive = localBody?.healthComponent?.alive == true;
        if (_wasAlive && !alive)
        {
            Trigger(HapticEffect.Death, HapticPattern.Taper(100, 5), PluginConfig.ContinuousToyTarget.Value);
            _killChainCount = 0;
        }
        _wasAlive = alive;
        if (!alive || localBody == null) return;

        if (PluginConfig.EnableLowHealth.Value)
        {
            float threshold = Mathf.Max(0.01f, PluginConfig.LowHealthThreshold.Value);
            float hp = localBody.healthComponent.combinedHealthFraction;
            if (hp < threshold)
            {
                float urgency = Mathf.Clamp01(1 - hp / threshold);
                float period = Mathf.Lerp(PluginConfig.HeartbeatSlowPeriod.Value, PluginConfig.HeartbeatFastPeriod.Value, urgency);
                Continuous(HapticEffect.LowHealth, HapticPattern.HeartbeatSample(Now, period, Mathf.Lerp(30, 70, urgency)));
            }
        }
        var tele = TeleporterInteraction.instance;
        if (tele != null && !tele.isCharged && tele.chargeFraction > 0)
            Continuous(HapticEffect.Teleporter, tele.chargeFraction * 75);

        if (!PluginConfig.EnableEliteProximity.Value && !PluginConfig.EnableCrowdPanic.Value) return;
        float eliteRadius = Mathf.Max(1, PluginConfig.EliteProximityRadius.Value);
        float crowdRadius = Mathf.Max(1, PluginConfig.CrowdPanicRadius.Value);
        float nearestElite = float.MaxValue;
        int enemies = 0;
        foreach (var body in CharacterBody.readOnlyInstancesList)
        {
            if (body == null || body == localBody || body.teamComponent == null
                || body.teamComponent.teamIndex == TeamIndex.Player || body.healthComponent?.alive != true) continue;
            float distance = Vector3.Distance(localBody.corePosition, body.corePosition);
            if (body.isElite) nearestElite = Mathf.Min(nearestElite, distance);
            if (!body.isBoss && distance < crowdRadius) enemies++;
        }
        if (nearestElite < eliteRadius) Continuous(HapticEffect.EliteProximity, Mathf.Lerp(60, 0, nearestElite / eliteRadius));
        if (enemies > 0) Continuous(HapticEffect.CrowdPanic, Mathf.Min(enemies * 10, 60));
    }

    private static void Continuous(HapticEffect effect, float percent)
    {
        if (!HapticSettings.Enabled(effect)) return;
        // Tiny chains or damage tails that round to zero must not hide an audible effect.
        // Heartbeat rests deliberately keep their place in the hierarchy.
        if (effect != HapticEffect.LowHealth && HapticEngine.ToIntensity(percent,
            HapticSettings.Multiplier(effect), OutputScale, HapticSettings.MaximumPercent.Value) == 0) return;
        foreach (int slot in ResolveSlots(PluginConfig.ContinuousToyTarget.Value)) Engines[slot].SetContinuous(effect, percent);
    }

    private static void Trigger(HapticEffect effect, HapticPattern pattern, ToyTarget target)
    {
        if (!CanPlay || !HapticSettings.Enabled(effect)) return;
        foreach (int slot in ResolveSlots(target)) Engines[slot].Trigger(effect, pattern, Now);
    }

    private static IEnumerable<int> ResolveSlots(ToyTarget target)
    {
        bool second = HapticSettings.PreviewOnly.Value || ToyId2 != null;
        if (target == ToyTarget.Toy2 && second) { yield return 1; yield break; }
        yield return 0;
        if (target == ToyTarget.Both && second) yield return 1;
    }

    internal static void OnDamage(float damage, float maxHp, bool isDot)
    {
        if (!CanPlay || !HapticSettings.Enabled(HapticEffect.Damage) || maxHp <= 0) return;
        _damageSource = Mathf.Clamp(_damageSource + Mathf.Clamp01(damage / maxHp) * 100 * (isDot ? 0.4f : 1), 0, 100);
    }

    internal static void OnKill()
    {
        if (!CanPlay || !HapticSettings.Enabled(HapticEffect.KillChain)) return;
        if (Time.time > _killChainExpiry) { _killChainCount = 0; _killChainExpiry = Time.time; }
        _killChainCount++;
        // Preserve accumulated chain time from the original mod.
        _killChainExpiry += Mathf.Max(0.1f, PluginConfig.KillChainWindow.Value);
        float fraction = Mathf.Min(1, (_killChainCount - 1f) / Mathf.Max(PluginConfig.KillsToMax.Value - 1, 1));
        _killChainSource = Mathf.Pow(100, fraction);
    }

    internal static void OnItemPickup(ItemTier tier)
    {
        var pattern = tier switch
        {
            ItemTier.Tier1 or ItemTier.VoidTier1 => HapticPattern.Pickup(1, 15),
            ItemTier.Tier2 or ItemTier.VoidTier2 => HapticPattern.Pickup(2, 30),
            ItemTier.Tier3 or ItemTier.VoidTier3 or ItemTier.Boss or ItemTier.VoidBoss => HapticPattern.Pickup(3, 55),
            ItemTier.Lunar => HapticPattern.Pickup(2, 45),
            _ => null,
        };
        if (pattern != null) Trigger(HapticEffect.ItemPickup, pattern, PluginConfig.EventToyTarget.Value);
    }

    private static void OnBossEngageGlobal(TeleporterInteraction _) =>
        Trigger(HapticEffect.BossEngage, HapticPattern.Boss(), PluginConfig.EventToyTarget.Value);

    private static void OnGameOverGlobal(Run run, GameEndingDef ending)
    {
        if (ending != null && ending.isWin) Trigger(HapticEffect.Victory, HapticPattern.Victory(), PluginConfig.EventToyTarget.Value);
    }

    internal static void Preview()
    {
        if (!CanPlay) { Logger.LogInfo("Resume feedback and connect, or enable Preview Only, to audition an effect."); return; }
        HapticEffect effect = HapticSettings.PreviewEffect.Value;
        HapticPattern pattern = effect switch
        {
            HapticEffect.Damage => HapticPattern.Taper(65, 0.8f),
            HapticEffect.KillChain => HapticPattern.Taper(80, PluginConfig.TaperCap.Value),
            HapticEffect.LowHealth => HapticPattern.Heartbeat(PluginConfig.HeartbeatSlowPeriod.Value, 60),
            HapticEffect.Teleporter => new HapticPattern(3, t => t / 3 * 75),
            HapticEffect.Death => HapticPattern.Taper(100, 5),
            HapticEffect.EliteProximity => new HapticPattern(3, t => 60 * (1 - Math.Abs(t - 1.5f) / 1.5f)),
            HapticEffect.CrowdPanic => new HapticPattern(3, t => 20 * (1 + (int)t)),
            HapticEffect.ItemPickup => HapticPattern.Pickup(3, 55),
            HapticEffect.BossEngage => HapticPattern.Boss(),
            _ => HapticPattern.Victory(),
        };
        bool eventRoute = effect == HapticEffect.ItemPickup || effect == HapticEffect.BossEngage || effect == HapticEffect.Victory;
        Trigger(effect, pattern, eventRoute ? PluginConfig.EventToyTarget.Value : PluginConfig.ContinuousToyTarget.Value);
    }

    internal static void TogglePause()
    {
        Paused = !Paused;
        StopAndReset();
        Logger.LogInfo(Paused ? "Feedback paused. Press F8 to resume." : "Feedback resumed.");
    }

    internal static void ResetSources() => StopAndReset();
    internal static void StopAndReset()
    {
        _damageSource = _killChainSource = _killChainExpiry = 0;
        _killChainCount = 0;
        _wasAlive = false;
        foreach (var engine in Engines) engine.Clear();
        Array.Clear(DevicePercent, 0, DevicePercent.Length);
        _stopTask = Task.WhenAll(new[] { _stopTask }.Concat(Outputs.Values.Select(output => output.StopAsync())).ToArray());
        Outputs.Clear();
    }

    private static HapticOutput Output(string toy)
    {
        if (!Outputs.TryGetValue(toy, out var output))
        {
            string url = BaseUrl;
            output = new HapticOutput(intensity => SendVibrate(url, toy, intensity),
                error => Logger.LogWarning($"Lovense output failed at {url}: {error}"));
            Outputs.Add(toy, output);
        }
        return output;
    }

    internal static void Connect() => _ = InitAsync();
    internal static void Disconnect() => _ = DisconnectAsync();

    private static async Task DisconnectAsync()
    {
        int version = ++_connectionVersion;
        _connectionBusy = true;
        ConnectionStatus = "Disconnecting";
        StopAndReset();
        await _stopTask;
        if (version != _connectionVersion) return;
        ToyId = ToyId2 = null;
        _connectionBusy = false;
        ConnectionStatus = "Disconnected";
    }

    private static async Task InitAsync()
    {
        int version = ++_connectionVersion;
        _connectionBusy = true;
        ConnectionStatus = "Connecting";
        StopAndReset();
        await _stopTask;
        if (version != _connectionVersion) return;
        ToyId = ToyId2 = null;
        string url = BaseUrl;
        try
        {
            var response = await SendCommand(url, new { command = "GetToys" });
            if (version != _connectionVersion) return;
            var token = response["data"]?["toys"];
            var toys = token as JObject ?? JObject.Parse(token?.ToString() ?? "{}");
            var ids = toys.Properties().Where(p => p.Value["status"]?.ToString() != "0").Select(p => p.Name).Take(2).ToArray();
            if (ids.Length == 0) throw new InvalidOperationException("No connected devices found.");
            ToyId = ids[0];
            ToyId2 = ids.Length > 1 ? ids[1] : null;
            ConnectionStatus = ids.Length == 2 ? "2 devices connected" : "Connected";
            Logger.LogInfo("Lovense " + ConnectionStatus + ". Use Preview Effect to test output.");
        }
        catch (Exception e)
        {
            if (version != _connectionVersion) return;
            ConnectionStatus = "Connection failed";
            Logger.LogError($"Lovense connection failed at {url}:\n{e}");
        }
        finally { if (version == _connectionVersion) _connectionBusy = false; }
    }

    private static async Task SendVibrate(string url, string toy, int intensity)
    {
        await SendCommand(url, new
        {
            toy, command = "Function", action = $"Vibrate:{intensity}",
            timeSec = 2, loopRunningSec = 0, loopPauseSec = 0, apiVer = 1
        });
    }

    private static async Task<JObject> SendCommand(string url, object payload)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var result = await Http.PostAsync(url, content);
        var body = await result.Content.ReadAsStringAsync();
        return JObject.Parse(body);
    }

    private void OnGUI() => HapticOverlay.Draw();
    private void OnDestroy()
    {
        Disconnect();
        TeleporterInteraction.onTeleporterBeginChargingGlobal -= OnBossEngageGlobal;
        Run.onServerGameOver -= OnGameOverGlobal;
        _harmony?.UnpatchSelf();
        HapticOverlay.Dispose();
    }
}
