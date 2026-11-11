using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Neonflow.Skies.Editor
{
    public class NeonflowSkyImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Replace("\\","/").Contains("/NeonflowSkies/Panoramas/")) return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;t.textureShape=TextureImporterShape.Texture2D;
            t.mipmapEnabled=false;t.sRGBTexture=true;t.npotScale=TextureImporterNPOTScale.None;
            t.textureCompression=TextureImporterCompression.Uncompressed;
            t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=4096;
        }
    }
    public static class NeonflowSkySetup
    {
        const string Root="Assets/NeonflowSkies/";
        [MenuItem("Tools/Neonflow Skies/Create skybox materials")]
        public static void CreateMaterials()
        {
            var shader=Shader.Find("Neonflow/Synthwave Skybox");
            if(shader==null) throw new InvalidOperationException("Neonflow skybox shader is missing.");
            Directory.CreateDirectory(Root+"Materials");AssetDatabase.Refresh();
            foreach(string id in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"Panoramas"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(id);
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string name=Path.GetFileNameWithoutExtension(path);
                string target=Root+"Materials/"+name+".mat";
                if(File.Exists(target))continue;
                var m=new Material(shader);m.SetTexture("_MainTex",tex);m.SetColor("_Tint",Color.white);m.SetFloat("_Exposure",1);
                m.SetFloat("_Rotation",0);m.SetFloat("_SeamBlend",.045f);m.SetFloat("_PoleBlend",.23f);
                m.SetFloat("_HorizonOffset", name.StartsWith("01_") ? -.09f : name.StartsWith("02_") || name.StartsWith("03_") ? -.045f : -.06f);
                m.SetFloat("_VerticalScale",name.StartsWith("01_") ? 2.0f : 1.15f);
                m.SetFloat("_HorizontalScale",name.StartsWith("01_") ? 3.0f : 1.0f);
                bool grid=name.StartsWith("01_") || name.StartsWith("02_") || name.StartsWith("03_") || name.StartsWith("05_");
                m.SetFloat("_GridEnabled",grid ? 1 : 0);
                m.SetColor("_GridColor",name.StartsWith("02_") ? new Color(.05f,.75f,.9f) : name.StartsWith("05_") ? new Color(.5f,.48f,.9f) : new Color(1,.08f,.55f));
                m.SetColor("_GroundColor",new Color(.015f,.018f,.06f));
                m.SetFloat("_RetroSun",name.StartsWith("01_") ? 1 : 0);
                m.SetFloat("_SunRadius",12);m.SetFloat("_SunElevation",12);
                AssetDatabase.CreateAsset(m,target);
            }
            AssetDatabase.SaveAssets();Debug.Log("Neonflow sky materials are in Assets/NeonflowSkies/Materials. Select a material and use Apply selected skybox.");
        }
        [MenuItem("Tools/Neonflow Skies/Apply selected skybox")]
        public static void ApplySelected()
        {
            var m=Selection.activeObject as Material;
            if(m==null || m.shader.name!="Neonflow/Synthwave Skybox"){Debug.LogWarning("Select a Neonflow sky material in Project first.");return;}
            RenderSettings.skybox=m;
            if(Camera.main!=null){Undo.RecordObject(Camera.main,"Camera skybox");Camera.main.clearFlags=CameraClearFlags.Skybox;}
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
