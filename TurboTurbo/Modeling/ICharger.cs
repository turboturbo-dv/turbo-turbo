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
    float ChargeAtFullPower { get; }

    float Boost { get; }
    bool Surging { get; }
    float LambdaCalibration { get; }
    float ExhaustHeat { get; }

    void Tick(float delta, float fuelPerStroke, float overfuel, float rpmNorm, float governorNorm, bool engineOn);

    ICharger Clone();
}
