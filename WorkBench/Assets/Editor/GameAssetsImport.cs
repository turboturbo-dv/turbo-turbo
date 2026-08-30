using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Wires the AssetRipper-exported DE6 into the bench scene: strips the
    /// dead DV scripts, remaps materials that reference DV's custom shaders
    /// (which don't survive the export) onto Standard, and frames the loco
    /// so the shimmer quad sits at the exhaust stack.
    /// </summary>
    public static class GameAssetsImport
    {
        private const string PrefabPath = "Assets/GameAssets/GameObject/LocoDE6.prefab";

        internal static void AddDe6ToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("WorkBench", $"DE6 prefab not found at {PrefabPath}", "OK");
                return;
            }

            var loco = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            loco.transform.position = new Vector3(0f, -4f, 8f);
            loco.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // 1. remove every missing-script placeholder (all DV behaviours)
            int removed = 0;
            foreach (var t in loco.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            Debug.Log($"DE6 import: removed {removed} missing scripts");

            // 2. remap materials on DV's custom shaders -> Standard
            var remapped = new System.Collections.Generic.HashSet<Material>();
            foreach (var r in loco.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string name = m.shader != null ? m.shader.name : "";
                    if (name.StartsWith("DV/") || name == "" || name == "Hidden/InternalErrorShader")
                    {
                        if (!remapped.Contains(m))
                        {
                            remapped.Add(m);
                            var std = new Material(Shader.Find("Standard"));
                            if (m.HasProperty("_MainTex")) std.mainTexture = m.mainTexture;
                            if (m.HasProperty("_Color")) std.color = m.color;
                            std.name = m.name + " (remap)";
                            m.shader = std.shader;
                            m.CopyPropertiesFromMaterial(std);
                        }
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(r);
            }
            Debug.Log($"DE6 import: remapped {remapped.Count} DV materials to Standard");

            // 3. lighting (remapped materials are lit)
            if (Object.FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("BenchLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
            }

            // 4. the shimmer anchor is a fixed manual value for now (set on
            // the ShimmerBench component; the heat quad itself is created at
            // Play time by ShimmerBench.Start())

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
