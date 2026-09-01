using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BuildBundle
{
    private const string HeatShimmerAsset = "Assets/Shimmer/HeatShimmer.shader";
    private const string SmokeAsset = "Assets/Shimmer/SmokeShader.shader";
    private const string OutputPath = "AssetBundles";
    private const string BundleFile = OutputPath + "/turboturbo_assets";
    private const string BundleName = "turboturbo_assets";
    private const string GamePathPref = "TurboTurbo.GamePath";

    [MenuItem("TurboTurbo/Build Bundle")]
    public static void Build()
    {
        BuildInternal();
    }

    [MenuItem("TurboTurbo/Verify Bundle")]
    public static void Verify()
    {
        VerifyInternal(out _);
    }

    [MenuItem("TurboTurbo/Build and Deploy")]
    public static void BuildAndDeploy()
    {
        if (!BuildInternal())
        {
            Debug.LogError("<b>DEPLOY ABORTED</b> - build failed");
            return;
        }

        if (!VerifyInternal(out int okShaders))
        {
            Debug.LogError("<b>DEPLOY ABORTED</b> - verification failed");
            return;
        }

        string gamePath = ResolveGamePath();
        if (gamePath == null)
        {
            Debug.LogError("<b>DEPLOY ABORTED</b> - Derail Valley install not found");
            return;
        }

        string dest = Path.Combine(gamePath, "Mods", "TurboTurbo", BundleName);
        try
        {
            File.Copy(BundleFile, dest, overwrite: true);
            Debug.Log($"<b>DEPLOY OK</b>: {dest} ({new FileInfo(dest).Length} bytes, {okShaders}/2 shaders supported)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<b>DEPLOY FAILED</b>: {e.Message} (is the game running?)");
        }
    }

    private static bool BuildInternal()
    {
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { HeatShimmerAsset, SmokeAsset },
            },
        };

        if (!Directory.Exists(OutputPath))
        {
            Directory.CreateDirectory(OutputPath);
        }

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            OutputPath,
            builds,
            BuildAssetBundleOptions.UncompressedAssetBundle,
            BuildTarget.StandaloneWindows64);

        if (manifest == null)
        {
            Debug.LogError("BuildAssetBundles failed");
            return false;
        }

        bool listed = false;
        foreach (string name in manifest.GetAllAssetBundles())
        {
            if (name == BundleName) listed = true;
        }
        if (!listed)
        {
            Debug.LogError($"{BundleName} missing from the build manifest");
            return false;
        }

        Debug.Log($"<b>BUILD OK</b>: {BundleFile} ({new FileInfo(BundleFile).Length} bytes)");
        return true;
    }

    private static bool VerifyInternal(out int supportedShaders)
    {
        supportedShaders = 0;

        if (!File.Exists(BundleFile))
        {
            Debug.LogError("verification failed: bundle file missing");
            return false;
        }

        var bundle = AssetBundle.LoadFromFile(BundleFile);
        if (bundle == null)
        {
            Debug.LogError("verification failed: bundle did not load");
            return false;
        }

        bool ok = true;
        foreach (string asset in new[] { HeatShimmerAsset, SmokeAsset })
        {
            Shader shader = bundle.LoadAsset<Shader>(asset);
            bool supported = shader != null && shader.isSupported;
            Debug.Log(supported
                ? $"<b>VERIFY OK</b>: shader='{shader.name}', supported={shader.isSupported}"
                : $"verification failed: '{asset}' missing or unsupported");
            ok &= supported;
            if (supported) supportedShaders++;
        }
        bundle.Unload(false);
        return ok;
    }

    private static string ResolveGamePath()
    {
        string cached = EditorPrefs.GetString(GamePathPref, "");
        if (IsValidGameDir(cached))
        {
            return cached;
        }

        foreach (string candidate in CandidateGamePaths())
        {
            if (IsValidGameDir(candidate))
            {
                EditorPrefs.SetString(GamePathPref, candidate);
                Debug.Log($"game path resolved: {candidate}");
                return candidate;
            }
        }

        string picked = EditorUtility.OpenFolderPanel(
            "Select the Derail Valley install folder (contains DerailValley.exe)", "C:\\", "");
        if (IsValidGameDir(picked))
        {
            EditorPrefs.SetString(GamePathPref, picked);
            return picked;
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> CandidateGamePaths()
    {
        string steam = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
        steam = Path.Combine(steam, "Steam");
        var libs = new System.Collections.Generic.List<string> { steam };

        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
        }

        foreach (string lib in libs)
        {
            yield return Path.Combine(lib, "steamapps", "common", "Derail Valley");
        }
    }

    private static bool IsValidGameDir(string path)
    {
        return !string.IsNullOrEmpty(path)
               && File.Exists(Path.Combine(path, "DerailValley.exe"))
               && Directory.Exists(Path.Combine(path, "Mods"));
    }
}
