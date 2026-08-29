using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// All configuration keys in one place. Section and key names match the
/// shipped config file, so existing installations keep their values.
/// </summary>
internal static class TurboConfig
{
    // --- SimInspector -------------------------------------------------
    internal static ConfigEntry<bool> InspectorEnabled;
    internal static ConfigEntry<bool> InspectorDieselOnly;
    internal static ConfigEntry<KeyboardShortcut> InspectorDumpCarKey;
    internal static ConfigEntry<KeyboardShortcut> InspectorDumpLocosKey;
    internal static ConfigEntry<KeyboardShortcut> InspectorWatchKey;
    internal static ConfigEntry<string> InspectorWatchPorts;
    internal static ConfigEntry<float> InspectorWatchInterval;

    // --- Turbo physics ------------------------------------------------
    internal static ConfigEntry<bool> SimEnabled;
    internal static ConfigEntry<KeyboardShortcut> SimToggleKey;
    internal static ConfigEntry<string> TurboLocos;
    internal static ConfigEntry<float> TauUp;
    internal static ConfigEntry<float> TauDown;
    internal static ConfigEntry<float> RpmTorqueExponent;
    internal static ConfigEntry<float> RpmBoostExponent;
    internal static ConfigEntry<float> AirNAFraction;
    internal static ConfigEntry<float> LambdaCalibration;
    internal static ConfigEntry<float> SmokeOnsetLambda;
    internal static ConfigEntry<float> SmokeOpaqueLambda;
    internal static ConfigEntry<float> TorqueLambdaFloor;
    internal static ConfigEntry<float> ThermalK;
    internal static ConfigEntry<float> MinSpoolTau;
    internal static ConfigEntry<bool> DebugLog;

    // --- Whine audio ---------------------------------------------------
    internal static ConfigEntry<float> WhineVolume;
    internal static ConfigEntry<string> AudioMode;
    internal static ConfigEntry<float> GameStylePitchMin;
    internal static ConfigEntry<float> GameStylePitchMax;
    internal static ConfigEntry<float> VolumeExponent;
    internal static ConfigEntry<float> CabCutoff;
    internal static ConfigEntry<float> ExtCutoff;
    internal static ConfigEntry<float> CabFadeTau;
    internal static ConfigEntry<float> MaxTurboRpm;
    internal static ConfigEntry<float> PitchScale;
    internal static ConfigEntry<float> WhineGain;
    internal static ConfigEntry<float> FlowGain;
    internal static ConfigEntry<float> DuctResGain;
    internal static ConfigEntry<float> DuctQ;

    // --- Exhaust smoke ---------------------------------------------------
    internal static ConfigEntry<bool> SmokeEnabled;
    internal static ConfigEntry<bool> TakeOverExhaust;
    internal static ConfigEntry<bool> HeatShimmerEnabled;
    internal static ConfigEntry<float> HeatShimmerStrength;
    internal static ConfigEntry<int> HeatShimmerMode;
    internal static ConfigEntry<float> HeatShimmerRadius;
    internal static ConfigEntry<float> HeatShimmerHeight;
    internal static ConfigEntry<float> HeatShimmerSpeed;
    internal static ConfigEntry<float> HeatShimmerFreq;
    internal static ConfigEntry<bool> HeatShimmerUsePostStack;
    internal static ConfigEntry<bool> HeatShimmerFullscreenTriangle;
    internal static ConfigEntry<float> CleanRate;
    internal static ConfigEntry<float> CleanAlpha;
    internal static ConfigEntry<float> ExhaustSpeed;
    internal static ConfigEntry<float> SmokeMaxRate;
    internal static ConfigEntry<float> SmokeParticleAlpha;
    internal static ConfigEntry<float> SmokeSizeMult;

