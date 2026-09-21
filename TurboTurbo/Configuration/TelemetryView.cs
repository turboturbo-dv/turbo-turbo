using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Live engine telemetry readouts for the profile editor.</summary>
internal static class TelemetryView
{
    private const string GovernorValueTooltip =
        "Amount of engine power requested by the governor to match engine RPM to the requested throttle setting.";

    private const string LoadValueTooltip =
        "Normalised fuel flow into the engine. Both governor demand and RPM affect this.";

    private const string LambdaValueTooltip =
        "Air-to-fuel ratio, relative to a stoichiometric (chemically balanced) mixture:" +
        "1 is balanced, below 1 is rich (excess fuel), above 1 is lean (excess air).";

    private const string ChargeValueTooltip =
        "Relative cylinder air pressure. 1.0 is normal atmospheric pressure; turbo boost raises it further. " +
        "High RPM on naturally aspirated engines may cause a slight vacuum, dropping it below 1.";

    public static void Draw(EngineSimulationHost host)
    {
        var model = host.CombustionModel;
        if (model == null)
        {
            GUILayout.Label("waiting for engine model…");
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Governor {model.Demand * 100f:0}%", GovernorValueTooltip));
        GUILayout.Label($"RPM {model.RpmNorm * 100f:0}%");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Speed {host.AbsSpeed * 3.6f:0.0} km/h");
        GUILayout.Label($"Load {model.FuelNorm * 100f:0}%");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Lambda {model.Lambda:0.00}", LambdaValueTooltip));
        GUILayout.Label(new GUIContent($"Charge {model.Charge:0.00}", ChargeValueTooltip));
        GUILayout.EndHorizontal();
    }
}