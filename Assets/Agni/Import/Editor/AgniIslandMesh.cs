// Agni Royale — IMPORT v1: puts the island ground (AgniIslandGround, built from code on load) into the arena scene.
using UnityEditor;
using UnityEngine;

namespace Agni.Import.EditorTools
{
    public static class AgniIslandMesh
    {
        public static GameObject Build(Transform parent, Material grass, Material sand)
        {
            AssetDatabase.DeleteAsset("Assets/Agni/Net/Arena/IslandGround.asset");
            var go = new GameObject("Ground"); go.transform.SetParent(parent, false);
            var g = go.AddComponent<AgniIslandGround>(); g.grass = grass; g.sand = sand; g.Build();
            return go;
        }
    }
}
