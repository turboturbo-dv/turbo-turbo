namespace TurboTurbo.Modeling;

public enum ChargerKind
{
    Turbo,
    Atmospheric,
}

/// <summary>Charger abstraction: allows the CombustionModel to be reused for both N/A and turbocharged engines.</summary>
public interface ICharger
{
    float Charge { get; }

    /// <summary>
    /// Rated charge at full power. This is a theoretical number intended for calibration; charge under actual simulation
    /// circumstances may deviate from this.
    /// </summary>
    float RatedCharge { get; }

    /// <summary>
    /// Effective compression ratio of the cylinders, counted from the moment the intake closes.
    /// </summary>
    float CompressionRatio { get; }

    /// <summary>
    /// NTU of the intake manifold/aftercooler at rated flow. Determines how strongly the intake air is
    /// pulled towards the block temperature.
    /// </summary>
    float IntakeNtu { get; }

    /// <summary>
    /// Isentropic efficiency of the compressor. 1 is 100% efficient,
    /// lower values heat intake air more to achieve the same charge.
    /// </summary>
    float CompressorEfficiency { get; }

    /// <summary>Pressure ratio across the exhaust turbine; 1 when there is none.</summary>
    float TurbinePressureRatio { get; }

    float Boost { get; }
    bool Surging { get; }
    float LambdaCalibration { get; }

    void Tick(float delta, float rpmNorm, float governorNorm,
        float exhaustEnergy, bool engineOn);

    ICharger Clone();
}
