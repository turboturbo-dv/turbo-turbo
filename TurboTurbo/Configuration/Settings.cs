using System.Collections.Generic;

using TurboTurbo.Profiles;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

public class Settings : UnityModManager.ModSettings
{
    public KeyBinding ToggleDevPanel = new KeyBinding();
    public KeyBinding OpenProfileEditor = new KeyBinding();

    public bool AuthoringMode;
    public string AuthoringTargetModId = "";

    public List<LocoProfile> LocoProfiles = new();

    public bool IsAuthoring => AuthoringMode && !string.IsNullOrEmpty(AuthoringTargetModId);

    internal ResolutionMode ResolutionMode => IsAuthoring ? ResolutionMode.Authoring : ResolutionMode.Normal;
}
