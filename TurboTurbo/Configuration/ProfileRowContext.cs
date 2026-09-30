using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Everything a profile table row needs to render itself.</summary>
internal readonly struct ProfileRowContext
{
    public readonly LiveryCatalog.LiveryInfo Livery;
    public readonly LocoProfile BuiltIn;
    public readonly LocoProfile Mod;
    public readonly string ModName;
    public readonly LocoProfile User;
    public readonly bool Boarded;
    public readonly TrainCar Car;
    public readonly Orchestrator Orchestrator;
    public readonly bool Authoring;

    public ProfileRowContext(
        LiveryCatalog.LiveryInfo livery,
        LocoProfile builtIn,
        LocoProfile mod,
        string modName,
        LocoProfile user,
        bool boarded,
        TrainCar car,
        Orchestrator orchestrator,
        bool authoring)
    {
        Livery = livery;
        BuiltIn = builtIn;
        Mod = mod;
        ModName = modName;
        User = user;
        Boarded = boarded;
        Car = car;
        Orchestrator = orchestrator;
        Authoring = authoring;
    }

    public string Id => Livery.Id;
}
