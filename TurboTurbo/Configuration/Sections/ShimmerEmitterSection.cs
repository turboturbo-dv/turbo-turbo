using System;
using System.Collections.Generic;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ShimmerEmitterSection
{
    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var shimmers = new List<ShimmerParticles>();
        foreach (var e in host.Exhausts)
        {
            shimmers.Add(e.Shimmer);
        }

        if (shimmers.Count == 0) return null;

        var section = new Section("Shimmer emitter", onRequiresReconfigure) { OnToggle = onToggle };
        var f = shimmers[0];
        section.AddFloat("Start size", "Particle size [m] at emission.",
            0.1f, 3f, true, () => f.tuning.startSize, v => { foreach (var s in shimmers) s.tuning.startSize = v; }, TweakGrade.Basic);
        section.AddFloat("End size", "Particle size [m] at end of lifetime.",
            1f, 10f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in shimmers) s.tuning.sizeOverLifetimeEnd = v; });
        section.AddFloat("Size variance", "Random variance in particle size:\n" +
                                          " * 0 means all particles are the exact same size as similarly-aged neighbours.\n" +
                                          " * 1 means all particles can be anywhere between half as small and twice as big relative to their neighbours.",
            0f, 1f, true, () => f.tuning.startSizeVariance, v => { foreach (var s in shimmers) s.tuning.startSizeVariance = v; }, TweakGrade.Basic);
        section.AddFloat("Lifetime", "Particle lifetime in seconds.",
            0.5f, 6f, true, () => f.tuning.lifetime, v => { foreach (var s in shimmers) s.tuning.lifetime = v; });
        section.AddFloat("Min rate", "Emission rate [p/s] at zero load.",
            0f, 20f, false, () => f.tuning.idleRate, v => { foreach (var s in shimmers) s.tuning.idleRate = v; });
        section.AddFloat("Max rate", "Emission rate [p/s] at full load.",
            0f, 40f, false, () => f.tuning.fullRate, v => { foreach (var s in shimmers) s.tuning.fullRate = v; });
        section.AddFloat("Drag", "Strength of air drag on the particles.",
            0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in shimmers) s.tuning.drag = v; });
        section.AddFloat("Buoyancy", "Constant upward drift [m/s].",
            0f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in shimmers) s.tuning.buoyancy = v; });
        section.AddFloat("Min strength", "Shimmer strength at zero load.",
            0f, 1f, false, () => f.tuning.baseStrength, v => { foreach (var s in shimmers) s.tuning.baseStrength = v; }, TweakGrade.Basic);
        section.AddFloat("Max strength", "Shimmer strength at full load.",
            0f, 0.05f, false, () => f.tuning.strength, v => { foreach (var s in shimmers) s.tuning.strength = v; }, TweakGrade.Basic);
        section.AddFloat("Hold time", "Fraction of the particle's lifetime held at full strength before the decay function takes over.",
            0f, 1f, true, () => f.tuning.shimmerHoldTime, v => { foreach (var s in shimmers) s.tuning.shimmerHoldTime = v; });
        section.AddFloat("Decay K", "Decay tuning constant. Larger values give a steeper initial drop after the hold time passes.",
            0f, 8f, true, () => f.tuning.decayK, v => { foreach (var s in shimmers) s.tuning.decayK = v; });
        section.AddFloat("Frequency", "Noise frequency of the shimmer field.",
            1f, 20f, false, () => f.tuning.freq, v => { foreach (var s in shimmers) s.tuning.freq = v; });
        section.AddFloat("Min radius", "Displacement radius at zero load.",
            0.2f, 1f, false, () => f.tuning.idleRadius, v => { foreach (var s in shimmers) s.tuning.idleRadius = v; });
        section.AddFloat("Max radius", "Displacement radius at full load.",
            0.2f, 1f, false, () => f.tuning.fullRadius, v => { foreach (var s in shimmers) s.tuning.fullRadius = v; });
        section.AddFloat("Min animation speed", "Noise scroll speed at zero load.",
            0f, 3f, false, () => f.tuning.idleAnimSpeed, v => { foreach (var s in shimmers) s.tuning.idleAnimSpeed = v; });
        section.AddFloat("Max animation speed", "Noise scroll speed at full load.",
            0f, 5f, false, () => f.tuning.fullAnimSpeed, v => { foreach (var s in shimmers) s.tuning.fullAnimSpeed = v; });
        section.AddFloat("Speed multiplier", "Multiplier on the noise scroll speed.",
            0f, 4f, false, () => f.tuning.speedMultiplier, v => { foreach (var s in shimmers) s.tuning.speedMultiplier = v; });
        section.AddFloat("Max dispersion speed", "Locomotive speed [m/s] at which dispersion reaches full strength.",
            1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in shimmers) s.tuning.speedNormMax = v; });
        section.AddFloat("Dispersion lifetime multiplier", "Particle lifetime multiplier at full dispersion.",
            0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in shimmers) s.tuning.speedLifetimeScale = v; });
        section.AddFloat("Dispersion jitter", "Extra emission jitter [m/s] at full dispersion.",
            0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in shimmers) s.tuning.speedJitter = v; });
        section.AddFloat("Y-offset", "Vertical offset [m] of the emission point relative to the exhaust. " +
                                     "This can help adjust the spawn height of shimmer particles relative to smoke particles.",
            -1f, 1f, false, () => f.tuning.yOffset, v => { foreach (var s in shimmers) s.tuning.yOffset = v; });
        section.AddBool("outline", "Debug: outline the shimmer billboards.",
            false, () => f.outline, v => { foreach (var s in shimmers) s.outline = v; });
        section.AddInt("debug", "Shader debug mode (0=off, 1=mask, 2=offset+mask).",
            0, 2, false, () => f.debug, v => { foreach (var s in shimmers) s.debug = v; });
        return section;
    }
}