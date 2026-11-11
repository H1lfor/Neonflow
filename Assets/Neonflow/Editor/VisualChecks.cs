using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Neonflow.Editor
{
    [InitializeOnLoad]
    public static class VisualChecks
    {
        static int step;
        static double next;
        static NeonflowGame game;
        static bool keepSyntheticTracking;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static VisualChecks()
        {
            if (SessionState.GetBool("Neonflow.Visual", false))
            {
                EditorApplication.update += Tick;
                next = EditorApplication.timeSinceStartup + 3;
            }
        }

        public static void Begin()
        {
            CoreChecks.Run();
            SessionState.SetBool("Neonflow.Visual", true);
            SaveStore.OverridePath = Path.GetFullPath("TestResults/visual-save.json");
            step = 0;
            keepSyntheticTracking = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            next = EditorApplication.timeSinceStartup + 3;
            EditorSceneManager.OpenScene("Assets/Neonflow/Scenes/Neonflow.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Call(string method, params object[] args) => typeof(NeonflowGame).GetMethod(method, Private).Invoke(game, args);
        static void Show(ScreenState state) => Call("Show", state);
        static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            game = UnityEngine.Object.FindAnyObjectByType<NeonflowGame>();
            if (!game)
                return;
            if (keepSyntheticTracking)
            {
                var tracking = (Recognition)typeof(NeonflowGame).GetField("recognition", Private).GetValue(game);
                typeof(Recognition).GetField("lastFrame", Private).SetValue(tracking, Time.realtimeSinceStartup);
            }

            if (EditorApplication.timeSinceStartup < next)
                return;
            next = EditorApplication.timeSinceStartup + 2;
            try
            {
                switch (step++)
                {
                    case 0:
                        var rec = UnityEngine.Object.FindAnyObjectByType<Recognition>();
                        typeof(Recognition).GetMethod("Stop", Private).Invoke(rec, null);
                        rec.enabled = false;
                        rec.Status = "error.camera_missing";
                        if (rec.PreviewTexture)
                            UnityEngine.Object.Destroy(rec.PreviewTexture);
                        AudioListener.volume = 0;
                        Show(ScreenState.Title);
                        break;
                    case 1:
                        Capture("title");
                        Show(ScreenState.Menu);
                        break;
                    case 2:
                        Capture("menu");
                        var tracks = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text);
                        typeof(NeonflowGame).GetField("selected", Private).SetValue(game, tracks.tracks[9]);
                        var testSave = (SaveData)typeof(NeonflowGame).GetField("save", Private).GetValue(game);
                        // In-memory visual fixtures only; never write to the player's save.
                        testSave.runs.Clear();
                        for (int i = 0; i < 4; i++)
                            testSave.runs.Add(new RunRecord { trackId = tracks.tracks[9].id, completed = true, rank = i == 0 ? "A" : "E", score = i == 0 ? 123456 : 2591, accuracy = i == 0 ? .842f : .371f, maxCombo = i == 0 ? 25 : 1, date = "2026-11-11T00:00:00Z" });
                        for (int i = 0; i < 2; i++)
                            testSave.runs.Add(new RunRecord { trackId = tracks.tracks[0].id, completed = true, rank = "E", score = 2591, accuracy = .371f, maxCombo = 1, date = "2026-11-11T00:00:00Z" });
                        Show(ScreenState.Songs);
                        break;
                    case 3:
                        Capture("songs-long-title");
                        Capture("songs-results-16x10", 1600, 1000);
                        var details = (RectTransform)typeof(NeonflowGame).GetField("root", Private).GetValue(game);
                        var scroll = details.Find("RealResults/Viewport").GetComponent<ScrollRect>();
                        if (!scroll.GetComponent<Image>().raycastTarget)
                            throw new Exception("Result history cannot receive scroll input");
                        float before = scroll.content.anchoredPosition.y;
                        scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { scrollDelta = new Vector2(0, -1) });
                        if (scroll.content.anchoredPosition.y <= before)
                            throw new Exception("Result history scroll did not move");
                        Capture("songs-results-scrolled");
                        var shortTracks = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text);
                        typeof(NeonflowGame).GetField("selected", Private).SetValue(game, shortTracks.tracks[0]);
                        Show(ScreenState.Songs);
                        step = 30;
                        break;
                    case 30:
                        Capture("songs-results-short");
                        Call("OpenSettings", ScreenState.Menu);
                        step = 4;
                        break;
                    case 4:
                        Capture("settings");
                        typeof(NeonflowGame).GetField("tutorial", Private).SetValue(game, 5);
                        Show(ScreenState.Tutorial);
                        break;
                    case 5:
                        var layout = (RectTransform)typeof(NeonflowGame).GetField("root", Private).GetValue(game);
                        var lesson = layout.Find("Text").GetComponent<Text>();
                        if (lesson.preferredHeight > lesson.rectTransform.rect.height + 1)
                            throw new Exception("Healing tutorial text overflows");
                        Capture("tutorial-healing");
                        Show(ScreenState.Camera);
                        break;
                    case 6:
                        var calibration = (RectTransform)typeof(NeonflowGame).GetField("root", Private).GetValue(game);
                        if (calibration.Find("Ready").gameObject.activeSelf)
                            throw new Exception("Old calibration button still visible");
                        var button = calibration.Find("RetryCamera") as RectTransform;
                        var panel = calibration.Find("InstructionPanel") as RectTransform;
                        var corners = new Vector3[4];
                        button.GetWorldCorners(corners);
                        var cut = panel.GetComponent<Neonflow.VectorUI.VectorCutGraphic>().CutDistance;
                        foreach (var corner in corners)
                        {
                            var local = panel.InverseTransformPoint(corner);
                            float u = Mathf.InverseLerp(panel.rect.yMin, panel.rect.yMax, local.y);
                            if (local.y < panel.rect.yMin || local.y > panel.rect.yMax || local.x < panel.rect.xMin + cut * u || local.x > panel.rect.xMax - cut + cut * u)
                                throw new Exception("Retry button outside slanted panel");
                        }

                        Capture("camera-missing");
                        Capture("camera-panel-16x10", 1600, 1000);
                        game.StartSong(true);
                        break;
                    case 7:
                        if (game.State != ScreenState.Playing)
                        {
                            step--;
                            break;
                        }

                        Capture("hud", 1600, 900);
                        Capture("hud-16x10", 1600, 1000);
                        game.CurrentBattle.playerHP = 50;
                        game.CurrentBattle.Apply(.8f, 1, 1);
                        Call("ShowJudgement");
                        var hud = (RectTransform)typeof(NeonflowGame).GetField("hudRoot", Private).GetValue(game);
                        if (hud.Find("Healing").GetComponent<Text>().text != "+4,9 HP")
                            throw new Exception("Healing HUD missing actual gain");
                        Capture("hud-healing");
                        var synthetic = new TrackingFrame
                        {
                            pose = new Landmark[33]
                        };
                        for (int i = 0; i < synthetic.pose.Length; i++)
                            synthetic.pose[i] = new Landmark
                            {
                                x = .5f,
                                y = .5f,
                                v = 1
                            };
                        synthetic.pose[11].x = .4f;
                        synthetic.pose[12].x = .6f;
                        var template = PoseEvaluator.Template("arms_v");
                        var joints = new[]
                        {
                            13,
                            15,
                            14,
                            16
                        };
                        for (int i = 0; i < joints.Length; i++)
                        {
                            synthetic.pose[joints[i]].x = .5f + .2f * (template[i].x + .306f);
                            synthetic.pose[joints[i]].y = .5f - .2f * template[i].y;
                        }

                        foreach (var cue in game.CurrentBattle.cues)
                        {
                            cue.pose = "arms_v";
                            cue.left = cue.right = null;
                        }

                        typeof(NeonflowGame).GetField("shown", Private).SetValue(game, -1);
                        var recognition = (Recognition)typeof(NeonflowGame).GetField("recognition", Private).GetValue(game);
                        typeof(Recognition).GetProperty("Latest").SetValue(recognition, synthetic);
                        keepSyntheticTracking = true;
                        Call("SetSky", 4);
                        break;
                    case 8:
                        var accuracyLabel = (Text)typeof(NeonflowGame).GetField("accuracyText", Private).GetValue(game);
                        if (accuracyLabel.text != game.T("hud.percent").Replace("{accuracy}", "100"))
                            throw new Exception("Live percent under camera did not reach 100 for previous 80 percent: " + accuracyLabel.text);
                        Capture("hud-live-percent-100");
                        keepSyntheticTracking = false;
                        Capture("hud-pastel");
                        Call("Pause");
                        break;
                    case 9:
                        Capture("pause");
                        Call("Leave");
                        break;
                    default:
                        File.WriteAllText("TestResults/visual-render.txt", "Rendered actual Unity scene/UI to 1600x900 and 1600x1000. Recognition stopped before capture; no user images saved. Human inspection required for PNGs.\n");
                        SessionState.SetBool("Neonflow.Visual", false);
                        EditorApplication.update -= Tick;
                        EditorApplication.ExitPlaymode();
                        EditorApplication.delayCall += () => EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText("TestResults/visual-render.txt", e.ToString());
                SessionState.SetBool("Neonflow.Visual", false);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture(string name, int width = 1600, int height = 900)
        {
            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var mode = canvas.renderMode;
            var oldTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(), null);
            foreach (var label in canvas.GetComponentsInChildren<Text>())
                label.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            foreach (var safe in canvas.GetComponentsInChildren<Neonflow.VectorUI.VectorSafeRoot>())
                typeof(Neonflow.VectorUI.VectorSafeRoot).GetMethod("LateUpdate", Private).Invoke(safe, null);
            var game = UnityEngine.Object.FindAnyObjectByType<NeonflowGame>();
            typeof(NeonflowGame).GetMethod("LateUpdate", Private).Invoke(game, null);
            foreach (var graphic in canvas.GetComponentsInChildren<Neonflow.VectorUI.VectorCutGraphic>())
            {
                graphic.RefreshScreenMetrics();
                graphic.SetVerticesDirty();
            }

            Canvas.ForceUpdateCanvases();
            var robot = UnityEngine.Object.FindAnyObjectByType<RobotView>();
            if (robot && robot.gameObject.activeInHierarchy)
            {
                var bounds = robot.GetComponentInChildren<Renderer>().bounds;
                foreach (var renderer in robot.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(renderer.bounds);
                Debug.Log("VISUAL_ROBOT " + name + " pos=" + robot.transform.position + " scale=" + robot.transform.localScale + " bounds=" + bounds + " viewport=" + camera.WorldToViewportPoint(bounds.center) + " culling=" + camera.cullingMask);
            }

            camera.Render();
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("TestResults/Visual");
            File.WriteAllBytes("TestResults/Visual/" + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.active = active;
            camera.targetTexture = oldTarget;
            camera.ResetAspect();
            canvas.renderMode = mode;
            RenderTexture.ReleaseTemporary(target);
            Debug.Log("VISUAL_CAPTURE " + name);
        }
    }
}
