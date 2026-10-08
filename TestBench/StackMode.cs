using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public static class StackMode
    {
        public static void Run(bool once)
        {
            var settings = new CombustionModel.Settings();
            settings.Validate();
            var stackSettings = new StackModel.Settings();
            var bench = new StackBench(settings, stackSettings);
            var ambientK = PhysicsConstants.ReferenceAmbientK;

            var fields = new BenchScreen.Field[]
            {
                new BenchScreen.Field
                {
                    Label = "Stack fill time [s]",
                    Description = "Time [s] of cold idling needed to fill an empty exhaust stack with unburned fuel. " +
                                  "Lower = the stack fouls more quickly.",
                    Default = stackSettings.FillTime,
                    Get = () => stackSettings.FillTime,
                    Set = v => stackSettings.FillTime = v
                },
                new BenchScreen.Field
                {
                    Label = "Stack clear time [s]",
                    Description = "Time [s] for a full exhaust stack to dry out at full exhaust temperature. " +
                                  "Lower = the stack clears more quickly once hot.",
                    Default = stackSettings.ClearTime,
                    Get = () => stackSettings.ClearTime,
                    Set = v => stackSettings.ClearTime = v
                },
                new BenchScreen.Field
                {
                    Label = "Stack wall tau [s]",
                    Description = "Thermal lag [s] of the exhaust metal. Higher = the stack reacts more slowly to " +
                                  "changes in exhaust temperature, so it warms up and dries out more slowly.",
                    Default = stackSettings.TauWall,
                    Get = () => stackSettings.TauWall,
                    Set = v => stackSettings.TauWall = v
                },
                new BenchScreen.Field
                {
                    Label = "Flow warm bias",
                    Description = "Speeds up exhaust wall warm-up with exhaust flow, so idling heats the stack more " +
                                  "slowly than a hard pull.",
                    Default = stackSettings.FlowWarmBias,
                    Get = () => stackSettings.FlowWarmBias,
                    Set = v => stackSettings.FlowWarmBias = v
                },
                new BenchScreen.Field
                {
                    Label = "Capture cold [K]",
                    Description = "Exhaust wall temperature [K] at and below which unburned fuel fully condenses onto " +
                                  "the stack.",
                    Default = stackSettings.CaptureColdK,
                    Get = () => stackSettings.CaptureColdK,
                    Set = v => stackSettings.CaptureColdK = v
                },
                new BenchScreen.Field
                {
                    Label = "Capture hot [K]",
                    Description = "Exhaust wall temperature [K] at and above which unburned fuel no longer condenses.",
                    Default = stackSettings.CaptureHotK,
                    Get = () => stackSettings.CaptureHotK,
                    Set = v => stackSettings.CaptureHotK = v
                },
                new BenchScreen.Field
                {
                    Label = "Clear start [K]",
                    Description = "Exhaust wall temperature [K] at which condensed fuel starts to boil back off.",
                    Default = stackSettings.ClearStartK,
                    Get = () => stackSettings.ClearStartK,
                    Set = v => stackSettings.ClearStartK = v
                },
                new BenchScreen.Field
                {
                    Label = "Clear full [K]",
                    Description = "Exhaust wall temperature [K] at which condensed fuel boils off at its full rate.",
                    Default = stackSettings.ClearFullK,
                    Get = () => stackSettings.ClearFullK,
                    Set = v => stackSettings.ClearFullK = v
                },
                new BenchScreen.Field
                {
                    Label = "Contact idle",
                    Description = "Fraction of unburned fuel that contacts the stack wall at zero exhaust flow.",
                    Default = stackSettings.ContactIdle,
                    Get = () => stackSettings.ContactIdle,
                    Set = v => stackSettings.ContactIdle = v
                },
                new BenchScreen.Field
                {
                    Label = "Contact full flow",
                    Description = "Fraction of unburned fuel that contacts the stack wall at full exhaust flow.",
                    Default = stackSettings.ContactFullFlow,
                    Get = () => stackSettings.ContactFullFlow,
                    Set = v => stackSettings.ContactFullFlow = v
                },
                new BenchScreen.Field
                {
                    Label = "Max deposit",
                    Description = "Cap on the fraction of unburned fuel that deposits, leaving a floor that always " +
                                  "escapes as visible vapour.",
                    Default = stackSettings.StickMax,
                    Get = () => stackSettings.StickMax,
                    Set = v => stackSettings.StickMax = v
                },
                new BenchScreen.Field
                {
                    Label = "Reference unburned",
                    Description = "Unburned fuel rate used to calibrate the fill time. Should match a cold idle if the " +
                                  "fill time is to be accurate.",
                    Default = stackSettings.ReferenceUnburned,
                    Get = () => stackSettings.ReferenceUnburned,
                    Set = v => stackSettings.ReferenceUnburned = v
                },
            };

            new BenchScreen("TurboTurbo stack TestBench", fields,
                () => bench.StackReport(ambientK), () => stackSettings.Validate()).Run(once);
        }
    }
}
