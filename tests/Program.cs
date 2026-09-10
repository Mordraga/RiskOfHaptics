using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LovenseRoR2;

internal static class Tests
{
    private static int _passed;
    private static int Rank(HapticEffect effect) => PriorityProfiles.Get(PriorityProfile.Balanced, effect);
    private static bool Enabled(HapticEffect _) => true;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Test(string name, Action run) { run(); _passed++; Console.WriteLine("PASS " + name); }
    private static async Task TestAsync(string name, Func<Task> run) { await run(); _passed++; Console.WriteLine("PASS " + name); }

    private static async Task Main()
    {
        Test("Higher-priority events interrupt; interrupted events never resume", () =>
        {
            var engine = new HapticEngine();
            engine.Trigger(HapticEffect.ItemPickup, HapticPattern.Taper(55, 10), 0);
            Check(engine.Select(0, Rank, Enabled).Effect == HapticEffect.ItemPickup, "Pickup should start");
            engine.Trigger(HapticEffect.BossEngage, HapticPattern.Boss(), 0.1f);
            Check(engine.Select(0.1f, Rank, Enabled).Effect == HapticEffect.BossEngage, "Boss should interrupt");
            engine.Select(3, Rank, Enabled);
            Check(engine.Select(3.1f, Rank, Enabled).Effect == null, "No event backlog");
        });
        Test("Lower-priority new events are discarded even before their expiry", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Damage, 50);
            engine.Trigger(HapticEffect.ItemPickup, HapticPattern.Pickup(3, 55), 0);
            Check(engine.Select(0, Rank, Enabled).Effect == HapticEffect.Damage, "Damage should win");
            engine.BeginFrame();
            Check(engine.Select(0.1f, Rank, Enabled).Effect == null, "Suppressed pickup must not replay");
        });
        Test("Continuous conditions resume after an event expires", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Teleporter, 50);
            engine.Trigger(HapticEffect.BossEngage, HapticPattern.Boss(), 0);
            Check(engine.Select(1, Rank, Enabled).Effect == HapticEffect.BossEngage, "Boss should win");
            Check(engine.Select(2.5f, Rank, Enabled).Effect == HapticEffect.Teleporter, "Teleporter should resume");
        });
        Test("Independent devices do not interrupt one another", () =>
        {
            var first = new HapticEngine(); var second = new HapticEngine();
            first.SetContinuous(HapticEffect.KillChain, 70);
            second.Trigger(HapticEffect.Victory, HapticPattern.Victory(), 0);
            Check(first.Select(1, Rank, Enabled).Percent == 70, "Device one chain must continue");
            Check(second.Select(1, Rank, Enabled).Effect == HapticEffect.Victory, "Device two must play victory");
        });
        Test("Heartbeat silence retains priority", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.LowHealth, 0);
            engine.SetContinuous(HapticEffect.KillChain, 100);
            var selection = engine.Select(0, Rank, Enabled);
            Check(selection.Effect == HapticEffect.LowHealth && selection.Percent == 0, "Heartbeat gaps must stay silent");
        });
        Test("Equal priorities keep the current effect", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Teleporter, 40);
            engine.Select(0, _ => 50, Enabled);
            engine.SetContinuous(HapticEffect.Damage, 70);
            Check(engine.Select(0.1f, _ => 50, Enabled).Effect == HapticEffect.Teleporter, "Equal ranks must not flicker");
        });
        Test("Changing priorities immediately changes the winner", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Damage, 50);
            engine.SetContinuous(HapticEffect.KillChain, 80);
            Check(engine.Select(0, Rank, Enabled).Effect == HapticEffect.Damage, "Balanced selects damage");
            Check(engine.Select(0.1f, e => PriorityProfiles.Get(PriorityProfile.ZoneControl, e), Enabled).Effect == HapticEffect.KillChain, "Zone Control selects chain");
        });
        Test("Disabled or zero-multiplier effects leave room for other sources", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Teleporter, 40);
            engine.Trigger(HapticEffect.Victory, HapticPattern.Victory(), 0);
            Check(engine.Select(0, Rank, e => e != HapticEffect.Victory).Effect == HapticEffect.Teleporter, "Muted victory must not block");
            Check(engine.Select(0.1f, Rank, Enabled).Effect == HapticEffect.Teleporter, "Muted event must be discarded");
        });
        Test("Clear discards every source", () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Damage, 100);
            engine.Trigger(HapticEffect.Victory, HapticPattern.Victory(), 0);
            engine.Clear();
            Check(engine.Select(0, Rank, Enabled).Effect == null, "Reset must be empty");
        });
        Test("One-shot and continuous sources share global scaling and exact caps", () =>
        {
            Check(HapticEngine.ToIntensity(80, 0.5f, 0.5f, 100) == 4, "Both multipliers must apply");
            Check(HapticEngine.ToIntensity(100, 2, 2, 42) == 8, "42% cap must floor to 40%");
            Check(HapticEngine.ToIntensity(100, 2, 0, 100) == 0, "Global zero must mute");
            Check(HapticEngine.ToIntensity(100, 2, 2, 0) == 0, "Zero cap must mute");
            Check(HapticEngine.ToIntensity(float.NaN, 1, 1, 100) == 0, "Invalid data must mute");
            foreach (var profile in Enum.GetValues<PriorityProfile>())
                foreach (var effect in Enum.GetValues<HapticEffect>())
                    Check(PriorityProfiles.Get(profile, effect) >= 0 && PriorityProfiles.Get(profile, effect) <= 100, "Every profile must define bounded ranks");
        });
        Test("Profiles express distinct combat-loop priorities", () =>
        {
            void Above(PriorityProfile p, HapticEffect a, HapticEffect b) => Check(PriorityProfiles.Get(p, a) > PriorityProfiles.Get(p, b), $"{p}: {a} must beat {b}");
            Above(PriorityProfile.RapidFire, HapticEffect.KillChain, HapticEffect.Damage);
            Above(PriorityProfile.MobileKiting, HapticEffect.EliteProximity, HapticEffect.KillChain);
            Above(PriorityProfile.PrecisionBurst, HapticEffect.BossEngage, HapticEffect.KillChain);
            Above(PriorityProfile.AbilityBurst, HapticEffect.KillChain, HapticEffect.CrowdPanic);
            Above(PriorityProfile.MeleeMomentum, HapticEffect.KillChain, HapticEffect.CrowdPanic);
            Above(PriorityProfile.ZoneControl, HapticEffect.KillChain, HapticEffect.Damage);
            Above(PriorityProfile.ZoneControl, HapticEffect.Teleporter, HapticEffect.Damage);
            Above(PriorityProfile.Attrition, HapticEffect.Damage, HapticEffect.KillChain);
            Above(PriorityProfile.HealthTrading, HapticEffect.KillChain, HapticEffect.LowHealth);
            Above(PriorityProfile.HealthTrading, HapticEffect.EliteProximity, HapticEffect.Damage);
        });
        Test("Patterns have distinct shapes, defined silence, and finite lifetimes", () =>
        {
            Check(HapticPattern.Pickup(2, 30).Sample(0.1f) == 30, "Pickup first pulse");
            Check(HapticPattern.Pickup(2, 30).Sample(0.2f) == 0, "Pickup gap");
            Check(HapticPattern.Pickup(2, 30).Sample(0.4f) == 30, "Pickup second pulse");
            Check(HapticPattern.Boss().Sample(1) > HapticPattern.Boss().Sample(0.5f), "Boss builds upward");
            Check(HapticPattern.Victory().Sample(1) > HapticPattern.Victory().Sample(0.1f), "Victory grows across pulses");
            foreach (var pattern in new[] { HapticPattern.Boss(), HapticPattern.Victory(), HapticPattern.Pickup(3, 55), HapticPattern.Taper(100, 4), HapticPattern.Heartbeat(1, 60) })
                Check(pattern.Sample(pattern.Duration) == 0 && pattern.Sample(-1) == 0, "Patterns must end at zero");
        });
        await TestAsync("Transport coalesces rapid updates and stops after the in-flight command", async () =>
        {
            var sent = new List<int>();
            var pending = new TaskCompletionSource<bool>();
            var output = new HapticOutput(value => { sent.Add(value); return sent.Count == 1 ? pending.Task : Task.CompletedTask; }, e => throw e);
            output.Tick(10, 0);
            output.Tick(15, 0.1f);
            output.Tick(20, 0.2f);
            Check(sent.SequenceEqual(new[] { 10 }), "Must never send concurrent requests to one device");
            var stopped = output.StopAsync();
            Check(!stopped.IsCompleted, "Stop must wait for in-flight output");
            pending.SetResult(true);
            await stopped;
            output.Tick(20, 1);
            Check(sent.SequenceEqual(new[] { 10, 0 }), "Stop must be final, with no stale queued intensity");
        });
        await TestAsync("Transport samples latest intensity, rate limits, and refreshes before expiry", async () =>
        {
            var sent = new List<int>();
            var output = new HapticOutput(value => { sent.Add(value); return Task.CompletedTask; }, e => throw e);
            output.Tick(10, 0); output.Tick(15, 0.01f); output.Tick(20, 0.06f);
            Check(sent.SequenceEqual(new[] { 10, 20 }), "Intermediate command must be coalesced");
            output.Tick(20, 1); output.Tick(20, 1.6f);
            Check(sent.SequenceEqual(new[] { 10, 20, 20 }), "Refresh before two-second command expiry");
            await output.StopAsync();
        });
        await TestAsync("Transport failures retry without marking output delivered", async () =>
        {
            int calls = 0, errors = 0;
            var output = new HapticOutput(_ => { calls++; return calls == 1 ? Task.FromException(new Exception("offline")) : Task.CompletedTask; }, _ => errors++);
            output.Tick(10, 0); output.Tick(10, 0.2f);
            Check(calls == 1 && errors == 1, "Failures must back off");
            output.Tick(10, 1.1f);
            Check(calls == 2, "Unchanged failed intensity must retry");
            await output.StopAsync();
        });
        await TestAsync("One slow device never blocks the other device", async () =>
        {
            var pending = new TaskCompletionSource<bool>();
            var firstSent = new List<int>(); var secondSent = new List<int>();
            var first = new HapticOutput(value => { firstSent.Add(value); return firstSent.Count == 1 ? pending.Task : Task.CompletedTask; }, e => throw e);
            var second = new HapticOutput(value => { secondSent.Add(value); return Task.CompletedTask; }, e => throw e);
            first.Tick(10, 0);
            second.Tick(5, 0); second.Tick(15, 0.1f);
            Check(secondSent.SequenceEqual(new[] { 5, 15 }), "Second device must keep updating");
            var stop = first.StopAsync();
            Check(ReferenceEquals(stop, first.StopAsync()), "Repeated stops must share one final stop");
            pending.SetResult(true);
            await Task.WhenAll(stop, second.StopAsync());
        });
        await TestAsync("Profile changes keep sending through the existing device output", async () =>
        {
            var engine = new HapticEngine();
            engine.SetContinuous(HapticEffect.Damage, 40);
            engine.SetContinuous(HapticEffect.KillChain, 80);
            var sent = new List<int>();
            var pending = new TaskCompletionSource<bool>();
            var output = new HapticOutput(value =>
            {
                sent.Add(value);
                return sent.Count == 1 ? pending.Task : Task.CompletedTask;
            }, e => throw e);
            void Tick(PriorityProfile profile, float now)
            {
                var selection = engine.Select(now, effect => PriorityProfiles.Get(profile, effect), Enabled);
                output.Tick(HapticEngine.ToIntensity(selection.Percent, 1, 1, 100), now);
            }

            Tick(PriorityProfile.Balanced, 0);
            Tick(PriorityProfile.ZoneControl, 0.1f); // Change while the previous command is in flight.
            Check(sent.SequenceEqual(new[] { 8 }), "Must finish the current request before sending the new winner");
            pending.SetResult(true);
            Tick(PriorityProfile.ZoneControl, 0.2f);
            Tick(PriorityProfile.MobileKiting, 0.3f);
            Tick(PriorityProfile.MobileKiting, 2);
            Check(sent.SequenceEqual(new[] { 8, 16, 8, 8 }), "Changing profile must keep commands and keepalives flowing without a stop or reconnect");
            await output.StopAsync();
        });
        Console.WriteLine($"{_passed} tests passed.");
    }
}
