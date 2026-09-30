using System;

using TurboTurbo.Modeling;
using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class VelocitySection
{
    private static readonly ExhaustVelocitySettings _defaults = new();

    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var section = new Section("Exhaust velocity") { OnToggle = onToggle };
        section.AddFloat("Min load", "Exhaust plume speed [m/s] at zero load.\n" +
                                     "Normally you will want to keep this low, but consider raising it if smoke has " +
                                     "too much trouble clearing the loco when coasting at high speed.",
            0f, 5f, false, () => host.Velocity.Idle, v => host.Velocity.Idle = v, TweakGrade.Basic);
        section.AddFloat("Max load", "Exhaust plume speed [m/s] at full load.\n" +
                                     $"The DE6 default is {_defaults.FullLoad:0.0}. For less powerful engines, " +
                                     $"consider a lower value, as they tend not to project smoke out as far.\n" +
                                     $"More powerful engines may want a slight increase.",
            0f, 25f, false, () => host.Velocity.FullLoad, v => host.Velocity.FullLoad = v, TweakGrade.Basic);
        return section;
    }
}
