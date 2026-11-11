using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.Movements.Compact.Editor
{
    public static class NeonflowMovementBuilder
    {
        public const string Root = "Assets/NeonflowMovementUICompact/";
        static readonly string[] States = { "idle", "current", "next", "success", "miss", "disabled", "hold", "focus" };
        static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + path + ".png");

        static RectTransform Child(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static GameObject Card(string name, Vector2 size, string pose, string label, MovementCardState state)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(NeonflowMovementCard));
            var rect = go.GetComponent<RectTransform>();rect.sizeDelta = size;
            var card = go.GetComponent<NeonflowMovementCard>();
            card.box = go.GetComponent<Image>();card.box.type = Image.Type.Sliced;card.box.raycastTarget = false;
            card.stateSprites = new Sprite[States.Length];
            for (int i = 0; i < States.Length; i++) card.stateSprites[i] = Load("Boxes/box_" + States[i]);
            card.pictogram = Child(go.transform, "Pictogram", new Vector2(.07f, .10f), new Vector2(.93f, .81f)).gameObject.AddComponent<Image>();
            card.pictogram.raycastTarget = false;
            card.caption = Child(go.transform, "Caption", new Vector2(.08f, .82f), new Vector2(.90f, .92f)).gameObject.AddComponent<Text>();
            card.caption.font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/Exo2-SemiBold.ttf");
            card.caption.fontSize = size.x >= 300 ? 22 : 17;card.caption.resizeTextForBestFit = true;
            card.caption.resizeTextMinSize = 13;card.caption.resizeTextMaxSize = card.caption.fontSize;
            card.caption.alignment = TextAnchor.UpperRight;card.caption.color = new Color32(225,217,248,255);card.caption.raycastTarget = false;
            var track = Child(go.transform, "HoldTrack", new Vector2(.18f,.095f), new Vector2(.82f,.11f)).gameObject.AddComponent<Image>();
            track.sprite = Load("Bars/hold_track");track.type = Image.Type.Sliced;track.raycastTarget = false;
            card.holdFill = Child(track.transform, "Fill", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            card.holdFill.sprite = Load("Bars/hold_fill");card.holdFill.type = Image.Type.Filled;
            card.holdFill.fillMethod = Image.FillMethod.Horizontal;card.holdFill.fillOrigin = 0;card.holdFill.raycastTarget = false;
            card.SetPictogram(Load(pose), label);card.SetState(state);card.SetHoldProgress(0);
            return go;
        }

        [MenuItem("Tools/Neonflow Movements Compact/Rebuild presentation prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "Prefabs");
            var standard = Card("NF_MovementCard", new Vector2(240,300), "Poses/arms_v", "ARMS V", MovementCardState.Next);
            var current = Card("NF_MovementCard_Current", new Vector2(320,400), "Poses/arms_v", "ARMS V", MovementCardState.Current);
            var hand = Card("NF_HandCue", new Vector2(220,260), "Hands/peace", "TWO FINGERS", MovementCardState.Hold);
            foreach (var go in new[] { standard, current, hand })
            {
                PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + go.name + ".prefab");
                Object.DestroyImmediate(go);
            }
            var strip = new GameObject("NF_MovementStrip", typeof(RectTransform));
            strip.GetComponent<RectTransform>().sizeDelta = new Vector2(1080,300);
            string[] poseNames = { "arms_v", "reach_left", "clap", "shoulders_up" };
            string[] labels = { "ARMS V", "REACH LEFT", "CLAP", "SHRUG UP" };
            for (int i = 0; i < 4; i++)
            {
                var child = Card("Cue_" + i, new Vector2(240,300), "Poses/" + poseNames[i], labels[i], i == 0 ? MovementCardState.Current : MovementCardState.Next);
                var rect = child.GetComponent<RectTransform>();rect.SetParent(strip.transform,false);rect.anchoredPosition = new Vector2(-405+i*270,0);
            }
            PrefabUtility.SaveAsPrefabAsset(strip, Root + "Prefabs/NF_MovementStrip.prefab");Object.DestroyImmediate(strip);
            AssetDatabase.SaveAssets();
        }
    }
}
