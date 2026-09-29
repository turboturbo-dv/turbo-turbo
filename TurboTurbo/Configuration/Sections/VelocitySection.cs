using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class VelocitySection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var section = new Section("Exhaust velocity") { OnToggle = onToggle };
        section.AddFloat("Min load", "Exhaust plume speed [m/s] at zero load.\n" +
                                     "Consider raising this if smoke has trouble clearing the loco at low power " +
                                     "and high speed.",
            0f, 5f, false, () => host.Velocity.Idle, v => host.Velocity.Idle = v, TweakGrade.Basic);
        section.AddFloat("Max load", "Exhaust plume speed [m/s] at full load.",
            0f, 20f, false, () => host.Velocity.FullLoad, v => host.Velocity.FullLoad = v, TweakGrade.Basic);
        return section;
    }
}