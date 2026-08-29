using System;
using NAudio.Wave;
using TurboTurbo;

namespace WhineBench;

/// <summary>
/// Realtime playback of the GameStyle whine: plays the rendered full-load
/// loop with variable-speed resampling (pitch) and gain, gliding smoothly
/// toward per-frame targets set by the console UI.
/// </summary>
internal sealed class TurboPlaybackProvider : ISampleProvider
{
    private volatile float[] _loop = new float[48000];
    private double _pitchTarget;
    private double _volumeTarget;
    private double _pitch;
    private double _volume;
    private double _phase;
    private const double GlideTau = 0.03; // seconds; kills buffer-rate zipper

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        float[] loop = _loop;
        int n = loop.Length;
        double dt = 1.0 / WaveFormat.SampleRate;
        double glide = 1.0 - Math.Exp(-dt / GlideTau);

        for (int i = 0; i < count; i++)
        {
            _pitch += (_pitchTarget - _pitch) * glide;
            _volume += (_volumeTarget - _volume) * glide;

            _phase += _pitch;
            if (_phase >= n) _phase -= n;
            int i0 = (int)_phase;
            int i1 = i0 + 1 >= n ? 0 : i0 + 1;
            double frac = _phase - Math.Floor(_phase);
            double s = loop[i0] * (1.0 - frac) + loop[i1] * frac;

            buffer[offset + i] = (float)(_volume * s);
        }
        return count;
    }

    /// <summary>Sets this frame's pitch and volume targets (UI thread).</summary>
    public void SetOutput(double pitch, double volume)
    {
        _pitchTarget = pitch;
        _volumeTarget = volume;
    }

    /// <summary>Atomically swaps in a freshly rendered loop (UI thread).</summary>
    public void SetLoop(float[] loop) => _loop = loop;

    /// <summary>The loop currently being played (for WAV export).</summary>
    public float[] CurrentLoop => _loop;
}
