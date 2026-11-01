// Adds the squad-invite deep link (agniroyale://join?code=XXXXXX) to the generated Android manifest,
// without replacing Unity's own manifest. Also makes sure INTERNET + Wi-Fi multicast (LAN room discovery) are declared.
#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Agni.Net.EditorTools
{
    public class AgniAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;
        const string NS = "http://schemas.android.com/apk/res/android";
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(file)) { Debug.LogWarning("[AgniManifest] not found: " + file); return; }
            var doc = new XmlDocument(); doc.Load(file);
            var manifest = doc.DocumentElement; var app = manifest["application"]; if (app == null) return;
            Perm(doc, manifest, "android.permission.INTERNET");
            Perm(doc, manifest, "android.permission.ACCESS_WIFI_STATE");
            Perm(doc, manifest, "android.permission.CHANGE_WIFI_MULTICAST_STATE");
            XmlElement act = null;
            foreach (XmlNode n in app.ChildNodes)
                if (n is XmlElement e && e.Name == "activity" && e.InnerXml.Contains("android.intent.category.LAUNCHER")) { act = e; break; }
            if (act == null) foreach (XmlNode n in app.ChildNodes) if (n is XmlElement e && e.Name == "activity") { act = e; break; }
            if (act == null) { Debug.LogWarning("[AgniManifest] no activity"); return; }
            if (act.InnerXml.Contains("agniroyale")) return;
            var f = doc.CreateElement("intent-filter");
            f.AppendChild(El(doc, "action", "android.intent.action.VIEW"));
            f.AppendChild(El(doc, "category", "android.intent.category.DEFAULT"));
            f.AppendChild(El(doc, "category", "android.intent.category.BROWSABLE"));
            var d = doc.CreateElement("data"); d.SetAttribute("scheme", NS, "agniroyale"); d.SetAttribute("host", NS, "join"); f.AppendChild(d);
            act.AppendChild(f);
            doc.Save(file); Debug.Log("[AgniManifest] deep link agniroyale://join added to " + act.GetAttribute("name", NS));
        }
        static XmlElement El(XmlDocument doc, string tag, string name) { var e = doc.CreateElement(tag); e.SetAttribute("name", NS, name); return e; }
        static void Perm(XmlDocument doc, XmlElement manifest, string name)
        {
            foreach (XmlNode n in manifest.ChildNodes) if (n is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", NS) == name) return;
            manifest.PrependChild(El(doc, "uses-permission", name));
        }
    }
}
#endif
