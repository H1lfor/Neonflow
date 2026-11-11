using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Neonflow.VectorUI;

namespace Neonflow
{
    public enum ScreenState
    {
        Title,
        Menu,
        Camera,
        Songs,
        Tutorial,
        Settings,
        Playing,
        Pause,
        Results,
        CameraSettings,
        ControlSettings,
        ConfirmExit,
        Loading,
        Characters
    }

    // Rules and capture time are independent of the Vector presentation.
    public partial class NeonflowGame : MonoBehaviour
    {
        GameAssets assets;
        Tracks library;
        Movement[] movements;
        CharacterCatalog characters;
        public CharacterProfile SelectedCharacter => characters.Get(save.settings.characterId);
        readonly Dictionary<string, string> words = new Dictionary<string, string>();
        SaveData save;
        Balance balance;
        Recognition recognition;
        readonly CalibrationHold calibrationHold = new CalibrationHold();
        RobotView robot;
        NeonflowCursorController cursor;
        AudioSource music, ui, effects;
        Track selected;
        AudioClip clip;
        Battle battle;
        ScreenState state, settingsReturn, cameraReturn = ScreenState.Songs;
        bool calibrated, practice, ending, pendingTutorial, loading;
        int tutorial, shown = -1, judged = -1, lastCount = -1, loadGeneration;
        double startDsp;
        string notice = "", search = "";
        Camera worldCamera;
        VectorSkyMotion skyMotion;
        readonly CultureInfo ru = CultureInfo.GetCultureInfo("ru-RU");
        public ScreenState State => state;
        public Battle CurrentBattle => battle;
        public double SongTime => AudioSettings.dspTime - startDsp;
        public bool NavigationBusy => transitioning || loading;

