using BepInEx.Configuration;
using CommandTerminal;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Registers and implements the in-game dev-console commands
/// (turbovol / turbosmoke / turbocfg). Registration is deferred until the
/// game's Terminal exists, then performed once.
/// </summary>
internal static class TurboConsole
{
    private static ConfigFile _config;
    private static bool _commandsRegistered;

    internal static void Bind(ConfigFile config)
    {
        _config = config;
    }

    internal static void HandleUpdate()
    {
        if (_commandsRegistered || Terminal.Shell == null) return;

        CommandInfo volCmd = Terminal.Shell.AddCommand(
            "turbovol",
            args =>
            {
                if (args.Length > 0)
                {
                    float v = args[0].Float;
                    if (Terminal.IssuedError) return;
                    TurboConfig.WhineVolume.Value = Mathf.Clamp(v, 0f, 1f);
                    _config.Save();
                }
                Terminal.Log($"turbo whine volume = {TurboConfig.WhineVolume.Value:0.00}");
            },
            0, 1, "Get/set turbo whine volume (0..1).", "[value]");
        Terminal.Autocomplete.Register(volCmd);

        CommandInfo smokeCmd = Terminal.Shell.AddCommand(
            "turbosmoke",
            args =>
            {
                if (args.Length >= 2)
                {
                    string key = args[0].String.ToLowerInvariant();
                    float v = args[1].Float;
                    if (Terminal.IssuedError) return;
                    switch (key)
                    {
                        case "rate":
                            TurboConfig.SmokeMaxRate.Value = Mathf.Clamp(v, 0f, 500f);
                            _config.Save();
                            break;
                        case "alpha":
                            TurboConfig.SmokeParticleAlpha.Value = Mathf.Clamp(v, 0.05f, 1f);
                            _config.Save();
                            break;
                        case "size":
                            TurboConfig.SmokeSizeMult.Value = Mathf.Clamp(v, 0.5f, 4f);
                            _config.Save();
                            break;
                        case "clean":
                            TurboConfig.CleanRate.Value = Mathf.Clamp(v, 0f, 300f);
                            _config.Save();
                            break;
                        case "speed":
                            TurboConfig.ExhaustSpeed.Value = Mathf.Clamp(v, 0.5f, 15f);
                            _config.Save();
                            break;
                        case "haze":
                            TurboConfig.CleanAlpha.Value = Mathf.Clamp(v, 0.05f, 1f);
                            _config.Save();
                            break;
                        case "shimmermode":
                            TurboConfig.HeatShimmerMode.Value = (int)Mathf.Clamp(v, 0f, 5f);
                            _config.Save();
                            break;
                        case "shimmer":
                            TurboConfig.HeatShimmerEnabled.Value = v > 0.5f;
                            _config.Save();
                            break;
                        case "shimmerstrength":
                            TurboConfig.HeatShimmerStrength.Value = Mathf.Clamp(v, 0f, 5f);
                            _config.Save();
                            break;
                        case "shimmerradius":
                            TurboConfig.HeatShimmerRadius.Value = Mathf.Clamp(v, 0.2f, 3f);
                            _config.Save();
                            break;
                        case "shimmerheight":
                            TurboConfig.HeatShimmerHeight.Value = Mathf.Clamp(v, 0.5f, 8f);
                            _config.Save();
                            break;
                        case "shimmerspeed":
                            TurboConfig.HeatShimmerSpeed.Value = Mathf.Clamp(v, 0f, 10f);
                            _config.Save();
                            break;
                        case "shimmerfreq":
                            TurboConfig.HeatShimmerFreq.Value = Mathf.Clamp(v, 0.2f, 6f);
                            _config.Save();
                            break;
                        default:
                            Terminal.Log("unknown key - use rate, alpha, size, clean, speed or haze");
                            return;
                    }
                }
                Terminal.Log($"soot: rate={TurboConfig.SmokeMaxRate.Value:0} alpha={TurboConfig.SmokeParticleAlpha.Value:0.00} sizeMult={TurboConfig.SmokeSizeMult.Value:0.00} " +
                             $"clean={TurboConfig.CleanRate.Value:0} haze={TurboConfig.CleanAlpha.Value:0.00} speed={TurboConfig.ExhaustSpeed.Value:0.00} enabled={TurboConfig.SmokeEnabled.Value} " +
                             $"shimmer={TurboConfig.HeatShimmerEnabled.Value} shimmermode={TurboConfig.HeatShimmerMode.Value} " +
                             $"shimmerstrength={TurboConfig.HeatShimmerStrength.Value:0.00} shimmerradius={TurboConfig.HeatShimmerRadius.Value:0.0} " +
                             $"shimmerheight={TurboConfig.HeatShimmerHeight.Value:0.0} shimmerspeed={TurboConfig.HeatShimmerSpeed.Value:0.0} shimmerfreq={TurboConfig.HeatShimmerFreq.Value:0.0}");
            },
            0, 2, "Get/set exhaust emitter parameters (rate/alpha/size = soot, clean/speed/haze = base haze, shimmer* = heat shimmer).", "[rate|alpha|size|clean|speed|haze|shimmer|shimmermode|shimmerstrength|shimmerradius|shimmerheight|shimmerspeed|shimmerfreq] [value]");
        Terminal.Autocomplete.Register(smokeCmd);

        CommandInfo cfgCmd = Terminal.Shell.AddCommand(
            "turbocfg",
            args =>
            {
                if (args.Length >= 2)
                {
                    string key = args[0].String.ToLowerInvariant();
                    float v = args[1].Float;
                    if (Terminal.IssuedError) return;
                    switch (key)
                    {
                        case "tqexp":
                            TurboConfig.RpmTorqueExponent.Value = Mathf.Clamp(v, 0f, 1f);
                            _config.Save();
                            break;
                        case "boostexp":
                            TurboConfig.RpmBoostExponent.Value = Mathf.Clamp(v, 0.5f, 2f);
                            _config.Save();
                            break;
                        default:
                            Terminal.Log("unknown key - use tqexp or boostexp");
                            return;
                    }
                }
                Terminal.Log($"turbo: tqexp={TurboConfig.RpmTorqueExponent.Value:0.00} boostexp={TurboConfig.RpmBoostExponent.Value:0.00}");
            },
            0, 2, "Get/set turbo physics exponents (tqexp = torque cap rpm blend, boostexp = boost ceiling rpm exponent).", "[tqexp|boostexp] [value]");
        Terminal.Autocomplete.Register(cfgCmd);

        _commandsRegistered = true;
        TurboModel.Log.LogInfo("console commands registered: turbovol, turbosmoke, turbocfg");
    }
}
