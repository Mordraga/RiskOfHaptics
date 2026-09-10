using System;
using System.Collections.Generic;
using System.Linq;

namespace LovenseRoR2;

internal enum HapticEffect
{
    Damage, KillChain, LowHealth, Teleporter, Death, EliteProximity, CrowdPanic,
    ItemPickup, BossEngage, Victory
}

internal enum PriorityProfile
{
    Balanced, RapidFire, MobileKiting, PrecisionBurst, AbilityBurst,
    MeleeMomentum, ZoneControl, Attrition, HealthTrading, Custom
}

internal static class PriorityProfiles
{
    internal static int Get(PriorityProfile profile, HapticEffect effect)
    {
        // In enum order. Intensity presets are deliberately independent of these ranks.
        int[] ranks = profile switch
        {
            PriorityProfile.RapidFire => new[] { 80, 85, 90, 30, 95, 50, 55, 40, 75, 100 },
            PriorityProfile.MobileKiting => new[] { 85, 65, 90, 35, 95, 80, 75, 30, 70, 100 },
            PriorityProfile.PrecisionBurst => new[] { 80, 55, 88, 40, 95, 85, 75, 45, 90, 100 },
            PriorityProfile.AbilityBurst => new[] { 80, 75, 85, 40, 95, 55, 50, 30, 90, 100 },
            PriorityProfile.MeleeMomentum => new[] { 80, 85, 90, 35, 95, 40, 30, 45, 75, 100 },
            PriorityProfile.ZoneControl => new[] { 60, 85, 80, 70, 95, 55, 50, 65, 90, 100 },
            PriorityProfile.Attrition => new[] { 85, 80, 90, 40, 95, 60, 50, 30, 75, 100 },
            PriorityProfile.HealthTrading => new[] { 35, 85, 55, 60, 95, 70, 65, 40, 90, 100 },
            _ => new[] { 80, 60, 75, 40, 95, 50, 45, 30, 90, 100 },
        };
        return ranks[(int)effect];
    }
}

internal sealed class HapticPattern
{
    internal readonly float Duration;
    private readonly Func<float, float> _sample;

    internal HapticPattern(float duration, Func<float, float> sample)
    {
        Duration = Math.Max(0.05f, duration);
        _sample = sample;
    }

    internal float Sample(float elapsed) => elapsed < 0 || elapsed >= Duration ? 0 : _sample(elapsed);

    internal static HapticPattern Taper(float peak, float duration) =>
        new HapticPattern(duration, t => peak * (1 - t / Math.Max(0.05f, duration)));

    internal static HapticPattern Pickup(int pulses, float peak) =>
        new HapticPattern(pulses * 0.32f, t => t % 0.32f < 0.16f ? peak : 0);

    internal static HapticPattern Boss() => new HapticPattern(2.5f,
        t => t < 1.7f ? 60 * t / 1.7f : t < 1.9f ? 0 : 60 * (2.5f - t) / 0.6f);

    internal static HapticPattern Victory() => new HapticPattern(4,
        t => t < 2.4f ? (t % 0.8f < 0.4f ? 30 + 20 * (int)(t / 0.8f) : 0) : 90 * (4 - t) / 1.6f);

    internal static HapticPattern Heartbeat(float period, float peak) => new HapticPattern(period * 3,
        t => HeartbeatSample(t, period, peak));

    internal static float HeartbeatSample(float time, float period, float peak)
    {
        period = Math.Max(0.1f, period);
        float phase = time % period / period;
        float Pulse(float start, float width)
        {
            float d = Math.Abs(phase - start);
            d = Math.Min(d, 1 - d);
            return d < width ? 1 - d / width : 0;
        }
        return Math.Max(Pulse(0, 0.12f), Pulse(0.22f, 0.10f) * 0.7f) * peak;
    }
}

internal readonly struct HapticSelection
{
    internal readonly HapticEffect? Effect;
    internal readonly float Percent;
    internal HapticSelection(HapticEffect? effect, float percent) { Effect = effect; Percent = percent; }
}

// Independent of Unity and transport: every device owns one arbiter.
internal sealed class HapticEngine
{
    private sealed class Event
    {
        internal readonly HapticPattern Pattern;
        internal readonly float Start;
        internal Event(HapticPattern pattern, float start) { Pattern = pattern; Start = start; }
    }

    private readonly Dictionary<HapticEffect, float> _continuous = new();
    private readonly Dictionary<HapticEffect, Event> _events = new();
    private HapticEffect? _current;

    internal void BeginFrame() => _continuous.Clear();
    // A zero sample can still reserve priority, preserving silence between heartbeat pulses.
    internal void SetContinuous(HapticEffect effect, float percent) => _continuous[effect] = percent;
    internal void Trigger(HapticEffect effect, HapticPattern pattern, float now) => _events[effect] = new Event(pattern, now);
    internal void Clear() { _continuous.Clear(); _events.Clear(); _current = null; }

    internal HapticSelection Select(float now, Func<HapticEffect, int> priority, Func<HapticEffect, bool> enabled)
    {
        foreach (var pair in _events.ToArray())
            if (now - pair.Value.Start >= pair.Value.Pattern.Duration || !enabled(pair.Key)) _events.Remove(pair.Key);

        HapticEffect? winner = null;
        int rank = int.MinValue;
        // Stable enum ordering breaks new ties; the current effect keeps an equal-priority tie.
        foreach (HapticEffect effect in Enum.GetValues(typeof(HapticEffect)))
        {
            if (!enabled(effect) || (!_continuous.ContainsKey(effect) && !_events.ContainsKey(effect))) continue;
            int next = priority(effect);
            if (next > rank || (next == rank && effect == _current)) { winner = effect; rank = next; }
        }

        // No backlog: suppressed or interrupted one-shots never resume later.
        foreach (var effect in _events.Keys.ToArray())
            if (effect != winner) _events.Remove(effect);

        _current = winner;
        if (!winner.HasValue) return new HapticSelection(null, 0);
        var chosen = winner.Value;
        float value = _events.TryGetValue(chosen, out var ev)
            ? ev.Pattern.Sample(now - ev.Start) : _continuous[chosen];
        return new HapticSelection(chosen, value);
    }

    internal static int ToIntensity(float percent, float effectMultiplier, float globalMultiplier, int maximumPercent)
    {
        float scaled = percent * Math.Max(0, effectMultiplier) * Math.Max(0, globalMultiplier);
        if (float.IsNaN(scaled)) return 0;
        int ceiling = Math.Max(0, Math.Min(100, maximumPercent)) / 5;
        return Math.Min(ceiling, (int)Math.Round(Math.Max(0, Math.Min(100, scaled)) / 5));
    }
}
