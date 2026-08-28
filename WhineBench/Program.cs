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
      --turborpm <rpm>  [gemini] max turbo shaft speed          (default 36000)
      --bpfscale <x>    [gemini] BPF scale (1.0 = physical)     (default 1.0)
      --whinegain <x>   [gemini] tonal whine branch gain        (default 0.4)
      --whineexp <x>    [gemini] whine gain exponent            (default 1.5)
      --flowgain <x>    [gemini] broadband flow branch gain     (default 0.6)
      --ductgain <x>    [gemini] intake duct resonance gain     (default 0.5)
      --ductq <x>       [gemini] intake duct resonance Q        (default 2.0)
      --jitter <x>      [gemini] pitch jitter amount            (default 0.008)
      --cab             [gemini] enable cab filter (muffled interior sound)
      --cabcut <hz>     [gemini] cab filter cutoff               (default 2000)
      --gamestyle       render the exact in-game loops + pitch-mapped sweep preview
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
        Write("sweep", sw, p.SampleRate, opts.TryGetValue("sweepout", out var so) ? so : "logs/whine_sweep.wav");
    }
}

if (renderGemini)
{
    int sr = I("samplerate", 44100);

    GeminiParams MakeParams(bool filtered) => new GeminiParams
    {
        SampleRate = sr,
        BladeCount = D("blades", 12),
        MaxTurboRpm = D("turborpm", 36000),
        BpfScale = D("bpfscale", 1.0),
        WhineGain = D("whinegain", 0.4),
        WhineGainExponent = D("whineexp", 1.5),
        FlowGain = D("flowgain", 0.6),
        DuctResGain = D("ductgain", 0.5),
        DuctQ = D("ductq", 2.0),
        JitterAmount = D("jitter", 0.008),
        JitterHz = D("jitterhz", 10),
        CabFilter = filtered,
        CabFilterCutoffHz = D("cabcut", 2000),
    };

    if (opts.ContainsKey("gamestyle"))
    {
        string tag = opts.TryGetValue("tag", out var tg) ? "_" + tg : "";
        // exactly what the mod plays: full-load seamless loop, pitched over boost
        float[] loopExt = WhineSynthGemini.RenderLoop(MakeParams(false), D("loopseconds", 2.0), 1.0);
        Write($"gamestyle loop (ext{tag})", loopExt, sr, $"logs/whine_loop_ext{tag}.wav");
        float[] loopCab = WhineSynthGemini.RenderLoop(MakeParams(true), D("loopseconds", 2.0), 1.0);
        Write($"gamestyle loop (cab{tag})", loopCab, sr, $"logs/whine_loop_cab{tag}.wav");

        double pitchMin = D("pitchmin", 0.11), pitchMax = D("pitchmax", 1.0), vol = D("volume", 0.8);
        Write($"gamestyle sweep (ext{tag})", WhineSynth.RenderSweep(loopExt, sr, D("sweep", 8), D("tau", 0.8), pitchMin, pitchMax, vol),
            sr, $"logs/whine_sweep_ext{tag}.wav");
        Write($"gamestyle sweep (cab{tag})", WhineSynth.RenderSweep(loopCab, sr, D("sweep", 8), D("tau", 0.8), pitchMin, pitchMax, vol),
            sr, $"logs/whine_sweep_cab{tag}.wav");
    }

    if (sweep)
    {
        float[] sw = WhineSynthGemini.RenderSweep(MakeParams(false), D("sweep", 8));
        Write("gemini sweep", sw, sr, opts.TryGetValue("sweepout", out var so) ? so : "logs/whine_sweep_gemini.wav");
    }
    else if (style == "gemini")
    {
        // gemini-only run without --sweep: default to a steady clip so something renders
        float[] steady = WhineSynthGemini.RenderSteady(MakeParams(false), D("gsteady", 6), 0.8);
        Write("gemini steady", steady, sr, opts.TryGetValue("steadyout", out var so) ? so : "logs/whine_steady_gemini.wav");
    }

    if (opts.ContainsKey("gsteady"))
    {
        float[] steady = WhineSynthGemini.RenderSteady(MakeParams(false), D("gsteady", 6), 0.8);
        Write("gemini steady", steady, sr, opts.TryGetValue("steadyout", out var so2) ? so2 : "logs/whine_steady_gemini.wav");
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
