using System.Globalization;
using TurboTurbo;

const string usage = """
    WhineBench - synthesizes turbo whine and writes WAVs.

    Two synthesis styles, renderable alone or together:
      ours   - seamless loop + resampling pitch sweep (WhineSynth)
      gemini - per-sample DSP: BPF oscillator + wavefolding + jitter, tracked
               flow noise, surge flutter (WhineSynthGemini)

    Usage: dotnet run --project WhineBench -c Release [-- options]

    Options (all optional):
      --style <s>       ours | gemini | both           (default ours)
      --base <hz>       [ours] base partial frequency  (default 1150)
      --h2 <gain>       [ours] 2nd partial gain        (default 0.5)
      --h3 <gain>       [ours] 3rd partial gain        (default 0.25)
      --noise <gain>    [ours] whoosh gain             (default 0.18)
      --seconds <s>     [ours] loop duration           (default 2.5)
      --blades <n>      [gemini] compressor blade count          (default 12)
      --turborpm <rpm>  [gemini] max turbo shaft speed          (default 80000)
      --gsteady <s>     [gemini] also render steady-state clip at load 0.8 (default off)
      --sweep <s>       render a spool sweep of this duration
      --tau <s>         sweep spool smoothing (ours)  (default 0.8)
      --pitchmin <x>    [ours] pitch at zero boost    (default 0.5)
      --pitchmax <x>    [ours] pitch at full boost    (default 2.2)
      --volume <x>      [ours] sweep peak volume      (default 0.8)
      --out <path>      loop output                   (default logs/whine_loop.wav)
      --play            open the last written file with the default player
    """;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine(usage);
    return;
}

var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (int i = 0; i + 1 < args.Length; i++)
{
    if (args[i].StartsWith("--")) opts[args[i][2..]] = args[i + 1];
}

double D(string key, double fallback) =>
    opts.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
        ? v : fallback;

int I(string key, int fallback) => (int)D(key, fallback);

string style = opts.TryGetValue("style", out var s) ? s.ToLowerInvariant() : "ours";
bool renderOurs = style is "ours" or "both";
bool renderGemini = style is "gemini" or "both";
bool sweep = opts.ContainsKey("sweep");

string lastWritten = null;

void Write(string label, float[] samples, int sampleRate, string path)
{
    WhineSynth.ExportWav(samples, sampleRate, path);
    Console.WriteLine($"{label}: {Path.GetFullPath(path)}");
    lastWritten = path;
}

if (renderOurs)
{
    var p = new WhineParams
    {
        BaseHz = D("base", 1150),
        Harmonic2 = D("h2", 0.5),
        Harmonic3 = D("h3", 0.25),
        NoiseGain = D("noise", 0.18),
        Seconds = D("seconds", 2.5),
        SampleRate = I("samplerate", 44100),
    };

    float[] loop = WhineSynth.Synthesize(p);
    Write("loop", loop, p.SampleRate, opts.TryGetValue("out", out var o) ? o : "logs/whine_loop.wav");

    if (sweep)
    {
        float[] sw = WhineSynth.RenderSweep(loop, p.SampleRate,
            D("sweep", 8), D("tau", 0.8), D("pitchmin", 0.5), D("pitchmax", 2.2), D("volume", 0.8));
        Write("sweep", sw, p.SampleRate, "logs/whine_sweep.wav");
    }
}

if (renderGemini)
{
    var g = new GeminiParams
    {
        SampleRate = I("samplerate", 44100),
        BladeCount = D("blades", 12),
        MaxTurboRpm = D("turborpm", 80000),
    };

    if (sweep)
    {
        float[] sw = WhineSynthGemini.RenderSweep(g, D("sweep", 8));
        Write("gemini sweep", sw, g.SampleRate, "logs/whine_sweep_gemini.wav");
    }
    else if (style == "gemini")
    {
        // gemini-only run without --sweep: default to a steady clip so something renders
        float[] steady = WhineSynthGemini.RenderSteady(g, D("gsteady", 6), 0.8);
        Write("gemini steady", steady, g.SampleRate, "logs/whine_steady_gemini.wav");
    }

    if (opts.ContainsKey("gsteady"))
    {
        float[] steady = WhineSynthGemini.RenderSteady(g, D("gsteady", 6), 0.8);
        Write("gemini steady", steady, g.SampleRate, "logs/whine_steady_gemini.wav");
    }
}

if (lastWritten != null && opts.ContainsKey("play"))
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = Path.GetFullPath(lastWritten),
        UseShellExecute = true,
    });
}
