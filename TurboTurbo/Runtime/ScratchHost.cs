using TurboTurbo.Profiles;

using UnityEngine;

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
            var ps = car.GetFirstComponentInChildren<ParticleSystem>(
                true, p => p.name.Contains("ExhaustEngineSmoke"));
            if (ps == null)
            {
                Log.Warn($"no exhaust particle system found on '{car.ID}', cannot create a profile");
                return null;
            }

            profile = new LocoProfile
            {
                LiveryId = liveryId,
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Name = ps.name }],
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
}