using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Neonflow.Editor
{
    public static class CharacterDelivery
    {
        public static void Review() { Export(); CharacterChecks.Begin(); }
        [Serializable] public class Node {public string name;public int parent;public Vector3 position,scale;public Quaternion rotation;}
        [Serializable] public class Part {public int[] indices;}
        [Serializable] public class Pose {public string name;public Vector3[] positions;public Quaternion[] rotations;}
        [Serializable] public class Source {public string name;public Vector3[] vertices,normals;public Vector2[] uv;public int[] joints;public float[] inverseBindMatrices;public Node[] nodes;public Part[] parts;public Pose[] poses;}
        [MenuItem("Neonflow/Export character delivery")]
        public static void Export()
        {
            CharacterModelBuilder.Build();
            CharacterChecks.DataAndModels();
            Directory.CreateDirectory("Models");Directory.CreateDirectory("TestResults/Characters");
            var catalog=JsonUtility.FromJson<MovementCatalogData>(Resources.Load<TextAsset>("Data/Movements_catalog").text).poses;
            foreach(string id in new[]{"light","solid"})
            {
                var model=UnityEngine.Object.Instantiate(Resources.Load<RobotRig>("Characters/"+id));
                model.name=id;
                var skin=model.GetComponent<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;var bones=skin.bones;
                var source=new Source{name=id,vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,joints=mesh.boneWeights.Select(w=>w.boneIndex0).ToArray(),parts=Enumerable.Range(0,4).Select(i=>new Part{indices=mesh.GetTriangles(i)}).ToArray(),nodes=bones.Select(t=>new Node{name=t.name,parent=Array.IndexOf(bones,t.parent),position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray()};
                var matrices=new List<float>();foreach(var m in mesh.bindposes)for(int col=0;col<4;col++)for(int row=0;row<4;row++)matrices.Add(m[row,col]);source.inverseBindMatrices=matrices.ToArray();
                var poses=new List<Pose>();
                string clipFolder="Assets/Neonflow/CharacterTests/"+id;Directory.CreateDirectory(clipFolder);AssetDatabase.Refresh();
                void SavePose(string name)
                {
                    poses.Add(new Pose{name=name,positions=bones.Select(t=>t.localPosition).ToArray(),rotations=bones.Select(t=>t.localRotation).ToArray()});
                    var clip=new AnimationClip{name=id+"_"+name,frameRate=30};
                    for(int i=1;i<bones.Length;i++)
                    {
                        string path=AnimationUtility.CalculateTransformPath(bones[i],model.transform);var p=bones[i].localPosition;var q=bones[i].localRotation;
                        string[] channels={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                        float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                        for(int c=0;c<channels.Length;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),channels[c]),AnimationCurve.Constant(0,1,values[c]));
                    }
                    clip.EnsureQuaternionContinuity();string assetPath=clipFolder+"/"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if(old){EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,assetPath);
                }
                model.Ready();SavePose("Ready_A");
                var camera=new GameObject("Model review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.22f;camera.backgroundColor=new Color(.84f,.81f,.86f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.cullingMask=1<<30;camera.enabled=false;
                foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                // Editor batch rendering can reuse the GPU skin from the preceding frame.
                // Bake the current pose on CPU for each review tile, using the actual weights.
                var frozen = new GameObject("Current pose render",typeof(MeshFilter),typeof(MeshRenderer));
                frozen.transform.SetParent(model.transform,false);frozen.layer=30;
                frozen.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                var baked=new Mesh();frozen.GetComponent<MeshFilter>().sharedMesh=baked;skin.enabled=false;
                var light=new GameObject("Model review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(30,145,0);light.cullingMask=1<<30;
                RenderSettings.ambientLight=new Color(.65f,.63f,.67f);
                Texture2D Frame(Vector3 focus,Vector3 direction,float size,int width=384,int height=448)
                {
                    skin.BakeMesh(baked);baked.RecalculateBounds();
                    camera.orthographicSize=size;camera.transform.position=focus+direction.normalized*4;camera.transform.LookAt(focus);
                    var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);return texture;
                }
                void Sheet(string name,List<Texture2D> tiles,int columns)
                {
                    int w=tiles[0].width,h=tiles[0].height,rows=(tiles.Count+columns-1)/columns;var sheet=new Texture2D(w*columns,h*rows,TextureFormat.RGB24,false);
                    for(int i=0;i<tiles.Count;i++){sheet.SetPixels(i%columns*w,(rows-1-i/columns)*h,w,h,tiles[i].GetPixels());UnityEngine.Object.DestroyImmediate(tiles[i]);}
                    sheet.Apply();File.WriteAllBytes("TestResults/Characters/"+id+"-"+name+".png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
                }
                var views=new List<Texture2D>();foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.right,new Vector3(-.6f,.12f,1)})views.Add(Frame(new Vector3(0,.95f,0),direction,1.03f));Sheet("views-front-back-side-quarter",views,4);
                var body=new List<Texture2D>();foreach(var move in catalog.Where(m=>m.category=="Poses")){model.Pose(PoseEvaluator.Template(move.id),"open","open",move.id);SavePose("Pose_"+move.id);body.Add(Frame(new Vector3(0,1.13f,0),Vector3.forward,1.23f));}Sheet("poses-catalog-order",body,4);
                var hands=new List<Texture2D>();foreach(var move in catalog.Where(m=>m.category=="Hands")){model.Pose(PoseEvaluator.Template("elbows_up"),move.id,move.id,"elbows_up");SavePose("Hand_"+move.id);var hand=model.Bone(HumanBodyBones.RightHand);hands.Add(Frame(hand.position+new Vector3(0,.06f,.02f),new Vector3(.12f,.02f,1),.165f,320,320));}Sheet("hands-catalog-order",hands,5);
                source.poses=poses.ToArray();File.WriteAllText("Models/"+id+".mesh.json",JsonUtility.ToJson(source));
                UnityEngine.Object.DestroyImmediate(model.gameObject);UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            File.WriteAllText("Models/MATERIALS.txt","Ivory #FFF2E1; Plum #250733; Pink #FF008C; Cyan #00E8F2. Satin plastic: metallic 0.08, smoothness 0.38. Only Cyan emits. Solid-color materials deliberately need no bitmap textures.\n");
            Debug.Log("CHARACTER_DELIVERY_EXPORTED: two meshes, 46 nonempty pose clips, model/hand/body review sheets.");
        }
    }
}
