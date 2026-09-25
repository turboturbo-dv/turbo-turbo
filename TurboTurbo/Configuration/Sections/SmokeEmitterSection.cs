using System;
using System.Collections.Generic;

using TurboTurbo.Runtime;
using TurboTurbo.WorkBench;

namespace TurboTurbo.Configuration.Sections;

internal static class SmokeEmitterSection
{
    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var smokes = new List<SmokeParticles>();
        foreach (var e in host.Exhausts)
        {
            smokes.Add(e.Smoke);
        }

        if (smokes.Count == 0) return null;

        var section = new Section("Smoke emitter", onRequiresReconfigure) { OnToggle = onToggle };
        var f = smokes[0];
        section.AddFloat("Start size", "Particle size [m] at emission.",
            0.1f, 3f, true, () => f.tuning.startSize, v => { foreach (var s in smokes) s.tuning.startSize = v; }, TweakGrade.Basic);
        section.AddFloat("End size", "Particle size [m] at end of lifetime.",
            1f, 15f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in smokes) s.tuning.sizeOverLifetimeEnd = v; });
        section.AddFloat("Size variance", "Random variance in particle size:\n" +
                                          " * 0 means all particles are the exact same size as similarly-aged neighbours.\n" +
                                          " * 1 means all particles can be anywhere between half as small and twice as big relative to their neighbours.",
            0f, 1f, true, () => f.tuning.startSizeVariance, v => { foreach (var s in smokes) s.tuning.startSizeVariance = v; }, TweakGrade.Basic);
        section.AddFloat("Lifetime", "Smoke particle lifetime in seconds.",
            0.5f, 6f, false, () => f.tuning.lifetime, v => { foreach (var s in smokes) s.tuning.lifetime = v; });
        section.AddFloat("Min rate", "Emission rate [p/s] at zero load.",
            0f, 60f, false, () => f.tuning.idleEmissionRate, v => { foreach (var s in smokes) s.tuning.idleEmissionRate = v; });
        section.AddFloat("Max rate", "Emission rate [p/s] at full load.",
            0f, 150f, false, () => f.tuning.fullEmissionRate, v => { foreach (var s in smokes) s.tuning.fullEmissionRate = v; });
        section.AddFloat("Drag", "Strength of air drag on the particles.",
            0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in smokes) s.tuning.drag = v; });
        section.AddFloat("Buoyancy", "Constant upward drift [m/s].",
            -1f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in smokes) s.tuning.buoyancy = v; });
        section.AddFloat("Max angular velocity", "Max random spin speed [deg/s], sign-randomized per particle.",
            0f, 90f, false, () => f.tuning.angularVelocityMax, v => { foreach (var s in smokes) s.tuning.angularVelocityMax = v; });
        section.AddFloat("Turbulence strength", "Turbulence noise field strength at full dispersion speed (zero at standstill).",
            0f, 3f, false, () => f.tuning.turbulenceStrength, v => { foreach (var s in smokes) s.tuning.turbulenceStrength = v; });
        section.AddFloat("Turbulence frequency", "Turbulence noise field frequency (lower = larger cells).",
            0.05f, 2f, true, () => f.tuning.turbulenceFrequency, v => { foreach (var s in smokes) s.tuning.turbulenceFrequency = v; });
        section.AddFloat("Turbulence scroll speed", "Turbulence noise field scroll speed.",
            0f, 3f, true, () => f.tuning.turbulenceScrollSpeed, v => { foreach (var s in smokes) s.tuning.turbulenceScrollSpeed = v; });
        section.AddFloat("Light saturation", "Environmental light color saturation on the smoke. Lower values limit environmental coloring.",
            0f, 1f, false, () => f.tuning.lightSaturation, v => { foreach (var s in smokes) s.SetLightSaturation(v); });
        section.AddFloat("Max shadow floor", "Adjusts shadow intensity based on smoke color. Higher values reduce shadow intensity on light-colored smoke.",
            0f, 1f, false, () => f.tuning.maxShadowFloor, v => { foreach (var s in smokes) s.SetMaxShadowFloor(v); });
        section.AddFloat("Max dispersion speed", "Locomotive speed [m/s] at which dispersion reaches full strength.",
            1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in smokes) s.tuning.speedNormMax = v; });
        section.AddFloat("Dispersion lifetime multiplier", "Particle lifetime multiplier at full dispersion.",
            0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in smokes) s.tuning.speedLifetimeScale = v; });
        section.AddFloat("Dispersion jitter", "Extra emission jitter [m/s] at full dispersion.",
            0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in smokes) s.tuning.speedJitter = v; });
        section.AddFloat("Min fade distance", "Camera distance [m] below which smoke is fully faded out.",
            0f, 10f, false, () => f.tuning.minFadeDist, v => { foreach (var s in smokes) s.SetMinFadeDist(v); });
        section.AddFloat("Max fade distance", "Camera distance [m] above which smoke is fully visible.",
            0f, 10f, false, () => f.tuning.maxFadeDist, v => { foreach (var s in smokes) s.SetMaxFadeDist(v); });
        return section;
    }
}