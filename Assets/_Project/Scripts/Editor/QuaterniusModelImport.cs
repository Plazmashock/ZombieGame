using UnityEditor;
using UnityEngine;

namespace LastCheckout.EditorTools
{
    /// <summary>
    /// Import settings for the downloaded 3D models (Assets/_Project/Resources/Models). Runs by itself
    /// whenever Unity imports one of them, so there is nothing to click.
    ///
    ///   every model:  readable mesh (the game measures the model to turn and size it correctly),
    ///                 no cameras/lights, materials taken from the file
    ///   Zombies/:     keep the skeleton + animations (Walk, Run, Punch, Death...), loop the walking ones
    ///   others:       no animation (they're props)
    ///   Zombie_Atlas: the colour palette texture, kept crisp (no blurry colour bleeding)
    /// </summary>
    public class QuaterniusModelImport : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Resources/Models/";

        bool Mine => assetPath.Replace('\\', '/').StartsWith(Root);
        bool IsZombie => assetPath.Replace('\\', '/').Contains("/Zombies/");

        void OnPreprocessModel()
        {
            if (!Mine) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (IsZombie)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = true;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!Mine || !IsZombie) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name;
                bool loop = n.EndsWith("Walk") || n.EndsWith("Run") || n.EndsWith("Idle") || n.EndsWith("Crawl") || n.EndsWith("Run_Arms");
                clips[i].loopTime = loop;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!Mine) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("Atlas"))
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.isReadable = false;
            }
        }
    }
}
