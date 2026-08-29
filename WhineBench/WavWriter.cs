using System;
using System.IO;

namespace WhineBench;

/// <summary>Writes float samples to a 16-bit PCM mono WAV file.</summary>
internal static class WavWriter
{
    public static void ExportWav(float[] samples, int sampleRate, string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            writer.Write(0x46464952);           // RIFF
            writer.Write(36 + dataBytes);
            writer.Write(0x45564157);           // WAVE
            writer.Write(0x20746D66);           // fmt
            writer.Write(16);
            writer.Write((short)1);             // PCM
            writer.Write((short)1);             // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);             // block align
            writer.Write((short)16);            // bits
            writer.Write(0x61746164);           // data
            writer.Write(dataBytes);
            foreach (float v in samples)
            {
                writer.Write((short)(Clamp(v, -1f, 1f) * 32767f));
            }
        }
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
}
