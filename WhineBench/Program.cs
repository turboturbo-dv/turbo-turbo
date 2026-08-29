using System.Diagnostics;
using System.Runtime;
using System.Text;
using NAudio.Wave;
using TurboTurbo;
using WhineBench;

const double PitchMin = 0.11, PitchMax = 1.0;   // loop pitch range over boost
const double SpoolTau = 0.8;                    // boost easing time constant

GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

double throttleTarget = 0.0, boost = 0.0;
bool engineOn = false;
double masterVolume = 0.8;
double blades = 12, bpfScale = 1.0, whineGain = 0.4, flowGain = 0.6, ductGain = 0.5, ductQ = 2.0, jitter = 0.008;
bool cabFilter = false;
bool rebuild = true;
bool showHelp = true;
bool quit = false;

var provider = new TurboPlaybackProvider();
IWavePlayer output = new WaveOutEvent { DesiredLatency = 200, NumberOfBuffers = 4 };
output.Init(provider.ToWaveProvider());
output.Play();

GeminiParams MakeParams(bool filtered) => new GeminiParams
{
    SampleRate = 48000,
    BladeCount = blades,
    MaxTurboRpm = 36000,
    BpfScale = bpfScale,
    IdleEngineRpmNorm = 0.332,
    TauSpool = 1.8,
    TauDump = 1.2,
    WhineGain = whineGain,
    WhineGainExponent = 3.5,
    FlowGain = flowGain,
    DuctResGain = ductGain,
    DuctQ = ductQ,
    JitterAmount = jitter,
    JitterHz = 10,
    CabFilter = filtered,
    CabFilterCutoffHz = 2000,
};

void RebuildLoop()
{
    // renders the exact loop the mod's GameStyle path would play with the
    // current synthesis parameters (full load, seamless)
    provider.SetLoop(WhineSynthGemini.RenderLoop(MakeParams(cabFilter), 8.0, 1.0));
}

RebuildLoop();

Console.Clear();
Console.CursorVisible = false;
var clock = Stopwatch.StartNew();
double lastTick = 0;
double lastDraw = -1;
double wobblePhase = 0;
var panel = new StringBuilder(1024);

while (!quit)
{
    // ---- keyboard ----------------------------------------------------
    while (Console.KeyAvailable)
    {
        switch (Console.ReadKey(true).Key)
        {
            case ConsoleKey.UpArrow: engineOn = true; throttleTarget = Math.Min(1.0, throttleTarget + 0.05); break;
            case ConsoleKey.DownArrow: throttleTarget = Math.Max(0.0, throttleTarget - 0.05); break;
            case ConsoleKey.E: engineOn = !engineOn; if (!engineOn) throttleTarget = 0.0; break;
            case ConsoleKey.O: masterVolume = Math.Min(1.0, masterVolume + 0.1); break;
            case ConsoleKey.P: masterVolume = Math.Max(0.0, masterVolume - 0.1); break;
            case ConsoleKey.Z: blades = Math.Max(8, blades - 1); rebuild = true; break;
            case ConsoleKey.X: blades = Math.Min(20, blades + 1); rebuild = true; break;
            case ConsoleKey.C: bpfScale = Math.Min(1.4, bpfScale + 0.05); rebuild = true; break;
            case ConsoleKey.V: bpfScale = Math.Max(0.5, bpfScale - 0.05); rebuild = true; break;
            case ConsoleKey.D1: whineGain = Math.Min(1.5, whineGain + 0.1); rebuild = true; break;
            case ConsoleKey.D2: whineGain = Math.Max(0.0, whineGain - 0.1); rebuild = true; break;
            case ConsoleKey.D3: flowGain = Math.Min(2.0, flowGain + 0.1); rebuild = true; break;
            case ConsoleKey.D4: flowGain = Math.Max(0.0, flowGain - 0.1); rebuild = true; break;
            case ConsoleKey.D5: ductGain = Math.Min(2.0, ductGain + 0.1); rebuild = true; break;
            case ConsoleKey.D6: ductGain = Math.Max(0.0, ductGain - 0.1); rebuild = true; break;
            case ConsoleKey.D7: ductQ = Math.Min(6.0, ductQ + 0.25); rebuild = true; break;
            case ConsoleKey.D8: ductQ = Math.Max(0.5, ductQ - 0.25); rebuild = true; break;
            case ConsoleKey.D9: jitter = Math.Min(0.03, jitter + 0.002); rebuild = true; break;
            case ConsoleKey.D0: jitter = Math.Max(0.0, jitter - 0.002); rebuild = true; break;
            case ConsoleKey.F: cabFilter = !cabFilter; rebuild = true; break;
            case ConsoleKey.W: WavWriter.ExportWav(provider.CurrentLoop, 48000, "logs/whine_bench_export.wav"); break;
            case ConsoleKey.H: showHelp = !showHelp; Console.Clear(); break;
            case ConsoleKey.Escape: quit = true; break;
        }
    }

    if (rebuild)
    {
        RebuildLoop();
        rebuild = false;
    }

    // ---- engine state --------------------------------------------------
    double now = clock.ElapsedTicks / (double)Stopwatch.Frequency;
    double dt = Math.Min(0.1, now - lastTick);
    lastTick = now;

    double demand = engineOn ? throttleTarget : 0.0;
    boost += (demand - boost) * (1.0 - Math.Exp(-dt / SpoolTau));

    // slow organic pitch wobble (replaces the loop-render jitter)
    wobblePhase += dt * 2.0 * Math.PI * 0.5;
    double wobble = 1.0 + 0.003 * Math.Sin(wobblePhase);

    double pitch = (PitchMin + (PitchMax - PitchMin) * boost) * wobble;
    double boostDelta = Math.Min(1.0, boost * demand);
    double volume = masterVolume * Math.Pow(boost, 1.5) * (0.10 + 0.90 * boostDelta);
    provider.SetOutput(pitch, volume);

    // ---- status panel (5 Hz, pre-built to keep the audio thread GC-quiet) --
    if (now - lastDraw > 0.2)
    {
        lastDraw = now;
        var sb = panel;
        sb.Clear();
        if (showHelp)
        {
            sb.AppendLine("TurboTurbo realtime bench");
            sb.AppendLine("=========================");
            sb.AppendLine("Up/Down  throttle +/-0.05        E        engine on/off");
            sb.AppendLine("O/P      master volume -/+       Z/X      blades -/+");
            sb.AppendLine("C/V      bpf scale -/+0.05       1/2      whine gain -/+");
            sb.AppendLine("3/4      flow gain -/+           5/6      duct gain -/+");
            sb.AppendLine("7/8      duct Q -/+0.25          9/0      pitch jitter -/+");
            sb.AppendLine("F        cab filter toggle       W        export loop to WAV");
            sb.AppendLine("H        hide help               Esc      quit");
        }
        sb.AppendLine(
            $"engine={(engineOn ? "RUNNING" : "OFF     ")} boost={boost:0.00} tone={7200 * pitch:0} Hz  " +
            $"vol={volume:0.00} cab={(cabFilter ? "IN " : "OUT")} blades={blades:0} bpf={bpfScale:0.00} " +
            $"wGain={whineGain:0.00} fGain={flowGain:0.00} duct={ductGain:0.00}/Q{ductQ:0.00} jit={jitter:0.000}");
        Console.SetCursorPosition(0, 0);
        Console.Write(sb.ToString());
    }

    Thread.Sleep(10);
}

output.Stop();
output.Dispose();
Console.CursorVisible = true;
Console.Clear();
