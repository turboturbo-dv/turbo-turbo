using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public static class IgnitionMode
    {
        public static void Run(bool once, ChargerKind charger)
        {
            var settings = new CombustionModel.Settings();
            var bench = new IgnitionBench(settings, charger);
            var ambientK = PhysicsConstants.ReferenceAmbientK;

            var fields = new BenchScreen.Field[]
            {
                new BenchScreen.Field
                {
                    Label = "Polytropic exponent",
                    Description = "Polytropic exponent of compression at rated speed with warm walls.",
                    Default = settings.PolytropicExponent,
                    Get = () => settings.PolytropicExponent,
                    Set = v => settings.PolytropicExponent = v
                },
                new BenchScreen.Field
                {
                    Label = "Ignition temp [K]",
                    Description = "End-of-compression temperature [K] at which half of the injected fuel ignites in " +
                                  "time, at idle speed and 30 bar.",
                    Default = settings.IgnitionTempK,
                    Get = () => settings.IgnitionTempK,
                    Set = v => settings.IgnitionTempK = v
                },
                new BenchScreen.Field
                {
                    Label = "Ignition spread",
                    Description = "Half-width of the transition from misfiring to clean combustion, in units of the log " +
                                  "ignition margin.",
                    Default = settings.IgnitionSpread,
                    Get = () => settings.IgnitionSpread,
                    Set = v => settings.IgnitionSpread = v
                },
                new BenchScreen.Field
                {
                    Label = "Late burn work",
                    Description = "Work produced by fuel that burns late, relative to a clean burn.",
                    Default = settings.LateBurnWork,
                    Get = () => settings.LateBurnWork,
                    Set = v => settings.LateBurnWork = v
                },
                new BenchScreen.Field
                {
                    Label = "Quench fraction",
                    Description = "Fraction of the fuel that misses the ignition window and never burns.",
                    Default = settings.QuenchFraction,
                    Get = () => settings.QuenchFraction,
                    Set = v => settings.QuenchFraction = v
                },
            };

            new BenchScreen($"TurboTurbo ignition TestBench ({charger})", fields,
                full => bench.Report(ambientK, full), () => settings.Validate()).Run(once);
        }
    }
}
