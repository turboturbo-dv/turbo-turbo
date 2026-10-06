using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class VelocitySection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var section = new Section("Exhaust velocity") { OnToggle = onToggle };
        section.AddFloat("Velocity coefficient", "Controls exhaust velocity. Higher values give higher velocity.",
            2f, 10f, false, () => host.Velocity.ExhaustVelocityCoefficient, v => host.Velocity.ExhaustVelocityCoefficient = v, TweakGrade.Basic);
        return section;
    }
}
