using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        void Songs()
        {
            TextAt("Heading", T("songs.title"));
            ExtraButton(root, "ChangeCharacter", T(SelectedCharacter.nameKey), 960, 122, 324, 66, () => Navigate(ScreenState.Characters), 25);
            TextAt("LocalResults", T("songs.results"));
            for (int i = 0; i < 5; i++)
            {
                var row = root.Find("SongRow" + i);
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            var viewport = MakeRect("TrackViewport", root, 760, 238, 1140, 726);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = MakeRect("TrackContent", viewport, 0, 0, 1140, 726);
            songScroll = viewport.gameObject.AddComponent<ScrollRect>();
            songScroll.viewport = viewport;
            songScroll.content = content;
            songScroll.horizontal = false;
            songScroll.movementType = ScrollRect.MovementType.Clamped;
            songScroll.scrollSensitivity = 55;
            var field = Find<InputField>("Search");
            field.SetTextWithoutNotify(search);
            ((Text)field.placeholder).text = T("songs.search");
            field.onValueChanged.AddListener(q =>
            {
                search = q;
                FillSongs(content, q);
            });
            FillSongs(content, search);
            UpdateTrackDetails();
            Bind("Back", Back);
            Bind("Practice", () => StartSong(true));
            Bind("Start", () => StartSong(false));
            if (!string.IsNullOrEmpty(notice))
                ExtraButton(root, "RetryTrack", T("common.retry"), 720, 182, 490, 60, () => StartSong(practice), 26);
        }

        void FillSongs(RectTransform content, string query)
        {
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            songRows.Clear();
            int rowIndex = 0;
            foreach (var track in library.tracks)
            {
                if (!(track.title + " " + track.artist).ToLowerInvariant().Contains(query.ToLowerInvariant()))
                    continue;
                var tr = track;
                var g = Instantiate(VectorAsset<GameObject>("Prefabs/Components/SongRow"), content, false);
                g.name = "Track_" + tr.id;
                var r = (RectTransform)g.transform;
                r.anchoredPosition = new Vector2(110, -16 - rowIndex * 142);
                r.sizeDelta = new Vector2(1010, 126);
                var title = g.transform.Find("Title").GetComponent<Text>();
                title.text = tr.title;
                title.fontSize = 36;
                StretchRowText(title.rectTransform, 64, 8, 210, 70);
                Ellipsis(title, 1);
                var artist = g.transform.Find("Artist").GetComponent<Text>();
                artist.text = tr.artist + "  ·  " + Timer(assets.Duration(tr.fileName));
                artist.fontSize = 25;
                StretchRowText(artist.rectTransform, 64, 78, 210, 38);
                Ellipsis(artist, 1);
                var bpm = g.transform.Find("BPM").GetComponent<Text>();
                bpm.text = tr.bpm.ToString("0.##", ru) + " BPM";
                bpm.rectTransform.anchorMin = bpm.rectTransform.anchorMax = new Vector2(1, 1);
                bpm.rectTransform.pivot = new Vector2(1, 1);
                bpm.rectTransform.anchoredPosition = new Vector2(-50, -12);
                bpm.rectTransform.sizeDelta = new Vector2(160, 94);
                foreach (var f in g.GetComponentsInChildren<VectorFlipbook>())
                    Stretch((RectTransform)f.transform);
                var button = g.GetComponent<Button>();
                var motion = WireVisual(button);
                motion.songRow = true;
                motion.SetSelected(tr.id == selected.id);
                songRows[tr.id] = motion;
                motion.FocusEntered = () =>
                {
                    float top = -r.anchoredPosition.y, view = content.anchoredPosition.y;
                    if (top < view + 2)
                        content.anchoredPosition = new Vector2(0, Mathf.Max(0, top - 2));
                    else if (top + 126 > view + 724)
                        content.anchoredPosition = new Vector2(0, top + 126 - 724);
                };
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectTrack(tr));
                rowIndex++;
            }

            content.sizeDelta = new Vector2(1140, Mathf.Max(726, rowIndex * 142 + 16));
            if (rowIndex == 0)
                NewText(content, "NoMatches", T("songs.no_matches"), 80, 80, 1000, 100, 40, VectorInteraction.Ivory);
            songScroll.verticalNormalizedPosition = 1;
        }

        static void StretchRowText(RectTransform r, float left, float top, float right, float height)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(left, -top - height);
            r.offsetMax = new Vector2(-right, -top);
        }

        static void Ellipsis(Text text, int lines)
        {
            // Keep the supplied font size. Truncation is confined to list cells;
            // full titles remain in the selected track/result panels.
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float maxHeight = lines * text.cachedTextGeneratorForLayout.GetPreferredHeight("Ag", settings) + .1f;
            string full = text.text;
            if (text.cachedTextGeneratorForLayout.GetPreferredHeight(full, settings) <= maxHeight)
                return;
            int lo = 0, hi = full.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                float h = text.cachedTextGeneratorForLayout.GetPreferredHeight(full.Substring(0, mid) + "…", settings);
                if (h <= maxHeight)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            text.text = full.Substring(0, lo).TrimEnd() + "…";
        }

        void SelectTrack(Track track)
        {
            if (NavigationBusy || state != ScreenState.Songs)
                return;
            Sound("select");
            selected = track;
            foreach (var pair in songRows)
                pair.Value.SetSelected(pair.Key == selected.id);
            StartCoroutine(FadeTrackDetails(screenGeneration));
        }

        IEnumerator FadeTrackDetails(int generation)
        {
            string[] names =
            {
                "SongTitle",
                "Artist",
                "BPM",
                "Difficulty",
                "RatingStars",
                "EmptyResults",
                "RealResults"
            };
            for (float t = 0; t < .08f; t += Time.unscaledDeltaTime)
            {
                if (generation != screenGeneration)
                    yield break;
                SetDetailAlpha(names, 1 - t / .08f);
                yield return null;
            }

            if (generation != screenGeneration)
                yield break;
            UpdateTrackDetails();
            for (float t = 0; t < .12f; t += Time.unscaledDeltaTime)
            {
                if (generation != screenGeneration)
                    yield break;
                SetDetailAlpha(names, t / .12f);
                yield return null;
            }

            if (generation == screenGeneration)
                SetDetailAlpha(names, 1);
        }

        void SetDetailAlpha(string[] names, float alpha)
        {
            foreach (var name in names)
            {
                var t = root.Find(name);
                if (!t)
                    continue;
                var group = t.GetComponent<CanvasGroup>();
                if (!group)
                    group = t.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alpha;
            }
        }

        void SettingsTabs()
        {
            Bind("Tab0", () => Navigate(ScreenState.Settings));
            Bind("Tab1", () => Navigate(ScreenState.CameraSettings));
            Bind("Tab2", () => Navigate(ScreenState.ControlSettings));
            Bind("Back", Back);
        }

        void SettingsUI()
        {
            SettingsTabs();
            float[] values =
            {
                save.settings.music,
                save.settings.ui,
                save.settings.effects
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var slider = Find<Slider>("Slider" + i);
                slider.SetValueWithoutNotify(values[i]);
                TextAt("Value" + i, Mathf.RoundToInt(values[i] * 100) + "%");
                slider.onValueChanged.AddListener(value =>
                {
                    if (index == 0)
                    {
                        save.settings.music = value;
                        music.volume = value;
                    }
                    else if (index == 1)
                        save.settings.ui = value;
                    else
                        save.settings.effects = value;
                    TextAt("Value" + index, Mathf.RoundToInt(value * 100) + "%");
                    Persist();
                });
            }
        }

        void CameraSettingsUI()
        {
            SettingsTabs();
            var field = Find<InputField>("CameraDevice");
            field.enabled = false;
            field.placeholder.gameObject.SetActive(false);
            var buttonRect = MakeRect("SelectDevice", field.transform, 0, 0, field.GetComponent<RectTransform>().rect.width, field.GetComponent<RectTransform>().rect.height);
            var hit = buttonRect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var b = buttonRect.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            WireVisual(b);
            b.onClick.AddListener(() =>
            {
                if (NavigationBusy)
                    return;
                var devices = WebCamTexture.devices;
                if (devices.Length == 0)
                    return;
                int i = Array.FindIndex(devices, d => d.name == save.settings.camera);
                save.settings.camera = devices[(i + 1) % devices.Length].name;
                calibrated = false;
                Persist();
                recognition.Restart(save.settings);
            });
            Bind("Calibrate", () =>
            {
                cameraReturn = ScreenState.CameraSettings;
                Navigate(ScreenState.Camera);
            });
        }

        void ControlSettingsUI()
        {
            SettingsTabs();
            var voice = Find<Toggle>("VoiceToggle");
            voice.SetIsOnWithoutNotify(save.settings.voice);
            voice.onValueChanged.AddListener(v =>
            {
                save.settings.voice = v;
                Persist();
                recognition.Restart(save.settings);
            });
            var reduced = Find<Toggle>("MotionToggle");
            reduced.SetIsOnWithoutNotify(save.settings.reducedMotion);
            reduced.onValueChanged.AddListener(v =>
            {
                save.settings.reducedMotion = v;
                skyMotion.reducedMotion = v;
                foreach (var fx in canvas.GetComponentsInChildren<VectorFlipbook>(true))
                {
                    fx.reducedMotion = v;
                    if (v)
                        fx.Stop();
                }

                foreach (var b in canvas.GetComponentsInChildren<VectorInteraction>(true))
                    b.reducedMotion = v;
                Persist();
            });
            Find<Text>("Commands").rectTransform.sizeDelta = new Vector2(1050, 84);
            voiceStatusText = NewText(root, "VoiceStatus", "", 650, 574, 1060, 40, 25);
            Find<Text>("Escape").rectTransform.anchoredPosition = new Vector2(650, -620);
            var mic = ExtraButton(root, "MicrophoneDevice", "", 635, 677, 1120, 58, () =>
            {
                var devices = recognition.Microphones;
                if (devices.Length == 0)
                    return;
                int i = Array.FindIndex(devices, d => d.id == save.settings.microphone);
                save.settings.microphone = i == devices.Length - 1 ? -1 : devices[(i + 1) % devices.Length].id;
                Persist();
                recognition.Restart(save.settings);
            }, 26);
            microphoneText = mic.GetComponentInChildren<Text>();
        }

        void TutorialUI()
        {
            string[] keys =
            {
                "tutorial.window",
                "tutorial.hold",
                "tutorial.score",
                "tutorial.sides",
                "tutorial.lowered",
                "tutorial.survive",
                "tutorial.voice"
            };
            TextAt("Step", T("tutorial.step." + tutorial));
            TextAt("Text", T(keys[tutorial]) + "\n\n" + T(tutorial == 0 ? "camera.framing" : tutorial == 3 ? "tutorial.lowered" : "tutorial.seated"));
            SetCard(root.Find("Example"), tutorial == 4 ? "diagonal_right" : "arms_v", false, false);
            Bind("Try", tutorial == keys.Length - 1 ? (Action)TryTutorial : () =>
            {
                tutorial++;
                Show(ScreenState.Tutorial);
            });
            TextAt("Try/Label", T(tutorial == keys.Length - 1 ? "tutorial.try" : "common.next"));
            Bind("Back", Back);
        }

        void ResultsUI()
        {
            var record = battle.record;
            TextAt("Heading", T(practice ? "results.practice" : record.victory ? "results.victory" : "results.defeat"));
            TextAt("Song", selected.title);
            var title = Find<Text>("Song");
            title.fontSize = 38;
            title.rectTransform.sizeDelta = new Vector2(740, 112);
            title.rectTransform.anchoredPosition = new Vector2(760, -300);
            TextAt("Author", selected.artist);
            TextAt("Rank", record.rank);
            TextAt("Value0", Number(record.score));
            TextAt("Value1", Percent(record.accuracy));
            TextAt("Value2", record.maxCombo.ToString());
            NewText(root, "JudgementCounts", T("judgement.perfect") + "  " + record.judgements[0] + "    " + T("judgement.great") + "  " + record.judgements[1] + "    " + T("judgement.good") + "  " + record.judgements[2] + "    " + T("judgement.miss") + "  " + record.judgements[3], 760, 793, 750, 50, 25);
            Bind("Retry", () => StartSong(practice));
            Bind("Select", Leave);
            Find<Button>("Retry").interactable = !ending;
            Find<Button>("Select").interactable = !ending;
            PlayResultEffect(record.completed);
        }

        void PlayResultEffect(bool victory)
        {
            resultFX = Effect(victory ? "victory" : "defeat", root, 448, 20, 1024, 270);
            resultFX.transform.SetAsFirstSibling();
            resultFX.Play();
            resultStarted = Time.unscaledTime;
            countResult = !save.settings.reducedMotion;
            if (countResult)
                UpdateResultCount();
        }

        void UpdateResultCount()
        {
            float elapsed = Time.unscaledTime - resultStarted;
            float t = Mathf.Clamp01((elapsed - .35f) / .6f);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            TextAt("Value0", Number(Mathf.RoundToInt(battle.record.score * eased)));
            foreach (var name in new[]
            {
                "Song",
                "Author",
                "Rank",
                "Key0",
                "Value0",
                "Key1",
                "Value1",
                "Key2",
                "Value2",
                "JudgementCounts"
            }

            )
            {
                var node = root.Find(name);
                var g = node.GetComponent<CanvasGroup>();
                if (!g)
                    g = node.gameObject.AddComponent<CanvasGroup>();
                g.alpha = elapsed < .35f ? 0 : Mathf.Clamp01((elapsed - .35f) / .15f);
            }

            if (t >= 1)
                countResult = false;
        }

        void FinishResultCount()
        {
            if (!countResult)
                return;
            resultStarted = Time.unscaledTime - 1;
            UpdateResultCount();
        }
    }
}
