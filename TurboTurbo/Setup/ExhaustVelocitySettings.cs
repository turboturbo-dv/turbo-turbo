namespace TurboTurbo.Setup;

/// <summary>
/// Exhaust plume speed range shared by a host's smoke and shimmer emitters.
/// </summary>
public sealed class ExhaustVelocitySettings
{
    public float Idle = 1.5f;
    public float FullLoad = 15f;

    public ExhaustVelocitySettings()
    {
    }

    public ExhaustVelocitySettings(ExhaustVelocitySettings other)
    {
        Idle = other.Idle;
        FullLoad = other.FullLoad;
    }
}