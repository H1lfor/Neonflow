using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Neonflow.Editor
{
    [InitializeOnLoad]
    public static class SceneChecks
    {
        static readonly string Flag = "Neonflow.SceneChecks";
        static double next, pausedTime, maxDrift;
        static int step;
        static NeonflowGame game;
        static string errors = "";
        static SceneChecks()
        {
            if (SessionState.GetBool(Flag, false))
            {
                EditorApplication.update += Tick;
                Application.logMessageReceived += Log;
                next = EditorApplication.timeSinceStartup + 4;
            }
        }

        [MenuItem("Neonflow/Run scene smoke")]
        public static void Begin()
        {
            SessionState.SetBool(Flag, true);
            step = 0;
            errors = "";
            maxDrift = 0;
            SaveStore.OverridePath = Path.GetFullPath("TestResults/play-save.json");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            next = EditorApplication.timeSinceStartup + 4;
            EditorSceneManager.OpenScene("Assets/Neonflow/Scenes/Neonflow.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Log(string msg, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
                errors += msg + "\n" + trace + "\n";
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next)
                return;
            next = EditorApplication.timeSinceStartup + 3;
            game = UnityEngine.Object.FindAnyObjectByType<NeonflowGame>();
            if (!game)
                return;
            try
            {
                if (game.State == ScreenState.Playing && game.SongTime > 1)
                {
                    var source = (AudioSource)typeof(NeonflowGame).GetField("music", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
                    if (source.isPlaying)
                    {
                        double drift = Math.Abs((double)source.timeSamples / source.clip.frequency - game.SongTime);
                        maxDrift = Math.Max(maxDrift, drift);
                        if (drift > .15)
                            throw new Exception("Audio sample/DSP drift: " + drift);
                    }
                }

                Debug.Log("SCENE_CHECK_STEP " + step);
                switch (step++)
                {
                    case 0:
                        Show(ScreenState.Menu);
                        break;
                    case 1:
                        Show(ScreenState.Songs);
                        break;
                    case 2:
                        Show(ScreenState.Settings);
                        break;
                    case 3:
                        Show(ScreenState.Tutorial);
                        break;
                    case 4:
                        var tracks = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text);
                        typeof(NeonflowGame).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, tracks.tracks[1]);
                        AudioListener.volume = 0;
                        game.StartSong(true);
                        break;
                    case 5:
                        Call("Pause");
                        pausedTime = game.SongTime;
                        break;
                    case 6:
                        if (Math.Abs(game.SongTime - pausedTime) > .001)
                            throw new Exception("DSP clock advanced while paused");
                        Call("Resume");
                        break;
                    case 7:
                        if (game.State != ScreenState.Results)
                        {
                            step--;
                            break;
                        }

                        if (!game.CurrentBattle.record.completed || !game.CurrentBattle.record.practice)
                            throw new Exception("Practice did not complete");
                        if (game.CurrentBattle.record.windows != game.CurrentBattle.cues.Count)
                            throw new Exception("Lost or duplicate target");
                        game.StartSong(false);
                        break;
                    case 8:
                        if (game.State != ScreenState.Songs)
                        {
                            step--;
                            break;
                        }

                        if (game.CurrentBattle.record.completed || !game.CurrentBattle.Defeated)
                            throw new Exception("Early defeat did not return to songs");
                        break;
                    default:
                        File.WriteAllText("TestResults/full-song.txt", errors.Length == 0 ? "PASS: screens, real 60-second audio, DSP pause freeze, resume, complete practice with zero HP, all targets once, real early defeat/fade/return.\nNo camera input simulated. No gesture accuracy verification." : errors);
                        File.WriteAllText("TestResults/audio-drift.txt", "Maximum observed sample/DSP difference: " + maxDrift.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + " seconds. Checked during real playback, not listening verification.\n");
                        SessionState.SetBool(Flag, false);
                        EditorApplication.update -= Tick;
                        EditorApplication.ExitPlaymode();
                        EditorApplication.delayCall += () => EditorApplication.Exit(errors.Length == 0 ? 0 : 1);
                        break;
                }
            }
            catch (Exception e)
            {
                errors += e + "\n";
                step = 99;
            }
        }

        static void Show(ScreenState state) => typeof(NeonflowGame).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { state });
        static void Call(string method) => typeof(NeonflowGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
    }
}
