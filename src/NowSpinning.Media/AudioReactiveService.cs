using NAudio.Wave;

namespace NowSpinning.Media;

public sealed class AudioReactiveService : IDisposable
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(33);
    private WasapiLoopbackCapture? _capture;
    private long _lastPublishedAt;
    private bool _disposed;

    public event EventHandler<float>? LevelChanged;
    public bool IsRunning => _capture is not null;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_capture is not null) return;
        _capture = new WasapiLoopbackCapture();
        _lastPublishedAt = 0;
        _capture.DataAvailable += OnDataAvailable;
        try
        {
            _capture.StartRecording();
        }
        catch
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.Dispose();
            _capture = null;
            throw;
        }
    }

    public void Stop()
    {
        if (_capture is null) return;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (args.BytesRecorded < sizeof(float)) return;
        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(args.Buffer.AsSpan(0, args.BytesRecorded));
        double sum = 0;
        var count = 0;
        for (var index = 0; index < samples.Length; index += 8)
        {
            var sample = samples[index];
            if (!float.IsFinite(sample)) continue;
            sum += sample * sample;
            count++;
        }
        var rms = count == 0 ? 0 : (float)Math.Sqrt(sum / count);
        var now = Environment.TickCount64;
        if (now - Volatile.Read(ref _lastPublishedAt) < PublishInterval.TotalMilliseconds) return;
        Volatile.Write(ref _lastPublishedAt, now);
        LevelChanged?.Invoke(this, Math.Clamp(rms * 3, 0, 1));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
