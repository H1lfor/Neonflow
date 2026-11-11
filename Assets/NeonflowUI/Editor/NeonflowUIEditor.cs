// Neonflow UI asset import and static screen-template builder.
// Requires Unity UI (com.unity.ugui). No camera/gameplay logic is installed.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.UI.Editor
{
    [Serializable] public class AssetEntry { public string path; public int[] border; }
    [Serializable] public class AssetManifest { public AssetEntry[] assets; }

    public class NeonflowSpriteImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace("\\", "/").Contains("/NeonflowUI/")) return;
            string root = assetPath.Substring(0, assetPath.IndexOf("/NeonflowUI/", StringComparison.Ordinal) + 12);
            string manifest = root + "manifest.json";
            if (!File.Exists(manifest)) return;
            var data = JsonUtility.FromJson<AssetManifest>(File.ReadAllText(manifest));
            if (data == null || data.assets == null) return;
            string relative = assetPath.Substring(root.Length);
            foreach (var item in data.assets)
            {
                if (item.path != relative) continue;
                var t = (TextureImporter)assetImporter;
                t.textureType = TextureImporterType.Sprite;
                t.spriteImportMode = SpriteImportMode.Single;
                t.spritePixelsPerUnit = 100;
                t.mipmapEnabled = false;
                t.alphaIsTransparency = true;
                t.sRGBTexture = true;
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                t.npotScale = TextureImporterNPOTScale.None;
                t.textureCompression = TextureImporterCompression.Uncompressed;
                t.maxTextureSize = 4096;
                t.spriteBorder = new Vector4(item.border[0], item.border[1], item.border[2], item.border[3]);
                var settings = new TextureImporterSettings();
                t.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                t.SetTextureSettings(settings);
                break;
            }
        }
    }

    public static class NeonflowScreenBuilder
    {
        const string Root = "Assets/NeonflowUI/";
        static Color Cyan = new Color32(53,232,242,255), Pink = new Color32(255,77,173,255), Muted = new Color32(133,146,183,255);
        static Sprite Load(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(Root + path + ".png"); }
        static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0,1);
            rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(w,h); return rt;
        }
        static Image Pic(Transform p,string path,float x,float y,float w,float h)
        {
            var image=Rect(p,Path.GetFileName(path),x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite=Load(path); image.raycastTarget=false;
            image.type=image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }
        static Image Movement(Transform parent, string id, float x, float y, float width, float height)
        {
            var image = Rect(parent, id, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NeonflowMovementUICompact/Poses/" + id + ".png");
            image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
        static Text Label(Transform p,string s,float x,float y,float w,float h,int size=26,bool body=false,Color? color=null)
        {
            var text=Rect(p,s,x,y,w,h).gameObject.AddComponent<Text>();
            text.font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/"+(body ? "Exo2-Regular.ttf" : size >= 40 ? "Exo2-ExtraBold.ttf" : "Exo2-SemiBold.ttf"));
            text.text=s; text.fontSize=size; text.color=color ?? Color.white;
            text.raycastTarget=false; text.alignment=TextAnchor.MiddleLeft;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; return text;
        }
        static Button Button(Transform p,string label,float x,float y,float w=400,string style="primary")
        {
            var image=Pic(p,"Buttons/"+style+"_normal",x,y,w,106); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState { highlightedSprite=Load("Buttons/"+style+"_hover"),pressedSprite=Load("Buttons/"+style+"_pressed"),selectedSprite=Load("Buttons/"+style+"_focus"),disabledSprite=Load("Buttons/"+style+"_disabled") };
            var t=Label(image.transform,label,32,16,w-64,74,28);t.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        static GameObject CanvasRoot(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var bg=Pic(go.transform,"Backgrounds/synthwave_sunset",0,0,1920,1080);
            bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;
            Label(go.transform,"NEONFLOW",70,38,900,68,48);
            // Screen IDs are internal; do not render them as player-facing headings.
            return go;
        }
        static void TrackSelect(Transform p)
        {
            Pic(p,"Panels/card",56,169,684,822); Pic(p,"AlbumArt/pink_horizon",110,205,570,390).preserveAspect=true;
            Label(p,"Neon Odyssey",108,625,600,65,42);Label(p,"Grand_Project",108,698,570,40,21,false,Cyan);
            Label(p,"130 BPM",108,750,570,40,24,true);
            string[] diffs={"","",""};
            for(int i=0;i<3;i++){Pic(p,"SongSelect/difficulty_"+(i==1?"selected":"normal"),92+i*202,794,204,96);Label(p,diffs[i],124+i*202,819,150,40,20);}
            Button(p,"Играть",84,878,628);
            Pic(p,"Inputs/field_normal",792,163,1048,104);Pic(p,"Icons/search",820,191,48,48);Label(p,"Название или автор",886,188,880,54,23,true,Muted);
            string[] titles={"Visibility (Synthwave)","Neon Odyssey","Lady of the 80's","80s Synth","Futuristic Pulse"};
            string[] art={"midnight_run","pink_horizon","violet_pulse","solar_drive","midnight_run"};
            for(int i=0;i<5;i++)
            {
                bool selected=i==1;float x=selected?764:800, y=288+i*136;
                var image=Pic(p,"SongSelect/song_row_"+(selected?"selected":"normal"),x,y,selected?1084:1048,136);image.raycastTarget=true;
                var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.SpriteSwap;
                b.spriteState=new SpriteState{highlightedSprite=Load("SongSelect/song_row_hover"),pressedSprite=Load("SongSelect/song_row_selected"),selectedSprite=Load("SongSelect/song_row_focus"),disabledSprite=Load("SongSelect/song_row_disabled")};
                Pic(image.transform,"AlbumArt/"+art[i],48,28,80,80);Label(image.transform,titles[i],150,25,690,46,29);
                Label(image.transform,"",152,73,690,35,20,true,selected?Cyan:Muted);
            }
        }
        static void MainMenu(Transform p)
        {
            Label(p,"",94,240,1050,180,68);Label(p,"",96,430,920,50,29,true);
            Button(p,"Играть",78,510,460);Button(p,"Обучение",78,630,460,"secondary");Button(p,"Настройки",78,750,460,"quiet");
        }
        static void Calibration(Transform p)
        {
            Pic(p,"Panels/camera",80,180,1140,790);Movement(p,"arms_t",470,310,384,448);
            Label(p,"Камера",140,230,980,50,25,false,Cyan);
            Pic(p,"Panels/window",1260,180,580,790);Label(p,"Проверка камеры",1308,230,480,65,35);
            Label(p,"Сядьте или встаньте перед камерой.\n\nВ кадре должны быть плечи и кисти.",1308,330,450,310,30,true);
            Pic(p,"Bars/track",1308,700,450,48);Pic(p,"Bars/fill_cyan",1308,700,300,48);Button(p,"Продолжить",1290,810,510);
        }
        static void Gameplay(Transform p)
        {
            Pic(p,"Panels/hud",60,158,382,180);Label(p,"Очки",100,190,300,44,24,false,Cyan);Label(p,"0",100,240,320,68,48);
            Pic(p,"Panels/hud",1480,158,382,180);Label(p,"Комбо",1518,190,300,44,24,false,Pink);Label(p,"0",1518,240,300,68,48);
            Pic(p,"Gameplay/timing_ring",792,334,336,336);Movement(p,"arms_up",864,358,192,224);
            Pic(p,"Feedback/perfect_plate",758,680,404,112);var t=Label(p,"Perfect",790,700,340,70,35,false,Cyan);t.alignment=TextAnchor.MiddleCenter;
            Label(p,"",760,840,400,45,24,false,Pink);Pic(p,"Bars/track",745,900,430,48);Pic(p,"Bars/fill_pink",745,900,320,48);
        }
        static void Pause(Transform p)
        {
            Pic(p,"Panels/modal",540,190,840,750);Label(p,"Пауза",620,250,680,80,55);
            Button(p,"Продолжить",600,380,720);Button(p,"Повторить",600,510,720,"secondary");Button(p,"Выбор песни",600,640,720,"quiet");
        }
        static void Results(Transform p)
        {
            Pic(p,"Panels/window",220,180,1480,760);Label(p,"Victory",290,230,1300,80,52);Label(p,"",292,318,1300,40,26,false,Cyan);
            Label(p,"—",310,400,230,220,180,false,Pink);Label(p,"Очки   —",650,420,920,70,42);
            Label(p,"Точность   —\nМакс. комбо   —\nPerfect   —     Great   —",650,530,900,200,28);
            Button(p,"Повторить",290,780,630);Button(p,"Выбор песни",960,780,660,"secondary");
        }
        static void Settings(Transform p)
        {
            Pic(p,"Panels/window",180,180,1560,780);Label(p,"Настройки",250,230,1000,70,46);
            string[] labels={"Музыка","Эффекты","Интерфейс"};
            for(int i=0;i<3;i++)
            {
                float y=350+i*130;Label(p,labels[i],260,y,500,60,29,true);
                var track=Pic(p,"Bars/track",820,y,760,48);track.raycastTarget=true;
                var fill=Pic(track.transform,"Bars/fill_cyan",0,0,500,48);
                var handle=Pic(track.transform,"Bars/thumb",470,-8,64,64);
                var slider=track.gameObject.AddComponent<Slider>();slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=.66f;
            }
            Label(p,"Голосовое управление",260,750,1000,60,29,true);
            var off=Pic(p,"Inputs/toggle_off",1450,746,128,64);off.raycastTarget=true;
            var on=Pic(off.transform,"Inputs/toggle_on",0,0,128,64);
            var toggle=off.gameObject.AddComponent<Toggle>();toggle.targetGraphic=off;toggle.graphic=on;toggle.isOn=false;
            Button(p,"Применить",1160,840,480);
        }
        [MenuItem("Tools/Neonflow UI/Reapply sprite settings")]
        public static void Reimport()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root.TrimEnd('/')}))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
        }
        [MenuItem("Tools/Neonflow UI/Build 7 screen templates")]
        public static void Build()
        {
            if(!Directory.Exists(Root)){Debug.LogError("Place NeonflowUI inside Assets/ first.");return;}
            Reimport();Directory.CreateDirectory(Root+"Prefabs");AssetDatabase.Refresh();
            string[] names={"MainMenu","TrackSelect","Calibration","Gameplay","Pause","Results","Settings"};
            Action<Transform>[] builders={MainMenu,TrackSelect,Calibration,Gameplay,Pause,Results,Settings};
            for(int i=0;i<names.Length;i++)
            {
                GameObject go=CanvasRoot("NF_"+names[i]);
                try{builders[i](go.transform);string path=Root+"Prefabs/NF_"+names[i]+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();Debug.Log("Neonflow: 7 static screen templates created. Add one prefab to a scene. Add the EventSystem suitable for your input system to test controls. Connect game actions yourself.");
        }
    }
}
