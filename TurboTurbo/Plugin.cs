using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace TurboTurbo;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "io.geluk.turboturbo";
    public const string Name = "TurboTurbo";
    public const string Version = "0.1.0";

    internal static ManualLogSource Log { get; private set; }

    private Harmony _harmony;

    private void Awake()
    {
        Log = Logger;
        _harmony = new Harmony(Guid);

        TurboConfig.Bind(Config);
        TurboConsole.Bind(Config);
        _harmony.PatchAll();

        Log.LogInfo($"{Name} {Version} loaded");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    private void Update()
    {
        SimInspector.Update();
        TurboModel.HandleUpdate();
        TurboConsole.HandleUpdate();
    }
}
