using System;
using System.Collections.Generic;
using System.Linq;

using DV;
using DV.ThingTypes;

namespace TurboTurbo.Profiles;

/// <summary>
/// Enumerates the liveries the game knows about, including ones added by other mods.
/// These can be targeted when creating or editing a profile.
/// </summary>
internal static class LiveryCatalog
{
    internal record struct LiveryInfo(string Id, string TypeId, string LocalizationKey, bool IsLoco, bool IsHidden);

    internal static List<LiveryInfo> All()
    {
        var model = DVObjectModel.current ?? Globals.G?.Types;
        if (model?.Liveries == null) return new List<LiveryInfo>();

        return model.Liveries
            .Where(livery => livery != null)
            .Select(livery => new LiveryInfo(
                livery.id,
                livery.parentType != null ? livery.parentType.id : "",
                livery.localizationKey,
                livery.parentType != null && CarTypes.IsLocomotive(livery),
                livery.isHidden))
            .ToList();
    }

    internal static List<LiveryInfo> LocoLiveries()
    {
        // we may want to filter further here
        return All()
            .Where(livery => livery.IsLoco && !livery.IsHidden)
            .OrderBy(livery => livery.Id, StringComparer.Ordinal)
            .ToList();
    }
}