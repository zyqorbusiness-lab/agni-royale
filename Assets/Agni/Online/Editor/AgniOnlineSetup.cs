// Agni Royale ▸ Online — links this project to Sujon's Unity Cloud project (the ONLY value the online layer needs).
// 1) put the Project ID (+ optional org id) in Assets/Agni/Online/Resources/AgniOnline/AgniOnlineConfig.json
// 2) menu Agni Royale ▸ Online ▸ Link Cloud Project  (or batch: -executeMethod Agni.Social.EditorTools.AgniOnlineSetup.LinkBatch -agniCloudProject <id> [-agniCloudOrg <org>])
// 3) restart the editor once. Same thing as Edit ▸ Project Settings ▸ Services ▸ "Link to existing project".
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Agni.Social.EditorTools
{
    public static class AgniOnlineSetup
    {
        const string Cfg = "Assets/Agni/Online/Resources/AgniOnline/AgniOnlineConfig.json";
        const string PS = "ProjectSettings/ProjectSettings.asset";

        [MenuItem("Agni Royale/Online/Link Cloud Project")]
        public static void Link()
        {
            var cfg = JsonUtility.FromJson<AgniOnlineConfig>(File.ReadAllText(Cfg));
            if (string.IsNullOrEmpty(cfg.cloudProjectId)) { EditorUtility.DisplayDialog("Agni Online", "AgniOnlineConfig.json me cloudProjectId khaali hai.\ncloud.unity.com ▸ project ▸ Settings se Project ID copy karo.", "OK"); return; }
            Apply(cfg.cloudProjectId, null);
            EditorUtility.DisplayDialog("Agni Online", "Project ID likh diya: " + cfg.cloudProjectId + "\nEditor ek baar restart karo.", "OK");
        }

        public static void LinkBatch()
        {
            var a = System.Environment.GetCommandLineArgs();
            string Arg(string k) { int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
            var id = Arg("-agniCloudProject"); if (string.IsNullOrEmpty(id)) { var cfg = JsonUtility.FromJson<AgniOnlineConfig>(File.ReadAllText(Cfg)); id = cfg.cloudProjectId; }
            if (string.IsNullOrEmpty(id)) { Debug.LogError("[AgniOnline] no project id"); EditorApplication.Exit(2); return; }
            Apply(id, Arg("-agniCloudOrg")); EditorApplication.Exit(0);
        }

        static void Apply(string projectId, string org)
        {
            var t = File.ReadAllText(PS);
            t = Regex.Replace(t, @"(\n  cloudProjectId:)[^\n]*", "$1 " + projectId);
            t = Regex.Replace(t, @"(\n  projectName:)[^\n]*", "$1 Agni Royale");
            if (!string.IsNullOrEmpty(org)) t = Regex.Replace(t, @"(\n  organizationId:)[^\n]*", "$1 " + org);
            t = Regex.Replace(t, @"(\n  cloudEnabled:)[^\n]*", "$1 1");
            File.WriteAllText(PS, t);
            var cfgText = File.ReadAllText(Cfg); var cfg2 = JsonUtility.FromJson<AgniOnlineConfig>(cfgText); cfg2.cloudProjectId = projectId; File.WriteAllText(Cfg, JsonUtility.ToJson(cfg2, true));
            Debug.Log("[AgniOnline] linked cloud project " + projectId + (string.IsNullOrEmpty(org) ? "" : " org " + org) + " — restart the editor");
        }
    }
}
