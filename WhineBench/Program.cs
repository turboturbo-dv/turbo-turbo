using System.Globalization;
using TurboTurbo;
using WhineBench;

const string usage = """
    WhineBench - renders the GameStyle turbo whine and writes WAVs.

    Usage: dotnet run --project WhineBench -c Release [-- options]

    Options (all optional):
      --blades <n>      compressor blade count                (default 12)
      --turborpm <rpm>  max turbo shaft speed                (default 36000)
      --bpfscale <x>    BPF scale (1.0 = physical)           (default 1.0)
      --whinegain <x>   tonal whine branch gain              (default 0.4)
      --whineexp <x>    whine gain exponent                  (default 3.5)
      --flowgain <x>    broadband flow branch gain           (default 0.6)
      --ductgain <x>    intake duct resonance gain           (default 0.5)
      --ductq <x>       intake duct resonance Q              (default 2.0)
      --jitter <x>      pitch jitter amount                  (default 0.008)
      --cab             enable cab filter (muffled interior sound)
      --cabcut <hz>     cab filter cutoff                    (default 2000)
      --gamestyle       render the exact in-game loops + pitch-mapped sweep preview
      --loopseconds <s> [gamestyle] loop duration             (default 2.0)
      --sweep <s>       render a spool sweep of this duration
      --gsteady <s>     also render steady-state clip at load 0.8 (default off)
      --tau <s>         sweep spool smoothing                (default 0.8)
      --pitchmin <x>    sweep pitch at zero boost            (default 0.11)
      --pitchmax <x>    sweep pitch at full boost            (default 1.0)
      --volume <x>      sweep peak volume                    (default 0.8)
      --volumeexp <x>   dipole volume curve exponent         (default 1.5, gamestyle sweeps)
      --out <path>      loop output                          (default logs/whine_loop.wav)
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

int sr = I("samplerate", 44100);
bool sweep = opts.ContainsKey("sweep");

GeminiParams MakeParams(bool filtered) => new GeminiParams
{
    SampleRate = sr,
    BladeCount = D("blades", 12),
    MaxTurboRpm = D("turborpm", 36000),
    BpfScale = D("bpfscale", 1.0),
    WhineGain = D("whinegain", 0.4),
    WhineGainExponent = D("whineexp", 3.5),
    FlowGain = D("flowgain", 0.6),
    DuctResGain = D("ductgain", 0.5),
    DuctQ = D("ductq", 2.0),
    JitterAmount = D("jitter", 0.008),
    JitterHz = D("jitterhz", 10),
    CabFilter = filtered,
    CabFilterCutoffHz = D("cabcut", 2000),
};

string lastWritten = null;

void Write(string label, float[] samples, int sampleRate, string path)
{
    WavWriter.ExportWav(samples, sampleRate, path);
    Console.WriteLine($"{label}: {Path.GetFullPath(path)}");
    lastWritten = path;
}

if (opts.ContainsKey("gamestyle"))
{
    string tag = opts.TryGetValue("tag", out var tg) ? "_" + tg : "";
    // exactly what the mod plays: full-load seamless loop, pitched over boost
    float[] loopExt = WhineSynthGemini.RenderLoop(MakeParams(false), D("loopseconds", 2.0), 1.0);
    Write($"gamestyle loop (ext{tag})", loopExt, sr, $"logs/whine_loop_ext{tag}.wav");
    float[] loopCab = WhineSynthGemini.RenderLoop(MakeParams(true), D("loopseconds", 2.0), 1.0);
    Write($"gamestyle loop (cab{tag})", loopCab, sr, $"logs/whine_loop_cab{tag}.wav");

    double pitchMin = D("pitchmin", 0.11), pitchMax = D("pitchmax", 1.0), vol = D("volume", 0.8);
    double volExp = D("volumeexp", 1.5);
    Write($"gamestyle sweep (ext{tag})", SweepPlayer.RenderSweep(loopExt, sr, D("sweep", 8), D("tau", 0.8), pitchMin, pitchMax, vol, volExp, true),
        sr, $"logs/whine_sweep_ext{tag}.wav");
    Write($"gamestyle sweep (cab{tag})", SweepPlayer.RenderSweep(loopCab, sr, D("sweep", 8), D("tau", 0.8), pitchMin, pitchMax, vol, volExp, true),
        sr, $"logs/whine_sweep_cab{tag}.wav");
}

if (sweep)
{
    float[] sw = WhineSynthGemini.RenderSweep(MakeParams(false), D("sweep", 8));
    Write("gemini sweep", sw, sr, opts.TryGetValue("sweepout", out var so) ? so : "logs/whine_sweep_gemini.wav");
}

if (opts.ContainsKey("gsteady"))
{
    float[] steady = WhineSynthGemini.RenderSteady(MakeParams(false), D("gsteady", 6), 0.8);
    Write("gemini steady", steady, sr, opts.TryGetValue("steadyout", out var so2) ? so2 : "logs/whine_steady_gemini.wav");
}

if (lastWritten != null && opts.ContainsKey("play"))
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = Path.GetFullPath(lastWritten),
        UseShellExecute = true,
    });
}
