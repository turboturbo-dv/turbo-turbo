using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public static class EngineMode
    {
        public static void Run(bool once)
        {
            var settings = new CombustionModel.Settings();
            var bench = new EngineBench(settings);
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
                    Label = "Cylinder tau [s]",
                    Description = "Combustion to cylinder thermal lag [s]. How quickly cylinder temperature reacts " +
                                  "to a change in engine power. Lower = the cylinders respond more quickly.",
                    Default = settings.TauCylinder,
                    Get = () => settings.TauCylinder,
                    Set = v => settings.TauCylinder = v
                },
                new BenchScreen.Field
                {
                    Label = "Block tau [s]",
                    Description = "Cylinder to engine block thermal lag [s]. How quickly the cylinders feed heat " +
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
                    Label = "Cylinder gain [K]",
                    Description = "Cylinder temperature rise above the block at full power [K]. Raise to increase the " +
                                  "temperature difference between cylinders and block at steady-state full power.",
                    Default = settings.CylinderGainK,
                    Get = () => settings.CylinderGainK,
                    Set = v => settings.CylinderGainK = v
                },
                new BenchScreen.Field
                {
                    Label = "Cold wall floor [K]",
                    Description = "Cylinder temperature [K] below which the full cold-temperature combustion penalty " +
                                  "is applied.",
                    Default = settings.ColdWallFloorK,
                    Get = () => settings.ColdWallFloorK,
                    Set = v => settings.ColdWallFloorK = v
                },
                new BenchScreen.Field
                {
                    Label = "Warm wall target [K]",
                    Description = "Cylinder temperature [K] above which the cold-temperature penalty has completely " +
                                  "disappeared.",
                    Default = settings.WarmWallTargetK,
                    Get = () => settings.WarmWallTargetK,
                    Set = v => settings.WarmWallTargetK = v
                },
                new BenchScreen.Field
                {
                    Label = "Min burn at cold",
                    Description = "Maximum reachable combustion efficiency when the full cold-temperature penalty " +
                                  "is applied.",
                    Default = settings.MinBurnFractionAtCold,
                    Get = () => settings.MinBurnFractionAtCold,
                    Set = v => settings.MinBurnFractionAtCold = v
                },
            };

            new BenchScreen("TurboTurbo engine TestBench", fields,
                () => bench.Report(ambientK), () => settings.Validate()).Run(once);
        }
    }
}
