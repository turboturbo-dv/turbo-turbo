using System;
using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal sealed class ProfileOverview
{
    private const float MarkerWidth = 16f;
    private const float NameWidth = 180f;
    private const float PillWidth = 104f;
    private const float LayersWidth = PillWidth * 3f + Styles.ProfileOverviewRowMargin * 2f;
    private const float EnabledWidth = 56f;
    private const float ActionsWidth = 160f;
    private const float RowSpacing = 4f;

    private static readonly string[] LayerOrder = { "Built-in", "Mod", "User" };

    // These locos have no diesel exhaust to simulate, so they get no profile row.
    // This won't exclude modded vehicles, but these are harder to check when we don't have a TrainCar object yet.
    private static readonly HashSet<string> ExcludedLiveries = new(StringComparer.Ordinal)
    {
        "HandCar",
        "LocoMicroshunter",
        "LocoS060",
        "LocoS282A",
    };

    private static readonly Color32 PillFill = new(0x4A, 0x4A, 0x4A, 0xFF);
    private static readonly Color32 PillText = new(0xD0, 0xD0, 0xD0, 0xFF);
    private static readonly Color32 DotActive = new(0x6F, 0xCF, 0x73, 0xFF);
    private static readonly Color32 DotInactive = new(0xC5, 0x6A, 0x5A, 0xFF);
    private static readonly Color32 DotShadowed = new(0x9A, 0x9A, 0x9A, 0xFF);

    private static readonly Logger Log = TurboTurbo.Log.ForContext("overview");

    public void Draw()
    {
        var car = PlayerManager.Car;
        var boardedLiveryId = car != null && car.IsLoco && car.carLivery != null
            ? car.carLivery.id
            : null;

        GUILayout.Label("Profiles", Styles.BoldLabel);
        GUILayout.Space(2f);

        var orchestrator = Orchestrator.Instance;
        var authoring = SettingsStore.Current.IsAuthoring;

        GUILayout.BeginVertical(Styles.OverviewBox);
        DrawHeader();
        var liveries = OrderedLiveries();
        for (var i = 0; i < liveries.Count; i++)
        {
            if (i > 0) GUILayout.Space(RowSpacing);
            DrawRow(BuildContext(liveries[i], boardedLiveryId, car, orchestrator, authoring));
        }
        GUILayout.EndVertical();
    }

    private List<LiveryCatalog.LiveryInfo> OrderedLiveries()
    {
        var list = LiveryCatalog.GetLiveries().Where(l => !ExcludedLiveries.Contains(l.Id)).ToList();
        list.Sort((a, b) =>
        {
            var byGroup = b.IsModded.CompareTo(a.IsModded);
            if (byGroup != 0) return byGroup;

            var byName = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            return byName != 0 ? byName : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });
        return list;
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(GUIContent.none, Styles.RowLabel, GUILayout.Width(MarkerWidth));
        GUILayout.Label("Locomotive", Styles.BoldLabel, GUILayout.Width(NameWidth));
        GUILayout.Label("Layers", Styles.BoldLabel, GUILayout.Width(LayersWidth));
        GUILayout.Label("Enabled", Styles.BoldLabel, GUILayout.Width(EnabledWidth));
        GUILayout.Label("Actions", Styles.BoldLabel, GUILayout.Width(ActionsWidth));
        GUILayout.EndHorizontal();
        Styles.HLine();
    }

    private static ProfileRowContext BuildContext(
        LiveryCatalog.LiveryInfo livery,
        string boardedLiveryId,
        TrainCar car,
        Orchestrator orchestrator,
        bool authoring)
    {
        return new ProfileRowContext(
            livery,
            Controller.TryGetConfiguration(livery.Id),
            ProfileRepository.TryGetModProfile(livery.Id),
            ProfileRepository.TryGetModName(livery.Id),
            authoring ? null : ProfileRepository.TryGetUserProfile(livery.Id),
            livery.Id == boardedLiveryId,
            car,
            orchestrator,
            authoring);
    }

    private void DrawRow(ProfileRowContext ctx)
    {
        var present = BuildLayers(ctx);

        GUILayout.BeginHorizontal();

        GUILayout.Label(ctx.Boarded ? new GUIContent("▶", "You have boarded this locomotive.") : GUIContent.none,
            Styles.RowLabel, GUILayout.Width(MarkerWidth));
        GUILayout.Label(
            new GUIContent(ctx.Livery.Name, "Board this locomotive to create or edit its profile."),
            Styles.RowLabel, GUILayout.Width(NameWidth));

        DrawLayerSlots(present);

        DrawEnabled(ctx);

        DrawActions(ctx);

        GUILayout.EndHorizontal();
    }

    private static Dictionary<string, Layer> BuildLayers(ProfileRowContext ctx)
    {
        var present = new Dictionary<string, Layer>(3);
        if (ctx.BuiltIn != null) present["Built-in"] = new Layer("Stock", "This profile is built into TurboTurbo", true, false);
        if (ctx.Mod != null) present["Mod"] = new Layer("Mod", $"This profile is supplied by another mod ({ctx.ModName})", ctx.Mod.Enabled, false);
        if (ctx.User != null) present["User"] = new Layer("User", "This profile was created by you", ctx.User.Enabled, false);

        var effective = present.ContainsKey("User") ? "User"
            : present.ContainsKey("Mod") ? "Mod"
            : present.ContainsKey("Built-in") ? "Built-in"
            : null;
        if (effective != null)
        {
            var layer = present[effective];
            present[effective] = new Layer(layer.Label, layer.Tooltip, layer.Enabled, true);
        }

        return present;
    }

    private void DrawLayerSlots(Dictionary<string, Layer> present)
    {
        GUILayout.BeginHorizontal(GUILayout.Width(LayersWidth));
        for (var i = 0; i < LayerOrder.Length; i++)
        {
            if (present.TryGetValue(LayerOrder[i], out var layer)) DrawPill(layer);
            else GUILayout.Label(GUIContent.none, Styles.EmptySlot, GUILayout.Width(PillWidth));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPill(Layer layer)
    {
        var dot = layer.Effective ? (layer.Enabled ? DotActive : DotInactive) : DotShadowed;

        GUILayout.Label(
            new GUIContent(layer.Label, layer.Tooltip),
            Styles.Pill(PillFill, dot, PillText),
            GUILayout.Width(PillWidth));
    }

    private void DrawEnabled(ProfileRowContext ctx)
    {
        if (ctx.User == null)
        {
            GUILayout.Label(GUIContent.none, Styles.EmptySlot, GUILayout.Width(16f));
            GUILayout.Space(EnabledWidth - 16f);
            return;
        }

        var enabled = GUILayout.Toggle(ctx.User.Enabled, GUIContent.none, Styles.EnabledToggle, GUILayout.Width(16f));
        GUILayout.Space(EnabledWidth - 16f);
        if (enabled == ctx.User.Enabled) return;

        ProfileRepository.SetEnabled(ctx.Id, enabled);
        ctx.Orchestrator?.ReloadHostsForLivery(ctx.Id);
    }

    private void DrawActions(ProfileRowContext ctx)
    {
        GUILayout.BeginHorizontal(GUILayout.Width(ActionsWidth));

        if (ctx.Authoring)
        {
            var authored = ProfileRepository.ModSuppliesAuthoringLivery(ctx.Id);

            if (ctx.Boarded)
            {
                var content = authored
                    ? new GUIContent("Edit", $"Edit the mod profile definition (defined in: {ctx.ModName}).")
                    : new GUIContent("Create", $"Create a mod profile for this locomotive (written to: {ctx.ModName})");
                if (GUILayout.Button(content, Styles.ActionButton)) ProfileEditorPresenter.Instance?.Open(ctx.Car);
            }

            if (authored
                && GUILayout.Button(new GUIContent("Delete", $"Delete this mod profile (removed from: {ctx.ModName})."), Styles.ActionButton))
            {
                var error = ProfileRepository.DeleteFromAuthoringMod(ctx.Id);
                if (error != null) Log.Warn($"could not delete mod profile '{ctx.Id}' from {ctx.ModName}: {error}");
                else ctx.Orchestrator?.ReloadHostsForLivery(ctx.Id);
            }
        }
        else
        {
            if (ctx.Boarded)
            {
                var content = ctx.User == null
                    ? new GUIContent("Create", "Create a new user profile for this locomotive.")
                    : new GUIContent("Edit", "Edit the profile.");
                if (GUILayout.Button(content, Styles.ActionButton)) ProfileEditorPresenter.Instance?.Open(ctx.Car);
            }

            if (ctx.User != null
                && GUILayout.Button(new GUIContent("Delete", "Delete your user profile for this locomotive."), Styles.ActionButton))
            {
                ProfileRepository.DeleteProfile(ctx.Id);
                ctx.Orchestrator?.ReloadHostsForLivery(ctx.Id);
                Log.Info($"deleted profile '{ctx.Id}'");
            }
        }

        GUILayout.EndHorizontal();
    }

    private readonly struct Layer
    {
        public readonly string Label;
        public readonly string Tooltip;
        public readonly bool Enabled;
        public readonly bool Effective;

        public Layer(string label, string tooltip, bool enabled, bool effective)
        {
            Label = label;
            Tooltip = tooltip;
            Enabled = enabled;
            Effective = effective;
        }
    }
}
