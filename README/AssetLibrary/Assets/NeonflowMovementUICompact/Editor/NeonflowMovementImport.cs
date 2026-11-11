using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Neonflow.Movements.Compact.Editor
{
    [Serializable] public class SpriteRecord { public string path; public int[] border; }
    [Serializable] public class SpriteRecords { public SpriteRecord[] assets; }

    public sealed class NeonflowMovementImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace("\\", "/");
            const string folder = "/NeonflowMovementUICompact/";
            int marker = path.IndexOf(folder, StringComparison.Ordinal);
            if (marker < 0 || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            string root = path.Substring(0, marker + folder.Length);
            string manifestPath = root + "manifest.json";
            if (!File.Exists(manifestPath)) return;
            var records = JsonUtility.FromJson<SpriteRecords>(File.ReadAllText(manifestPath));
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = 1024;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            foreach (var record in records.assets)
                if (root + record.path == path)
                {
                    importer.spriteBorder = new Vector4(record.border[0], record.border[1], record.border[2], record.border[3]);
                    break;
                }
        }
    }
}
