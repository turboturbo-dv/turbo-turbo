using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Everything a profile table row needs to render itself.</summary>
internal readonly struct ProfileRowContext
{
    public readonly LiveryCatalog.LiveryInfo Livery;
    public readonly ProfileResolution Resolution;
    public readonly bool Boarded;
    public readonly TrainCar Car;
    public readonly Orchestrator Orchestrator;

    public ProfileRowContext(
        LiveryCatalog.LiveryInfo livery,
        ProfileResolution resolution,
        bool boarded,
        TrainCar car,
        Orchestrator orchestrator)
    {
        Livery = livery;
        Resolution = resolution;
        Boarded = boarded;
        Car = car;
        Orchestrator = orchestrator;
    }

    public string Id => Livery.Id;
}
