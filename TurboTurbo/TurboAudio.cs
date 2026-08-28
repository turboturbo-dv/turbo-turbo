using System;
using BepInEx.Configuration;
using CommandTerminal;
using DV.Simulation.Ports;
using UnityEngine;
using UnityEngine.Audio;

namespace TurboTurbo;

/// <summary>
/// Config + console command + runtime audio for the turbo whine.
/// The whine is a per-sample DSP (GeminiTurboDsp) played through a streaming
/// AudioClip, driven by TurboModel's boost state so sound and power limiting
/// share one source of truth.
/// </summary>
internal static class TurboAudio
{
    internal static ConfigEntry<float> WhineVolume;
    internal static ConfigEntry<string> AudioMode;
    internal static ConfigEntry<float> GameStylePitchMin;
    internal static ConfigEntry<float> GameStylePitchMax;
    internal static ConfigEntry<float> CabCutoff;
    internal static ConfigEntry<float> ExtCutoff;
    internal static ConfigEntry<float> CabFadeTau;
    internal static ConfigEntry<float> MaxTurboRpm;
    internal static ConfigEntry<float> PitchScale;
    internal static ConfigEntry<float> WhineGain;
    internal static ConfigEntry<float> FlowGain;
    internal static ConfigEntry<float> DuctResGain;
    internal static ConfigEntry<float> DuctQ;

    private static ConfigFile _config;
    private static bool _commandsRegistered;

    internal static void Bind(ConfigFile config)
    {
        _config = config;
        WhineVolume = config.Bind("TurboAudio", "WhineVolume", 0.6f,
            "Turbo whine volume (0..1). Also settable via the 'turbovol' console command.");
        AudioMode = config.Bind("TurboAudio", "AudioMode", "GameStyle",
            "GameStyle = pre-rendered loop + per-frame pitch/volume like vanilla LayeredAudio (always smooth). DSP = live per-sample synthesis.");
        GameStylePitchMin = config.Bind("TurboAudio", "GameStylePitchMin", 0.11f,
            "[GameStyle] loop playback pitch at zero boost (loop is rendered at full load).");
        GameStylePitchMax = config.Bind("TurboAudio", "GameStylePitchMax", 1.0f,
            "[GameStyle] loop playback pitch at full boost.");
        CabCutoff = config.Bind("TurboAudio", "CabCutoff", 2000f,
            "Cab filter cutoff [Hz] when the player is inside the loco.");
        ExtCutoff = config.Bind("TurboAudio", "ExtCutoff", 18000f,
            "Cab filter cutoff [Hz] when outside (near-transparent).");
        CabFadeTau = config.Bind("TurboAudio", "CabFadeTau", 0.5f,
            "Seconds to crossfade the cab filter when entering/leaving the cab.");
        MaxTurboRpm = config.Bind("TurboAudio", "MaxTurboRpm", 36000f,
            "Peak turbo shaft speed [rpm] for a large-frame ~1600 kW engine turbo.");
        PitchScale = config.Bind("TurboAudio", "PitchScale", 1.0f,
            "Blade-passing frequency scale (1.0 = physical pitch, tops out ~7.2 kHz at full spool).");
        WhineGain = config.Bind("TurboAudio", "WhineGain", 0.4f,
            "Tonal whine branch gain.");
        FlowGain = config.Bind("TurboAudio", "FlowGain", 0.6f,
            "Broadband flow branch gain.");
        DuctResGain = config.Bind("TurboAudio", "DuctResGain", 0.5f,
            "Gain of the resonant intake-duct band-pass layered onto the flow branch.");
        DuctQ = config.Bind("TurboAudio", "DuctQ", 2.0f,
            "Resonance (Q) of the intake-duct band-pass.");
    }

    internal static void HandleUpdate()
    {
        if (_commandsRegistered || Terminal.Shell == null) return;

        CommandInfo cmd = Terminal.Shell.AddCommand(
            "turbovol",
            args =>
            {
                if (args.Length > 0)
                {
                    float v = args[0].Float;
                    if (Terminal.IssuedError) return;
                    WhineVolume.Value = Mathf.Clamp(v, 0f, 1f);
                    _config.Save();
                }
                Terminal.Log($"turbo whine volume = {WhineVolume.Value:0.00}");
            },
            0, 1, "Get/set turbo whine volume (0..1).", "[value]");
        Terminal.Autocomplete.Register(cmd);

        _commandsRegistered = true;
        TurboModel.Log.LogInfo("console command 'turbovol' registered");
    }

    internal static GeminiParams CreateParams()
    {
        return new GeminiParams
        {
            SampleRate = AudioSettings.outputSampleRate,
            MaxTurboRpm = MaxTurboRpm.Value,
            BpfScale = PitchScale.Value,
            WhineGain = WhineGain.Value,
            WhineGainExponent = 1.5,
            FlowGain = FlowGain.Value,
            DuctResGain = DuctResGain.Value,
            DuctQ = DuctQ.Value,
            CabFilter = true,
            CabFilterCutoffHz = ExtCutoff.Value,
        };
    }
}

