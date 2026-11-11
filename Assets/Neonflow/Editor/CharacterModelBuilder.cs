using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Neonflow.Editor
{
    // Editable source for two real, rigid-skinned mechanical characters. Units are metres.
    public static class CharacterModelBuilder
    {
        const string Folder = "Assets/Neonflow/Resources/Characters";
        const int Ivory = 0, Plum = 1, Pink = 2, Cyan = 3;
        [MenuItem("Neonflow/Build character models")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var colors = new[] { "#FFF2E1", "#250733", "#FF008C", "#00E8F2" };
            var names = new[] { "Ivory", "Plum", "Pink", "Cyan" };
            var materials = new Material[4];
            for (int i = 0; i < materials.Length; i++)
            {
                string path = Folder + "/" + names[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool fresh = !m;
                if (fresh) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ColorUtility.TryParseHtmlString(colors[i], out var color);
                m.name = names[i]; m.color = color;
                m.SetFloat("_Metallic", .08f); m.SetFloat("_Smoothness", .38f);
                m.SetColor("_EmissionColor", i == Cyan ? color * .65f : Color.black);
                if (i == Cyan) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
                if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                materials[i] = m;
            }
            foreach (bool solid in new[] { false, true }) Create(solid, materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NEONFLOW CHARACTER MODELS: built Light and Solid, skinned meshes and Humanoid avatars");
        }

        static void Create(bool solid, Material[] materials)
        {
            string id = solid ? "solid" : "light";
            var go = new GameObject(solid ? "Solid Robot" : "Light Robot");
            var rig = go.AddComponent<RobotRig>(); rig.characterId = id;
            rig.shoulderHeight = solid ? 1.26f : 1.43f;
            rig.poseScale = solid ? .44f : .48f;
            var all = new List<Transform> { go.transform };
            Transform Bone(HumanBodyBones name, Transform parent, Vector3 position)
            {
                var t = new GameObject(name.ToString()).transform;
                t.SetParent(parent, false); t.position = position;
                rig.bones[(int)name] = t; all.Add(t); return t;
            }
            var hips = Bone(HumanBodyBones.Hips, go.transform, new Vector3(0, solid ? .83f : .91f, 0));
            var spine = Bone(HumanBodyBones.Spine, hips, new Vector3(0, solid ? .98f : 1.04f, 0));
            var chest = Bone(HumanBodyBones.Chest, spine, new Vector3(0, solid ? 1.13f : 1.25f, 0));
            var upperChest = Bone(HumanBodyBones.UpperChest, chest, new Vector3(0, solid ? 1.23f : 1.40f, 0));
            var neck = Bone(HumanBodyBones.Neck, upperChest, new Vector3(0, solid ? 1.38f : 1.55f, 0));
            var head = Bone(HumanBodyBones.Head, neck, new Vector3(0, solid ? 1.52f : 1.67f, 0));
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0; float sign = left ? -1 : 1;
                var shoulder = Bone(left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder, upperChest, new Vector3(sign * (solid ? .19f : .15f), rig.shoulderHeight, 0));
                var arm = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, shoulder, new Vector3(sign * (solid ? .29f : .23f), rig.shoulderHeight, 0));
                var elbow = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, arm, new Vector3(sign * (solid ? .58f : .54f), rig.shoulderHeight, 0));
                var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, elbow, new Vector3(sign * .84f, rig.shoulderHeight, 0));
                int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                float handScale = solid ? 1.12f : 1;
                for (int f = 0; f < 5; f++)
                {
                    Transform parent = hand;
                    for (int k = 0; k < 3; k++)
                    {
                        float length = DigitLength(f, k - 1) * handScale;
                        Vector3 local = k == 0 ? new Vector3(sign * (f == 0 ? .045f : .072f) * handScale, (f == 0 ? .055f : .035f - (f - 1) * .024f) * handScale, 0) : new Vector3(sign * length, 0, 0);
                        var t = Bone((HumanBodyBones)(first + f * 3 + k), parent, parent.TransformPoint(local));
                        if (f == 0 && k == 0) t.localRotation = Quaternion.Euler(0, 0, sign * 48);
                        parent = t;
                    }
                }
                var thigh = Bone(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, hips, new Vector3(sign * (solid ? .115f : .10f), hips.position.y, 0));
                var knee = Bone(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, thigh, new Vector3(thigh.position.x, solid ? .44f : .49f, .012f));
                var foot = Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, knee, new Vector3(thigh.position.x, .075f, 0));
                Bone(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes, foot, new Vector3(thigh.position.x, .035f, .13f));
            }
            var mesh = new ArmorMesh(go.transform, all);
            void Box(Transform bone, Vector3 center, Vector3 size, int mat, float cut = .18f, float rotation = 0) => mesh.Poly(bone, center, Outline(size.x, size.y, cut), size.z, mat, Mathf.Min(size.z * .18f, .014f), Quaternion.Euler(0, 0, rotation));
            void Bar(Transform bone, Vector3 start, Vector3 end, float width, float depth, int mat, float taper = 1)
            {
                Vector3 delta = end - start;
                float len = delta.magnitude;
                var shape = new[] { new Vector2(-len / 2, -width / 2), new Vector2(len / 2, -width * taper / 2), new Vector2(len / 2, width * taper / 2), new Vector2(-len / 2, width / 2) };
                mesh.Poly(bone, (start + end) / 2, shape, depth, mat, Mathf.Min(depth * .17f, .012f), Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
            }
            Box(hips, new Vector3(0, .015f, 0), new Vector3(solid ? .29f : .245f, .13f, .19f), Plum);
            Box(spine, new Vector3(0, .005f, 0), new Vector3(solid ? .28f : .205f, .19f, .18f), solid ? Plum : Ivory);
            Box(spine, new Vector3(0, -.037f, .098f), new Vector3(solid ? .285f : .19f, .026f, .014f), Cyan, .04f);
            var torso = solid ? new[] { new Vector2(-.17f,-.10f), new Vector2(.17f,-.10f), new Vector2(.245f,.12f), new Vector2(.19f,.19f), new Vector2(-.19f,.19f), new Vector2(-.245f,.12f) } : new[] { new Vector2(-.125f,-.14f), new Vector2(.125f,-.14f), new Vector2(.205f,.11f), new Vector2(.145f,.19f), new Vector2(-.145f,.19f), new Vector2(-.205f,.11f) };
            mesh.Poly(chest, Vector3.zero, torso, solid ? .235f : .205f, solid ? Plum : Ivory, .025f, Quaternion.identity);
            for (int facing = -1; facing <= 1; facing += 2)
            {
                var stripe = solid ? new[] { new Vector2(-.22f,.115f), new Vector2(-.11f,.005f), new Vector2(0,-.032f), new Vector2(.11f,.005f), new Vector2(.22f,.115f), new Vector2(.17f,.17f), new Vector2(-.17f,.17f) } : new[] { new Vector2(-.178f,.11f), new Vector2(.16f,-.075f), new Vector2(.185f,.01f), new Vector2(-.145f,.185f) };
                mesh.Poly(chest, new Vector3(0,0,facing * (solid ? .12f : .105f)), stripe, .006f, Pink, .001f, Quaternion.identity);
            }
            if (solid)
                for (int s = -1; s <= 1; s += 2) Box(chest, new Vector3(s * .176f,-.025f,.123f), new Vector3(.065f,.075f,.018f), Ivory, .2f, -s * 16);
            Box(neck, new Vector3(0,solid ? -.015f : -.04f,0), new Vector3(.09f,solid ? .10f : .16f,.09f), Plum);
            var helmet = solid ? Outline(.335f,.265f,.18f) : new[] { new Vector2(-.07f,-.125f), new Vector2(.07f,-.125f), new Vector2(.125f,-.025f), new Vector2(.117f,.095f), new Vector2(.07f,.13f), new Vector2(-.07f,.13f), new Vector2(-.117f,.095f), new Vector2(-.125f,-.025f) };
            mesh.Poly(head, Vector3.zero, helmet, solid ? .23f : .21f, Ivory, .022f, Quaternion.identity);
            Box(head, new Vector3(0,.018f,solid ? .121f : .111f), new Vector3(solid ? .282f : .215f,solid ? .115f : .102f,.022f), Plum, .14f);
            if (solid)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(head, new Vector3(s*.068f,.025f,.137f),new Vector3(.075f,.021f,.008f),Cyan,.08f);
                    Box(head, new Vector3(s*.168f,.033f,-.018f),new Vector3(.012f,.14f,.16f),Pink,.2f);
                }
                Box(head,new Vector3(0,.02f,-.12f),new Vector3(.265f,.20f,.012f),Plum);
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    float x0 = -.094f + i * .031333f, x1 = x0 + .032f;
                    Bar(head,new Vector3(x0,.012f + .32f*x0*x0,.127f),new Vector3(x1,.012f + .32f*x1*x1,.127f),.015f,.009f,Cyan);
                }
            }
            Box(head, new Vector3(0,.126f,-.012f),new Vector3(solid ? .22f : .055f,.014f,.19f),Pink,.06f);
            Box(head, new Vector3(0,.04f,-.112f),new Vector3(solid ? .21f : .055f,.17f,.009f),Pink,.06f);
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0; float sign = left ? -1 : 1;
                var shoulder = rig.Bone(left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
                var arm = rig.Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var elbow = rig.Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = rig.Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Box(shoulder, new Vector3(sign*.075f,.014f,0),new Vector3(solid ? .19f : .14f,solid ? .17f : .125f,solid ? .21f : .16f),solid ? Pink : Ivory,.2f);
                Box(arm,Vector3.zero,new Vector3(.075f,.09f,.09f),Plum);
                float upperLength = elbow.localPosition.magnitude, lowerLength = hand.localPosition.magnitude;
                Bar(arm,new Vector3(sign*.065f,0,0),new Vector3(sign*(upperLength-.045f),0,0),solid ? .10f : .085f,solid ? .12f : .10f,solid ? Plum : Ivory,.85f);
                Box(elbow,Vector3.zero,new Vector3(.07f,.086f,.09f),Plum);
                Bar(elbow,new Vector3(sign*.045f,0,0),new Vector3(sign*(lowerLength-.035f),0,0),solid ? .15f : .108f,solid ? .155f : .112f,Ivory,.73f);
                Bar(elbow,new Vector3(sign*.07f,.003f,solid ? .081f : .061f),new Vector3(sign*(lowerLength-.06f),.003f,solid ? .081f : .061f),.022f,.008f,Cyan,.7f);
                Box(hand,Vector3.zero,new Vector3(.04f,.06f,.055f),Plum);
                float handScale = solid ? 1.12f : 1;
                Box(hand,new Vector3(sign*.041f,0,0)*handScale,new Vector3(.082f,.104f,.046f)*handScale,Ivory,.16f);
                Box(hand,new Vector3(sign*.038f,0,-.025f)*handScale,new Vector3(.045f,.055f,.008f)*handScale,solid ? Pink : Plum,.1f);
                int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                for (int f = 0; f < 5; f++)
                    for (int k = 0; k < 3; k++)
                    {
                        var digit = rig.bones[first+f*3+k];
                        float length = DigitLength(f,k)*handScale;
                        Box(digit,new Vector3(sign*length*.5f,0,0),new Vector3(length-.004f,(f==0 ? .023f:.019f)*handScale,.025f*handScale),Ivory,.16f);
                        Box(digit,Vector3.zero,new Vector3(.008f,.017f,.02f)*handScale,Plum,.18f);
                    }
                var thigh = rig.Bone(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var knee = rig.Bone(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                var foot = rig.Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                float thighLength = thigh.position.y-knee.position.y, shinLength=knee.position.y-foot.position.y;
                Bar(thigh,new Vector3(0,-.07f,0),new Vector3(0,-thighLength+.045f,0),solid ? .17f:.125f,solid ? .19f:.14f,solid ? Plum:Ivory,.8f);
                Box(thigh,new Vector3(sign*(solid ? .066f:.049f),-thighLength*.46f,.063f),new Vector3(.038f,thighLength*.54f,.012f),Pink,.14f,-sign*5);
                Box(knee,Vector3.zero,new Vector3(solid ? .135f:.095f,.085f,.11f),Plum);
                if(solid) Box(knee,new Vector3(0,0,.07f),new Vector3(.14f,.115f,.04f),Ivory,.24f);
                Bar(knee,new Vector3(0,-.065f,0),new Vector3(0,-shinLength+.085f,0),solid ? .15f:.105f,solid ? .16f:.125f,solid ? Plum:Ivory,.9f);
                Box(foot,new Vector3(0,.065f,0),new Vector3(solid ? .09f:.07f,.10f,.08f),Plum);
                Box(foot,new Vector3(0,-.005f,.065f),new Vector3(solid ? .18f:.14f,.115f,solid ? .30f:.265f),Ivory,.22f);
                Box(foot,new Vector3(0,-.057f,.065f),new Vector3(solid ? .185f:.145f,.036f,solid ? .305f:.27f),Plum,.12f);
                Box(foot,new Vector3(0,.054f,.045f),new Vector3(solid ? .10f:.075f,.012f,.10f),Pink,.12f);
                Box(foot,new Vector3(0,-.022f,solid ? .216f:.199f),new Vector3(solid ? .125f:.09f,.025f,.01f),Cyan,.08f);
            }
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMaterials=materials; renderer.bones=all.ToArray(); renderer.rootBone=hips;
            renderer.quality=SkinQuality.Bone1; renderer.updateWhenOffscreen=true;
            var modelMesh=mesh.Finish(id); Save(modelMesh,Folder+"/"+id+"_mesh.asset");
            renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+id+"_mesh.asset");
            renderer.localBounds=new Bounds(new Vector3(0,1.12f,0),new Vector3(2.7f,2.55f,1.2f));
            var humans=new List<HumanBone>();
            for(int i=0;i<rig.bones.Length;i++) if(rig.bones[i]) humans.Add(new HumanBone {humanName=HumanTrait.BoneName[i],boneName=rig.bones[i].name,limit=new HumanLimit{useDefaultValues=true}});
            var avatar=AvatarBuilder.BuildHumanAvatar(go,new HumanDescription {human=humans.ToArray(),skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,hasTranslationDoF=true});
            avatar.name=id+" Humanoid";
            if(!avatar.isValid||!avatar.isHuman) throw new InvalidOperationException("Invalid Humanoid for "+id);
            Save(avatar,Folder+"/"+id+"_avatar.asset");
            var animator=go.AddComponent<Animator>(); animator.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Folder+"/"+id+"_avatar.asset"); animator.applyRootMotion=false; animator.enabled=false;
            rig.Ready();
            PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+id+".prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static float DigitLength(int f,int k)
        {
            if(f==0) return k==0?.026f:k==1?.031f:.027f;
            float factor=f==2?1.12f:f==4?.82f:1;
            return (k==0?.042f:k==1?.027f:.022f)*factor;
        }
        static Vector2[] Outline(float w,float h,float cut)
        {
            float x=w/2,y=h/2,b=Mathf.Min(w,h)*cut;
            return new[]{new Vector2(-x+b,-y),new Vector2(x-b,-y),new Vector2(x,-y+b),new Vector2(x,y-b),new Vector2(x-b,y),new Vector2(-x+b,y),new Vector2(-x,y-b),new Vector2(-x,-y+b)};
        }
        static void Save(UnityEngine.Object obj,string path)
        {
            var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(existing is Mesh target && obj is Mesh source)
            {
                target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;
                target.normals=source.normals;target.tangents=source.tangents;target.uv=source.uv;
                target.bindposes=source.bindposes;target.boneWeights=source.boneWeights;target.subMeshCount=source.subMeshCount;
                for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i);
                target.bounds=source.bounds;EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(source);
            }
            else if(existing) {EditorUtility.CopySerialized(obj,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(obj);}
            else AssetDatabase.CreateAsset(obj,path);
        }
        sealed class ArmorMesh
        {
            readonly Transform root; readonly List<Transform> bones;
            readonly List<Vector3> vertices=new List<Vector3>(); readonly List<Vector2> uv=new List<Vector2>(); readonly List<BoneWeight> weights=new List<BoneWeight>();
            readonly List<int>[] faces={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            public ArmorMesh(Transform r,List<Transform> b) {root=r;bones=b;}
            public void Poly(Transform bone,Vector3 center,Vector2[] outline,float depth,int mat,float bevel,Quaternion rotation)
            {
                int n=outline.Length, index=bones.IndexOf(bone); var rings=new Vector3[4,n];
                Vector2 centroid=Vector2.zero; foreach(var p in outline) centroid+=p/n;
                float radius=outline.Min(p=>(p-centroid).magnitude); float inset=Mathf.Clamp01(1-bevel/Mathf.Max(.001f,radius));
                for(int r=0;r<4;r++) for(int i=0;i<n;i++) {Vector2 p=centroid+(outline[i]-centroid)*(r==0||r==3?inset:1);float z=r==0?depth/2:r==1?depth/2-bevel:r==2?-depth/2+bevel:-depth/2;rings[r,i]=new Vector3(p.x,p.y,z);}
                Vector3 Convert(Vector3 v)=>root.InverseTransformPoint(bone.TransformPoint(center+rotation*v));
                void Triangle(Vector3 a,Vector3 b,Vector3 c)
                {
                    foreach(var p in new[]{a,b,c}) {faces[mat].Add(vertices.Count);vertices.Add(Convert(p));uv.Add(new Vector2(p.x+.5f,p.y+.5f));weights.Add(new BoneWeight{boneIndex0=index,weight0=1});}
                }
                for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n;
                    Triangle(new Vector3(centroid.x,centroid.y,depth/2),rings[0,i],rings[0,j]);
                    Triangle(new Vector3(centroid.x,centroid.y,-depth/2),rings[3,j],rings[3,i]);
                    for(int r=0;r<3;r++) {Triangle(rings[r,i],rings[r+1,i],rings[r+1,j]);Triangle(rings[r,i],rings[r+1,j],rings[r,j]);}
                }
            }
            public Mesh Finish(string id)
            {
                var mesh=new Mesh{name=id+" beveled armor",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.bindposes=bones.Select(t=>t.worldToLocalMatrix*root.localToWorldMatrix).ToArray();mesh.subMeshCount=4;
                for(int i=0;i<4;i++)mesh.SetTriangles(faces[i],i);
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
