// Editor helper: builds Assets/Agni/Intro/Intro.unity (camera-less scene with one IntroMascot) and puts it FIRST in Build Settings.
// Menu: Agni Royale > Intro > Create Intro Scene + Put First In Build
// Batchmode: Unity -batchmode -quit -projectPath <proj> -executeMethod Agni.Intro.EditorTools.AgniIntroSetup.CreateBatch
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agni.Intro.EditorTools
{
    public static class AgniIntroSetup
    {
        public const string ScenePath = "Assets/Agni/Intro/Intro.unity";

        [MenuItem("Agni Royale/Intro/Create Intro Scene + Put First In Build")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("AgniIntro");
            go.AddComponent<IntroMascot>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[AgniIntro] Intro.unity created and set as build index 0. Next scene: " + (list.Count > 1 ? list[1].path : "(none)"));
        }

        public static void CreateBatch() { Create(); }
    }
}
