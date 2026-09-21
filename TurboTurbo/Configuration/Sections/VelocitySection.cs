using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class VelocitySection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var section = new Section("exhaust velocity", "exhaustVelocity") { OnToggle = onToggle };
        section.AddFloat("idleVelocity", "Exhaust plume speed [m/s] at idle heat.",
            0f, 5f, false, () => host.Velocity.Idle, v => host.Velocity.Idle = v);
        section.AddFloat("fullLoadVelocity", "Exhaust plume speed [m/s] at full heat.",
            0f, 20f, false, () => host.Velocity.FullLoad, v => host.Velocity.FullLoad = v);
        return section;
    }
}