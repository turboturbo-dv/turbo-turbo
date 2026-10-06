using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using JetBrains.Annotations;

using TurboTurbo.Profiles.Storage.V1;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Writes or removes a <c>&lt;LocoProfile&gt;</c> in a mod's <c>TurboConfig.xml</c>.
/// </summary>
internal static class ModProfileWriter
{
    /// <summary>Serializes a single profile as a standalone <c>&lt;LocoProfile&gt;</c> fragment.</summary>
    internal static string SerializeFragment(LocoProfile profile) => ToElement(profile).ToString();

    /// <summary>
    /// Writes <paramref name="profile"/> into <paramref name="modPath"/>, returning null if there was no error.
    /// </summary>
    internal static Error? Write(LocoProfile profile, string modPath)
    {
        var element = ToElement(profile);

        var loadResult = LoadOrCreateConfig(modPath, out var file);
        if (!loadResult.IsSuccess)
        {
            return loadResult.Error;
        }

        var profileResult = GetOrCreateProfiles(loadResult.Value, file);
        if (!profileResult.IsSuccess)
        {
            return profileResult.Error;
        }

        var existing = Find(profileResult.Value, profile.LiveryId);
        if (existing != null)
        {
            existing.ReplaceWith(element);
        }
        else
        {
            profileResult.Value.Add(element);
        }

        return Save(loadResult.Value, file);
    }

    /// <summary>
    /// Removes the profile for <paramref name="liveryId"/> from <paramref name="modPath"/>,
    /// returning null if there was no error.
    /// </summary>
    internal static Error? Delete(string liveryId, string modPath)
    {
        var result = LoadOrCreateConfig(modPath, out var file);
        if (!result.IsSuccess) return result.Error;
        if (!File.Exists(file)) return new Error($"no {TurboConfigCodec.FileName} in the target mod");

        var document = result.Value;

        var foundProfile = GetOrCreateProfiles(document, file)
            .Select(p => Find(p, liveryId));

        if (foundProfile is not { Value: { } existing })
        {
            return foundProfile.Error;
        }

        existing.Remove();
        return Save(document, file);

    }

    private static Result<XDocument> LoadOrCreateConfig(string modPath, out string file)
    {
        file = Path.Combine(modPath, TurboConfigCodec.FileName);
        try
        {
            var document = File.Exists(file)
                ? XDocument.Load(file)
                : new XDocument(new XElement("TurboConfig", new XElement("LocoProfiles")));
            return document;
        }
        catch (Exception e)
        {
            return new Error($"could not read {file}: {e.Message}");
        }
    }

    private static Result<XElement> GetOrCreateProfiles(XDocument document, string file)
    {
        var root = document.Root;
        if (root == null || root.Name != "TurboConfig")
        {
            return new Error($"{file} is not a TurboConfig.xml");
        }

        var profiles = root.Element("LocoProfiles");
        if (profiles == null)
        {
            profiles = new XElement("LocoProfiles");
            root.Add(profiles);
        }

        return profiles;
    }

    [CanBeNull]
    private static XElement Find(XElement profiles, string liveryId) =>
        profiles.Elements("LocoProfile").FirstOrDefault(e => (string)e.Element("LiveryId") == liveryId);

    private static Error? Save(XDocument document, string file)
    {
        try
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using var writer = XmlWriter.Create(file, settings);
            document.Save(writer);
        }
        catch (Exception e)
        {
            return new Error($"could not write {file}: {e.Message}");
        }

        return null;
    }

    private static XElement ToElement(LocoProfile profile)
    {
        var config = new TurboConfigXml { LocoProfiles = { ProfileMapper.ToXml(profile) } };
        using var writer = new StringWriter();
        TurboConfigCodec.Serializer.Serialize(writer, config);
        var document = XDocument.Parse(writer.ToString());
        return document.Root?.Element("LocoProfiles")?.Element("LocoProfile")
               ?? throw new InvalidOperationException("could not serialize profile");
    }
}
