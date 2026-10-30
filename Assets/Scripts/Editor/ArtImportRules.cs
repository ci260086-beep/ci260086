using UnityEditor;
using UnityEngine;

namespace DeepDrill.Editor
{
    /// <summary>Everything under Assets/Art imports as crisp 16 PPU pixel art.</summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        // New art reaches the game by itself, including while the project is open in the Editor.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith("Assets/Art/")) continue;
                EditorApplication.delayCall -= ProjectSetup.RefreshArtLibrary;
                EditorApplication.delayCall += ProjectSetup.RefreshArtLibrary;
                return;
            }
        }
    }
}
