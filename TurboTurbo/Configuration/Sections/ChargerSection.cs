using System;

using TurboTurbo.Configuration;
using TurboTurbo.Modeling;
using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ChargerSection
{
    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var model = host.CombustionModel;
        var aspirated = model.Charger is AtmosphericCharger;
        var section = new Section(aspirated ? "aspirated engine" : "turbo model", aspirated ? "aspirated" : "turbo",
            onRequiresReconfigure)
        {
            Open = true,
            OnToggle = onToggle,
        };
        var s = model.Tuning;
        section.AddFloat("rpmTorqueExponent",
            "Scales max torque capacity with engine speed. 0 = torque cap depends purely on cylinder charge density; 1 = torque cap scales linearly with RPM.",
            0f, 3f, false, () => s.RpmTorqueExponent, v => s.RpmTorqueExponent = v);
        section.AddFloat("torqueLambdaFloor",
            "Lambda below which extra fuel contributes no torque.",
            0.3f, 1f, false, () => s.TorqueLambdaFloor, v => s.TorqueLambdaFloor = v);
        if (model.Charger is TurboCharger turbo)
        {
            var t = turbo.Tuning;
            section.AddFloat("lambdaCalibration",
                "Global air-to-fuel scaling factor. Higher values lower lambda across all operating points, making the engine run richer.",
                1f, 4f, false, () => t.LambdaCalibration, v => t.LambdaCalibration = v);
            section.AddFloat("boostChargeMultiplier",
                "Charge gain per unit boost. Charge = 1 + BoostChargeMultiplier * Boost.",
                0f, 5f, false, () => t.BoostChargeMultiplier, v => t.BoostChargeMultiplier = v);
            section.AddFloat("rpmBoostExponent",
                "RPM penalty exponent on target boost equilibrium (Target = Demand * RPM^exponent). Higher values restrict turbo spooling at low engine RPM.",
                0f, 3f, false, () => t.RpmBoostExponent, v => t.RpmBoostExponent = v);
            section.AddFloat("tauUp",
                "Spool-up time constant in seconds.",
                0.25f, 8f, false, () => t.TauUp, v => t.TauUp = v);
            section.AddFloat("tauDown",
                "Blow-down time constant in seconds.",
                0.1f, 4f, false, () => t.TauDown, v => t.TauDown = v);
            section.AddFloat("minSpoolTau",
                "Floor for the spool-up time constant (stability under heavy overfuel).",
                0.1f, 2f, false, () => t.MinSpoolTau, v => t.MinSpoolTau = v);
            section.AddFloat("thermalK",
                "Thermal enthalpy feedback strength: overfueling shortens spool-up time.",
                0f, 3f, false, () => t.ThermalK, v => t.ThermalK = v);
            section.AddFloat("surgeRateThreshold",
                "Demand drop rate [1/s] that triggers a surge while boost is above 0.75.",
                0f, 60f, false, () => t.SurgeRateThreshold, v => t.SurgeRateThreshold = v);
        }
        else if (model.Charger is AtmosphericCharger na)
        {
            var a = na.Tuning;
            section.AddFloat("etaPeak",
                "Peak volumetric efficiency: charge density at zero RPM before choke losses.",
                0.5f, 1f, false, () => a.EtaPeak, v => { a.EtaPeak = v; a.Validate(); });
            section.AddFloat("chokeK",
                "High-RPM breathing loss factor. Higher values cause more choke loss overall.",
                0f, 1f, false, () => a.ChokeK, v => { a.ChokeK = v; a.Validate(); });
            section.AddFloat("chokeBeta",
                "High-RPM breathing loss exponent. Shapes the choke loss curve. Values above 1 shift choke loss to the top RPM range.",
                0f, 4f, false, () => a.ChokeBeta, v => { a.ChokeBeta = v; a.Validate(); });
            section.AddFloat("lambdaCalibration",
                "Global air-to-fuel scaling factor. Higher values lower lambda across all operating points, making the engine run richer.",
                0.2f, 1.5f, false, () => a.LambdaCalibration, v => a.LambdaCalibration = v);
        }

        return section;
    }
}