using NAudio.Wave;

namespace NowSpinning.Media;

public sealed class AudioReactiveService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private bool _disposed;

    public event EventHandler<float>? LevelChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_capture is not null) return;
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
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
