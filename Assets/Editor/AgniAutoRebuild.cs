using System.IO;
using UnityEditor;
using UnityEngine;

// Assets/Agni/Generated/ (terrain data, layers, meshes, prefabs) is not kept in git.
// After a fresh clone, rebuild the map once automatically when the editor opens (interactive editor only).
[InitializeOnLoad]
public static class AgniAutoRebuild
{
    static AgniAutoRebuild()
    {
        if (Application.isBatchMode) return;
        if (File.Exists("Assets/Agni/Generated/T_0_0.asset")) return;
        EditorApplication.delayCall += () =>
        {
            if (File.Exists("Assets/Agni/Generated/T_0_0.asset")) return;
            Debug.Log("[Agni] Generated map assets missing - rebuilding the map scene (Agni Royale -> Rebuild map scene)...");
            AgniWorldBuilder.BuildAll();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(AgniWorldBuilder.ScenePath);
        };
    }
}