    internal static void Bind(ConfigFile config)
    {
        // SimInspector
        InspectorEnabled = config.Bind("SimInspector", "Enabled", true,
            "Dump the sim component graph of cars to the log when they spawn.");
        InspectorDieselOnly = config.Bind("SimInspector", "DieselOnly", true,
            "Only dump diesel locomotives instead of every car.");
        InspectorDumpCarKey = config.Bind("SimInspector", "DumpCarKey", new KeyboardShortcut(KeyCode.F9),
            "Dump the sim graph of the car the player is currently in.");
        InspectorDumpLocosKey = config.Bind("SimInspector", "DumpLocosKey", new KeyboardShortcut(KeyCode.F8),
            "Dump the sim graph of all loaded diesel locomotives (nearest first).");
        InspectorWatchKey = config.Bind("SimInspector", "WatchKey", new KeyboardShortcut(KeyCode.F7),
            "Toggle live sampling of the current car's watched ports.");
        InspectorWatchPorts = config.Bind("SimInspector", "WatchPorts", "THROTTLE,GOAL_POWER,RPM,POWER_OUT,FUEL_CONS",
            "Comma-separated port id substrings to sample while watching.");
        InspectorWatchInterval = config.Bind("SimInspector", "WatchInterval", 0.1f,
            "Seconds between watch samples.");

        // Turbo physics
        SimEnabled = config.Bind("Turbo", "Enabled", true,
            "Master switch for the turbo simulation. Can also be toggled in-game with ToggleKey.");
        SimToggleKey = config.Bind("Turbo", "ToggleKey", new KeyboardShortcut(KeyCode.F6),
            "Hotkey to toggle the turbo simulation on/off while playing (A/B compare).");
        TurboLocos = config.Bind("Turbo", "TurboLocos", "LocoDiesel",
            "Comma-separated TrainCarType names whose engines are turbocharged (e.g. LocoDiesel,LocoDM3).");
        TauUp = config.Bind("Turbo", "TauUp", 3.0f,
            "Seconds of spool-up time constant (clean combustion).");
        TauDown = config.Bind("Turbo", "TauDown", 1.0f,
            "Seconds of blow-down (boost release) time constant. Active only when demand drops below boost.");
        RpmTorqueExponent = config.Bind("Turbo", "RpmTorqueExponent", 0f,
            "RPM blending in the torque cap: 0 = pure per-stroke charge (recommended - the boost envelope already carries the RPM dependence), 1 = strict airflow on top.");
        RpmBoostExponent = config.Bind("Turbo", "RpmBoostExponent", 1.2f,
            "RPM exponent bounding the boost equilibrium - exhaust mass flow scales with engine speed, so a lugging engine can never reach rated boost.");
        AirNAFraction = config.Bind("Turbo", "AirNAFraction", 0.55f,
            "Per-stroke charge index of naturally-aspirated operation (zero boost vs max boost).");
        LambdaCalibration = config.Bind("Turbo", "LambdaCalibration", 2.5f,
            "Air-to-fuel calibration constant for the lambda proxy (2.5 = full boost, full rack is exactly clean).");
        SmokeOnsetLambda = config.Bind("Turbo", "SmokeOnsetLambda", 0.85f,
            "Lambda where soot formation begins (fueling above this vs air is overfueling).");
        SmokeOpaqueLambda = config.Bind("Turbo", "SmokeOpaqueLambda", 0.45f,
            "Lambda where smoke reaches full opacity.");
        TorqueLambdaFloor = config.Bind("Turbo", "TorqueLambdaFloor", 0.7f,
            "Lambda below which extra fuel contributes no torque. Sets both the NA torque fraction at zero boost and how long the oxygen cap binds during spool (higher = wider power surge window, weaker lugging).");
        ThermalK = config.Bind("Turbo", "ThermalK", 0.8f,
            "Thermal enthalpy feedback strength: overfueling shortens spool-up time.");
        MinSpoolTau = config.Bind("Turbo", "MinSpoolTau", 0.5f,
            "Floor for the spool-up time constant (stability under heavy overfuel).");
        DebugLog = config.Bind("Turbo", "DebugLog", false,
            "Log overfuel/boost values while driving.");

        // Whine audio
        WhineVolume = config.Bind("TurboAudio", "WhineVolume", 0.4f,
            "Turbo whine volume (0..1). Also settable via the 'turbovol' console command.");
        AudioMode = config.Bind("TurboAudio", "AudioMode", "GameStyle",
            "GameStyle = pre-rendered loop + per-frame pitch/volume like vanilla LayeredAudio (always smooth). DSP = live per-sample synthesis.");
        GameStylePitchMin = config.Bind("TurboAudio", "GameStylePitchMin", 0.11f,
            "[GameStyle] loop playback pitch at zero boost (loop is rendered at full load).");
        GameStylePitchMax = config.Bind("TurboAudio", "GameStylePitchMax", 1.0f,
            "[GameStyle] loop playback pitch at full boost.");
        VolumeExponent = config.Bind("TurboAudio", "VolumeExponent", 1.5f,
            "Steepness of the whine volume curve over boost. Higher = whine stays submerged until high power (dipole aeroacoustic feel).");
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

        // Exhaust smoke
        SmokeEnabled = config.Bind("TurboSmoke", "Enabled", true,
            "Emit a black soot plume from the exhaust, driven by the smoke density signal.");
        HeatShimmerEnabled = config.Bind("TurboSmoke", "HeatShimmerEnabled", true,
            "Screen-space heat shimmer above hot exhausts (Route A: drives the shipped SCPE.Refraction effect with a generated DUDV map).");
        HeatShimmerStrength = config.Bind("TurboSmoke", "HeatShimmerStrength", 0.5f,
            "Heat shimmer strength multiplier.");
        HeatShimmerMode = config.Bind("TurboSmoke", "HeatShimmerMode", 0,
            "Glass shader probe preset: 0=mist pass A, 1=mist pass B, 2=weak mist, 3=droplet path, 4=column hidden, 5=custom bundle shader.");
        HeatShimmerRadius = config.Bind("TurboSmoke", "HeatShimmerRadius", 0.8f,
            "Heat column radius [m] (mode 5).");
        HeatShimmerHeight = config.Bind("TurboSmoke", "HeatShimmerHeight", 2.4f,
            "Heat column height [m] above the stack exit (mode 5).");
        HeatShimmerSpeed = config.Bind("TurboSmoke", "HeatShimmerSpeed", 1f,
            "Heat shimmer animation speed multiplier (mode 5).");
        HeatShimmerFreq = config.Bind("TurboSmoke", "HeatShimmerFreq", 1f,
            "Heat shimmer noise frequency multiplier - higher = finer wobble (mode 5).");
        HeatShimmerUsePostStack = config.Bind("TurboSmoke", "HeatShimmerUsePostStack", false,
            "Use the SCPE post-stack route (broken: zooms in DV's setup). When false, per-object GrabPass heat quads on the window glass shader are used instead.");
        HeatShimmerFullscreenTriangle = config.Bind("TurboSmoke", "HeatShimmerFullscreenTriangle", true,
            "Use PPv2's fullscreen-triangle blit instead of a plain Blit (post-stack route only; plain Blit breaks in DV's stack).");
        TakeOverExhaust = config.Bind("TurboSmoke", "TakeOverExhaust", true,
            "Our emitter replaces the vanilla exhaust system entirely (clean haze + soot in one). When false, vanilla keeps driving the clean exhaust and we only add soot.");
        CleanRate = config.Bind("TurboSmoke", "CleanRate", 30f,
            "Clean exhaust particle rate [particles/s] at full engine rpm (TakeOverExhaust only).");
        CleanAlpha = config.Bind("TurboSmoke", "CleanAlpha", 0.4f,
            "Opacity of the clean haze particles (the vanilla tint ships near-opaque for its additive shader - alpha-blend needs less).");
        ExhaustSpeed = config.Bind("TurboSmoke", "ExhaustSpeed", 2.5f,
            "Exhaust particle exit speed at full engine rpm (TakeOverExhaust only).");
        SmokeMaxRate = config.Bind("TurboSmoke", "MaxRate", 120f,
            "Soot particle emission rate [particles/s] at full smoke density.");
        SmokeParticleAlpha = config.Bind("TurboSmoke", "ParticleAlpha", 0.45f,
            "Peak opacity per soot particle. Lower = more translucent individual puffs.");
        SmokeSizeMult = config.Bind("TurboSmoke", "SizeMult", 0.9f,
            "Soot particle size relative to the vanilla exhaust particles.");

        TurboModel.Log = BepInEx.Logging.Logger.CreateLogSource("TurboModel");
        SimInspector.Log = BepInEx.Logging.Logger.CreateLogSource("SimInspector");
    }
}
