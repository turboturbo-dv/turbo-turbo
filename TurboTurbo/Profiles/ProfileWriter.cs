using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace TurboTurbo.Profiles;

/// <summary>
/// Writes or removes a <c>&lt;LocoProfile&gt;</c> in a mod's <c>TurboConfig.xml</c>, preserving the rest of the file.
/// </summary>
internal static class ProfileWriter
{
    internal const string ConfigFileName = "TurboConfig.xml";

    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(TurboConfig));

    /// <summary>Serializes a single profile as a standalone <c>&lt;LocoProfile&gt;</c> fragment.</summary>
    internal static string SerializeFragment(LocoProfile profile) => ToElement(profile)?.ToString() ?? "";

    /// <summary>Writes <paramref name="profile"/> into <paramref name="modPath"/>, returning an error or null.</summary>
    internal static string Write(LocoProfile profile, string modPath)
    {
        var element = ToElement(profile);
        if (element == null) return "could not serialize the profile";

        var document = Load(modPath, out var file, out var error);
        if (document == null) return error;

        var profiles = EnsureProfiles(document, file, out error);
        if (profiles == null) return error;

        var existing = Find(profiles, profile.LiveryId);
        if (existing != null) existing.ReplaceWith(element);
        else profiles.Add(element);

        return Save(document, file);
    }

    /// <summary>Removes the profile for <paramref name="liveryId"/> from <paramref name="modPath"/>.</summary>
    internal static string Delete(string liveryId, string modPath)
    {
        var document = Load(modPath, out var file, out var error);
        if (document == null) return error;
        if (!File.Exists(file)) return $"no {ConfigFileName} in the target mod";

        var profiles = EnsureProfiles(document, file, out error);
        if (profiles == null) return error;

        var existing = Find(profiles, liveryId);
        if (existing == null) return null;

        existing.Remove();
        return Save(document, file);
    }

    private static XDocument Load(string modPath, out string file, out string error)
    {
        file = Path.Combine(modPath, ConfigFileName);
        error = null;
        try
        {
            return File.Exists(file)
                ? XDocument.Load(file)
                : new XDocument(new XElement("TurboConfig", new XElement("LocoProfiles")));
        }
        catch (Exception e)
        {
            error = $"could not read {file}: {e.Message}";
            return null;
        }
    }

    private static XElement EnsureProfiles(XDocument document, string file, out string error)
    {
        error = null;
        var root = document.Root;
        if (root == null || root.Name != "TurboConfig")
        {
            error = $"{file} is not a TurboConfig.xml";
            return null;
        }

        var profiles = root.Element("LocoProfiles");
        if (profiles == null)
        {
            profiles = new XElement("LocoProfiles");
            root.Add(profiles);
        }

        return profiles;
    }

    private static XElement Find(XElement profiles, string liveryId) =>
        profiles.Elements("LocoProfile").FirstOrDefault(e => (string)e.Element("LiveryId") == liveryId);

    private static string Save(XDocument document, string file)
    {
        try
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using var writer = XmlWriter.Create(file, settings);
            document.Save(writer);
        }
        catch (Exception e)
        {
            return $"could not write {file}: {e.Message}";
        }

        return null;
    }

    private static XElement ToElement(LocoProfile profile)
    {
        var config = new TurboConfig { LocoProfiles = { profile } };
        using var writer = new StringWriter();
        Serializer.Serialize(writer, config);
        var document = XDocument.Parse(writer.ToString());
        return document.Root?.Element("LocoProfiles")?.Element("LocoProfile");
    }
}