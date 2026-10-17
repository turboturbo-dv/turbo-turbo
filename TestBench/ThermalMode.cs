using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public static class ThermalMode
    {
        public static void Run(bool once, ChargerKind charger)
        {
            var settings = new CombustionModel.Settings();
            var bench = new ThermalBench(settings, charger);
            var ambientK = PhysicsConstants.ReferenceAmbientK;

            var fields = new BenchScreen.Field[]
            {
                new BenchScreen.Field
                {
                    Label = "Rated exhaust T [K]",
                    Description = "Rated exhaust gas temperature [K] at the exhaust mouth at full power. " +
                                  "This is a target; transient conditions may overshoot it.",
                    Default = settings.RatedExhaustTempK,
                    Get = () => settings.RatedExhaustTempK,
                    Set = v => settings.RatedExhaustTempK = v
                },
                new BenchScreen.Field
                {
                    Label = "Chamber tau [s]",
                    Description = "Combustion chamber surface thermal lag [s]. How quickly the chamber surfaces react " +
                                  "to a change in engine power. Lower = the chamber responds more quickly.",
                    Default = settings.TauChamber,
                    Get = () => settings.TauChamber,
                    Set = v => settings.TauChamber = v
                },
                new BenchScreen.Field
                {
                    Label = "Block tau [s]",
                    Description = "Chamber to engine block thermal lag [s]. How quickly the chamber surfaces feed heat " +
                                  "into the engine block. Lower = the block responds more quickly.",
                    Default = settings.TauEngine,
                    Get = () => settings.TauEngine,
                    Set = v => settings.TauEngine = v
                },
                new BenchScreen.Field
                {
                    Label = "Cooldown open tau [s]",
                    Description = "Block-to-ambient cooldown lag [s] with the thermostat open. Maximum cooling rate " +
                                  "when the block is cooled at full capacity. Lower = more cooling.",
                    Default = settings.TauCooldownOpen,
                    Get = () => settings.TauCooldownOpen,
                    Set = v => settings.TauCooldownOpen = v
                },
                new BenchScreen.Field
                {
                    Label = "Cooldown closed tau [s]",
                    Description = "Block-to-ambient cooldown lag [s] with the thermostat shut. Ambient cooling rate " +
                                  "when the block is not actively cooled. Lower = more cooling.",
                    Default = settings.TauCooldownClosed,
                    Get = () => settings.TauCooldownClosed,
                    Set = v => settings.TauCooldownClosed = v
                },
                new BenchScreen.Field
                {
                    Label = "Chamber gain [K]",
                    Description = "Combustion chamber surface temperature rise above the block at full power [K].",
                    Default = settings.ChamberGainK,
                    Get = () => settings.ChamberGainK,
                    Set = v => settings.ChamberGainK = v
                },
            };

            new BenchScreen($"TurboTurbo thermal TestBench ({charger})", fields,
                _ => bench.Report(ambientK), () => settings.Validate()).Run(once);
        }
    }
}
