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
    float Boost { get; }
    bool Surging { get; }
    float LambdaCalibration { get; }
    float ExhaustHeat { get; }

    void Tick(float delta, float fuelDemand, float overfuel, float rpmNorm, float throttle, bool engineOn);

    ICharger Clone();
}