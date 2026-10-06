using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class ShimmerEmitterXml
{
    internal const float DefaultIdleRate = 5f;
    internal const float DefaultFullRate = 10f;
    internal const float DefaultLifetime = 1.5f;
    internal const float DefaultStartSize = 0.8f;
    internal const float DefaultStartSizeVariance = 0f;
    internal const float DefaultSizeOverLifetimeEnd = 4.8f;
    internal const float DefaultSizeOverLifetimeExponent = 1f;
    internal const float DefaultDrag = 0.8f;
    internal const float DefaultBuoyancy = 0.65f;
    internal const float DefaultSpeedNormMax = 15f;
    internal const float DefaultSpeedLifetimeScale = 0.4f;
    internal const float DefaultSpeedJitter = 0.5f;
    internal const float DefaultStrength = 0.014f;
    internal const float DefaultBaseStrength = 0.1f;
    internal const float DefaultFreq = 6f;
    internal const float DefaultIdleRadius = 0.8f;
    internal const float DefaultFullRadius = 1f;
    internal const float DefaultIdleAnimSpeed = 0.5f;
    internal const float DefaultFullAnimSpeed = 2f;
    internal const float DefaultSpeedMultiplier = 1f;
    internal const float DefaultShimmerHoldTime = 0.15f;
    internal const float DefaultDecayK = 4f;
    internal const float DefaultYOffset = 0.1f;

    [DefaultValue(DefaultIdleRate)]
    public float idleRate = DefaultIdleRate;

    [DefaultValue(DefaultFullRate)]
    public float fullRate = DefaultFullRate;

    [DefaultValue(DefaultLifetime)]
    public float lifetime = DefaultLifetime;

    [DefaultValue(DefaultStartSize)]
    public float startSize = DefaultStartSize;

    [DefaultValue(DefaultStartSizeVariance)]
    public float startSizeVariance = DefaultStartSizeVariance;

    [DefaultValue(DefaultSizeOverLifetimeEnd)]
    public float sizeOverLifetimeEnd = DefaultSizeOverLifetimeEnd;

    [DefaultValue(DefaultSizeOverLifetimeExponent)]
    public float sizeOverLifetimeExponent = DefaultSizeOverLifetimeExponent;

    [DefaultValue(DefaultDrag)]
    public float drag = DefaultDrag;

    [DefaultValue(DefaultBuoyancy)]
    public float buoyancy = DefaultBuoyancy;

    [DefaultValue(DefaultSpeedNormMax)]
    public float speedNormMax = DefaultSpeedNormMax;

    [DefaultValue(DefaultSpeedLifetimeScale)]
    public float speedLifetimeScale = DefaultSpeedLifetimeScale;

    [DefaultValue(DefaultSpeedJitter)]
    public float speedJitter = DefaultSpeedJitter;

    [DefaultValue(DefaultStrength)]
    public float strength = DefaultStrength;

    [DefaultValue(DefaultBaseStrength)]
    public float baseStrength = DefaultBaseStrength;

    [DefaultValue(DefaultFreq)]
    public float freq = DefaultFreq;

    [DefaultValue(DefaultIdleRadius)]
    public float idleRadius = DefaultIdleRadius;

    [DefaultValue(DefaultFullRadius)]
    public float fullRadius = DefaultFullRadius;

    [DefaultValue(DefaultIdleAnimSpeed)]
    public float idleAnimSpeed = DefaultIdleAnimSpeed;

    [DefaultValue(DefaultFullAnimSpeed)]
    public float fullAnimSpeed = DefaultFullAnimSpeed;

    [DefaultValue(DefaultSpeedMultiplier)]
    public float speedMultiplier = DefaultSpeedMultiplier;

    [DefaultValue(DefaultShimmerHoldTime)]
    public float shimmerHoldTime = DefaultShimmerHoldTime;

    [DefaultValue(DefaultDecayK)]
    public float decayK = DefaultDecayK;

    [DefaultValue(DefaultYOffset)]
    public float yOffset = DefaultYOffset;
}
