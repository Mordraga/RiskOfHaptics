using System;
using System.Threading.Tasks;

namespace LovenseRoR2;

// One in-flight request per device. Tick coalesces updates instead of building a queue.
internal sealed class HapticOutput
{
    private readonly Func<int, Task> _send;
    private readonly Action<Exception> _onError;
    private Task _inFlight = Task.CompletedTask;
    private Task? _stopTask;
    private bool _stopped;
    private int _lastSent = -1;
    private float _lastAttempt = -100;
    private float _lastSuccess = -100;

    internal HapticOutput(Func<int, Task> send, Action<Exception> onError) { _send = send; _onError = onError; }

    internal void Tick(int intensity, float now)
    {
        if (_stopped || !_inFlight.IsCompleted || now - _lastAttempt < 0.05f) return;
        if (intensity == _lastSent && now - _lastSuccess < 1.5f) return;
        _lastAttempt = now;
        _inFlight = SendAsync(intensity, now);
    }

    private async Task SendAsync(int intensity, float now)
    {
        try { await _send(intensity); _lastSent = intensity; _lastSuccess = now; }
        catch (Exception e) { _lastSent = -1; _lastAttempt = now + 0.95f; _onError(e); }
    }

    internal Task StopAsync()
    {
        if (_stopTask != null) return _stopTask;
        _stopped = true;
        return _stopTask = StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        await _inFlight;
        try { await _send(0); }
        catch (Exception e) { _onError(e); }
    }
}
