using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Profiles;

namespace TurboTurbo.Runtime;

internal static class ScratchHost
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");

    public static EngineSimulationHost Create(TrainCar car)
    {
        var liveryId = car.carLivery.id;

        // TryGetProfile already returns a fresh instance, the user profile needs a clone
        var profile = ProfileRepository.TryGetProfile(car)
            ?? ProfileRepository.TryGetUserProfile(liveryId)?.Clone();

        if (profile == null)
        {
            profile = new LocoProfile
            {
                LiveryId = liveryId,
                Exhausts = [DefaultExhaust(car)],
            };
        }

        var error = profile.Complete();
        if (error != null)
        {
            Log.Warn($"cannot create a profile for '{car.ID}': {error}");
            return null;
        }

        var host = car.gameObject.AddComponent<EngineSimulationHost>();
        host.Configure(profile);
        Orchestrator.Instance.Hosts.Add(host);
        return host;
    }

    private static LocoExhaust DefaultExhaust(TrainCar car)
    {
        var candidates = ExhaustTargets.FindCandidates(car);
        var names = candidates.Select(candidate => candidate.Name).ToList();

        var pick = ExhaustTargets.PickDefault(names);
        if (pick >= 0)
        {
            return new LocoExhaust { Kind = ExhaustKind.Replacement, Name = candidates[pick].Name };
        }

        Log.Info($"no exhaust particle system on '{car.ID}', adding an independent exhaust");
        return new LocoExhaust { Kind = ExhaustKind.Independent };
    }
}