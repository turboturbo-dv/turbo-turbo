namespace TurboTurbo;

/// <summary>General physics and unit conversion constants.</summary>
internal static class PhysicsConstants
{
    /// <summary>Standard reference ambient temperature (20°C).</summary>
    public const float ReferenceAmbientK = 293.15f;

    /// <summary>Air density [kg/m^3] at <see cref="ReferenceAmbientK"/>.</summary>
    public const float ReferenceAirDensity = 1.2f;

    /// <summary>Specific heat ratio of air.</summary>
    public const float SpecificHeatRatio = 1.4f;

    /// <summary>Kelvin = Celsius + this.</summary>
    public const float KelvinOffset = 273.15f;

    /// <summary>Meters per second to km/h.</summary>
    public const float MpsToKmh = 3.6f;
}
