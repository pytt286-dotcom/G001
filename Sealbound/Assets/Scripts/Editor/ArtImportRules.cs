using UnityEditor;
using UnityEngine;

namespace Sealbound.EditorTools
{
    /// <summary>
    /// Import settings for the free art in Assets/Resources/Art, applied automatically
    /// whenever a model or texture there is imported, so nobody has to click through Unity's import screens.
    /// </summary>
    public class ArtImportRules : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Resources/Art/";

        // Bump this whenever a rule changes so Unity re-imports the art with the new rules.
        public override uint GetVersion() => 3;

        static bool IsArt(string path) => path.Replace('\\', '/').StartsWith(ArtRoot);
        static bool IsRigged(string path) => path.Contains("/Characters/") || path.Contains("/Animations/");

        void OnPreprocessModel()
        {
            if (!IsArt(assetPath)) return;
            var m = (ModelImporter)assetImporter;

            m.isReadable = true;            // needed for rock colliders and for combining scenery meshes
            m.preserveHierarchy = true;     // keeps character and animation bone paths identical
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            if (IsRigged(assetPath))
            {
                // Every KayKit adventurer shares the same "Rig_Medium" skeleton, so a generic rig
                // lets the Knight, the Mage and the goblins all use the same animation files.
                m.animationType = ModelImporterAnimationType.Generic;
                m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                m.importAnimation = assetPath.Contains("/Animations/");
                m.optimizeGameObjects = false;
            }
            else
            {
                m.animationType = ModelImporterAnimationType.None;
                m.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!IsArt(assetPath) || !assetPath.Contains("/Animations/")) return;
            var m = (ModelImporter)assetImporter;
            var clips = m.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                c.loopTime = IsLooping(c.name);
                c.loopPose = false;
            }
            m.clipAnimations = clips;
        }

        static bool IsLooping(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            return n.Contains("idle") || n.Contains("walking") || n.Contains("running");
        }

        void OnPreprocessTexture()
        {
            if (!IsArt(assetPath)) return;
            var t = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Adventurers/Textures/"))
            {
                // Character colour sheets: kept readable and uncompressed so the game can
                // repaint skin (green goblins) from the same sheet at start-up.
                t.isReadable = true;
                t.textureCompression = TextureImporterCompression.Uncompressed;
            }
            t.mipmapEnabled = true;
            t.filterMode = FilterMode.Bilinear;
        }
    }
}
