using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class SmokeEmitterXml
{
    internal const float DefaultIdleEmissionRate = 15f;
    internal const float DefaultFullEmissionRate = 75f;
    internal const float DefaultLifetime = 4.5f;
    internal const float DefaultStartSize = 0.39f;
    internal const float DefaultStartSizeVariance = 0.29f;
    internal const float DefaultSizeOverLifetimeEnd = 6f;
    internal const float DefaultSizeOverLifetimeExponent = 0.65f;
    internal const float DefaultBuoyancy = 0.1f;
    internal const float DefaultDrag = 0.6f;
    internal const float DefaultAngularVelocityMax = 20f;
    internal const float DefaultSpeedNormMax = 15f;
    internal const float DefaultSpeedLifetimeScale = 0.3f;
    internal const float DefaultSpeedJitter = 0.5f;
    internal const float DefaultTurbulenceStrength = 1.25f;
    internal const float DefaultTurbulenceFrequency = 0.5f;
    internal const float DefaultTurbulenceScrollSpeed = 0f;
    internal const float DefaultLightSaturation = 0.35f;
    internal const float DefaultMaxShadowFloor = 0.65f;

    [DefaultValue(DefaultIdleEmissionRate)]
    public float idleEmissionRate = DefaultIdleEmissionRate;

    [DefaultValue(DefaultFullEmissionRate)]
    public float fullEmissionRate = DefaultFullEmissionRate;

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

    [DefaultValue(DefaultBuoyancy)]
    public float buoyancy = DefaultBuoyancy;

    [DefaultValue(DefaultDrag)]
    public float drag = DefaultDrag;

    [DefaultValue(DefaultAngularVelocityMax)]
    public float angularVelocityMax = DefaultAngularVelocityMax;

    [DefaultValue(DefaultSpeedNormMax)]
    public float speedNormMax = DefaultSpeedNormMax;

    [DefaultValue(DefaultSpeedLifetimeScale)]
    public float speedLifetimeScale = DefaultSpeedLifetimeScale;

    [DefaultValue(DefaultSpeedJitter)]
    public float speedJitter = DefaultSpeedJitter;

    [DefaultValue(DefaultTurbulenceStrength)]
    public float turbulenceStrength = DefaultTurbulenceStrength;

    [DefaultValue(DefaultTurbulenceFrequency)]
    public float turbulenceFrequency = DefaultTurbulenceFrequency;

    [DefaultValue(DefaultTurbulenceScrollSpeed)]
    public float turbulenceScrollSpeed = DefaultTurbulenceScrollSpeed;

    [DefaultValue(DefaultLightSaturation)]
    public float lightSaturation = DefaultLightSaturation;

    [DefaultValue(DefaultMaxShadowFloor)]
    public float maxShadowFloor = DefaultMaxShadowFloor;
}
