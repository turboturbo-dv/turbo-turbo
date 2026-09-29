using System;
using System.Collections.Generic;

using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal static class ProfileOverview
{
    private const float LabelWidth = 200f;

    // these locos have no diesel exhaust to simulate, so they get no profile row
    private static readonly HashSet<string> ExcludedLiveries = new(StringComparer.Ordinal)
    {
        "HandCar",
        "LocoMicroshunter",
        "LocoS060",
        "LocoS282A",
    };

    private static readonly Logger Log = TurboTurbo.Log.ForContext("overview");

    public static void Draw()
    {
        var car = PlayerManager.Car;
        var boardedLiveryId = car != null && car.IsLoco && car.carLivery != null
            ? car.carLivery.id
            : null;

        GUILayout.Label("Locomotive profiles", Styles.BoldLabel);
        if (boardedLiveryId == null)
        {
            GUILayout.Label("Board a locomotive to create or edit its profile.");
        }
        GUILayout.Space(2f);

        var orchestrator = Orchestrator.Instance;
        var authoring = SettingsStore.Current.IsAuthoring;

        GUILayout.BeginVertical(Styles.OverviewBox);
        foreach (var livery in LiveryCatalog.LocoLiveries())
        {
            if (ExcludedLiveries.Contains(livery.Id)) continue;

            var isBoarded = livery.Id == boardedLiveryId;

            GUILayout.BeginHorizontal();
            GUILayout.Label(livery.TypeId, GUILayout.Width(LabelWidth));
            GUILayout.Label(ProfileRepository.TryGetStatus(livery.Id).Label);

            if (!authoring)
            {
                DrawUserControls(livery.Id, orchestrator);
            }

            if (isBoarded)
            {
                GUILayout.Label("boarded");
                var host = orchestrator != null ? orchestrator.FindHost(car) : null;
                if (GUILayout.Button(host != null ? "Edit" : "New profile"))
                {
                    ProfileEditorPresenter.Instance?.Open(car);
                }
            }

            if (authoring)
            {
                DrawAuthoringControls(livery.Id, orchestrator);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private static void DrawAuthoringControls(string liveryId, Orchestrator orchestrator)
    {
        if (!ProfileRepository.ModSuppliesAuthoringLivery(liveryId)) return;

        var target = ModRegistry.DisplayName(SettingsStore.Current.AuthoringTargetModId);
        if (!GUILayout.Button(new GUIContent("delete", $"Remove this profile from '{target}'"))) return;

        var error = ProfileRepository.DeleteFromAuthoringMod(liveryId);
        if (error != null)
        {
            Log.Warn($"could not delete profile '{liveryId}' from the mod: {error}");
            return;
        }

        orchestrator?.ReloadHostsForLivery(liveryId);
    }

    private static void DrawUserControls(string liveryId, Orchestrator orchestrator)
    {
        var user = ProfileRepository.TryGetUserProfile(liveryId);
        if (user == null) return;

        var enabled = GUILayout.Toggle(user.Enabled, "enabled");
        if (enabled != user.Enabled)
        {
            ProfileRepository.SetEnabled(liveryId, enabled);
            orchestrator?.ReloadHostsForLivery(liveryId);
        }

        if (GUILayout.Button("Delete"))
        {
            ProfileRepository.DeleteProfile(liveryId);
            orchestrator?.ReloadHostsForLivery(liveryId);
        }
    }
}
