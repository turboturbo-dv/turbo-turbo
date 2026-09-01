using System.Linq;
using DV.ThingTypes;
using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    private static UnityModManager.ModEntry _modEntry;
    internal static Logger Log;

    public static void Load(UnityModManager.ModEntry entry)
    {
        _modEntry = entry;
        Log = new Logger(_modEntry.Logger);

        Controller.ConfigureEngine(TrainCarType.LocoDiesel, options => options
            .AddTurbo()
            .AddEngineExhaust(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "HighTempEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "DamagedEngineSmoke")?.transform));

        Orchestrator.Create(Log);
        
        
        
        Log.LogInfo("TurboTurbo ready!");
        
    }
}