/// <summary>
/// Per-loco turbo whine.
/// GameStyle mode: pre-rendered seamless loops (exterior + cab-filtered,
/// sample-aligned) played with per-frame pitch/volume exactly like vanilla
/// LayeredAudio - Unity ramps AudioSource parameters internally, so it is
/// always smooth.
/// DSP mode: live per-sample synthesis through a streaming AudioClip.
/// </summary>
internal sealed class TurboWhineAudio
{
    private readonly TrainCar _car;
    private readonly GeminiParams _params;
    private readonly GeminiTurboDsp _dsp;
    private readonly AudioClip _clip;
    private readonly GameObject _root;
    private readonly AudioSource _source;
    private readonly bool _isDspMode;

    private readonly AudioClip _clipCab;
    private readonly AudioSource _sourceCab;
    private readonly AudioSource _sourceExt;

    private double _audioBoost;   // eased 0..1 spool state used for audio
    private double _modelBoost;   // raw boost from TurboModel (diagnostics)
    private double _cabMix;       // 0 exterior .. 1 inside cab
    private double _demand;
    private double _rpmNorm;

    // diagnostics
    private long _pcmCalls;
    private float _lastRms;
    private float _lastPeak;
    private bool _loggedFirstPcm;
    private bool _loggedSignal;
    private double _diagTimer;

    internal TurboWhineAudio(TrainCar car, GeminiParams p)
    {
        _car = car;
        _params = p;
        _isDspMode = TurboAudio.AudioMode.Value.Equals("DSP", StringComparison.OrdinalIgnoreCase);

        int sr = p.SampleRate;
        _root = new GameObject("TurboTurbo.Whine");
        _root.transform.SetParent(car.transform, false);
        _root.transform.localPosition = new Vector3(0f, 3f, 0f);

        AudioMixerGroup mixerGroup = null;
        LayeredAudioPortReader reader = car.GetComponentInChildren<LayeredAudioPortReader>(true);
        if (reader != null)
        {
            LayeredAudio engineAudio = reader.GetComponent<LayeredAudio>();
            if (engineAudio != null) mixerGroup = engineAudio.audioMixerGroup;
        }

        if (_isDspMode)
        {
            _dsp = new GeminiTurboDsp(p) { UseExternalState = true };
            _clip = AudioClip.Create("TurboTurboWhine", sr, 1, sr, true, FillPcm);
            _source = _root.AddComponent<AudioSource>();
            _source.clip = _clip;
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.minDistance = 5f;
            _source.maxDistance = 300f;
            _source.playOnAwake = false;
            _source.outputAudioMixerGroup = mixerGroup;
            _source.volume = 0f;
            _source.Play();
            TurboModel.Log.LogInfo($"whine audio created (DSP) on [{_car.ID}] sr={sr} " +
                                   $"mixer={(mixerGroup != null ? mixerGroup.name : "<none>")}");
        }
        else
        {
            // two sample-aligned loops: identical seed, one cab-filtered
            var extParams = CloneParams(p, filtered: false);
            var cabParams = CloneParams(p, filtered: true);
            _clip = MakeLoopClip(extParams);
            _clipCab = MakeLoopClip(cabParams);

            _sourceExt = _root.AddComponent<AudioSource>();
            ConfigureLoopSource(_sourceExt, _clip, mixerGroup);
            _sourceCab = _root.AddComponent<AudioSource>();
            ConfigureLoopSource(_sourceCab, _clipCab, mixerGroup);
            TurboModel.Log.LogInfo($"whine audio created (GameStyle) on [{_car.ID}] " +
                                   $"loop={_clip.samples} samples sr={sr} mixer={(mixerGroup != null ? mixerGroup.name : "<none>")}");
        }
    }

    private static GeminiParams CloneParams(GeminiParams p, bool filtered)
    {
        return new GeminiParams
        {
            SampleRate = p.SampleRate,
            BladeCount = p.BladeCount,
            MaxTurboRpm = p.MaxTurboRpm,
            BpfScale = p.BpfScale,
            IdleEngineRpmNorm = p.IdleEngineRpmNorm,
            TauSpool = p.TauSpool,
            TauDump = p.TauDump,
            WhineGain = p.WhineGain,
            WhineGainExponent = p.WhineGainExponent,
            FlowGain = p.FlowGain,
            DuctResGain = p.DuctResGain,
            DuctQ = p.DuctQ,
            JitterHz = p.JitterHz,
            JitterAmount = p.JitterAmount,
            SurgeRateThreshold = p.SurgeRateThreshold,
            CabFilter = filtered,
            CabFilterCutoffHz = TurboAudio.CabCutoff.Value,
            Seed = p.Seed,
        };
    }

