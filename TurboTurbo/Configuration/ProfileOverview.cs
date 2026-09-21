using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal static class ProfileOverview
{
    private const float LabelWidth = 200f;

    public static void Draw()
    {
        var car = PlayerManager.Car;
        var boardedLiveryId = car != null && car.IsLoco && car.carLivery != null
            ? car.carLivery.id
            : null;

        GUILayout.Label("locomotive profiles");
        if (boardedLiveryId == null)
        {
            GUILayout.Label("Board a locomotive to create or edit its profile.");
        }
        GUILayout.Space(2f);

        var orchestrator = Orchestrator.Instance;

        foreach (var livery in LiveryCatalog.LocoLiveries())
        {
            var isBoarded = livery.Id == boardedLiveryId;

            GUILayout.BeginHorizontal();
            GUILayout.Label(livery.TypeId, GUILayout.Width(LabelWidth));
            GUILayout.Label(ProfileRepository.TryGetStatus(livery.Id).Label);
            if (isBoarded)
            {
                GUILayout.Label("boarded");
                var host = orchestrator != null ? orchestrator.FindHost(car) : null;
                if (GUILayout.Button(host != null ? "Edit" : "New profile"))
                {
                    Main.EditPresenter.Open(car);
                }

                if (ProfileRepository.TryGetUserProfile(boardedLiveryId) != null
                    && GUILayout.Button("Delete"))
                {
                    ProfileRepository.DeleteProfile(boardedLiveryId);
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}