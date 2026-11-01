// Agni Royale — lives in the AgniArena scene. Server side: spawns the match once every peer has loaded the scene.
// Opened directly (editor Play, or solo START without a party): starts an offline host with bots.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Agni.Net
{
    public class AgniArenaBootstrap : MonoBehaviour
    {
        public float arenaRadius = 1750f;
        float t0; bool spawned;

        void Awake()
        {
            AgniMatch.ArenaRadius = arenaRadius; t0 = Time.realtimeSinceStartup;
            AgniVillages.Root = null; AgniVillages.Build(null); // houses + loot spots (same on server and clients)
            Agni.Import.AgniLandmarks.Root = null; Agni.Import.AgniLandmarks.Build(null); // IMPORT v1: Blender landmarks + their loot spots
            Application.targetFrameRate = AgniNetConfig.HeadlessServer ? 30 : Application.targetFrameRate;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) StripForHeadless();
            if (AgniNet.NM != null && AgniNet.NM.SceneManager != null) AgniNet.NM.SceneManager.OnLoadEventCompleted += Loaded;
        }
        /// -nographics dedicated server: nothing is ever drawn, so take every render-side object out of the scene
        /// (the player crashed natively a moment after loading the arena with them in place).
        static void StripForHeadless()
        {
            RenderSettings.skybox = null; RenderSettings.fog = false;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(l);
            foreach (var r in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(r);
            foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(p.gameObject);
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) r.enabled = false;
            Debug.Log("[AgniArena] headless: render objects stripped");
        }
        void OnDestroy() { if (AgniNet.NM != null && AgniNet.NM.SceneManager != null) AgniNet.NM.SceneManager.OnLoadEventCompleted -= Loaded; }
        void Start()
        {
            if (!AgniNet.Running && !AgniNetConfig.Has("-agniClient"))
            {   // scene opened on its own: offline practice = host with bots
                if (AgniNet.StartHost()) SpawnMatch();
            }
        }
        void Loaded(string scene, LoadSceneMode mode, List<ulong> done, List<ulong> timedOut)
        {
            if (scene != gameObject.scene.name) return;
            if (timedOut != null && timedOut.Count > 0) Debug.LogWarning("[AgniArena] clients timed out loading: " + timedOut.Count);
            SpawnMatch();
        }
        void Update()
        {   // safety net: if the load event never came (e.g. scene loaded before the server started), spawn anyway
            if (!spawned && AgniNet.IsServer && Time.realtimeSinceStartup - t0 > 20f) SpawnMatch();
        }
        void SpawnMatch()
        {
            if (spawned || !AgniNet.IsServer || AgniMatch.Instance != null) { spawned = true; return; }
            spawned = true;
            var go = Instantiate(Resources.Load<GameObject>("AgniNet/AgniMatch")); go.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[AgniArena] match spawned (" + AgniNet.Role + ")");
        }
    }
}