    private static AudioClip MakeLoopClip(GeminiParams p)
    {
        float[] samples = WhineSynthGemini.RenderLoop(p, 2.0, 1.0);
        var clip = AudioClip.Create("TurboTurboWhineLoop", samples.Length, 1, p.SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void ConfigureLoopSource(AudioSource src, AudioClip clip, AudioMixerGroup mixerGroup)
    {
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 1f;
        src.minDistance = 5f;
        src.maxDistance = 300f;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = mixerGroup;
        src.volume = 0f;
        src.Play();
    }

    /// <summary>Main-thread update: ease audio state toward game state.</summary>
    internal void UpdateFromModel(double boost01, double demand, double rpmNorm, float frameDt)
    {
        double boostTarget = TurboModel.SimActive ? boost01 : 0.0;
        _modelBoost = boost01;
        const double audioSpoolTau = 0.8; // seconds; matches the bench sweep easing
        _audioBoost += (boostTarget - _audioBoost) * (1.0 - Math.Exp(-frameDt / audioSpoolTau));

        double cabTarget = PlayerManager.Car == _car ? 1.0 : 0.0;
        double tauC = Math.Max(0.01, TurboAudio.CabFadeTau.Value);
        _cabMix += (cabTarget - _cabMix) * (1.0 - Math.Exp(-frameDt / tauC));

        _demand = demand;
        _rpmNorm = rpmNorm;

        if (_isDspMode)
        {
            _params.CabFilterCutoffHz = TurboAudio.ExtCutoff.Value
                + (TurboAudio.CabCutoff.Value - TurboAudio.ExtCutoff.Value) * _cabMix;
            _source.volume = TurboAudio.WhineVolume.Value;
        }
        else
        {
            // vanilla LayeredAudio mechanism: per-frame pitch + volume, Unity ramps internally
            float pitch = Mathf.Lerp(TurboAudio.GameStylePitchMin.Value, TurboAudio.GameStylePitchMax.Value, (float)_audioBoost);
            float vol = TurboAudio.WhineVolume.Value * Mathf.Pow((float)_audioBoost, 0.7f);
            _sourceExt.pitch = pitch;
            _sourceCab.pitch = pitch;
            _sourceExt.volume = vol * (float)(1.0 - _cabMix);
            _sourceCab.volume = vol * (float)_cabMix;
        }

        _diagTimer += frameDt;
        if (_diagTimer < 10.0) return;
        _diagTimer = 0.0;

        long calls = System.Threading.Interlocked.Read(ref _pcmCalls);
        TurboModel.Log.LogInfo($"whine: mode={( _isDspMode ? "DSP" : "GameStyle")} pcmCalls={calls} rms={_lastRms:0.0000} " +
                               $"audioBoost={_audioBoost:0.00} modelBoost={_modelBoost:0.00} demand={_demand:0.00} " +
                               $"rpmNorm={_rpmNorm:0.00} turboRpm={(_dsp != null ? _dsp.TurboRpm : 0):0} cab={_cabMix:0.0}");
        if (_isDspMode)
        {
            if (calls == 0)
            {
                TurboModel.Log.LogWarning("whine PCM callback never fired - streaming clip is not being read");
            }
            else if (_audioBoost > 0.2 && _lastRms < 0.0005f)
            {
                TurboModel.Log.LogWarning("whine PCM running but output is silent - DSP state problem");
            }
        }
    }

    internal void TriggerSurge() => _dsp.TriggerSurge();

    /// <summary>Audio-thread callback: fill with synthesized samples.</summary>
    private void FillPcm(float[] data)
    {
        const double makeupGain = 3.0; // offline renders normalize to 0.9 peak; real-time needs the same loudness headroom

        double dt = 1.0 / _params.SampleRate;
        _dsp.BeginBuffer(data.Length);
        _dsp.SetExternalState(
            _audioBoost * _params.MaxTurboRpm,
            1.0 + 2.5 * _audioBoost * Math.Max(0.0, _demand));

        double sumSq = 0.0, peak = 0.0;
        for (int i = 0; i < data.Length; i++)
        {
            double s = _dsp.ProcessSample(_rpmNorm, _demand, dt) * makeupGain;
            data[i] = (float)s;
            sumSq += s * s;
            double a = Math.Abs(s);
            if (a > peak) peak = a;
        }

        System.Threading.Interlocked.Increment(ref _pcmCalls);
        _lastRms = (float)Math.Sqrt(sumSq / data.Length);
        _lastPeak = (float)peak;

        if (!_loggedFirstPcm)
        {
            _loggedFirstPcm = true;
            TurboModel.Log.LogInfo($"whine PCM callback first fired (buffer={data.Length})");
        }
        if (!_loggedSignal && _lastPeak > 0.001f)
        {
            _loggedSignal = true;
            TurboModel.Log.LogInfo($"whine signal flowing (peak={_lastPeak:0.000})");
        }
    }

    internal void Destroy()
    {
        if (_sourceExt != null) _sourceExt.Stop();
        if (_sourceCab != null) _sourceCab.Stop();
        if (_source != null) _source.Stop();
        if (_clipCab != null) UnityEngine.Object.Destroy(_clipCab);
        if (_clip != null) UnityEngine.Object.Destroy(_clip);
        if (_root != null) UnityEngine.Object.Destroy(_root);
    }
}
