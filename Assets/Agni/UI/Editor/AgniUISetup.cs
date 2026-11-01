// Agni Royale — editor setup for the Loading + Lobby screens (scenes are tiny: one GameObject each; everything else is built by code).
//   Menu: Agni Royale ▸ UI ▸ Create Loading + Lobby scenes (Intro → Loading → Lobby → AgniMap)
//   Batch: Unity -batchmode -quit -nographics -projectPath <proj> -executeMethod Agni.UI.EditorTools.AgniUISetup.CreateBatch
//   Proof: DISPLAY=:93 Unity -batchmode -force-glcore -projectPath <proj> -executeMethod Agni.UI.EditorTools.AgniUISetup.ProofBatch -agniProof <outDir> -logFile <log>
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agni.UI.EditorTools
{
    public class AgniUITexturePost : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/AgniUI/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.sRGBTexture = true;
            ti.wrapMode = n == "stripes" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            bool big = n.StartsWith("poster") || n.StartsWith("lobby_bg");
            ti.maxTextureSize = big ? 2048 : 1024;
            ti.textureCompression = big ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            Vector4 b = Vector4.zero;
            foreach (var pre in new[] { "rr_", "btn_", "btno_", "ol_" })
                if (n.StartsWith(pre) && int.TryParse(n.Substring(pre.Length), out int r)) { float e = r + 4; b = new Vector4(e, e, e, e); }
            if (n == "shadow_24") b = new Vector4(70, 70, 70, 70);
            ti.spriteBorder = b;
        }
    }

    /// Lobby v2 audio import: music streams (low RAM), UI SFX decompress on load (zero latency).
    public class AgniAudioImportPost : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/AgniAudio/")) return;
            var ai = (AudioImporter)assetImporter; var s = ai.defaultSampleSettings;
            bool music = !System.IO.Path.GetFileName(assetPath).StartsWith("sfx_");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = music ? 0.6f : 0.7f;
            ai.defaultSampleSettings = s; ai.forceToMono = !music;
        }
    }

    /// Lobby v2 anime characters (Resources/AgniChars): Generic rig, imported normals (custom face normals), no animation.
    public class AgniCharImportPost : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/AgniChars/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = false;
            mi.importNormals = ModelImporterNormals.Import; mi.importBlendShapes = false; mi.isReadable = false;
        }
    }

    public static class AgniUISetup
    {
        public const string LoadingPath = "Assets/Agni/UI/Loading.unity", LobbyPath = "Assets/Agni/UI/Lobby.unity", IntroPath = "Assets/Agni/Intro/Intro.unity";
        const string MapPath = "Assets/Agni/Scenes/AgniMap.unity";

        [MenuItem("Agni Royale/UI/Create Loading + Lobby scenes (Intro → Loading → Lobby → Map)")]
        public static void Create()
        {
            // 1) intro scene first (from the intro worker's helper), then point it at Loading
            Agni.Intro.EditorTools.AgniIntroSetup.Create();
            var intro = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Single);
            var im = Object.FindAnyObjectByType<Agni.Intro.IntroMascot>();
            if (im != null) { im.nextSceneName = "Loading"; EditorUtility.SetDirty(im); }
            EditorSceneManager.SaveScene(intro);

            // 2) loading + lobby scenes
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("AgniLoading").AddComponent<AgniLoadingScreen>().nextScene = "Lobby";
            EditorSceneManager.SaveScene(s, LoadingPath);
            s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("AgniLobby").AddComponent<AgniLobby>().matchScene = "AgniMap";
            EditorSceneManager.SaveScene(s, LobbyPath);

            // 3) build order
            var order = new[] { IntroPath, LoadingPath, LobbyPath, MapPath };
            var rest = EditorBuildSettings.scenes.Where(x => !order.Contains(x.path)).ToList();
            var list = order.Where(p => File.Exists(p)).Select(p => new EditorBuildSettingsScene(p, true)).ToList(); list.AddRange(rest);
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[AgniUI] build order: " + string.Join(" -> ", EditorBuildSettings.scenes.Select(x => Path.GetFileNameWithoutExtension(x.path))));
        }
        public static void CreateBatch() { Create(); }

        [MenuItem("Agni Royale/UI/Render UI proof (play mode)")]
        public static void LobbyProofBatch()
        {
            EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            Debug.Log("[AgniUIProof] entering play mode (lobby)");
            EditorApplication.EnterPlaymode();
        }

        public static void ProofBatch()
        {
            EditorSceneManager.OpenScene(LoadingPath, OpenSceneMode.Single);
            Debug.Log("[AgniUIProof] entering play mode");
            EditorApplication.EnterPlaymode();
        }
    }
}
