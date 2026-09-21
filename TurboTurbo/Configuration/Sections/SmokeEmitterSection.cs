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

        var section = new Section("smoke emitter", "smokeEmitter", onRequiresReconfigure) { OnToggle = onToggle };
        var f = smokes[0];
        section.AddFloat("lifetime", "Particle lifetime in seconds.",
            0.5f, 6f, false, () => f.tuning.lifetime, v => { foreach (var s in smokes) s.tuning.lifetime = v; });
        section.AddFloat("startSizeMin", "Particle size range at emission [m].",
            0.1f, 3f, false, () => f.tuning.startSizeMin, v => { foreach (var s in smokes) s.tuning.startSizeMin = v; });
        section.AddFloat("startSizeMax", "Particle size range at emission [m].",
            0.1f, 3f, false, () => f.tuning.startSizeMax, v => { foreach (var s in smokes) s.tuning.startSizeMax = v; });
        section.AddFloat("sizeOverLifetimeStart", "Growth factor at emission.",
            0.1f, 3f, true, () => f.tuning.sizeOverLifetimeStart, v => { foreach (var s in smokes) s.tuning.sizeOverLifetimeStart = v; });
        section.AddFloat("sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
            1f, 15f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in smokes) s.tuning.sizeOverLifetimeEnd = v; });
        section.AddFloat("buoyancy", "Constant upward drift [m/s].",
            -1f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in smokes) s.tuning.buoyancy = v; });
        section.AddFloat("drag", "Air resistance decaying the inherited train velocity.",
            0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in smokes) s.tuning.drag = v; });
        section.AddFloat("angularVelocityMax", "Max random spin speed [deg/s], sign-randomized per particle.",
            0f, 90f, false, () => f.tuning.angularVelocityMax, v => { foreach (var s in smokes) s.tuning.angularVelocityMax = v; });
        section.AddFloat("idleEmissionRate", "Emission rate [p/s] at idle heat.",
            0f, 60f, false, () => f.tuning.idleEmissionRate, v => { foreach (var s in smokes) s.tuning.idleEmissionRate = v; });
        section.AddFloat("fullEmissionRate", "Emission rate [p/s] at full heat.",
            0f, 150f, false, () => f.tuning.fullEmissionRate, v => { foreach (var s in smokes) s.tuning.fullEmissionRate = v; });
        section.AddFloat("speedNormMax", "Speed [m/s] at which speed-based dispersion reaches full strength.",
            1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in smokes) s.tuning.speedNormMax = v; });
        section.AddFloat("speedLifetimeScale", "Particle lifetime multiplier at full dispersion.",
            0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in smokes) s.tuning.speedLifetimeScale = v; });
        section.AddFloat("speedJitter", "Extra emission jitter [m/s] at full dispersion.",
            0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in smokes) s.tuning.speedJitter = v; });
        section.AddFloat("turbulenceStrength", "Turbulence noise field strength at full dispersion speed (zero at standstill).",
            0f, 3f, false, () => f.tuning.turbulenceStrength, v => { foreach (var s in smokes) s.tuning.turbulenceStrength = v; });
        section.AddFloat("turbulenceFrequency", "Turbulence noise field frequency (lower = larger cells).",
            0.05f, 2f, true, () => f.tuning.turbulenceFrequency, v => { foreach (var s in smokes) s.tuning.turbulenceFrequency = v; });
        section.AddFloat("turbulenceScrollSpeed", "Turbulence noise field scroll speed.",
            0f, 3f, true, () => f.tuning.turbulenceScrollSpeed, v => { foreach (var s in smokes) s.tuning.turbulenceScrollSpeed = v; });
        section.AddFloat("lightSaturation", "Environmental light color saturation on the smoke. Lower values limit environmental coloring.",
            0f, 1f, false, () => f.tuning.lightSaturation, v => { foreach (var s in smokes) s.SetLightSaturation(v); });
        section.AddFloat("maxShadowFloor", "Adjusts shadow intensity based on smoke color. Higher values reduce shadow intensity on light-colored smoke.",
            0f, 1f, false, () => f.tuning.maxShadowFloor, v => { foreach (var s in smokes) s.SetMaxShadowFloor(v); });
        section.AddFloat("minFadeDist", "Camera distance [m] below which smoke is fully faded out.",
            0f, 10f, false, () => f.tuning.minFadeDist, v => { foreach (var s in smokes) s.SetMinFadeDist(v); });
        section.AddFloat("maxFadeDist", "Camera distance [m] above which smoke is fully visible.",
            0f, 10f, false, () => f.tuning.maxFadeDist, v => { foreach (var s in smokes) s.SetMaxFadeDist(v); });
        return section;
    }
}