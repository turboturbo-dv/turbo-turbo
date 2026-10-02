using System;
using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;
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

        GUILayout.BeginVertical(Styles.OverviewBox);
        DrawHeader();
        var liveries = OrderedLiveries();
        for (var i = 0; i < liveries.Count; i++)
        {
            if (i > 0) GUILayout.Space(RowSpacing);
            DrawRow(BuildContext(liveries[i], boardedLiveryId, car, orchestrator));
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
        Orchestrator orchestrator)
    {
        return new ProfileRowContext(
            livery,
            ProfileService.Resolve(livery.Id, SettingsStore.Current.ResolutionMode),
            livery.Id == boardedLiveryId,
            car,
            orchestrator);
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
        foreach (var entry in ctx.Resolution.Present)
        {
            present[TierKey(entry.Tier)] = new Layer(TierLabel(entry.Tier), TierTooltip(entry), false, false);
        }

        if (ctx.Resolution.Winner is { } winner)
        {
            var key = TierKey(winner.Tier);
            var layer = present[key];
            present[key] = new Layer(layer.Label, layer.Tooltip, !ctx.Resolution.Disabled, true);
        }

        return present;
    }

    private static ProfileEntry? UserEntry(ProfileResolution resolution) =>
        resolution.Present.FirstOrNull(e => e.Tier == ProfileTier.User);

    private static ProfileEntry? ModEntry(ProfileResolution resolution) =>
        resolution.Present.FirstOrNull(e => e.Tier == ProfileTier.Mod);

    private static string TierKey(ProfileTier tier) => tier switch
    {
        ProfileTier.BuiltIn => "Built-in",
        ProfileTier.Mod => "Mod",
        _ => "User",
    };

    private static string TierLabel(ProfileTier tier) => tier switch
    {
        ProfileTier.BuiltIn => "Stock",
        ProfileTier.Mod => "Mod",
        _ => "User",
    };

    private static string TierTooltip(ProfileEntry entry) => entry.Tier switch
    {
        ProfileTier.BuiltIn => "This profile is built into TurboTurbo",
        ProfileTier.Mod => $"This profile is supplied by another mod ({entry.Origin})",
        _ => "This profile was created by you",
    };

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
        var user = UserEntry(ctx.Resolution);
        if (user == null)
        {
            GUILayout.Label(GUIContent.none, Styles.EmptySlot, GUILayout.Width(16f));
            GUILayout.Space(EnabledWidth - 16f);
            return;
        }

        var profile = user.Value.Profile;
        var enabled = GUILayout.Toggle(profile.Enabled, GUIContent.none, Styles.EnabledToggle, GUILayout.Width(16f));
        GUILayout.Space(EnabledWidth - 16f);
        if (enabled == profile.Enabled) return;

        ProfileService.User.SetEnabled(ctx.Id, enabled);
        ctx.Orchestrator?.ReloadHostsForLivery(ctx.Id);
    }

    private void DrawActions(ProfileRowContext ctx)
    {
        GUILayout.BeginHorizontal(GUILayout.Width(ActionsWidth));

        var user = UserEntry(ctx.Resolution);

        if (ctx.Resolution.Mode == ResolutionMode.Authoring)
        {
            var authored = AuthoringService.HasProfile(ctx.Id);
            var modName = ModEntry(ctx.Resolution)?.Origin;

            if (ctx.Boarded)
            {
                var content = authored
                    ? new GUIContent("Edit", $"Edit the mod profile definition (defined in: {modName}).")
                    : new GUIContent("Create", $"Create a mod profile for this locomotive (written to '{modName}')");
                if (GUILayout.Button(content, Styles.ActionButton)) ProfileEditorPresenter.Instance?.Open(ctx.Car);
            }

            if (authored
                && GUILayout.Button(new GUIContent("Delete", $"Delete this mod profile (removed from '{modName}')"), Styles.ActionButton))
            {
                var error = AuthoringService.Delete(ctx.Id);
                if (error != null) Log.Warn($"could not delete mod profile '{ctx.Id}' from {modName}: {error}");
                else ctx.Orchestrator?.ReloadHostsForLivery(ctx.Id);
            }
        }
        else
        {
            if (ctx.Boarded)
            {
                var content = user == null
                    ? new GUIContent("Create", "Create a new user profile for this locomotive.\n\n" +
                                               "If a builtin profile or a mod profile exists, the user profile will start out with those settings. " +
                                               "Otherwise, the DE6 defaults will be applied.")
                    : new GUIContent("Edit", "Edit the profile.");
                if (GUILayout.Button(content, Styles.ActionButton)) ProfileEditorPresenter.Instance?.Open(ctx.Car);
            }

            if (user != null
                && GUILayout.Button(new GUIContent("Delete", "Delete your user profile for this locomotive."), Styles.ActionButton))
            {
                ProfileService.User.DeleteProfile(ctx.Id);
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
