using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Everything a profile table row needs to render itself.</summary>
internal readonly struct ProfileRowContext
{
    public readonly LiveryCatalog.LiveryInfo Livery;
    public readonly ProfileResolution Resolution;
    public readonly TrainCar LiveryBoardedCar;
    public readonly Orchestrator Orchestrator;

    public ProfileRowContext(
        LiveryCatalog.LiveryInfo livery,
        ProfileResolution resolution,
        TrainCar liveryBoardedCar,
        Orchestrator orchestrator)
    {
        Livery = livery;
        Resolution = resolution;
        LiveryBoardedCar = liveryBoardedCar;
        Orchestrator = orchestrator;
    }

    public string Id => Livery.Id;
    public bool BoardedThisLivery => LiveryBoardedCar != null;
    public bool Supported => LiveryBoardedCar != null && DieselEngineBinder.Supports(LiveryBoardedCar);
}
