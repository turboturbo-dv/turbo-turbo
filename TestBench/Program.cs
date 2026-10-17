using Spectre.Console;
using Spectre.Console.Cli;

using TestBench;

// redirected output has no console width, and Spectre would wrap the wide tables at 80 columns
if (Console.IsOutputRedirected) AnsiConsole.Profile.Width = 240;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("TestBench");

    config.AddCommand<ThermalCommand>("thermal")
        .WithDescription("Engine heat flow and thermal response bench (default).");
    config.AddCommand<IgnitionCommand>("ignition")
        .WithDescription("Ignition, late burn and unburned fuel bench.");
    config.AddCommand<StackCommand>("stack")
        .WithDescription("Exhaust-stack model bench.");
    config.AddCommand<SimCommand>("sim")
        .WithDescription("Realtime engine + stack simulation.");
});

return app.Run(args);
