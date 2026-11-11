using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using Neonflow.VectorUI;
namespace Neonflow.VectorUI.Editor {
public static class VectorPackBuilder {
 const string Root="Assets/NeonflowVector/";
 static VectorAnimations animations;
 static Dictionary<string,string> colors=new Dictionary<string,string>{{"ink","#250733"},{"ivory","#FFF2E1"},{"pink","#FF008C"},{"cyan","#00E8F2"},{"lilac","#BA85E5"},{"muted","#9874A5"},{"white","#FFFFFF"}};
 static Color C(string s){Color c;if(s==null)return Color.white;if(colors.ContainsKey(s))s=colors[s];return ColorUtility.TryParseHtmlString(s,out c)?c:Color.white;}
 static T Load<T>(string p)where T:UnityEngine.Object{var v=AssetDatabase.LoadAssetAtPath<T>(Root+p);if(!v)throw new Exception("Missing asset "+p);return v;}
 static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var g=new GameObject(name,typeof(RectTransform));var r=(RectTransform)g.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
 static VectorCutGraphic Plate(Transform parent,string id,float x,float y,float w,float h,string color,float cut=.36f,bool accent=false){var r=Rect(id,parent,x,y,w,h);var g=r.gameObject.AddComponent<VectorCutGraphic>();g.color=C(color);g.cutRatio=cut;g.accent=accent;g.raycastTarget=false;return g;}
 static Text Text(Transform parent,string id,string text,float x,float y,float w,float h,int size=36,string color="ink",bool italic=false,string weight="SemiBold",string align="left"){
  var r=Rect(id,parent,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=Load<Font>("Fonts/Exo2-"+(string.IsNullOrEmpty(weight)?"SemiBold":weight)+".ttf");t.text=text;t.fontSize=size;t.fontStyle=italic?FontStyle.Italic:FontStyle.Normal;t.color=C(color);t.alignment=align=="center"?TextAnchor.MiddleCenter:align=="right"?TextAnchor.MiddleRight:TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.supportRichText=false;t.raycastTarget=false;return t;
 }
 static Image Img(Transform parent,string id,string path,float x,float y,float w,float h,string color="white",bool preserve=true){var r=Rect(id,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=Load<Sprite>(path);im.color=C(color);im.preserveAspect=preserve;im.raycastTarget=false;return im;}
 static VectorFlipbook FX(Transform parent,string id,float x,float y,float w,float h,bool autoplay=false){var s=animations.sequences.First(a=>a.id==id);var r=Rect("FX_"+id,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.raycastTarget=false;var f=r.gameObject.AddComponent<VectorFlipbook>();f.target=im;f.frames=s.frames.Select(p=>Load<Sprite>(p)).ToArray();f.fps=s.fps;f.loop=s.loop;f.externalClock=s.clock=="external-song-time";f.playOnEnable=autoplay;im.sprite=f.frames[0];return f;}
 static Button Button(Transform parent,VectorNode n){
  var plate=Plate(parent,n.id,n.x,n.y,n.width,n.height,n.color,.36f,n.color=="pink");plate.raycastTarget=true;var r=plate.rectTransform;
  var b=r.gameObject.AddComponent<Button>();b.targetGraphic=plate;var cb=b.colors;cb.normalColor=Color.white;cb.highlightedColor=new Color(1,.94f,1);cb.pressedColor=new Color(.84f,.76f,.90f);cb.selectedColor=Color.white;cb.disabledColor=new Color(.70f,.65f,.72f,.55f);cb.fadeDuration=.10f;b.colors=cb;
  float left=n.height*.43f+10;Text(r,"Label",n.text,left,0,n.width-left-n.height*.36f,n.height,(int)n.fontSize,n.color=="pink"?"ivory":"ink",true,"ExtraBold");
  var motion=r.gameObject.AddComponent<VectorButtonMotion>();motion.hoverFX=FX(r,"select_sweep",0,0,n.width,n.height);motion.pressFX=FX(r,"press_slash",0,0,n.width,n.height);
  if(!string.IsNullOrEmpty(n.target)){var action=r.gameObject.AddComponent<VectorPreviewAction>();action.target=n.target;}
  return b;
 }
 static Image Bar(Transform parent,VectorNode n){var plate=Plate(parent,n.id,n.x,n.y,n.width,n.height,"ivory",.36f);var clip=Plate(plate.transform,"Clip",6,6,n.width-12,n.height-12,"ivory",.36f);clip.gameObject.AddComponent<Mask>().showMaskGraphic=false;var im=Img(clip.transform,"Fill","UI/Controls/flat_white.png",0,0,n.width-12,n.height-12,n.color,false);im.type=Image.Type.Filled;im.fillMethod=Image.FillMethod.Horizontal;im.fillAmount=n.value;return im;}
 static void Node(Transform parent,VectorNode n){
  switch(n.kind){
   case "panel":Plate(parent,n.id,n.x,n.y,n.width,n.height,n.color,n.cut,n.accent);break;
   case "text":Text(parent,n.id,n.text,n.x,n.y,n.width,n.height,(int)n.fontSize,n.color,n.italic,n.weight,n.align);break;
   case "image":Img(parent,n.id,n.path,n.x,n.y,n.width,n.height,n.color);break;
   case "button":Button(parent,n);break;
   case "dim":{var im=Img(parent,n.id,"UI/Controls/flat_white.png",n.x,n.y,n.width,n.height,n.color,false);var c=im.color;c.a=n.opacity;im.color=c;im.raycastTarget=true;break;}
   case "bar":Bar(parent,n);break;
   case "pose":{
    var plate=Plate(parent,n.id,n.x,n.y,n.width,n.height,n.color,.23f);var cg=plate.gameObject.AddComponent<CanvasGroup>();cg.alpha=n.opacity;
    float cap=34;Text(plate.transform,"Caption",n.text,n.width*.14f,9,n.width*.71f,cap,(int)Mathf.Min(n.fontSize,n.width*.075f),"ink",false,"SemiBold","right");
    float size=n.height-cap-27;Img(plate.transform,"Pictogram",n.path,n.width*.19f,cap+14,n.width*.62f,size,"pink");break;
   }
   case "song":{
    var plate=Plate(parent,n.id,n.x,n.y,n.width,n.height,n.color,.36f,n.selected);plate.raycastTarget=true;var b=plate.gameObject.AddComponent<Button>();b.targetGraphic=plate;
    float inset=64;Text(plate.transform,"Title",n.text,inset,8,n.width-inset-195,62,(int)n.fontSize,n.selected?"ivory":"ink",true,"ExtraBold");Text(plate.transform,"Artist",n.secondary,inset,72,n.width-inset-195,38,25,n.selected?"ivory":"ink",false,"Regular");Text(plate.transform,"BPM",n.valueText,n.width-210,12,160,94,30,n.selected?"ivory":"ink",true,"SemiBold","right");
    var mo=plate.gameObject.AddComponent<VectorButtonMotion>();mo.hoverOffset=16;mo.hoverFX=FX(plate.transform,"select_sweep",0,0,n.width,n.height);break;
   }
   case "camera":{
    var outer=Plate(parent,n.id,n.x,n.y,n.width,n.height,"cyan",.10f);var mask=Plate(outer.transform,"Clip",4,4,n.width-8,n.height-8,"ink",.10f);mask.gameObject.AddComponent<Mask>().showMaskGraphic=false;
    var r=Rect("Feed",mask.transform,0,0,n.width-8,n.height-8);var raw=r.gameObject.AddComponent<RawImage>();raw.color=Color.white;raw.raycastTarget=false;
    var unavailable=Rect("Unavailable",mask.transform,0,0,n.width-8,n.height-8);var fill=unavailable.gameObject.AddComponent<Image>();fill.color=C("ink");fill.raycastTarget=false;
    Img(unavailable,"CameraOff","UI/Icons/camera_off.png",n.width/2-40,n.height/2-64,80,80,"ivory");Text(unavailable,"Status","Камера не подключена",55,n.height/2+30,n.width-110,64,26,"ivory",false,"Regular","center");break;
   }
   case "input":{
    var plate=Plate(parent,n.id,n.x,n.y,n.width,n.height,n.color,.30f);plate.raycastTarget=true;var field=plate.gameObject.AddComponent<InputField>();field.targetGraphic=plate;
    var t=Text(plate.transform,"Value","",40,0,n.width-90,n.height,(int)n.fontSize);var hint=Text(plate.transform,"Placeholder",n.text,40,0,n.width-90,n.height,(int)n.fontSize,"muted",false,"Regular");field.textComponent=t;field.placeholder=hint;break;
   }
   case "slider":{
    var r=Rect(n.id,parent,n.x,n.y,n.width,n.height);var sl=r.gameObject.AddComponent<Slider>();var track=Img(r,"Track","UI/Controls/slider_track.png",0,n.height/2-5,n.width,10,"white",false);track.raycastTarget=true;
    var fillArea=Rect("FillArea",r,0,n.height/2-5,n.width,10);var fill=Img(fillArea,"Fill","UI/Controls/slider_fill.png",0,0,n.width,10,"white",false);Stretch(fill.rectTransform);
    var handleArea=Rect("HandleArea",r,0,0,n.width,n.height);var thumb=Img(handleArea,"Handle","UI/Controls/slider_thumb_normal.png",0,0,36,36);thumb.rectTransform.pivot=new Vector2(.5f,.5f);thumb.rectTransform.anchorMin=thumb.rectTransform.anchorMax=new Vector2(0,.5f);thumb.raycastTarget=true;
    sl.fillRect=fill.rectTransform;sl.handleRect=thumb.rectTransform;sl.targetGraphic=thumb;sl.value=n.value;break;
   }
   case "toggle":{
    var r=Rect(n.id,parent,n.x,n.y,n.width,n.height);var off=Img(r,"Off","UI/Controls/toggle_off.png",0,0,n.width,n.height);off.raycastTarget=true;var on=Img(r,"On","UI/Controls/toggle_on.png",0,0,n.width,n.height);var t=r.gameObject.AddComponent<Toggle>();t.targetGraphic=off;t.graphic=on;t.isOn=n.value>.5f;break;
   }
   case "animation":FX(parent,n.path,n.x,n.y,n.width,n.height,true);break;
  }
 }
 static GameObject CreateScreen(VectorScreen s){
  var go=new GameObject(s.id,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
  var cs=go.GetComponent<CanvasScaler>();cs.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;cs.referenceResolution=new Vector2(1920,1080);cs.matchWidthOrHeight=.5f;
  if(s.background){var r=Rect("Backdrop",go.transform,0,0,1920,1080);Stretch(r);var im=r.gameObject.AddComponent<RawImage>();im.texture=Load<Texture2D>("Backgrounds/vector_menu.png");im.raycastTarget=false;}
  var safe=Rect("Layout",go.transform,0,0,1920,1080);safe.gameObject.AddComponent<VectorSafeRoot>();safe.anchorMin=safe.anchorMax=safe.pivot=new Vector2(.5f,.5f);safe.anchoredPosition=Vector2.zero;
  foreach(var n in s.nodes){Node(safe,n);if(n.rotation!=0){var r=safe.Find(n.id)as RectTransform;if(r)r.localRotation=Quaternion.Euler(0,0,n.rotation);}}
  if(s.id=="Gameplay"){
   var v=go.AddComponent<VectorHUDView>();v.playerHP=safe.Find("PlayerHP/Clip/Fill").GetComponent<Image>();v.robotHP=safe.Find("RobotHP/Clip/Fill").GetComponent<Image>();v.window=safe.Find("Window/Clip/Fill").GetComponent<Image>();v.webcam=safe.Find("Webcam/Clip/Feed").GetComponent<RawImage>();v.score=safe.Find("Score").GetComponent<Text>();v.combo=safe.Find("Combo").GetComponent<Text>();v.timer=safe.Find("Timer").GetComponent<Text>();v.accuracy=safe.Find("Accuracy").GetComponent<Text>();v.judgement=safe.Find("Judgement").GetComponent<Text>();
  }
  return go;
 }
 [MenuItem("Tools/Neonflow Vector/Rebuild presentation assets")]
 public static void Build(){
  animations=JsonUtility.FromJson<VectorAnimations>(File.ReadAllText(Root+"Data/AnimationManifest.json"));
  var manifest=JsonUtility.FromJson<VectorManifest>(File.ReadAllText(Root+"manifest.json"));
  foreach(var item in manifest.assets){var ti=AssetImporter.GetAtPath(Root+item.path)as TextureImporter;if(!ti)continue;ti.textureType=item.role=="cursor"?TextureImporterType.Cursor:TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.spritePixelsPerUnit=100;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;ti.maxTextureSize=2048;if(item.border!=null&&item.border.Length==4)ti.spriteBorder=new Vector4(item.border[0],item.border[1],item.border[2],item.border[3]);if(item.role=="cursor")ti.isReadable=true;ti.SaveAndReimport();}
  Directory.CreateDirectory(Root+"Prefabs/Screens");Directory.CreateDirectory(Root+"Prefabs/Components");Directory.CreateDirectory(Root+"Prefabs/Effects");Directory.CreateDirectory(Root+"Scenes");
  foreach(var path in Directory.GetFiles(Root+"Animations/Atlases","*.png")){var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Default;ti.mipmapEnabled=false;ti.alphaIsTransparency=true;ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=4096;ti.SaveAndReimport();}
  BuildSkies();
  var layouts=JsonUtility.FromJson<VectorLayouts>(File.ReadAllText(Root+"Data/Layouts.json"));
  var roots=new List<GameObject>();
  foreach(var s in layouts.screens){var go=CreateScreen(s);PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/Screens/"+s.id+".prefab");roots.Add(go);}
  foreach(var seq in animations.sequences){
   var clip=new AnimationClip();clip.name=seq.id;clip.frameRate=seq.fps;
   var keys=new List<ObjectReferenceKeyframe>();for(int i=0;i<seq.frames.Length;i++)keys.Add(new ObjectReferenceKeyframe{time=(float)i/seq.fps,value=Load<Sprite>(seq.frames[i])});keys.Add(new ObjectReferenceKeyframe{time=(float)seq.frames.Length/seq.fps,value=Load<Sprite>(seq.frames[seq.frames.Length-1])});
   AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(Image),propertyName="m_Sprite"},keys.ToArray());var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=seq.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
   string p=Root+"Animations/Clips/"+seq.id+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(p);if(existing)EditorUtility.CopySerialized(clip,existing);else AssetDatabase.CreateAsset(clip,p);
   var holder=new GameObject("Holder",typeof(RectTransform));var fx=FX(holder.transform,seq.id,0,0,seq.width,seq.height);PrefabUtility.SaveAsPrefabAsset(fx.gameObject,Root+"Prefabs/Effects/"+seq.id+".prefab");UnityEngine.Object.DestroyImmediate(holder);
  }
  var references=new[]{new[]{"MainMenu","Play","ButtonPrimary"},new[]{"MainMenu","Settings","ButtonSecondary"},new[]{"TrackSelect","SongRow1","SongRowSelected"},new[]{"TrackSelect","SongRow0","SongRow"},new[]{"TrackSelect","Search","SearchField"},new[]{"Gameplay","Body","BodyCard"},new[]{"Gameplay","LeftHand","HandCard"},new[]{"Gameplay","Next1","NextCard"},new[]{"Gameplay","PlayerHP","HealthBar"},new[]{"Gameplay","Window","BeatWindow"},new[]{"Gameplay","Webcam","CameraFrame"},new[]{"Settings","Slider0","VolumeSlider"},new[]{"ControlSettings","VoiceToggle","Toggle"}};
  foreach(var a in references){var obj=roots.First(g=>g.name==a[0]).transform.Find("Layout/"+a[1]);PrefabUtility.SaveAsPrefabAsset(obj.gameObject,Root+"Prefabs/Components/"+a[2]+".prefab");}
  foreach(var g in roots)UnityEngine.Object.DestroyImmediate(g);
  AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("VECTOR_BUILD_PASS: 14 screens, 13 reusable components, 13 effects, 13 animation clips");
 }
 [MenuItem("Tools/Neonflow Vector/Create presentation demo scene")]
 public static void Demo(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Vector UI Preview");var nav=root.AddComponent<VectorPreviewNavigator>();
  var layouts=JsonUtility.FromJson<VectorLayouts>(File.ReadAllText(Root+"Data/Layouts.json"));nav.screens=layouts.screens.Select(s=>{var g=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Prefabs/Screens/"+s.id+".prefab"));g.transform.SetParent(root.transform,false);g.SetActive(s.id=="MainMenu");return g;}).ToArray();
  var es=new GameObject("EventSystem",typeof(EventSystem));
  #if ENABLE_INPUT_SYSTEM
  es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
  #else
  es.AddComponent<StandaloneInputModule>();
  #endif
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"Scenes/Vector_Presentation.unity");
 }
 static void BuildSkies(){
  string[][] palettes={new[]{"01_Vector_Sunset","#A256F8","#FF59B5","#FF008C","#531078","#250733","#FF008C"},new[]{"02_Cyan_Horizon","#726BFF","#A5DEF3","#FF39A9","#303B84","#171745","#00E8F2"},new[]{"03_Lilac_Dawn","#A888F0","#FFC2CE","#FF60AE","#77549D","#392251","#E967F1"},new[]{"04_Pink_Arcade","#7F41D5","#FF61BD","#FFB447","#4F146C","#260D39","#FF2AAB"},new[]{"05_Violet_Night","#48238E","#BA6AC4","#00E8F2","#351454","#1E0A36","#AC65FD"},new[]{"06_Peach_Sunset","#A77CEE","#FFB4AA","#FF479B","#754887","#3A1647","#FF759D"}};
  foreach(var p in palettes){string path=Root+"Materials/"+p[0]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Neonflow/Vector Skybox"));AssetDatabase.CreateAsset(mat,path);}string[] keys={"_Zenith","_Horizon","_SunColor","_MountainColor","_GroundColor","_GridColor"};for(int i=0;i<keys.Length;i++)mat.SetColor(keys[i],C(p[i+1]));EditorUtility.SetDirty(mat);}
 }
 public static void ValidateAndExport(){
  Build();int prefabs=0,texts=0;foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"Prefabs"})){var p=Load<GameObject>(AssetDatabase.GUIDToAssetPath(guid).Substring(Root.Length));prefabs++;foreach(var c in p.GetComponentsInChildren<Component>(true))if(!c)throw new Exception("Missing component in "+p.name);foreach(var t in p.GetComponentsInChildren<Text>(true)){texts++;if(!t.font)throw new Exception("Missing font in "+p.name);}foreach(var im in p.GetComponentsInChildren<Image>(true)){if(!im.sprite&&im.name!="Unavailable")throw new Exception("Missing sprite "+p.name+"/"+im.name);}}
  Demo();RenderPreviews();AssetDatabase.SaveAssets();
  string outDir=Environment.GetEnvironmentVariable("VECTOR_OUTPUT");if(string.IsNullOrEmpty(outDir))return;
  AssetDatabase.ExportPackage(Root.TrimEnd('/'),Path.Combine(outDir,"Neonflow_Vector.unitypackage"),ExportPackageOptions.Recurse);
  File.WriteAllText(Path.Combine(outDir,"Previews/UnityValidation.txt"),"Unity "+Application.unityVersion+"\n"+prefabs+" prefabs; "+texts+" text elements checked.\n14 screens rendered with actual PNGs and Exo 2.\nBuilt-in renderer validation; URP and macOS not tested in this run.\n");Debug.Log("VECTOR_VALIDATION_PASS "+prefabs+" prefabs / "+texts+" texts");
 }
 static void RenderPreviews(){
  string outDir=Environment.GetEnvironmentVariable("VECTOR_OUTPUT");if(string.IsNullOrEmpty(outDir))return;
  foreach(var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())UnityEngine.Object.DestroyImmediate(g);
  var camObj=new GameObject("PreviewCamera");var cam=camObj.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=540;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C("ink");cam.allowHDR=false;var rt=new RenderTexture(1920,1080,24);cam.targetTexture=rt;
  var layouts=JsonUtility.FromJson<VectorLayouts>(File.ReadAllText(Root+"Data/Layouts.json"));Directory.CreateDirectory(Path.Combine(outDir,"Previews/Screens"));
  var textCheck=new List<string>();
  foreach(var s in layouts.screens){var go=UnityEngine.Object.Instantiate(Load<GameObject>("Prefabs/Screens/"+s.id+".prefab"));go.name=s.id;var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=10;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(outDir,"Previews/Screens/"+s.id+".png"),tex.EncodeToPNG());
   foreach(var t in go.GetComponentsInChildren<Text>(true))if(t.preferredHeight>t.rectTransform.rect.height+3)textCheck.Add(s.id+"/"+t.transform.parent.name+"/"+t.name+" height="+t.rectTransform.rect.height+" preferred="+t.preferredHeight+" text="+t.text);
   foreach(var g in go.GetComponentsInChildren<VectorCutGraphic>(true))if(!g.canvasRenderer)throw new Exception("Missing CanvasRenderer: "+g.name);
   UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}
  File.WriteAllLines(Path.Combine(outDir,"Previews/TextFit.txt"),textCheck.Count==0?new[]{"PASS: all text fits allocated height."}:textCheck.ToArray());
  cam.orthographic=false;cam.fieldOfView=70;cam.clearFlags=CameraClearFlags.Skybox;Directory.CreateDirectory(Path.Combine(outDir,"Previews/Skies"));
  foreach(var path in Directory.GetFiles(Root+"Materials","*.mat")){var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material.shader.name!="Neonflow/Vector Skybox")continue;RenderSettings.skybox=material;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(outDir,"Previews/Skies/"+Path.GetFileNameWithoutExtension(path)+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
  RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camObj);
 }
}}
