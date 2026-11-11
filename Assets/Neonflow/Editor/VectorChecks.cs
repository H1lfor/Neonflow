using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Neonflow.VectorUI;

namespace Neonflow.Editor
{
    [InitializeOnLoad]
    public static class VectorChecks
    {
        const string Flag = "Neonflow.VectorChecks";
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static NeonflowGame game;
        static int step, checks;
        static RectTransform pausedHud;
        static double next, deadline, pauseTime, maxDrift;
        static string errors = "";
        static bool defeatCaptured, loadingCaptured;
        static bool calibrationInjected;
        static double calibrationStarted;
        static VectorChecks()
        {
            if (SessionState.GetBool(Flag, false))
            {
                EditorApplication.update += Tick;
                Application.logMessageReceived += Log;
                next = EditorApplication.timeSinceStartup + 3;
                deadline = EditorApplication.timeSinceStartup + 240;
            }
        }

        public static void Begin()
        {
            CoreChecks.Run();
            SessionState.SetBool(Flag, true);
            step = checks = 0;
            errors = "";
            maxDrift = 0;
            defeatCaptured = false;
            loadingCaptured = false;
            calibrationInjected = false;
            SaveStore.OverridePath = Path.GetFullPath("TestResults/vector-save.json");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            next = EditorApplication.timeSinceStartup + 4;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorSceneManager.OpenScene("Assets/Neonflow/Scenes/Neonflow.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Log(string text, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
                errors += text + "\n" + trace + "\n";
        }

        static object Field(string name) => typeof(NeonflowGame).GetField(name, Private).GetValue(game);
        static void Set(string name, object value) => typeof(NeonflowGame).GetField(name, Private).SetValue(game, value);
        static void Call(string method, params object[] args) => typeof(NeonflowGame).GetMethod(method, Private).Invoke(game, args);
        static RectTransform Root => (RectTransform)Field("root");

        static T Node<T>(string name)
            where T : Component => Root.Find(name).GetComponent<T>();
        static void Click(string name) => Node<Button>(name).onClick.Invoke();
        static void Check(bool result, string label)
        {
            checks++;
            if (!result)
                throw new Exception("Vector check: " + label);
        }

        static void State(ScreenState expected) => Check(game.State == expected, "state " + expected + " actual " + game.State);
        static void Shot(string name, int width = 1920, int height = 1080) => VisualChecks.Capture("vector-" + name, width, height);
        static void CameraFrameCheck(int width, int height, string name)
        {
            var recognition = (Recognition)Field("recognition");
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = x < 8 ? Color.cyan : x >= width - 8 ? Color.magenta : y < 8 ? Color.yellow : y >= height - 8 ? Color.green : x % 80 < 2 || y % 60 < 2 ? new Color32(80, 90, 110, 255) : new Color32(30, 35, 50, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var property = typeof(Recognition).GetProperty("PreviewTexture");
            property.SetValue(recognition, texture);
            Call("UpdateVectorUI");
            var feed = Node<RawImage>("Webcam/Clip/Feed").rectTransform;
            var area = ((RectTransform)feed.parent).rect.size;
            var size = feed.sizeDelta;
            Check(Math.Abs(size.x / size.y - (float)width / height) < .001, "camera aspect preserved " + name);
            Check(size.x <= area.x - 2 * (area.y * .035f + 4) + .01f && size.y <= area.y - 8 + .01f, "all camera edges inside mask " + name);
            var overlay = Node<RectTransform>("Webcam/Clip/Skeleton");
            Check(overlay.sizeDelta == size && overlay.anchoredPosition == feed.anchoredPosition, "camera and landmarks aligned " + name);
            Shot(name);
            Shot(name + "-16x10", 1920, 1200);
            property.SetValue(recognition, null);
            UnityEngine.Object.Destroy(texture);
            Call("UpdateVectorUI");
        }

        static void RobotFrameCheck(string pose)
        {
            var robot = (RobotView)Field("robot");
            robot.SetCue(new Cue { pose = pose, left = "open", right = "open", robotAccuracy = 1 }, 100);
            typeof(RobotView).GetField("positions", Private).SetValue(robot, PoseEvaluator.Template(pose));
            typeof(RobotView).GetMethod("Update", Private).Invoke(robot, null);
            var camera = Camera.main;
            camera.aspect = 1920f / 1080;
            Call("LateUpdate");
            var renderer = robot.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = renderer.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes;
            Vector3 World(int i) => (renderer.bones[weights[i].boneIndex0].localToWorldMatrix * bind[weights[i].boneIndex0]).MultiplyPoint3x4(vertices[i]);
            var bounds = new Bounds(World(0), Vector3.zero);
            for (int i=1;i<vertices.Length;i++) bounds.Encapsulate(World(i));
            Check(bounds.size.y > 2 && bounds.size.y < 4.5, "robot readable scale " + pose);
            var a = camera.WorldToViewportPoint(bounds.min);
            var b = camera.WorldToViewportPoint(bounds.max);
            Check(a.x > .36f && b.x < .795f && a.y > .06f && b.y < .635f, "extreme robot pose clear of HUD " + pose + " min=" + a + " max=" + b);
            Shot("robot-" + pose);
            Shot("robot-" + pose + "-16x10", 1920, 1200);
            camera.ResetAspect();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            game = UnityEngine.Object.FindAnyObjectByType<NeonflowGame>();
            if (!game)
                return;
            try
            {
                if (calibrationInjected)
                {
                    var capture = (Recognition)Field("recognition");
                    typeof(Recognition).GetField("lastFrame", Private).SetValue(capture, Time.realtimeSinceStartup);
                }

                if (EditorApplication.timeSinceStartup > deadline)
                    throw new Exception("Vector test timeout at step " + step);
                if (game.State == ScreenState.Results && game.CurrentBattle.Defeated && !defeatCaptured && Time.unscaledTime - (float)Field("resultStarted") > .95f)
                {
                    Shot("results-defeat");
                    defeatCaptured = true;
                }

                if (game.State == ScreenState.Loading && !loadingCaptured)
                {
                    Shot("loading");
                    loadingCaptured = true;
                }

                if (EditorApplication.timeSinceStartup < next)
                    return;
                if (game.NavigationBusy)
                    return;
                next = EditorApplication.timeSinceStartup + 2;
                if (game.State == ScreenState.Playing && game.SongTime > 1)
                {
                    var source = (AudioSource)Field("music");
                    if (source.isPlaying)
                    {
                        double drift = Math.Abs((double)source.timeSamples / source.clip.frequency - game.SongTime);
                        maxDrift = Math.Max(maxDrift, drift);
                        Check(drift < .15, "audio DSP drift");
                    }
                }

                Debug.Log("VECTOR_STEP " + step);
                switch (step++)
                {
                    case 0:
                        var recognition = (Recognition)Field("recognition");
                        typeof(Recognition).GetMethod("Stop", Private).Invoke(recognition, null);
                        recognition.enabled = false;
                        if (recognition.PreviewTexture)
                            UnityEngine.Object.Destroy(recognition.PreviewTexture);
                        recognition.Status = "error.camera_missing";
                        recognition.VoiceStatus = "status.voice_unavailable";
                        AudioListener.volume = 0;
                        Call("Show", ScreenState.Title);
                        break;
                    case 1:
                        Shot("title");
                        Click("Continue");
                        Check(game.NavigationBusy, "wipe input shield");
                        Click("Continue");
                        break;
                    case 2:
                        State(ScreenState.Menu);
                        Shot("menu");
                        Shot("menu-720", 1280, 720);
                        Shot("menu-1440", 2560, 1440);
                        Shot("menu-16x10", 1920, 1200);
                        Click("Settings");
                        break;
                    case 3:
                        State(ScreenState.Settings);
                        Shot("settings");
                        Node<Slider>("Slider0").value = .42f;
                        Check(Math.Abs(SaveStore.Load().settings.music - .42f) < .001, "volume persisted");
                        Click("Tab1");
                        break;
                    case 4:
                        State(ScreenState.CameraSettings);
                        Shot("camera-settings");
                        Click("Calibrate");
                        break;
                    case 5:
                        State(ScreenState.Camera);
                        Shot("calibration");
                        CameraFrameCheck(640, 480, "calibration-frame-4x3");
                        CameraFrameCheck(640, 360, "calibration-frame-16x9");
                        Check(!Node<Button>("Ready").interactable, "calibration blocked without actual tracking");
                        var frame = new TrackingFrame
                        {
                            pose = new Landmark[33],
                            left = new Landmark[21],
                            right = new Landmark[21]
                        };
                        for (int i = 0; i < 33; i++)
                            frame.pose[i] = new Landmark
                            {
                                x = .5f,
                                y = .5f,
                                v = 1
                            };
                        for (int i = 0; i < 21; i++)
                        {
                            frame.left[i] = new Landmark
                            {
                                x = .3f,
                                y = .3f,
                                v = 1
                            };
                            frame.right[i] = new Landmark
                            {
                                x = .7f,
                                y = .3f,
                                v = 1
                            };
                        }

                        typeof(Recognition).GetProperty("Latest").SetValue((Recognition)Field("recognition"), frame);
                        calibrationInjected = true;
                        calibrationStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 6:
                        if (game.State == ScreenState.Camera)
                        {
                            Shot("calibration-countdown");
                            step--;
                            break;
                        }

                        State(ScreenState.CameraSettings);
                        Check(EditorApplication.timeSinceStartup - calibrationStarted >= 3 && (bool)Field("calibrated"), "auto calibration after three seconds without Ready click");
                        calibrationInjected = false;
                        typeof(Recognition).GetProperty("Latest").SetValue((Recognition)Field("recognition"), null);
                        Click("Tab2");
                        break;
                    case 7:
                        State(ScreenState.ControlSettings);
                        Shot("control-settings");
                        Node<Toggle>("MotionToggle").isOn = true;
                        Check(SaveStore.Load().settings.reducedMotion, "reduced motion persisted");
                        Node<Toggle>("MotionToggle").isOn = false;
                        var tracks = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text);
                        Set("selected", tracks.tracks[9]);
                        Call("Show", ScreenState.Songs);
                        break;
                    case 8:
                        State(ScreenState.Songs);
                        Check(Root.Find("TrackViewport/TrackContent").childCount == 11, "all 11 real tracks");
                        var firstRow = Node<RectTransform>("TrackViewport/TrackContent/Track_1");
                        var secondRow = Node<RectTransform>("TrackViewport/TrackContent/Track_2");
                        Check(Math.Abs(firstRow.anchoredPosition.y - secondRow.anchoredPosition.y - 142) < .1, "track rows retain spacing");
                        Check(firstRow.Find("Artist").GetComponent<Text>().text.Length > 5, "artist label survives ellipsis");
                        Check(Node<Button>("Start").targetGraphic.canvasRenderer.GetColor().a > .99f, "buttons remain opaque after wipe");
                        Shot("tracks-long-title");
                        Node<InputField>("Search").text = "zzzx-unmatched";
                        break;
                    case 9:
                        Check(Root.Find("TrackViewport/TrackContent").childCount == 1, "empty search state");
                        Shot("tracks-empty");
                        Node<InputField>("Search").text = "Hero";
                        break;
                    case 10:
                        Check(Root.Find("TrackViewport/TrackContent").childCount == 1, "search filters actual library");
                        Root.Find("TrackViewport/TrackContent/Track_2").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 11:
                        Check(((Track)Field("selected")).id == 2, "track selection bound");
                        var selectedRow = Node<RectTransform>("TrackViewport/TrackContent/Track_2");
                        Check(selectedRow.anchoredPosition.x >= 16 && selectedRow.anchoredPosition.x + selectedRow.rect.width <= 1140, "selected focused track inside viewport with hover allowance");
                        Check(Math.Abs(selectedRow.GetComponent<VectorCutGraphic>().CutDistance / selectedRow.rect.height - VectorCutGraphic.MenuSlope) < .001, "selected track keeps slope while widened");
                        Shot("tracks-selected");
                        Click("Practice");
                        break;
                    case 12:
                        if (game.State != ScreenState.Playing)
                        {
                            step--;
                            break;
                        }

                        Shot("hud");
                        Shot("hud-720", 1280, 720);
                        Shot("hud-1440", 2560, 1440);
                        Shot("hud-16x10", 1920, 1200);
                        CameraFrameCheck(640, 480, "hud-frame-4x3");
                        CameraFrameCheck(640, 360, "hud-frame-16x9");
                        RobotFrameCheck("arms_t");
                        RobotFrameCheck("arms_up");
                        Call("SetSky", 5);
                        Call("Pause");
                        pauseTime = game.SongTime;
                        pausedHud = (RectTransform)Field("hudRoot");
                        break;
                    case 13:
                        State(ScreenState.Pause);
                        Shot("pause");
                        Check(Math.Abs(game.SongTime - pauseTime) < .001, "pause freezes musical time");
                        Click("Action2");
                        break;
                    case 14:
                        State(ScreenState.Settings);
                        Check(Math.Abs(game.SongTime - pauseTime) < .001, "settings keep song paused");
                        Check(!((RectTransform)Field("hudRoot")).gameObject.activeSelf, "paused HUD hidden under settings");
                        Click("Back");
                        break;
                    case 15:
                        State(ScreenState.Pause);
                        Check(pausedHud && (RectTransform)Field("hudRoot") == pausedHud, "paused HUD preserved through settings");
                        Shot("pause-after-settings");
                        Click("Action0");
                        break;
                    case 16:
                        State(ScreenState.Playing);
                        Shot("hud-peach");
                        break;
                    case 17:
                        if (game.State != ScreenState.Results || game.NavigationBusy)
                        {
                            step--;
                            break;
                        }

                        Check(game.CurrentBattle.record.completed && game.CurrentBattle.record.practice, "full practice completes at zero HP");
                        Check(game.CurrentBattle.record.windows == game.CurrentBattle.cues.Count, "targets judged exactly once");
                        Call("FinishResultCount");
                        Shot("results-victory");
                        Click("Select");
                        break;
                    case 18:
                        State(ScreenState.Songs);
                        Node<InputField>("Search").text = "";
                        Click("Start");
                        break;
                    case 19:
                        if (game.State != ScreenState.Songs)
                        {
                            step--;
                            break;
                        }

                        Check(game.CurrentBattle.Defeated && !game.CurrentBattle.record.completed, "defeat fades and returns");
                        Check(defeatCaptured, "defeat screen visited");
                        Click("Back");
                        break;
                    case 20:
                        State(ScreenState.Characters);
                        Click("Back");
                        step = 24;
                        break;
                    case 24:
                        State(ScreenState.Menu);
                        Click("Tutorial");
                        step = 21;
                        break;
                    case 21:
                        State(ScreenState.Tutorial);
                        Shot("tutorial");
                        Click("Try");
                        Check((int)Field("tutorial") == 1, "tutorial advances");
                        Call("Show", ScreenState.Menu);
                        Click("Exit");
                        break;
                    case 22:
                        State(ScreenState.ConfirmExit);
                        Shot("confirm-exit");
                        Click("Cancel");
                        break;
                    case 23:
                        State(ScreenState.Menu);
                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                errors += exception + "\n";
                Finish();
            }
        }

        static void Finish()
        {
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/vector-checks.txt", errors.Length == 0 ? "PASS: " + checks + " UI/gameplay checks. Real prefabs, 11 tracks/search, volume/reduced-motion persistence, camera unavailable, tab/calibration navigation, 60-second practice, pause/settings/resume, single judgments, early defeat/fade, tutorial, confirm-exit. Hardware recognition not tested.\nMax sample/DSP drift: " + maxDrift.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + " seconds.\n" : errors);
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(errors.Length == 0 ? 0 : 1);
        }
    }
}
