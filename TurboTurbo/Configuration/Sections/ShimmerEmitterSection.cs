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

        var section = new Section("shimmer emitter", "shimmerEmitter", onRequiresReconfigure) { OnToggle = onToggle };
        var f = shimmers[0];
        section.AddFloat("idleRate", "Emission rate [p/s] at zero heat.",
            0f, 20f, false, () => f.tuning.idleRate, v => { foreach (var s in shimmers) s.tuning.idleRate = v; });
        section.AddFloat("fullRate", "Emission rate [p/s] at full heat.",
            0f, 40f, false, () => f.tuning.fullRate, v => { foreach (var s in shimmers) s.tuning.fullRate = v; });
        section.AddFloat("yOffset", "Vertical offset [m] of the emission point relative to the exhaust.",
            -2f, 2f, false, () => f.tuning.yOffset, v => { foreach (var s in shimmers) s.tuning.yOffset = v; });
        section.AddFloat("lifetime", "Particle lifetime in seconds.",
            0.5f, 6f, true, () => f.tuning.lifetime, v => { foreach (var s in shimmers) s.tuning.lifetime = v; });
        section.AddFloat("startSizeMin", "Billboard size range at emission [m].",
            0.1f, 3f, false, () => f.tuning.startSizeMin, v => { foreach (var s in shimmers) s.tuning.startSizeMin = v; });
        section.AddFloat("startSizeMax", "Billboard size range at emission [m].",
            0.1f, 3f, false, () => f.tuning.startSizeMax, v => { foreach (var s in shimmers) s.tuning.startSizeMax = v; });
        section.AddFloat("sizeOverLifetimeStart", "Growth factor at emission.",
            0.1f, 3f, true, () => f.tuning.sizeOverLifetimeStart, v => { foreach (var s in shimmers) s.tuning.sizeOverLifetimeStart = v; });
        section.AddFloat("sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
            1f, 10f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in shimmers) s.tuning.sizeOverLifetimeEnd = v; });
        section.AddFloat("gravity", "Gravity modifier (negative = buoyant).",
            -1f, 0.5f, true, () => f.tuning.gravity, v => { foreach (var s in shimmers) s.tuning.gravity = v; });
        section.AddFloat("drag", "Air resistance decaying the inherited train velocity.",
            0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in shimmers) s.tuning.drag = v; });
        section.AddFloat("buoyancy", "Constant upward drift [m/s].",
            0f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in shimmers) s.tuning.buoyancy = v; });
        section.AddFloat("strength", "Max shimmer displacement at full heat.",
            0f, 0.05f, false, () => f.tuning.strength, v => { foreach (var s in shimmers) s.tuning.strength = v; });
        section.AddFloat("baseStrength", "Displacement multiplier at zero heat (lerps to 1 at full heat).",
            0f, 1f, false, () => f.tuning.baseStrength, v => { foreach (var s in shimmers) s.tuning.baseStrength = v; });
        section.AddFloat("freq", "Noise frequency of the shimmer field.",
            1f, 20f, false, () => f.tuning.freq, v => { foreach (var s in shimmers) s.tuning.freq = v; });
        section.AddFloat("idleRadius", "Displacement radius at zero heat.",
            0.2f, 1f, false, () => f.tuning.idleRadius, v => { foreach (var s in shimmers) s.tuning.idleRadius = v; });
        section.AddFloat("fullRadius", "Displacement radius at full heat.",
            0.2f, 1f, false, () => f.tuning.fullRadius, v => { foreach (var s in shimmers) s.tuning.fullRadius = v; });
        section.AddFloat("idleAnimSpeed", "Noise scroll speed at zero heat.",
            0f, 3f, false, () => f.tuning.idleAnimSpeed, v => { foreach (var s in shimmers) s.tuning.idleAnimSpeed = v; });
        section.AddFloat("fullAnimSpeed", "Noise scroll speed at full heat.",
            0f, 5f, false, () => f.tuning.fullAnimSpeed, v => { foreach (var s in shimmers) s.tuning.fullAnimSpeed = v; });
        section.AddFloat("speedMultiplier", "Multiplier on the noise scroll speed.",
            0f, 4f, false, () => f.tuning.speedMultiplier, v => { foreach (var s in shimmers) s.tuning.speedMultiplier = v; });
        section.AddFloat("shimmerHoldTime", "Fraction of the particle's lifetime held at full strength before the decay function takes over.",
            0f, 1f, true, () => f.tuning.shimmerHoldTime, v => { foreach (var s in shimmers) s.tuning.shimmerHoldTime = v; });
        section.AddFloat("decayK", "Rational decay tuning constant. Larger k gives a steeper initial drop after the hold time passes.",
            0f, 8f, true, () => f.tuning.decayK, v => { foreach (var s in shimmers) s.tuning.decayK = v; });
        section.AddFloat("speedNormMax", "Speed [m/s] at which speed-based dispersion reaches full strength.",
            1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in shimmers) s.tuning.speedNormMax = v; });
        section.AddFloat("speedLifetimeScale", "Particle lifetime multiplier at full dispersion.",
            0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in shimmers) s.tuning.speedLifetimeScale = v; });
        section.AddFloat("speedJitter", "Extra emission jitter [m/s] at full dispersion.",
            0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in shimmers) s.tuning.speedJitter = v; });
        section.AddBool("outline", "Debug: outline the shimmer billboards.",
            false, () => f.outline, v => { foreach (var s in shimmers) s.outline = v; });
        section.AddInt("debug", "Shader debug mode (0=off, 1=mask, 2=offset+mask).",
            0, 2, false, () => f.debug, v => { foreach (var s in shimmers) s.debug = v; });
        section.AddInt("renderQueue", "Material render queue (3000 = smoke, 3010 = after the smoke).",
            2000, 4000, true, () => f.renderQueue, v => { foreach (var s in shimmers) s.renderQueue = v; });
        return section;
    }
}