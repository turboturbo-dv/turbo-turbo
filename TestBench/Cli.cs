using System.ComponentModel;
using System.Threading;

using Spectre.Console.Cli;

using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>Options shared by the report-only benches.</summary>
    public sealed class OnceSettings : CommandSettings
    {
        [CommandOption("--once")]
        [Description("Print the report once and exit instead of opening the interactive screen.")]
        public bool Once { get; set; }
    }

    /// <summary>Options for the benches that run the combustion model on its own.</summary>
    public sealed class EngineBenchSettings : CommandSettings
    {
        [CommandOption("--once")]
        [Description("Print the report once and exit instead of opening the interactive screen.")]
        public bool Once { get; set; }

        [CommandOption("--charger <CHARGER>")]
        [Description("Charger to run the engine with: Turbo or Atmospheric.")]
        [DefaultValue(ChargerKind.Turbo)]
        public ChargerKind Charger { get; set; } = ChargerKind.Turbo;
    }

    public sealed class ThermalCommand : Command<EngineBenchSettings>
    {
        public override int Execute(CommandContext context, EngineBenchSettings settings,
            CancellationToken cancellationToken)
        {
            ThermalMode.Run(settings.Once, settings.Charger);
            return 0;
        }
    }

    public sealed class IgnitionCommand : Command<EngineBenchSettings>
    {
        public override int Execute(CommandContext context, EngineBenchSettings settings,
            CancellationToken cancellationToken)
        {
            IgnitionMode.Run(settings.Once, settings.Charger);
            return 0;
        }
    }

    public sealed class StackCommand : Command<OnceSettings>
    {
        public override int Execute(CommandContext context, OnceSettings settings, CancellationToken cancellationToken)
        {
            StackMode.Run(settings.Once);
            return 0;
        }
    }

    public sealed class SimSettings : CommandSettings
    {
        [CommandOption("--once")]
        [Description("Advance the scripted simulation once and print the dashboard, instead of the realtime screen.")]
        public bool Once { get; set; }

        [CommandOption("--seconds <SECONDS>")]
        [Description("Simulated seconds to advance in --once mode.")]
        [DefaultValue(600f)]
        public float Seconds { get; set; } = 600f;

        [CommandOption("--rpm <RPM>")]
        [Description("Initial normalized rpm, from 0 to 1.")]
        [DefaultValue(0.3f)]
        public float Rpm { get; set; } = 0.3f;

        [CommandOption("--fps <FPS>")]
        [Description("Initial normalized fuel per stroke, from 0 to 1.")]
        [DefaultValue(0.10f)]
        public float Fps { get; set; } = 0.10f;
    }

    public sealed class SimCommand : Command<SimSettings>
    {
        public override int Execute(CommandContext context, SimSettings settings, CancellationToken cancellationToken)
        {
            SimMode.Run(settings.Once, settings.Seconds, settings.Rpm, settings.Fps);
            return 0;
        }
    }
}
