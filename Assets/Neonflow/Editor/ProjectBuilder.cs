using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace Neonflow.Editor
{
    public static class ProjectBuilder
    {
        [MenuItem("Neonflow/Prepare project")]
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            if (!Resources.Load<RobotRig>("Characters/light") || !Resources.Load<RobotRig>("Characters/solid"))
                CharacterModelBuilder.Build();
            if (!AssetDatabase.LoadAssetAtPath<Material>("Assets/Neonflow/Resources/LightUI.mat"))
            {
                AssetDatabase.CreateAsset(new Material(Shader.Find("Neonflow/Light UI")), "Assets/Neonflow/Resources/LightUI.mat");
            }

            var surface = AssetDatabase.LoadAssetAtPath<Material>("Assets/Neonflow/Resources/RobotSurface.mat");
            if (!surface)
            {
                surface = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                surface.EnableKeyword("_EMISSION");
                surface.SetColor("_EmissionColor", Color.cyan);
                AssetDatabase.CreateAsset(surface, "Assets/Neonflow/Resources/RobotSurface.mat");
            }

            var links = new List<AssetLink>();
            foreach (var folder in new[]
            {
                "Assets/NeonflowVector"
            }

            )
                foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (Directory.Exists(p))
                        continue;
                    // Prefabs reference their frames. Do not also load atlases,
                    // presentation scenes or the entire frame directory as a catalog.
                    if (p.Contains("/Animations/") || p.Contains("/Scenes/"))
                        continue;
                    var asset = AssetDatabase.LoadMainAssetAtPath(p);
                    if (asset is Texture2D)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                        if (sprite)
                            asset = sprite;
                    }

                    if (asset is Sprite || asset is Texture2D || asset is Font || asset is AudioClip || asset is Material || asset is GameObject && p.EndsWith(".prefab"))
                        links.Add(new AssetLink { key = p.Substring(7, p.Length - 7 - Path.GetExtension(p).Length), value = asset });
                }

            string path = "Assets/Neonflow/Resources/GameAssets.asset";
            var data = AssetDatabase.LoadAssetAtPath<GameAssets>(path);
            if (!data)
            {
                data = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.links = links.ToArray();
            EditorUtility.SetDirty(data);
            var durations = new List<TrackDuration>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Neonflow/Resources/Music" }))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
                durations.Add(new TrackDuration { file = Path.GetFileName(importer.assetPath), seconds = AssetDatabase.LoadAssetAtPath<AudioClip>(importer.assetPath).length });
            }

            data.durations = durations.ToArray();
            EditorUtility.SetDirty(data);
            Directory.CreateDirectory("Assets/Neonflow/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Neonflow", typeof(NeonflowGame));
            EditorSceneManager.SaveScene(scene, "Assets/Neonflow/Scenes/Neonflow.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Neonflow/Scenes/Neonflow.unity", true)
            };
            PlayerSettings.companyName = "Neonflow";
            PlayerSettings.productName = "Neonflow";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.macOS.cameraUsageDescription = "Камера распознаёт позы и жесты во время игры.";
            PlayerSettings.macOS.microphoneUsageDescription = "Микрофон распознаёт пять голосовых команд локально.";
            AssetDatabase.SaveAssets();
            Debug.Log("NEONFLOW_PREPARE_OK " + links.Count);
            CoreChecks.Run();
        }

        [MenuItem("Neonflow/Build Windows")]
        public static void BuildWindows()
        {
            Prepare();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Neonflow/Scenes/Neonflow.unity" }, locationPathName = "Builds/Windows/Neonflow.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            File.WriteAllText("TestResults/build.txt", report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed");
        }

        [MenuItem("Neonflow/Build macOS")]
        public static void BuildMacWithCharacters()
        {
            CharacterDelivery.Export();
            BuildMac();
        }

        public static void BuildMac()
        {
            Prepare();
            string platform = BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", UnityEditor.Build.OSArchitecture.x64ARM64.ToString());
            Debug.Log("NEONFLOW_MAC_ARCHITECTURE " + platform + " " + EditorUserBuildSettings.GetPlatformSettings(platform,"Architecture"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Neonflow/Scenes/Neonflow.unity" }, locationPathName = "Builds/macOS/Neonflow.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            File.WriteAllText("TestResults/build-macos.txt", report.summary.result + " errors=" + report.summary.totalErrors + "\nBuilt on Windows; not executed on a Mac.\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("macOS build failed");
        }
    }

    public static class CoreChecks
    {
        static int count;
        static void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception("CHECK FAILED: " + name);
            count++;
        }

        [MenuItem("Neonflow/Run core checks")]
        public static void Run()
        {
            count = 0;
            var b = new Balance();
            var tracks = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text).tracks;
            var moves = JsonUtility.FromJson<MovementCatalogData>(Resources.Load<TextAsset>("Data/Movements_catalog").text).poses;
            Check(tracks.Length == 11, "11 tracks");
            Check(Array.FindAll(moves, m => m.category == "Poses").Length == 12, "12 poses");
            Check(Array.FindAll(moves, m => m.category == "Hands").Length == 10, "10 hands");
            foreach (var t in tracks)
            {
                var audio = Resources.Load<AudioClip>("Music/" + Path.GetFileNameWithoutExtension(t.fileName));
                Check(audio != null, "audio " + t.id);
                Check(!t.timingVerifiedByListening, "unverified timing preserved");
                var cues = Generator.Create(t, audio.length, moves, 42, b);
                Check(Math.Abs(cues[0].target - cues[0].start - t.Window) < 1e-9, "full first window");
                Check(cues[cues.Count - 1].target <= audio.length, "no tail target");
                for (int i = 0; i < cues.Count; i++)
                {
                    Check(Math.Abs(cues[i].target - (t.firstBeatOffsetSeconds + i * 8 * 60 / t.bpm)) < 1e-9, "fractional grid no drift");
                    var p = Array.Find(moves, m => m.id == cues[i].pose);
                    Check(string.IsNullOrEmpty(cues[i].left) || Array.IndexOf(p.fingerCueAllowedHands, "left") >= 0, "left compatibility");
                    Check(string.IsNullOrEmpty(cues[i].right) || Array.IndexOf(p.fingerCueAllowedHands, "right") >= 0, "right compatibility");
                    if (i > 0)
                        Check(cues[i].pose != cues[i - 1].pose, "no repeated pose");
                }

                // Simulate uneven rendering, including multi-second stalls. The
                // audio clock must still judge every completed window once.
                var delayed = new Battle(t, audio.length, moves, 42, true, b);
                double clock = cues[0].start;
                double[] steps =
                {
                    1d / 144,
                    1d / 24,
                    .7,
                    3.8,
                    .05
                };
                int tick = 0;
                while (clock <= audio.length + 5)
                {
                    delayed.Advance(clock);
                    clock += steps[tick++ % steps.Length];
                }

                Check(delayed.record.windows == cues.Count, "variable FPS all targets once");
            }

            var cue = new Cue
            {
                start = 0,
                target = 4
            };
            var early = new WindowScore(cue, b);
            for (double t = 0; t <= 4; t += .05)
                early.Add(t, 1);
            var late = new WindowScore(cue, b);
            late.Add(3.95, 1);
            Check(early.Finish().hold > late.Finish().hold, "early hold reward");
            var flash = new WindowScore(cue, b);
            flash.Add(0, 1);
            Check(flash.Finish().accuracy == 0, "expired grace");
            var post = new WindowScore(cue, b);
            post.Add(4.01, 1);
            Check(post.Finish().accuracy == 0, "post-target rejected");
            var gap = new WindowScore(cue, b);
            gap.Add(0, 1);
            gap.Add(3.95, 1);
            Check(gap.Finish().hold < .1, "no hold across gap");
            var grace = new WindowScore(cue, b);
            grace.Add(3.9, 1);
            Check(grace.Finish().accuracy == 1, "bounded grace");
            bool duplicate = false;
            try
            {
                grace.Finish();
            }
            catch (InvalidOperationException)
            {
                duplicate = true;
            }

            Check(duplicate, "score once");
            var calibration = new CalibrationHold();
            Check(!calibration.Tick(true, 10) && !calibration.Tick(true, 12.999), "calibration waits full three seconds");
            Check(calibration.Tick(true, 13) && !calibration.Tick(true, 14), "calibration completes once");
            calibration.Reset();
            calibration.Tick(true, 20);
            Check(!calibration.Tick(false, 22.9), "lost tracking resets calibration");
            Check(!calibration.Tick(true, 23) && !calibration.Tick(true, 25.999) && calibration.Tick(true, 26), "reacquisition requires new three seconds");
            var healing = new Battle(tracks[0], 60, moves, 17, false, b);
            float prior = 0;
            foreach (float accuracy in new[]
            {
                .5f,
                .6f,
                .7f,
                .8f,
                .9f,
                1f
            }

            )
            {
                healing.playerHP = 50;
                healing.Apply(accuracy, 1, 1);
                Check(healing.LastHealing > prior && healing.playerHP > 50, "healing increases with accuracy " + accuracy);
                prior = healing.LastHealing;
            }

            healing.playerHP = 50;
            healing.Apply(.5f, 1, 1);
            Check(Math.Abs(healing.playerHP - 50.5f) < .001, "50 percent heals half HP without damage");
            healing.playerHP = 50;
            healing.Apply(.8f, 1, 1);
            Check(healing.LastHealing > 4.9f && healing.LastHealing < 5, "80 percent heals about five HP");
            healing.playerHP = 50;
            healing.Apply(1, 1, 1);
            Check(healing.playerHP == 60 && healing.robotHP == 100, "perfect heals ten and leaves robot independent");
            healing.playerHP = 99;
            healing.Apply(1, 1, 1);
            Check(healing.playerHP == 100 && healing.LastHealing == 1, "healing capped at maximum HP");
            healing.Apply(.499f, 1, 1);
            Check(healing.playerHP < 100 && healing.LastHealing == 0, "below 50 percent damages instead of heals");
            var once = new Battle(tracks[0], 60, moves, 17, false, b);
            once.playerHP = 50;
            once.Observe(once.cues[0].target, _ => 1);
            once.Advance(once.cues[0].target + b.inferenceWait);
            once.Advance(once.cues[0].target + b.inferenceWait);
            Check(once.playerHP == 60 && once.record.windows == 1, "target heals exactly once");
            var fatal = new Battle(tracks[0], 60, moves, 17, false, b);
            fatal.playerHP = 1;
            fatal.Observe(fatal.cues[1].target, _ => 1);
            fatal.Advance(fatal.cues[1].target + b.inferenceWait);
            Check(fatal.Defeated && fatal.index == 1, "lag cannot revive defeated player with a later target");
            var battle = new Battle(tracks[0], 60, moves, 17, false, b);
            battle.Apply(.5f, 1, 1);
            Check(battle.combo == 1, "50 percent combo survives");
            battle.Apply(.499f, 1, 1);
            Check(battle.combo == 0, "49.9 resets");
            Check(battle.robotHP == 100, "player cannot damage robot");
            battle.robotHP = 0;
            Check(!battle.Defeated, "robot zero no win");
            battle.playerHP = 0;
            Check(battle.Defeated, "player zero defeats");
            var practice = new Battle(tracks[0], 60, moves, 18, true, b);
            practice.playerHP = 0;
            Check(!practice.Defeated, "practice survives");
            practice.Complete(true);
            Check(practice.record.completed, "completion");
            Check(battle.cues[0].pose != practice.cues[0].pose || battle.cues[0].left != practice.cues[0].left || battle.cues[1].pose != practice.cues[1].pose, "new seed new sequence");
            string temp = Path.GetFullPath("TestResults/save-roundtrip.json");
            var save = new SaveData();
            save.runs.Add(practice.record);
            SaveStore.Write(save, temp);
            save.settings.music = .35f;
            SaveStore.Write(save, temp);
            var restored = JsonUtility.FromJson<SaveData>(File.ReadAllText(temp));
            Check(restored.runs[0].seed == 18 && Math.Abs(restored.settings.music - .35) < .001, "save atomic roundtrip");
            string originalSavePath = SaveStore.OverridePath;
            try
            {
                SaveStore.OverridePath = temp;
                Check(SaveStore.Load().runs[0].seed == 18, "save reload through store");
                File.WriteAllText(temp, "broken JSON");
                Check(SaveStore.Load().runs[0].seed == 18, "corrupt save recovers backup");
                SaveStore.Write(save, temp);
            }
            finally
            {
                SaveStore.OverridePath = originalSavePath;
            }

            // Synthetic landmarks test evaluator rules only, not model accuracy.
            var frame = new TrackingFrame
            {
                pose = new Landmark[33]
            };
            for (int i = 0; i < frame.pose.Length; i++)
                frame.pose[i] = new Landmark
                {
                    x = .5f,
                    y = .5f,
                    v = 1
                };
            frame.pose[11].x = .4f;
            frame.pose[12].x = .6f;
            var targetPose = PoseEvaluator.Template("arms_v");
            int[] requiredJoints =
            {
                13,
                15,
                14,
                16
            };
            for (int i = 0; i < requiredJoints.Length; i++)
            {
                frame.pose[requiredJoints[i]].x = .5f + .2f * targetPose[i].x;
                frame.pose[requiredJoints[i]].y = .5f - .2f * targetPose[i].y;
            }

            var targetCue = new Cue
            {
                pose = "arms_v"
            };
            float exact = PoseEvaluator.Evaluate(targetCue, frame, .55f);
            Check(exact > .99f, "upper body reference pose");
            foreach (float raw in new[]
            {
                0f,
                .4f,
                .6f,
                .64f,
                .8f,
                1f
            }

            )
            {
                for (int i = 0; i < requiredJoints.Length; i++)
                    frame.pose[requiredJoints[i]].x = .5f + .2f * (targetPose[i].x + .85f * (1 - raw));
                Check(Math.Abs(PoseEvaluator.Evaluate(targetCue, frame, .55f, 1) - raw) < .001, "raw pose fixture " + raw);
                Check(Math.Abs(PoseEvaluator.Evaluate(targetCue, frame, .55f, b.fullAccuracy) - Mathf.Min(1, raw / .64f)) < .001, "live accuracy boosted another 25 percent " + raw);
            }

            for (int i = 0; i < requiredJoints.Length; i++)
                frame.pose[requiredJoints[i]].x = .5f + .2f * (targetPose[i].x + .306f);
            var easier = new Battle(tracks[0], 60, moves, 17, false, b);
            easier.playerHP = 50;
            easier.cues[0].pose = "arms_v";
            easier.cues[0].left = easier.cues[0].right = null;
            easier.Observe(easier.cues[0].target, c => PoseEvaluator.Evaluate(c, frame, .55f, b.fullAccuracy));
            easier.Advance(easier.cues[0].target + b.inferenceWait);
            Check(easier.LastJudgement == "Perfect" && easier.playerHP > 59.99f, "previous live 80 percent now earns 100 and full healing");
            Check(PoseEvaluator.Evaluate(targetCue, null, .55f, b.fullAccuracy) == 0, "no tracking remains zero on easier scale");
            for (int i = 0; i < requiredJoints.Length; i++)
                frame.pose[requiredJoints[i]].x = .5f + .2f * targetPose[i].x;
            for (int i = 23; i < 33; i++)
                frame.pose[i].v = 0;
            Check(PoseEvaluator.Evaluate(targetCue, frame, .55f) == exact, "legs excluded");
            frame.pose[16].v = 0;
            Check(PoseEvaluator.Evaluate(targetCue, frame, .55f) < exact, "missing required wrist reduces score");
            frame.pose[16].v = 1;
            targetCue.right = "open";
            Check(PoseEvaluator.Evaluate(targetCue, frame, .55f) < exact, "missing required fingers retain weight");
            File.WriteAllText("TestResults/core-checks.txt", "PASS: " + count + " assertions. Real camera, microphone and listening not covered.\n");
            Debug.Log("NEONFLOW_CHECKS_OK " + count);
        }
    }
}
