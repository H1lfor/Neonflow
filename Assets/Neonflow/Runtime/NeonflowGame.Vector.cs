using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        Canvas canvas;
        RectTransform root, hudRoot, screenLayer;
        RawImage backdrop, preview;
        RectTransform overlayRect;
        GameObject cameraUnavailable;
        Text cameraStatus, warningText, countText, assessmentText, microphoneText, voiceStatusText;
        Button cameraContinue;
        Text calibrationStatus;
        Image calibrationProgress;
        Image hpPlayer, hpRobot, playerDamage, robotDamage, windowBar;
        Text scoreText, comboText, timerText, accuracyText;
        VectorFlipbook wipe, beatFX, holdFX, comboFX, resultFX;
        readonly Dictionary<string, VectorFlipbook> judgementFX = new Dictionary<string, VectorFlipbook>();
        GameObject transitionShield;
        Image transitionFade;
        bool transitioning, countResult;
        int screenGeneration;
        double judgementAt = -100, comboAt = -100;
        float lastPlayer = 1, lastRobot = 1, playerHitAt, robotHitAt, displayAccuracy, resultStarted;
        readonly Dictionary<int, VectorInteraction> songRows = new Dictionary<int, VectorInteraction>();
        ScrollRect songScroll;
        RectTransform resultsContent;
        void SetupUI()
        {
            canvas = new GameObject("Vector UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (!FindAnyObjectByType<EventSystem>())
                new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            backdrop = MakeRect("Vector backdrop", canvas.transform, 0, 0, 1920, 1080).gameObject.AddComponent<RawImage>();
            backdrop.texture = VectorAsset<Texture2D>("Backgrounds/vector_menu");
            backdrop.raycastTarget = false;
            Stretch(backdrop.rectTransform);
            screenLayer = MakeRect("Screens", canvas.transform, 0, 0, 1920, 1080);
            Stretch(screenLayer);
            var shield = MakeRect("Transition shield", canvas.transform, 0, 0, 1920, 1080);
            Stretch(shield);
            transitionShield = shield.gameObject;
            transitionFade = shield.gameObject.AddComponent<Image>();
            transitionFade.color = Color.clear;
            transitionFade.raycastTarget = true;
            wipe = Effect("screen_wipe", shield, 0, 0, 1920, 1080);
            Stretch((RectTransform)wipe.transform);
            wipe.externalClock = true;
            transitionShield.SetActive(false);
            cursor = gameObject.AddComponent<NeonflowCursorController>();
            cursor.defaultTexture = VectorAsset<Texture2D>("Cursor/cursor_default_32");
            cursor.hoverTexture = VectorAsset<Texture2D>("Cursor/cursor_hover_32");
            cursor.pressedTexture = VectorAsset<Texture2D>("Cursor/cursor_pressed_32");
            cursor.hotspot = new Vector2(5, 4);
            cursor.SetDefault();
        }

        static RectTransform MakeRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        Text NewText(Transform parent, string name, string value, float x, float y, float w, float h, int size = 30, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var t = MakeRect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = VectorAsset<Font>("Fonts/Exo2-SemiBold");
            t.text = value;
            t.fontSize = size;
            t.color = color ?? VectorInteraction.Ink;
            t.alignment = alignment;
            t.raycastTarget = false;
            t.supportRichText = false;
            return t;
        }

        TComponent Find<TComponent>(string path)
            where TComponent : Component => root.Find(path).GetComponent<TComponent>();
        void TextAt(string path, string value)
        {
            Find<Text>(path).text = value;
        }

        VectorFlipbook Effect(string id, Transform parent, float x, float y, float w, float h)
        {
            var prefab = VectorAsset<GameObject>("Prefabs/Effects/" + id);
            var g = Instantiate(prefab, parent, false);
            g.name = "FX_" + id;
            var r = (RectTransform)g.transform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            var f = g.GetComponent<VectorFlipbook>();
            f.reducedMotion = save.settings.reducedMotion;
            return f;
        }

        VectorInteraction WireVisual(Button b, float delay = -1)
        {
            if (b.TryGetComponent<VectorCutGraphic>(out var plate))
                plate.cutRatio = VectorCutGraphic.MenuSlope;
            var old = b.GetComponent<VectorButtonMotion>();
            var motion = b.GetComponent<VectorInteraction>();
            if (!motion)
                motion = b.gameObject.AddComponent<VectorInteraction>();
            if (old)
            {
                var r = (RectTransform)b.transform;
                var position = r.anchoredPosition;
                old.enabled = false;
                r.anchoredPosition = position;
                motion.hoverFX = old.hoverFX;
                motion.pressFX = old.pressFX;
                Destroy(old);
            }

            motion.cursor = cursor;
            motion.Hover = () => Sound("hover");
            motion.reducedMotion = save.settings.reducedMotion;
            motion.entranceDelay = delay;
            motion.Configure();
            return motion;
        }

        void Bind(string name, Action action)
        {
            var b = Find<Button>(name);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                if (transitioning || ending || loading || !b.interactable)
                    return;
                Sound("select");
                action();
            });
        }

        Button ExtraButton(Transform parent, string name, string text, float x, float y, float w, float h, Action action, int size = 30)
        {
            var g = Instantiate(VectorAsset<GameObject>("Prefabs/Components/ButtonSecondary"), parent, false);
            g.name = name;
            var r = (RectTransform)g.transform;
            r.localRotation = Quaternion.identity;
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            var label = g.GetComponentInChildren<Text>();
            label.text = text;
            label.fontSize = size;
            label.rectTransform.anchoredPosition = new Vector2(h * .43f + 10, 0);
            label.rectTransform.sizeDelta = new Vector2(w - h * .79f - 10, h);
            foreach (var f in g.GetComponentsInChildren<VectorFlipbook>())
                Stretch((RectTransform)f.transform);
            var b = g.GetComponent<Button>();
            WireVisual(b);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                if (!NavigationBusy && !ending && b.interactable)
                {
                    Sound("select");
                    action();
                }
            });
            foreach (var demo in g.GetComponentsInChildren<VectorPreviewAction>())
                Destroy(demo);
            return b;
        }

        RectTransform LoadLayout(string name)
        {
            var screen = Instantiate(VectorAsset<GameObject>("Prefabs/Screens/" + name));
            screen.SetActive(false);
            var layout = (RectTransform)screen.transform.Find("Layout");
            layout.SetParent(screenLayer, false);
            layout.name = name;
            CompactMenu(layout, name);
            foreach (var demo in layout.GetComponentsInChildren<VectorPreviewAction>(true))
                Destroy(demo);
            Destroy(screen);
            int i = 0;
            foreach (var b in layout.GetComponentsInChildren<Button>(true))
                WireVisual(b, name == "MainMenu" ? i++ * .035f : -1);
            foreach (var f in layout.GetComponentsInChildren<VectorFlipbook>(true))
                f.reducedMotion = save.settings.reducedMotion;
            return layout;
        }

        void Navigate(ScreenState next)
        {
            if (!transitioning)
                StartCoroutine(Transition(next));
        }

        IEnumerator Transition(ScreenState next, Action commit = null)
        {
            if (transitioning)
                yield break;
            transitioning = true;
            var inputGroup = screenLayer.GetComponent<CanvasGroup>();
            if (!inputGroup)
                inputGroup = screenLayer.gameObject.AddComponent<CanvasGroup>();
            inputGroup.interactable = false;
            transitionShield.SetActive(true);
            transitionShield.transform.SetAsLastSibling();
            bool reduced = save.settings.reducedMotion, changed = false;
            double start = Time.unscaledTimeAsDouble;
            double duration = reduced ? .12 : .6, threshold = reduced ? .06 : 8d / 30;
            try
            {
                while (true)
                {
                    double elapsed = Time.unscaledTimeAsDouble - start;
                    if (!changed && elapsed >= threshold)
                    {
                        Show(next);
                        commit?.Invoke();
                        changed = true;
                        Sound(next == ScreenState.Playing ? "start" : "back");
                    }

                    if (reduced)
                    {
                        transitionFade.color = new Color(VectorInteraction.Ink.r, VectorInteraction.Ink.g, VectorInteraction.Ink.b, (float)(1 - Math.Abs(elapsed / duration * 2 - 1)));
                        wipe.Stop();
                    }
                    else
                        wipe.SampleSeconds(elapsed);
                    if (elapsed >= duration)
                        break;
                    yield return null;
                }

                if (!changed)
                {
                    Show(next);
                    commit?.Invoke();
                }
            }
            finally
            {
                wipe.Stop();
                transitionFade.color = Color.clear;
                transitionShield.SetActive(false);
                transitioning = false;
                inputGroup.interactable = true;
            }
        }

        void Show(ScreenState next)
        {
            calibrationHold.Reset();
            screenGeneration++;
            state = next;
            cursor.SetDefault();
            bool resume = next == ScreenState.Playing && hudRoot && battle != null;
            bool pausedSettings = AudioListener.pause && (next == ScreenState.Settings || next == ScreenState.CameraSettings || next == ScreenState.ControlSettings || next == ScreenState.Camera);
            bool preserve = next == ScreenState.Pause || resume || pausedSettings;
            foreach (Transform child in screenLayer)
            {
                if (preserve && child == hudRoot)
                {
                    child.gameObject.SetActive(!pausedSettings);
                    continue;
                }

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (!preserve)
                hudRoot = null;
            preview = null;
            overlayRect = null;
            cameraUnavailable = null;
            cameraStatus = null;
            warningText = null;
            cameraContinue = null;
            calibrationStatus = null;
            calibrationProgress = null;
            microphoneText = null;
            voiceStatusText = null;
            bool world = next == ScreenState.Playing || next == ScreenState.Pause;
            robot.gameObject.SetActive(world);
            backdrop.gameObject.SetActive(!world);
            if (next == ScreenState.Pause && !hudRoot && battle != null)
            {
                root = LoadLayout("Gameplay");
                HUD();
                preview = null;
                overlayRect = null;
                cameraUnavailable = null;
                cameraStatus = null;
                warningText = null;
            }

            if (resume)
            {
                root = hudRoot;
                BindCamera();
                warningText = hudRoot.Find("TrackingStatus/Status").GetComponent<Text>();
                return;
            }

            string id = next switch
            {
                ScreenState.Menu => "MainMenu",
                ScreenState.Camera => "Calibration",
                ScreenState.Songs => "TrackSelect",
                ScreenState.Playing => "Gameplay",
                ScreenState.Results => battle != null && !battle.record.completed ? "ResultsDefeat" : "ResultsVictory",
                _ => next.ToString()};
            root = next == ScreenState.Characters ? CharacterScreen() : LoadLayout(id);
            switch (next)
            {
                case ScreenState.Title:
                    Bind("Continue", Play);
                    break;
                case ScreenState.Menu:
                    Bind("Play", Play);
                    Bind("Settings", () => OpenSettings(ScreenState.Menu));
                    Bind("Tutorial", () =>
                    {
                        tutorial = 0;
                        Navigate(ScreenState.Tutorial);
                    });
                    Bind("Exit", () => Navigate(ScreenState.ConfirmExit));
                    break;
                case ScreenState.Songs:
                    Songs();
                    break;
                case ScreenState.Settings:
                    SettingsUI();
                    break;
                case ScreenState.CameraSettings:
                    CameraSettingsUI();
                    break;
                case ScreenState.ControlSettings:
                    ControlSettingsUI();
                    break;
                case ScreenState.Camera:
                    BindCamera();
                    cameraContinue = Find<Button>("Ready");
                    cameraContinue.onClick.RemoveAllListeners();
                    cameraContinue.interactable = false;
                    cameraContinue.gameObject.SetActive(false);
                    calibrationStatus = NewText(root, "CalibrationStatus", "", 1260, 640, 440, 46, 30);
                    var progressTrack = MakeRect("CalibrationProgress", root, 1260, 702, 440, 8).gameObject.AddComponent<Image>();
                    progressTrack.color = new Color32(222, 201, 228, 255);
                    progressTrack.raycastTarget = false;
                    calibrationProgress = MakeRect("Fill", progressTrack.transform, 0, 0, 440, 8).gameObject.AddComponent<Image>();
                    calibrationProgress.color = VectorInteraction.Pink;
                    calibrationProgress.raycastTarget = false;
                    Stretch(calibrationProgress.rectTransform);
                    Bind("Back", Back);
                    var retry = ExtraButton(root, "RetryCamera", T("common.retry"), 1235, 774, 450, 64, () =>
                    {
                        calibrationHold.Reset();
                        recognition.Restart(save.settings);
                    }, 28);
                    retry.GetComponent<VectorInteraction>().normal = VectorInteraction.Pink;
                    retry.GetComponentInChildren<Text>().color = VectorInteraction.Ivory;
                    break;
                case ScreenState.Tutorial:
                    TutorialUI();
                    break;
                case ScreenState.Playing:
                    HUD();
                    break;
                case ScreenState.Pause:
                    Bind("Action0", Resume);
                    Bind("Action1", () => StartSong(practice));
                    Bind("Action2", () => OpenSettings(ScreenState.Pause));
                    Bind("Action3", Leave);
                    break;
                case ScreenState.Results:
                    ResultsUI();
                    break;
                case ScreenState.ConfirmExit:
                    Bind("Cancel", () => Navigate(ScreenState.Menu));
                    Bind("Confirm", () => Application.Quit());
                    break;
            }

            if (!string.IsNullOrEmpty(notice))
                NewText(root, "Notice", notice, 650, 900, 1150, 50, 26, VectorInteraction.Ivory);
            string focusName = next == ScreenState.Characters ? "Choose_" + SelectedCharacter.id : next == ScreenState.Songs ? "Start" : next == ScreenState.Camera ? "RetryCamera" : null;
            var first = focusName != null ? Find<Button>(focusName) : root.GetComponentInChildren<Button>();
            if (first)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void BindCamera()
        {
            // Wider live view, with the entire sensor frame inside the slanted border.
            var frameRect = Find<RectTransform>("Webcam");
            bool calibration = state == ScreenState.Camera;
            frameRect.anchoredPosition = calibration ? new Vector2(80, -264) : new Vector2(30, -184);
            frameRect.sizeDelta = calibration ? new Vector2(1040, 585) : new Vector2(660, 372);
            frameRect.GetComponent<VectorCutGraphic>().cutRatio = .035f;
            preview = Find<RawImage>("Webcam/Clip/Feed");
            var mask = (RectTransform)preview.transform.parent;
            mask.anchoredPosition = new Vector2(4, -4);
            mask.sizeDelta = frameRect.sizeDelta - Vector2.one * 8;
            mask.GetComponent<VectorCutGraphic>().cutRatio = .035f;
            mask.GetComponent<Mask>().showMaskGraphic = true;
            cameraUnavailable = mask.Find("Unavailable").gameObject;
            ((RectTransform)cameraUnavailable.transform).sizeDelta = mask.sizeDelta;
            var off = (RectTransform)cameraUnavailable.transform.Find("CameraOff");
            off.anchoredPosition = new Vector2(mask.sizeDelta.x / 2 - 40, -mask.sizeDelta.y / 2 + 64);
            cameraStatus = cameraUnavailable.transform.Find("Status").GetComponent<Text>();
            cameraStatus.rectTransform.anchoredPosition = new Vector2(36, -mask.sizeDelta.y / 2 - 30);
            cameraStatus.rectTransform.sizeDelta = new Vector2(mask.sizeDelta.x - 72, 64);
            overlayRect = mask.Find("Skeleton") as RectTransform;
            if (!overlayRect)
            {
                overlayRect = MakeRect("Skeleton", mask, 0, 0, preview.rectTransform.rect.width, preview.rectTransform.rect.height);
                overlayRect.gameObject.AddComponent<TrackingOverlay>().Source = recognition;
            }

            overlayRect.SetSiblingIndex(preview.transform.GetSiblingIndex() + 1);
        }

        internal static Rect FitCameraFrame(Vector2 area, float ratio)
        {
            // Both horizontal margins clear the slanted clip at every height.
            float side = area.y * .035f + 4;
            float width = Mathf.Min(area.x - 2 * side, (area.y - 8) * ratio);
            var size = new Vector2(width, width / ratio);
            return new Rect((area - size) * .5f, size);
        }

        void UpdateVectorUI()
        {
            if (backdrop && backdrop.texture)
            {
                float canvasAspect = ((RectTransform)canvas.transform).rect.width / ((RectTransform)canvas.transform).rect.height;
                float imageAspect = (float)backdrop.texture.width / backdrop.texture.height;
                backdrop.uvRect = canvasAspect > imageAspect ? new Rect(0, (1 - imageAspect / canvasAspect) / 2, 1, imageAspect / canvasAspect) : new Rect((1 - canvasAspect / imageAspect) / 2, 0, canvasAspect / imageAspect, 1);
            }

            if (preview)
            {
                var frame = recognition.PreviewTexture;
                cameraUnavailable.SetActive(!frame);
                cameraStatus.text = T(recognition.Status);
                if (frame)
                {
                    preview.texture = frame;
                    var area = ((RectTransform)preview.transform.parent).rect.size;
                    float ratio = (float)frame.width / frame.height;
                    var fit = FitCameraFrame(area, ratio);
                    preview.rectTransform.sizeDelta = fit.size;
                    preview.rectTransform.anchoredPosition = new Vector2(fit.x, -fit.y);
                    overlayRect.sizeDelta = fit.size;
                    overlayRect.anchoredPosition = preview.rectTransform.anchoredPosition;
                }
            }

            if (state == ScreenState.Camera)
            {
                bool complete = calibrationHold.Tick(recognition.CanCalibrate && !NavigationBusy, Time.unscaledTimeAsDouble);
                cameraContinue.interactable = false;
                calibrationStatus.text = recognition.CanCalibrate ? T("camera.hold").Replace("{seconds}", Mathf.CeilToInt((float)calibrationHold.Remaining).ToString()) : T("camera.wait");
                calibrationProgress.rectTransform.anchorMax = new Vector2((float)(1 - calibrationHold.Remaining / 3), 1);
                TextAt("Framing", T(recognition.CanCalibrate ? "camera.ready" : recognition.Tracked ? "camera.raise_hands" : "camera.framing"));
                if (complete)
                    CalibrateDone();
            }

            if (warningText)
                warningText.text = T(recognition.Status);
            if (voiceStatusText)
                voiceStatusText.text = T(recognition.VoiceStatus);
            if (microphoneText)
                microphoneText.text = T("settings.microphone") + ": " + (save.settings.microphone < 0 ? T("settings.default_device") : Array.Find(recognition.Microphones, d => d.id == save.settings.microphone)?.name ?? "—");
            if (state == ScreenState.CameraSettings)
                TextAt("CameraDevice/Value", recognition.CameraTexture ? recognition.CameraTexture.deviceName : T(recognition.Status));
            if (state == ScreenState.Title && !save.settings.reducedMotion)
            {
                float scale = 1.006f + Mathf.Sin(Time.unscaledTime * Mathf.PI / 2) * .006f;
                root.Find("Logo").localScale = Vector3.one * scale;
                var label = Find<Text>("Continue/Label");
                var color = label.color;
                color.a = .825f + .175f * Mathf.Sin(Time.unscaledTime * Mathf.PI / .8f);
                label.color = color;
            }

            if (state == ScreenState.Results && countResult)
                UpdateResultCount();
        }
    }
}
