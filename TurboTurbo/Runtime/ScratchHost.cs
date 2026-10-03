using TurboTurbo.Configuration;
using TurboTurbo.Profiles;

namespace TurboTurbo.Runtime;

internal static class ScratchHost
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");

    public static Result<EngineSimulationHost> Create(TrainCar car)
    {
        var liveryId = car.carLivery.id;
        var mode = SettingsStore.Current.ResolutionMode;

        // the resolver returns shared instances, so clone before the editor can tune it.
        // While authoring, the user tier is skipped entirely, to prevent it from interfering
        // with the authored profiles.
        var profile = ProfileService.Resolve(liveryId, mode).Effective?.Profile.Clone();
        if (profile == null && mode == ResolutionMode.Normal)
        {
            profile = ProfileService.User.Get(liveryId)?.Clone();
        }

        if (profile == null)
        {
            profile = new LocoProfile
            {
                LiveryId = liveryId,
                Exhausts = [DefaultExhaust(car)],
            };
        }

        var error = profile.Normalize();
        if (error is { } e) return e;

        return HostFactory.Create(car, profile);
    }

    private static LocoExhaust DefaultExhaust(TrainCar car)
    {
        var path = ExhaustTargets.TryDefaultPath(car);
        if (path != null) return LocoExhaust.Replacement(path);

        Log.Info($"no exhaust particle system on '{car.DisplayId()}', adding an independent exhaust");
        return LocoExhaust.Independent();
    }
}
