using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

using Spectre.Console;
using Spectre.Console.Rendering;

using TurboTurbo;
using TurboTurbo.Modeling;
using TurboTurbo.WorkBench;

using UnityEngine;

using Color = UnityEngine.Color;

namespace TestBench
{
    public static class SimMode
    {
        private const string HelpText =
            "[grey]W/S rpm    E/D fps    1-5 speed (1x 2x 5x 10x 100x)    Space pause    R reset    Q quit[/]";

        private const float Step = 0.05f;
        private const float RefreshSeconds = 0.1f;
        private const float MaxFrameSeconds = 0.5f;

        // representative backgrounds to preview the plume against
        private static readonly Color Sky = new(0.53f, 0.81f, 0.92f);
        private static readonly Color Foliage = new(0.24f, 0.48f, 0.18f);

        public static void Run(string[] args)
        {
            var once = Array.Exists(args, a => a == "--once");
            var seconds = ArgFloat(args, "--seconds", 600f);
            var rpm = ArgFloat(args, "--rpm", 0.3f);
            var fps = ArgFloat(args, "--fps", 0.10f);

            var sim = new Sim(
                new CombustionModel.Settings(),
                new StackModel.Settings(),
                new ExhaustSmokeModel.Settings(),
                new ExhaustVelocitySettings(),
                PhysicsConstants.ReferenceAmbientK);

            if (once)
            {
                sim.Rpm = rpm;
                sim.Fps = fps;
                var steps = (int)(seconds / BenchCommon.Dt);
                for (var i = 0; i < steps; i++)
                {
                    sim.StepOnce();
                }

                AnsiConsole.Write(Root(sim, showHelp: false));
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var last = stopwatch.Elapsed;

            AnsiConsole.Live(Root(sim, showHelp: true)).Start(ctx =>
            {
                ctx.Refresh();

                var quit = false;
                while (!quit)
                {
                    while (Console.KeyAvailable)
                    {
                        switch (Console.ReadKey(true).Key)
                        {
                            case ConsoleKey.W:
                                sim.Rpm += Step;
                                break;
                            case ConsoleKey.S:
                                sim.Rpm -= Step;
                                break;
                            case ConsoleKey.E:
                                sim.Fps += Step;
                                break;
                            case ConsoleKey.D:
                                sim.Fps -= Step;
                                break;
                            case ConsoleKey.D1:
                                sim.Speed = 1f;
                                break;
                            case ConsoleKey.D2:
                                sim.Speed = 2f;
                                break;
                            case ConsoleKey.D3:
                                sim.Speed = 5f;
                                break;
                            case ConsoleKey.D4:
                                sim.Speed = 10f;
                                break;
                            case ConsoleKey.D5:
                                sim.Speed = 100f;
                                break;
                            case ConsoleKey.Spacebar:
                                sim.Paused = !sim.Paused;
                                break;
                            case ConsoleKey.R:
                                sim.Recreate();
                                break;
                            case ConsoleKey.Q:
                            case ConsoleKey.Escape:
                                quit = true;
                                break;
                        }
                    }

                    var now = stopwatch.Elapsed;
                    var elapsed = (float)(now - last).TotalSeconds;
                    last = now;
                    sim.Advance(Math.Min(elapsed, MaxFrameSeconds));

                    ctx.UpdateTarget(Root(sim, showHelp: true));
                    Thread.Sleep((int)(RefreshSeconds * 1000));
                }
            });
        }

        private static IRenderable Root(Sim sim, bool showHelp)
        {
            var parts = new List<IRenderable>
            {
                new Rule("[yellow]TurboTurbo sim TestBench[/]").LeftJustified(),
                Dashboard(sim),
            };

            if (showHelp) parts.Add(new Markup(HelpText));

            return new Rows(parts);
        }

        private static IRenderable Dashboard(Sim sim)
        {
            var e = sim.Engine;
            var s = sim.Stack;

            var input = Kv();
            input.AddRow("rpm", $"{sim.Rpm:P0}");
            input.AddRow("fps", $"{sim.Fps:P0}");
            input.AddRow("fuel", $"{sim.Rpm * sim.Fps:0.000}");
            input.AddRow("speed", sim.Paused ? "[yellow]paused[/]" : $"{sim.Speed:0}x");
            input.AddRow("sim time", BenchCommon.Fmt(sim.SimTime));

            var engine = Kv();
            engine.AddRow("charge", $"{e.Charge:0.000}");
            engine.AddRow("boost", $"{e.Boost:0.000}");
            engine.AddRow("lambda", $"{e.Lambda:0.00}");
            engine.AddRow("burn air", $"{e.BurnFractionAir:0.000}");
            engine.AddRow("burn temp", $"{e.BurnFractionTemp:0.000}");
            engine.AddRow("burn", $"{e.BurnFraction:0.000}");
            engine.AddRow("EGT manif", $"{BenchCommon.ToC(e.GasTemperature + e.TurbineTemperatureDropK):0.0} C");
            engine.AddRow("EGT mouth", $"{BenchCommon.ToC(e.GasTemperature):0.0} C");
            engine.AddRow("turbine drop", $"{e.TurbineTemperatureDropK:0.0} K");
            engine.AddRow("mass flow", $"{e.MassFlow:0.000}");
            engine.AddRow("exh energy", $"{e.ExhaustEnergy:0.000}");
            engine.AddRow("exh velocity", $"{e.ExhaustVelocity:0.0} m/s");

            var thermal = Kv();
            thermal.AddRow("cylinder", $"{BenchCommon.ToC(e.CylinderTempK):0.0} C");
            thermal.AddRow("block", $"{BenchCommon.ToC(e.EngineTempK):0.0} C");
            thermal.AddRow("thermostat", $"{e.ThermostatOpen:0.00}");

            var stack = Kv();
            stack.AddRow("wall", $"{BenchCommon.ToC(s.ExhaustWallTempK):0.0} C");
            stack.AddRow("wet stack", $"{s.WetStack:0.00}");
            stack.AddRow("deposit", $"{s.DepositRate:0.000}");
            stack.AddRow("slip", $"{s.SlipRate:0.000}");
            stack.AddRow("evaporate", $"{s.EvaporateRate:0.000}");
            stack.AddRow("vapour", $"{s.Vapour:0.00}");

            var smoke = Kv();
            var intensity = PlumeIntensity(sim);
            smoke.AddRow("colour", Swatch(sim.Smoke.Color));
            smoke.AddRow("intensity", $"{intensity:0.00}");
            smoke.AddRow("sky", Strip(Sky, sim.Smoke.Color, intensity));
            smoke.AddRow("foliage", Strip(Foliage, sim.Smoke.Color, intensity));
            smoke.AddRow("particulate", $"{sim.Smoke.ParticulateMass:0.00}");
            smoke.AddRow("max", $"{sim.Smoke.MaxParticulateMass:0.00}");

            var left = new Rows(new IRenderable[]
            {
                Box("input", input),
                Box("engine", engine),
            });

            var right = new Rows(new IRenderable[]
            {
                Box("thermal", thermal),
                Box("stack", stack),
                Box("smoke", smoke),
            });

            return new Columns(new IRenderable[] { left, right }) { Expand = false };
        }

        private static Table Kv()
        {
            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn("k");
            table.AddColumn(Render.Num("v"));
            return table;
        }

        private static Panel Box(string title, Table table) =>
            new Panel(table).Header(title).RoundedBorder();

        private static string Swatch(Color color)
        {
            var hex = Hex(color);
            return $"[on #{hex}]        [/] #{hex}";
        }

        // two columns of background, four of plume-blended colour, then two more background;
        // eight cells total, so the strip is exactly as wide as a normal swatch
        private static string Strip(Color background, Color smoke, float intensity)
        {
            var blended = Blend(smoke, background, intensity);
            var bg = Block(background);
            var blend = Block(blended);
            return bg + bg + blend + blend + blend + blend + bg + bg + $" {Hex(blended)}";
        }

        private static string Block(Color color) => $"[on #{Hex(color)}] [/]";

        private static string Hex(Color color)
        {
            var r = (int)Math.Round(Mathf.Clamp01(color.r) * 255f);
            var g = (int)Math.Round(Mathf.Clamp01(color.g) * 255f);
            var b = (int)Math.Round(Mathf.Clamp01(color.b) * 255f);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        // per-particle opacity at a 1 m particle width: the shader's size^falloff term becomes 1,
        // so tau reduces to the particulate mass produced per emitted particle
        private static float PlumeIntensity(Sim sim)
        {
            var heat = Mathf.Clamp01(sim.Engine.ExhaustEnergy);
            var rate = Mathf.Lerp(SmokeParticles.Settings.DefaultIdleEmissionRate,
                SmokeParticles.Settings.DefaultFullEmissionRate, heat);
            return Mathf.Clamp01(sim.Smoke.ParticulateMass / Mathf.Max(1e-4f, rate));
        }

        private static Color Blend(Color smoke, Color background, float alpha)
        {
            return new Color(
                (smoke.r * alpha) + (background.r * (1f - alpha)),
                (smoke.g * alpha) + (background.g * (1f - alpha)),
                (smoke.b * alpha) + (background.b * (1f - alpha)));
        }

        private static float ArgFloat(string[] args, string name, float fallback)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }

    /// <summary>Realtime engine + stack simulation state.</summary>
    internal sealed class Sim
    {
        private const int MaxStepsPerFrame = 500;

        private readonly CombustionModel.Settings _combustion;
        private readonly StackModel.Settings _stackSettings;
        private readonly ExhaustSmokeModel.Settings _smokeSettings;
        private readonly ExhaustVelocitySettings _velocity;
        private readonly float _ambientK;

        private float _rpm;
        private float _fps;
        private float _accumulator;

        public Sim(CombustionModel.Settings combustion, StackModel.Settings stackSettings,
            ExhaustSmokeModel.Settings smokeSettings, ExhaustVelocitySettings velocity, float ambientK)
        {
            _combustion = combustion;
            _stackSettings = stackSettings;
            _smokeSettings = smokeSettings;
            _velocity = velocity;
            _ambientK = ambientK;
            Recreate();
        }

        public CombustionModel Engine { get; private set; }
        public StackModel Stack { get; private set; }
        public ExhaustSmokeModel Smoke { get; private set; }
        public float SimTime { get; private set; }
        public float Speed { get; set; } = 1f;
        public bool Paused { get; set; }

        public float Rpm
        {
            get => _rpm;
            set => _rpm = Mathf.Clamp01(value);
        }

        public float Fps
        {
            get => _fps;
            set => _fps = Mathf.Clamp01(value);
        }

        public void Recreate()
        {
            var charger = new TurboCharger(new TurboCharger.Settings());
            Engine = new CombustionModel(() => 0f, () => _rpm * _fps, () => _rpm, () => _ambientK,
                charger, _combustion, _velocity);
            Stack = new StackModel(_stackSettings, _ambientK);
            Smoke = new ExhaustSmokeModel { Tuning = _smokeSettings };
            SimTime = 0f;
            _accumulator = 0f;
        }

        public void Advance(float realSeconds)
        {
            if (Paused || Speed <= 0f) return;

            _accumulator += realSeconds * Speed / BenchCommon.Dt;
            var steps = (int)_accumulator;
            if (steps <= 0) return;
            if (steps > MaxStepsPerFrame) steps = MaxStepsPerFrame;
            _accumulator -= steps;

            for (var i = 0; i < steps; i++)
            {
                StepOnce();
            }
        }

        public void StepOnce()
        {
            Engine.Tick(BenchCommon.Dt, engineOn: true);

            var unburned = Engine.FuelNorm * (1f - Engine.BurnFractionTemp);
            var flow = Mathf.Clamp01(Engine.MassFlow / Mathf.Max(1e-4f, Engine.Charger.ChargeAtFullPower));
            Stack.Tick(BenchCommon.Dt, engineOn: true, unburned, flow, Engine.GasTemperature, _ambientK);

            Smoke.Update(Engine.Lambda, Engine.RpmNorm, Engine.ExhaustEnergy, engineOn: true, BenchCommon.Dt,
                Stack.Vapour);

            SimTime += BenchCommon.Dt;
        }
    }
}