        public string T(string key) => words.TryGetValue(key, out var value) ? value : key;
        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            assets = Resources.Load<GameAssets>("GameAssets");
            library = JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text);
            movements = JsonUtility.FromJson<MovementCatalogData>(Resources.Load<TextAsset>("Data/Movements_catalog").text).poses;
            foreach (var word in JsonUtility.FromJson<Words>(Resources.Load<TextAsset>("Data/UI_TEXTS_RU").text).strings)
                words[word.id] = word.text;
            balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("Data/Balance").text);
            save = SaveStore.Load();
            characters = CharacterCatalog.Load();
            characters.Validate(movements);
            save.settings.characterId = SelectedCharacter.id;
            selected = library.tracks[0];
            music = gameObject.AddComponent<AudioSource>();
            ui = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            ui.ignoreListenerPause = true;
            music.volume = save.settings.music;
            SetupWorld();
            SetupUI();
            recognition = gameObject.AddComponent<Recognition>();
            recognition.SongTime = () => battle != null && (state == ScreenState.Playing || AudioListener.pause) ? SongTime : Time.realtimeSinceStartupAsDouble;
            recognition.OnVoice = Voice;
            recognition.OnFrame = Frame;
            recognition.Launch(save.settings);
            Show(ScreenState.Title);
        }

        TAsset VectorAsset<TAsset>(string key)
            where TAsset : UnityEngine.Object => assets.Get<TAsset>("NeonflowVector/" + key);
        void SetupWorld()
        {
            worldCamera = Camera.main;
            if (!worldCamera)
                worldCamera = new GameObject("World camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            worldCamera.tag = "MainCamera";
            worldCamera.transform.position = new Vector3(0, 2.6f, -8.4f);
            worldCamera.transform.rotation = Quaternion.identity;
            worldCamera.fieldOfView = 45;
            worldCamera.clearFlags = CameraClearFlags.Skybox;
            worldCamera.cullingMask &= ~(1 << 30);
            RenderSettings.ambientLight = new Color(.72f, .63f, .82f);
            var light = new GameObject("Vector soft key", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            robot = new GameObject("NEON-01").AddComponent<RobotView>();
            robot.Build(SelectedCharacter.id);
            skyMotion = gameObject.AddComponent<VectorSkyMotion>();
            SetSky(0);
        }

        void SetSky(int index)
        {
            string[] names =
            {
                "01_Vector_Sunset",
                "02_Cyan_Horizon",
                "03_Lilac_Dawn",
                "04_Pink_Arcade",
                "05_Violet_Night",
                "06_Peach_Sunset"
            };
            skyMotion.enabled = false;
            skyMotion.material = VectorAsset<Material>("Materials/" + names[Mathf.Clamp(index, 0, 5)]);
            skyMotion.menuMode = false;
            skyMotion.reducedMotion = save.settings.reducedMotion;
            skyMotion.enabled = true;
        }

        void Sound(string id, bool effect = false)
        {
            var audio = VectorAsset<AudioClip>("Audio/" + id);
            if (audio)
                (effect ? effects : ui).PlayOneShot(audio, effect ? save.settings.effects : save.settings.ui);
        }

        void Persist()
        {
            try
            {
                SaveStore.Write(save);
            }
            catch (Exception e)
            {
                notice = T("error.result_save");
                Debug.LogWarning(e);
            }
        }

        void Play()
        {
            if (NavigationBusy || ending)
                return;
            if (state == ScreenState.Title)
                Navigate(ScreenState.Menu);
            else if (state == ScreenState.Menu)
            {
                pendingTutorial = false;
                Navigate(ScreenState.Characters);
            }
            else if (state == ScreenState.Characters)
                PlayCharacterChoice();
            else if (state == ScreenState.Songs)
                StartSong(false);
        }

        void Back()
        {
            if (transitioning || ending)
                return;
            if (state == ScreenState.Loading)
            {
                loadGeneration++;
                loading = false;
                music.Stop();
                Navigate(ScreenState.Songs);
                return;
            }

            switch (state)
            {
                case ScreenState.Playing:
                    Pause();
                    break;
                case ScreenState.Pause:
                    Resume();
                    break;
                case ScreenState.Settings:
                case ScreenState.CameraSettings:
                case ScreenState.ControlSettings:
                    Persist();
                    Navigate(settingsReturn);
                    break;
                case ScreenState.Camera:
                    pendingTutorial = false;
                    Navigate(cameraReturn == ScreenState.CameraSettings ? ScreenState.CameraSettings : ScreenState.Characters);
                    break;
                case ScreenState.Songs:
                    Navigate(ScreenState.Characters);
                    break;
                case ScreenState.Characters:
                    pendingTutorial = false;
                    Navigate(ScreenState.Menu);
                    break;
                case ScreenState.Results:
                    Leave();
                    break;
                case ScreenState.Tutorial:
                    if (tutorial > 0)
                    {
                        tutorial--;
                        Show(ScreenState.Tutorial);
                    }
                    else
                        Navigate(ScreenState.Menu);
                    break;
                case ScreenState.ConfirmExit:
                    Navigate(ScreenState.Menu);
                    break;
                case ScreenState.Menu:
                    Navigate(ScreenState.ConfirmExit);
                    break;
                case ScreenState.Title:
                    break;
                default:
                    Navigate(ScreenState.Menu);
                    break;
            }
        }

        void Voice(string command)
        {
            if (NavigationBusy || ending)
                return;
            switch (command)
            {
                case "пауза":
                    if (state == ScreenState.Playing)
                        Pause();
                    break;
                case "продолжить":
                    if (state == ScreenState.Pause)
                        Resume();
                    break;
                case "повторить":
                    if (state == ScreenState.Pause || state == ScreenState.Results)
                        StartSong(practice);
                    break;
                case "назад":
                    if (state != ScreenState.Playing && state != ScreenState.Menu && state != ScreenState.Title)
                        Back();
                    break;
                case "играть":
                    if (state == ScreenState.Menu || state == ScreenState.Songs || state == ScreenState.Characters)
                        Play();
                    break;
            }
        }

        void OpenSettings(ScreenState from)
        {
            settingsReturn = from;
            Navigate(ScreenState.Settings);
        }

        void CalibrateDone()
        {
            if (!recognition.CanCalibrate)
                return;
            recognition.Calibrate();
            calibrated = true;
            if (pendingTutorial)
            {
                pendingTutorial = false;
                StartSong(true);
            }
            else
                Navigate(cameraReturn);
        }

        void TryTutorial()
        {
            selected = library.tracks[1];
            pendingTutorial = true;
            Navigate(ScreenState.Characters);
        }

        public void StartSong(bool training)
        {
            if (ending || loading || transitioning)
                return;
            practice = training;
            loading = true;
            notice = "";
            AudioListener.pause = false;
            music.Stop();
            recognition.NewEpoch();
            StartCoroutine(BeginLoaded(++loadGeneration));
        }

        IEnumerator BeginLoaded(int generation)
        {
            yield return Transition(ScreenState.Loading);
            var request = Resources.LoadAsync<AudioClip>("Music/" + Path.GetFileNameWithoutExtension(selected.fileName));
            yield return request;
            if (generation != loadGeneration)
                yield break;
            clip = request.asset as AudioClip;
            if (clip)
            {
                clip.LoadAudioData();
                while (clip.loadState == AudioDataLoadState.Loading)
                    yield return null;
            }

            if (generation != loadGeneration)
                yield break;
            if (!clip || clip.loadState == AudioDataLoadState.Failed)
            {
                loading = false;
                notice = T("error.song_load");
                yield return Transition(ScreenState.Songs);
                yield break;
            }

            battle = new Battle(selected, clip.length, SelectedCharacter.Movements(movements), Guid.NewGuid().GetHashCode(), practice, balance, SelectedCharacter.id);
            judged = 0;
            shown = -1;
            lastCount = -1;
            SetSky(UnityEngine.Random.Range(0, 6));
            yield return Transition(ScreenState.Playing, () =>
            {
                // The target is exposed before its complete eight-beat window starts.
                startDsp = AudioSettings.dspTime + .4 + selected.Window - selected.firstBeatOffsetSeconds;
                music.clip = clip;
                music.timeSamples = 0;
                music.volume = save.settings.music;
                music.PlayScheduled(startDsp);
                recognition.NewEpoch();
                loading = false;
            });
        }

        void Frame(TrackingFrame frame)
        {
            if (state == ScreenState.Playing && !ending && !loading)
                battle?.Observe(frame.time, cue => EvaluateCue(cue, frame));
        }

        float EvaluateCue(Cue cue, TrackingFrame frame) => PoseEvaluator.Evaluate(cue, frame, recognition.Neutral, balance.fullAccuracy);
        void Pause()
        {
            if (state != ScreenState.Playing || ending || transitioning || loading)
                return;
            AudioListener.pause = true;
            recognition.NewEpoch();
            Sound("pause");
            Show(ScreenState.Pause);
        }

        void Resume()
        {
            if (state != ScreenState.Pause || transitioning)
                return;
            recognition.NewEpoch();
            AudioListener.pause = false;
            Show(ScreenState.Playing);
        }

        void Leave()
        {
            music.Stop();
            AudioListener.pause = false;
            recognition.NewEpoch();
            Navigate(ScreenState.Songs);
        }

        IEnumerator Finish(bool complete)
        {
            ending = true;
            battle.Complete(complete);
            save.runs.Add(battle.record);
            Persist();
            if (!complete)
            {
                Show(ScreenState.Results);
                float volume = music.volume;
                for (float t = 0; t < 1.2f; t += Time.unscaledDeltaTime)
                {
                    music.volume = volume * (1 - t / 1.2f);
                    yield return null;
                }

                music.Stop();
                ending = false;
                yield return Transition(ScreenState.Songs);
            }
            else
            {
                music.Stop();
                ending = false;
                yield return Transition(ScreenState.Results);
            }
        }

        void Update()
        {
            if (!assets)
                return;
            var kb = Keyboard.current;
            if (state == ScreenState.Title && !NavigationBusy && (kb?.anyKey.wasPressedThisFrame == true || Mouse.current?.leftButton.wasPressedThisFrame == true))
            {
                Play();
                return;
            }

            if (kb?.escapeKey.wasPressedThisFrame == true)
                Back();
            if (state == ScreenState.Results && (kb?.enterKey.wasPressedThisFrame == true || Mouse.current?.leftButton.wasPressedThisFrame == true))
                FinishResultCount();
            UpdateVectorUI();
            robot.Paused = AudioListener.pause || transitioning && state != ScreenState.Playing;
            robot.ReducedMotion = save.settings.reducedMotion;
            if (state != ScreenState.Playing || battle == null || ending || loading)
                return;
            double time = SongTime;
            int visual = Math.Max(0, (int)Math.Floor((time - selected.firstBeatOffsetSeconds) / selected.Window) + 1);
            UpdateCues(visual);
            battle.Advance(time);
            robot.Beat = (time - selected.firstBeatOffsetSeconds) * selected.bpm / 60;
            robot.SetCue(visual < battle.cues.Count ? battle.cues[visual] : null, battle.robotHP);
            skyMotion.SampleSong(time, (float)(robot.Beat - Math.Floor(robot.Beat)));
            float accuracy = recognition.Tracked && visual < battle.cues.Count ? EvaluateCue(battle.cues[visual], recognition.Latest) : 0;
            UpdateHUD(time, visual, accuracy);
            int count = time < selected.firstBeatOffsetSeconds ? Math.Min(8, (int)Math.Ceiling((selected.firstBeatOffsetSeconds - time) / (60 / selected.bpm))) : 0;
            countText.text = count > 0 ? count.ToString() : "";
            if (count != lastCount)
            {
                if (count > 0)
                    Sound("countdown", true);
                lastCount = count;
            }

            if (battle.index != judged)
            {
                ShowJudgement();
                Sound(battle.LastAccuracy >= balance.perfect ? "perfect" : "miss", true);
                judged = battle.index;
            }

            if (battle.Defeated)
                StartCoroutine(Finish(false));
            else if (time >= clip.length + balance.inferenceWait)
                StartCoroutine(Finish(true));
        }

        string Number(int value) => value.ToString("N0", ru).Replace('\u00a0', ' ');
        string Percent(float value) => (value * 100).ToString("0.#", ru) + "%";
        static string Timer(double seconds)
        {
            int n = Math.Max(0, (int)Math.Ceiling(seconds));
            return (n / 60).ToString("00") + ":" + (n % 60).ToString("00");
        }

        void OnDestroy()
        {
            AudioListener.pause = false;
        }
    }
}
