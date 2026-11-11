using UnityEditor;
using UnityEngine;
namespace Neonflow.Editor
{
    public class NeonflowCursorImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace("\\", "/").Contains("/NeonflowCursor/") || !assetPath.EndsWith(".png")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Cursor;
            t.isReadable = true; t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.npotScale = TextureImporterNPOTScale.None;
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
