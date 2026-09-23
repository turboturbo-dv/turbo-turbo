using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Live engine telemetry readouts for the profile editor.</summary>
internal static class TelemetryView
{
    private const string GovernorValueTooltip =
        "Amount of engine power requested by the governor.\n" +
        "It raises or lowers power to match engine RPM to the requested throttle setting.";

    private const string LoadValueTooltip =
        "Normalised fuel flow into the engine.\n" +
        "Both governor demand (fuel per stroke) and RPM (strokes per second) affect this.";

    private const string LambdaValueTooltip =
        "Air-to-fuel ratio, relative to a stoichiometric (chemically balanced) mixture:\n" +
        " * 1 is balanced\n" +
        " * below 1 is rich (excess fuel)\n" +
        " * above 1 is lean (excess air)";

    private const string ChargeValueTooltip =
        "Cylinder air pressure, relative to atmospheric.\n" +
        " * 1 means the air charge is exactly equal to atmospheric pressure\n" +
        " * turbo boost will raise it above 1\n" +
        " * on naturally aspirated engines, charge is less than 1 due to intake restrictions that scale with RPM";

    public static void Draw(EngineSimulationHost host)
    {
        GUILayout.BeginVertical(Styles.TelemetryBox);

        var model = host.CombustionModel;
        if (model == null)
        {
            GUILayout.Label("waiting for engine model…");
        }
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"Governor {model.Demand * 100f:0}%", GovernorValueTooltip));
            GUILayout.Label($"RPM {model.RpmNorm * 100f:0}%");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed {host.AbsSpeed * 3.6f:0.0} km/h");
            GUILayout.Label(new GUIContent($"Load {model.FuelNorm * 100f:0}%", LoadValueTooltip));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"Lambda {model.Lambda:0.00}", LambdaValueTooltip));
            GUILayout.Label(new GUIContent($"Charge {model.Charge:0.00}", ChargeValueTooltip));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }
}