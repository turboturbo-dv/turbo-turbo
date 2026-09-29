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
        "Total fuel flow into the engine, as a percentage of the maximum possible.\n" +
        "Both governor demand (fuel per stroke) and RPM (strokes per second) affect this.";

    private const string LambdaValueTooltip =
        "Air-to-fuel ratio, relative to a stoichiometric (chemically balanced) mixture:\n" +
        " * 1 is balanced\n" +
        " * above 1 is lean (excess air)\n" +
        " * below 1 is rich (excess fuel)\n\n" +
        "Because of this, lambda goes down when more fuel is injected into the engine, and it rises when more air is " +
        "supplied. This process is what causes soot to clear when a turbocharger spins up.";

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
            GUILayout.Label(new GUIContent($"Governor {model.GovernorNorm * 100f:0}%", GovernorValueTooltip));
            GUILayout.Label($"RPM {model.RpmNorm * 100f:0}%");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed {host.AbsSpeed * 3.6f:0.0} km/h");
            GUILayout.Label(new GUIContent($"Load {model.FuelNorm * 100f:0}%", LoadValueTooltip));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"Lambda {model.Lambda:0.00}",
                LambdaValueTooltip + $"\n\nOnce lambda drops below {host.Profile.Smoke.SootOnsetLambda:0.00}, soot starts forming.\n" +
                $"When lambda reaches {host.Profile.Smoke.SootOpaqueLambda:0.00}, soot has reached maximum intensity."));
            GUILayout.Label(new GUIContent($"Charge {model.Charge:0.00}", ChargeValueTooltip));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }
}
