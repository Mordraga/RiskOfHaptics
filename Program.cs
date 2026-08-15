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
    public const string PluginGUID    = "com.mordraga.lovenserisk";
    public const string PluginName    = "LovenseRoR2";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger = null!;
    internal static string? ToyId;
    internal static string? ToyId2;

    internal static float DamageSource;
    internal static float DeathSource;
    internal static float KillChainSource;
    internal static int   CurrentPercent;

    private static int   _lastSentIntensity = -1;
    private static float _lastSendTime      = 0f;
    private static bool  _wasAlive;

    private static int   _killChainCount;
    private static float _killChainExpiry;
    private static float _patternEndTime    = -1f;
    private static float _taperStartTime;
    private static int   _taperStartPercent;

    private static string BaseUrl = "";

    private static readonly HttpClient Http = null!;

    private const float DamageDecay = 15f;
    private const float DeathDecay  =  4f;

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

        PluginConfig.Initialize(Config);
        HapticOverlay.Initialize();

        BaseUrl = $"https://{PluginConfig.Ip.Value}:{PluginConfig.Port.Value}/command";
        PluginConfig.Ip.SettingChanged   += (_, _) => BaseUrl = $"https://{PluginConfig.Ip.Value}:{PluginConfig.Port.Value}/command";
        PluginConfig.Port.SettingChanged += (_, _) => BaseUrl = $"https://{PluginConfig.Ip.Value}:{PluginConfig.Port.Value}/command";

        new Harmony(PluginGUID).PatchAll();

        TeleporterInteraction.onTeleporterBeginChargingGlobal += OnBossEngageGlobal;
        Run.onServerGameOver += OnGameOverGlobal;

        if (PluginConfig.AutoConnect.Value) _ = InitAsync();
    }

    private void Update()
    {
        if (ToyId == null) return;

        float dt = Time.deltaTime;

        DamageSource = Mathf.Max(0, DamageSource - dt * DamageDecay);
        DeathSource  = Mathf.Max(0, DeathSource  - dt * DeathDecay);

        if (Input.GetKeyDown(KeyCode.Escape) && ToyId != null)
        {
            ResetSources();
            _ = SendStop(AllConnectedToys());
        }

        // when chain expires fire a PatternV2 taper, duration = half chain time, capped
        if (_killChainCount > 0 && Time.time > _killChainExpiry)
        {
            float rampDivisor = Mathf.Max(PluginConfig.KillsToMax.Value - 1, 1);
            int   peakPos     = Mathf.RoundToInt(Mathf.Clamp(Mathf.Pow(100f, (_killChainCount - 1f) / rampDivisor), 0f, 100f));
            float taperDurationS = Mathf.Min(_killChainCount * PluginConfig.KillChainWindow.Value / 2f, PluginConfig.TaperCap.Value);

            PlayBurst(peakPos, taperDurationS);

            _killChainCount = 0;
            KillChainSource = 0f;
        }

        float lowHealthSource = 0f;
        float teleSource      = 0f;
        float eliteSource     = 0f;
        float crowdSource     = 0f;

        if (Run.instance != null)
        {
            var localBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;

            bool isAlive = localBody != null && localBody.healthComponent?.alive == true;
            if (_wasAlive && !isAlive && PluginConfig.EnableDeath.Value)
            {
                DeathSource      = 20f * PluginConfig.MultDeath.Value;
                _killChainCount  = 0;
                KillChainSource  = 0f;
                _killChainExpiry = 0f;
            }
            _wasAlive = isAlive;

            if (PluginConfig.EnableLowHealth.Value && localBody?.healthComponent != null)
            {
                float hpFrac    = localBody.healthComponent.combinedHealthFraction;
                float threshold = PluginConfig.LowHealthThreshold.Value;
                if (hpFrac < threshold)
                {
                    float urgency = 1f - hpFrac / threshold;
                    float period  = Mathf.Lerp(PluginConfig.HeartbeatSlowPeriod.Value, PluginConfig.HeartbeatFastPeriod.Value, urgency);
                    float phase   = (Time.time % period) / period;
                    float beat    = Mathf.Max(PulseAt(phase, 0f, 0.12f), PulseAt(phase, 0.22f, 0.10f) * 0.7f);
                    lowHealthSource = beat * Mathf.Lerp(6f, 14f, urgency) * PluginConfig.MultLowHealth.Value;
                }
            }

            if (PluginConfig.EnableTeleporter.Value)
            {
                var tele = TeleporterInteraction.instance;
                if (tele != null && !tele.isCharged && tele.chargeFraction > 0f)
                    teleSource = tele.chargeFraction * 15f * PluginConfig.MultTeleporter.Value;
            }

            if (localBody != null && (PluginConfig.EnableEliteProximity.Value || PluginConfig.EnableCrowdPanic.Value))
            {
                Vector3 playerPos       = localBody.corePosition;
                float   eliteRadius     = PluginConfig.EliteProximityRadius.Value;
                float   crowdRadius     = PluginConfig.CrowdPanicRadius.Value;
                float   nearestEliteDist = float.MaxValue;
                int     enemyCount       = 0;

                foreach (var body in CharacterBody.readOnlyInstancesList)
                {
                    if (body == null || body == localBody) continue;
                    if (body.teamComponent == null || body.teamComponent.teamIndex == TeamIndex.Player) continue;
                    if (body.healthComponent == null || !body.healthComponent.alive) continue;

                    float dist = Vector3.Distance(playerPos, body.corePosition);

                    if (PluginConfig.EnableEliteProximity.Value && body.isElite && dist < nearestEliteDist)
                        nearestEliteDist = dist;

                    if (PluginConfig.EnableCrowdPanic.Value && !body.isBoss && dist < crowdRadius)
                        enemyCount++;
                }

                if (PluginConfig.EnableEliteProximity.Value && nearestEliteDist < eliteRadius)
                    eliteSource = Mathf.Lerp(12f, 0f, nearestEliteDist / eliteRadius) * PluginConfig.MultElite.Value;

                if (PluginConfig.EnableCrowdPanic.Value && enemyCount > 0)
                    crowdSource = Mathf.Min(enemyCount * 2f, 12f) * PluginConfig.MultCrowdPanic.Value;
            }
        }
        else
        {
            _wasAlive = false;
        }

        float diffScale = (PluginConfig.EnableDiffScale.Value && Run.instance != null)
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

        float damage    = PluginConfig.EnableDamage.Value    ? DamageSource    * PluginConfig.MultDamage.Value    : 0f;
        float death     = PluginConfig.EnableDeath.Value     ? DeathSource                                        : 0f;
        float killChain = PluginConfig.EnableKillChain.Value ? KillChainSource * PluginConfig.MultKillChain.Value : 0f;

        int intensity = Mathf.RoundToInt(
            Mathf.Clamp((damage + death + killChain + lowHealthSource + teleSource + eliteSource + crowdSource) * diffScale * PluginConfig.MultGlobal.Value, 0f, 20f)
        );

        CurrentPercent = Mathf.RoundToInt(intensity / 20f * 100f);

        // Fansly: short-lived commands (timeSec=2) + resend every 1.5s lets tips naturally override
        bool commandExpiring = Time.time - _lastSendTime > 1.5f;
        if (intensity != _lastSentIntensity || commandExpiring)
        {
            _lastSentIntensity = intensity;
            _lastSendTime      = Time.time;
            _ = TrySendVibrate(intensity, ResolveToys(PluginConfig.ContinuousToyTarget.Value));
        }
    }

    private static float PulseAt(float phase, float start, float width)
    {
        float d = Mathf.Abs(phase - start);
        d = Mathf.Min(d, 1f - d);
        return d < width ? 1f - d / width : 0f;
    }

    private void OnGUI() => HapticOverlay.Draw();

    internal static void OnDamage(float damage, float maxHp, bool isDot)
    {
        if (!PluginConfig.EnableDamage.Value) return;
        float add = Mathf.Clamp01(damage / maxHp) * 20f;
        DamageSource = Mathf.Clamp(DamageSource + (isDot ? add * 0.4f : add), 0f, 20f);
    }

    internal static void OnKill()
    {
        if (ToyId == null || !PluginConfig.EnableKillChain.Value) return;

        if (Time.time > _killChainExpiry)
        {
            _killChainCount  = 0;
            _killChainExpiry = Time.time;
        }

        _killChainCount++;
        _killChainExpiry += PluginConfig.KillChainWindow.Value;

        float rampDivisor = Mathf.Max(PluginConfig.KillsToMax.Value - 1, 1);
        KillChainSource = Mathf.Min(Mathf.Pow(100f, (_killChainCount - 1) / rampDivisor) * 0.2f, 20f);
    }

    internal static void ResetSources()
    {
        DamageSource = DeathSource = KillChainSource = 0f;
        _killChainCount    = 0;
        _killChainExpiry   = 0f;
        _patternEndTime    = -1f;
        _lastSentIntensity = -1;
        _lastSendTime      = 0f;
        CurrentPercent     = 0;
    }

    internal static void Connect() => _ = InitAsync();

    internal static void Disconnect()
    {
        ToyId  = null;
        ToyId2 = null;
        DamageSource = DeathSource = KillChainSource = 0f;
        _killChainCount    = 0;
        _killChainExpiry   = 0f;
        _patternEndTime    = -1f;
        _lastSentIntensity = -1;
        _lastSendTime      = 0f;
        CurrentPercent     = 0;
        Logger.LogInfo("Lovense disconnected.");
    }

    internal static void OnItemPickup(ItemTier tier)
    {
        if (ToyId == null || !PluginConfig.EnableItemPickup.Value) return;

        float basePeak = tier switch
        {
            ItemTier.Tier1 or ItemTier.VoidTier1                                     => 15f,
            ItemTier.Tier2 or ItemTier.VoidTier2                                     => 30f,
            ItemTier.Tier3 or ItemTier.VoidTier3 or ItemTier.Boss or ItemTier.VoidBoss => 55f,
            ItemTier.Lunar                                                            => 45f,
            _                                                                         => 0f,
        };
        if (basePeak <= 0f) return;

        int peak = Mathf.RoundToInt(Mathf.Clamp(basePeak * PluginConfig.MultItemPickup.Value, 0f, 100f));
        PlayBurst(peak, 0.6f);
    }

    private static void OnBossEngageGlobal(TeleporterInteraction _)
    {
        if (ToyId == null || !PluginConfig.EnableBossEngage.Value) return;
        int peak = Mathf.RoundToInt(Mathf.Clamp(60f * PluginConfig.MultBossEngage.Value, 0f, 100f));
        PlayBurst(peak, 2.5f);
    }

    private static void OnGameOverGlobal(Run run, GameEndingDef ending)
    {
        if (ToyId == null || !PluginConfig.EnableVictory.Value || !ending.isWin) return;
        int peak = Mathf.RoundToInt(Mathf.Clamp(90f * PluginConfig.MultVictory.Value, 0f, 100f));
        PlayBurst(peak, 4f);
    }

    private static void PlayBurst(int peakPercent, float durationS)
    {
        int taperMs = Mathf.RoundToInt(durationS * 1000f);

        _taperStartTime    = Time.time;
        _taperStartPercent = peakPercent;
        _patternEndTime    = Time.time + durationS;

        _ = TrySendPatternV2(new object[]
        {
            new { ts = 0,       pos = peakPercent },
            new { ts = taperMs, pos = 0 },
        }, ResolveToys(PluginConfig.EventToyTarget.Value));
    }

    private static IEnumerable<string> ResolveToys(ToyTarget target)
    {
        if (target == ToyTarget.Toy2 && ToyId2 != null) { yield return ToyId2; yield break; }
        if (target == ToyTarget.Both)
        {
            if (ToyId  != null) yield return ToyId;
            if (ToyId2 != null) yield return ToyId2;
            yield break;
        }
        if (ToyId != null) yield return ToyId;
    }

    private static IEnumerable<string> AllConnectedToys()
    {
        if (ToyId  != null) yield return ToyId;
        if (ToyId2 != null) yield return ToyId2;
    }

    private static async Task InitAsync()
    {
        _patternEndTime = Time.time + 1.1f;
        try
        {
            var resp = await SendCommand(new { command = "GetToys" });
            var toysStr = resp["data"]?["toys"]?.ToString();
            if (toysStr == null)
            {
                _patternEndTime = -1f;
                Logger.LogError($"Lovense init failed: unexpected response: {resp}");
                return;
            }
            var toys = JObject.Parse(toysStr);
            var toyNames = toys.Properties().Select(p => p.Name).ToList();
            if (toyNames.Count == 0)
            {
                _patternEndTime = -1f;
                Logger.LogWarning("Lovense: no toys found. Is the toy connected?");
                return;
            }
            ToyId  = toyNames[0];
            ToyId2 = toyNames.Count > 1 ? toyNames[1] : null;
            Logger.LogInfo(ToyId2 != null
                ? $"Lovense connected: {ToyId} (Toy 1), {ToyId2} (Toy 2)"
                : $"Lovense connected: {ToyId}");

            await TrySendPatternV2(new object[]
            {
                new { ts =   0, pos =   0 },
                new { ts = 200, pos =  30 },
                new { ts = 400, pos =  60 },
                new { ts = 600, pos = 100 },
                new { ts = 900, pos =   0 },
            }, AllConnectedToys());
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

    internal static async Task TrySendPatternV2(object[] actions, IEnumerable<string> toys)
    {
        foreach (var toy in toys)
        {
            try
            {
                await SendCommand(new { toy, command = "PatternV2", type = "Setup", actions, apiVer = 1 });
                await SendCommand(new { toy, command = "PatternV2", type = "Play",  apiVer = 1 });
            }
            catch (Exception e) { Logger.LogError($"Lovense PatternV2 failed: {e}"); }
        }
    }

    private static async Task TrySendVibrate(int intensity, IEnumerable<string> toys)
    {
        foreach (var toy in toys)
        {
            try { await SendCommand(new
            {
                toy, command = "Function", action = $"Vibrate:{intensity}",
                timeSec = 2, loopRunningSec = 0, loopPauseSec = 0, apiVer = 1
            }); }
            catch (Exception e) { Logger.LogWarning($"Lovense vibrate failed: {e.Message}"); }
        }
    }

    private static async Task SendStop(IEnumerable<string> toys)
    {
        foreach (var toy in toys)
        {
            try { await SendCommand(new
            {
                toy, command = "Function", action = "Vibrate:0",
                timeSec = 0, loopRunningSec = 0, loopPauseSec = 0, apiVer = 1
            }); }
            catch { }
        }
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
