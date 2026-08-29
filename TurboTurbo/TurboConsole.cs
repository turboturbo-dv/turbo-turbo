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
                    TurboAudio.WhineVolume.Value = Mathf.Clamp(v, 0f, 1f);
                    _config.Save();
                }
                Terminal.Log($"turbo whine volume = {TurboAudio.WhineVolume.Value:0.00}");
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
                            TurboModel.SmokeMaxRate.Value = Mathf.Clamp(v, 0f, 500f);
                            _config.Save();
                            break;
                        case "alpha":
                            TurboModel.SmokeParticleAlpha.Value = Mathf.Clamp(v, 0.05f, 1f);
                            _config.Save();
                            break;
                        case "size":
                            TurboModel.SmokeSizeMult.Value = Mathf.Clamp(v, 0.5f, 4f);
                            _config.Save();
                            break;
                        case "clean":
                            TurboModel.CleanRate.Value = Mathf.Clamp(v, 0f, 300f);
                            _config.Save();
                            break;
                        case "speed":
                            TurboModel.ExhaustSpeed.Value = Mathf.Clamp(v, 0.5f, 15f);
                            _config.Save();
                            break;
                        case "haze":
                            TurboModel.CleanAlpha.Value = Mathf.Clamp(v, 0.05f, 1f);
                            _config.Save();
                            break;
                        default:
                            Terminal.Log("unknown key - use rate, alpha, size, clean, speed or haze");
                            return;
                    }
                }
                Terminal.Log($"soot: rate={TurboModel.SmokeMaxRate.Value:0} alpha={TurboModel.SmokeParticleAlpha.Value:0.00} sizeMult={TurboModel.SmokeSizeMult.Value:0.00} " +
                             $"clean={TurboModel.CleanRate.Value:0} haze={TurboModel.CleanAlpha.Value:0.00} speed={TurboModel.ExhaustSpeed.Value:0.00} enabled={TurboModel.SmokeEnabled.Value}");
            },
            0, 2, "Get/set exhaust emitter parameters (rate/alpha/size = soot, clean/speed/haze = base haze).", "[rate|alpha|size|clean|speed|haze] [value]");
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
                            TurboModel.RpmTorqueExponent.Value = Mathf.Clamp(v, 0f, 1f);
                            _config.Save();
                            break;
                        case "boostexp":
                            TurboModel.RpmBoostExponent.Value = Mathf.Clamp(v, 0.5f, 2f);
                            _config.Save();
                            break;
                        default:
                            Terminal.Log("unknown key - use tqexp or boostexp");
                            return;
                    }
                }
                Terminal.Log($"turbo: tqexp={TurboModel.RpmTorqueExponent.Value:0.00} boostexp={TurboModel.RpmBoostExponent.Value:0.00}");
            },
            0, 2, "Get/set turbo physics exponents (tqexp = torque cap rpm blend, boostexp = boost ceiling rpm exponent).", "[tqexp|boostexp] [value]");
        Terminal.Autocomplete.Register(cfgCmd);

        _commandsRegistered = true;
        TurboModel.Log.LogInfo("console commands registered: turbovol, turbosmoke, turbocfg");
    }
}
