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
    internal record struct LiveryInfo(string Id, string TypeId, bool IsLoco, bool IsHidden);

    internal static List<LiveryInfo> LocoLiveries()
    {
        var model = DVObjectModel.current ? DVObjectModel.current : Globals.G.Types;
        if (model == null || model.Liveries == null) return [];

        return model.Liveries
            .Where(livery => livery != null)
            .Select(livery => new LiveryInfo(
                livery.id,
                livery.parentType != null ? livery.parentType.id : "",
                livery.parentType != null && CarTypes.IsLocomotive(livery),
                livery.isHidden))
            .Where(livery => livery is {IsLoco: true, IsHidden: false})
            .OrderBy(livery => livery.Id, StringComparer.Ordinal)
            .ToList();
    }
}