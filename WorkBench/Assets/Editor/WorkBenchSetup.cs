using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    public static class WorkBenchSetup
    {
        [MenuItem("TurboTurbo/WorkBench Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("ShimmerBench");
            go.AddComponent<ShimmerBench>();

            EditorSceneManager.SaveScene(scene, "Assets/Shimmer/Bench.unity");
            EditorUtility.DisplayDialog("WorkBench", "Bench scene created at Assets/Shimmer/Bench.unity\n\nPress Play and tweak the ShimmerBench sliders.", "OK");
        }
    }
}
