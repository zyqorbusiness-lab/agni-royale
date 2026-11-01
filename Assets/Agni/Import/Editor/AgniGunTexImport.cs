// Agni Royale — IMPORT v1: gun texture import settings (normal maps, linear metal/smoothness, 1024 cap for phones)
using UnityEditor;
namespace Agni.Import.EditorTools
{
    public class AgniGunTexImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/AgniImport/Guns/")) return;
            var ti = (TextureImporter)assetImporter; ti.maxTextureSize = 1024; ti.mipmapEnabled = true;
            if (assetPath.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("MetallicSmoothness") || assetPath.Contains("FlowNoise")) ti.sRGBTexture = false;
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 1024; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/AgniImport/Guns/")) return;
            var mi = (ModelImporter)assetImporter; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importCameras = false; mi.importLights = false;
        }
    }
}
