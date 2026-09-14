using System.Collections.Generic;

using TurboTurbo.Profiles;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

public class Settings : UnityModManager.ModSettings
{
    public KeyBinding ToggleDevPanel = new KeyBinding();

    public List<LocoProfile> LocoProfiles = new();
}