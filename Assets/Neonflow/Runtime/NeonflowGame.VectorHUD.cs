using System;
using UnityEngine;
using UnityEngine.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        Text healingText;
        void HUD()
        {
            hudRoot = root;
            TextAt("You", T(SelectedCharacter.nameKey));
            Find<Text>("You").fontSize = 28;
            Find<Text>("You").rectTransform.sizeDelta = new Vector2(285, 42);
            shown = -1;
            hpPlayer = Find<Image>("PlayerHP/Clip/Fill");
            hpRobot = Find<Image>("RobotHP/Clip/Fill");
            windowBar = Find<Image>("Window/Clip/Fill");
            scoreText = Find<Text>("Score");
            comboText = Find<Text>("Combo");
            timerText = Find<Text>("Timer");
            accuracyText = Find<Text>("Accuracy");
            assessmentText = Find<Text>("Judgement");
            scoreText.text = Number(battle.record.score);
            comboText.text = T("stats.combo").ToUpperInvariant() + " " + battle.combo;
            timerText.text = Timer(clip.length);
            accuracyText.text = T("hud.percent").Replace("{accuracy}", "0");
            assessmentText.text = "";
            healingText = NewText(root, "Healing", "", 410, 88, 300, 42, 28, VectorInteraction.Ivory, TextAnchor.MiddleRight);
            lastPlayer = battle.playerHP / balance.hp;
            lastRobot = battle.robotHP / balance.hp;
            hpPlayer.fillAmount = lastPlayer;
            hpRobot.fillAmount = lastRobot;
            playerDamage = DamageTrail(hpPlayer);
            robotDamage = DamageTrail(hpRobot);
            BindCamera();
            Find<RectTransform>("AccuracyPanel").anchoredPosition = new Vector2(65, -574);
            Find<RectTransform>("AccuracyPanel").sizeDelta = new Vector2(595, 68);
            accuracyText.rectTransform.anchoredPosition = new Vector2(90, -582);
            accuracyText.rectTransform.sizeDelta = new Vector2(540, 50);
            var status = MakeRect("TrackingStatus", root, 65, 660, 595, 86).gameObject.AddComponent<VectorCutGraphic>();
            status.color = VectorInteraction.Ivory;
            status.cutRatio = .2f;
            status.raycastTarget = false;
            warningText = NewText(status.transform, "Status", "", 36, 5, 523, 76, 27);
            countText = NewText(root, "Countdown", "", 860, 420, 450, 140, 100, VectorInteraction.Ivory, TextAnchor.MiddleCenter);
            ExtraButton(root, "PauseButton", T("pause.title"), 80, 900, 380, 82, Pause, 34);
            if (practice)
                NewText(root, "PracticeLabel", T("songs.practice"), 100, 768, 500, 60, 30, VectorInteraction.Ivory);
            beatFX = Effect("beat_pulse", root, 680, 133, 800, 280);
            beatFX.transform.SetSiblingIndex(root.Find("LeftHand").GetSiblingIndex());
            holdFX = Effect("hold_scan", root, 90, 986, 1740, 70);
            holdFX.transform.SetSiblingIndex(root.Find("Window").GetSiblingIndex());
            holdFX.target.color = new Color(1, 1, 1, .45f);
            comboFX = Effect("combo", root, 1510, 86, 400, 190);
            comboFX.transform.SetSiblingIndex(root.Find("Combo").GetSiblingIndex());
            judgementFX.Clear();
            foreach (string id in new[]
            {
                "perfect",
                "great",
                "good",
                "miss"
            }

            )
            {
                var fx = Effect(id, root, 1340, 872, 512, 170);
                fx.transform.SetSiblingIndex(root.Find("Judgement").GetSiblingIndex());
                judgementFX.Add(id, fx);
            }

            judgementAt = comboAt = -100;
            UpdateCues(Math.Max(0, (int)Math.Floor((SongTime - selected.firstBeatOffsetSeconds) / selected.Window) + 1));
        }

        static Image DamageTrail(Image fill)
        {
            var trail = Instantiate(fill, fill.transform.parent);
            trail.name = "Damage trail";
            trail.color = new Color32(230, 64, 105, 255);
            trail.transform.SetSiblingIndex(0);
            return trail;
        }

        void SetCard(Transform card, string id, bool hand, bool mirror)
        {
            card.gameObject.SetActive(!string.IsNullOrEmpty(id));
            if (string.IsNullOrEmpty(id))
                return;
            var pict = card.Find("Pictogram").GetComponent<Image>();
            pict.sprite = VectorAsset<Sprite>("Movements/" + (hand ? "Hands/" : "Poses/") + id);
            pict.preserveAspect = true;
            CenterPivot(pict.rectTransform);
            pict.color = VectorInteraction.Pink;
            pict.rectTransform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
            card.Find("Caption").GetComponent<Text>().text = T("movement." + id);
        }

        void UpdateCues(int index)
        {
            if (!hudRoot || shown == index)
                return;
            shown = index;
            var current = index < battle.cues.Count ? battle.cues[index] : null;
            SetCard(hudRoot.Find("Body"), current?.pose, false, false);
            SetCard(hudRoot.Find("LeftHand"), current?.left, true, true);
            SetCard(hudRoot.Find("RightHand"), current?.right, true, false);
            foreach (string name in new[]
            {
                "Body",
                "LeftHand",
                "RightHand"
            }

            )
                CueMotion(hudRoot.Find(name), 12, .14f, 1);
            for (int j = 1; j <= 3; j++)
            {
                var container = hudRoot.Find("Next" + j);
                var cue = index + j < battle.cues.Count ? battle.cues[index + j] : null;
                SetCard(container, cue?.pose, false, false);
                NextHand(container, "LeftCue", cue?.left, true, 25);
                NextHand(container, "RightCue", cue?.right, false, 269);
                CueMotion(container, 170, .18f, j == 1 ? 1 : j == 2 ? .55f : .28f);
            }
        }

        void NextHand(Transform container, string name, string id, bool mirror, float x)
        {
            var t = container.Find(name);
            Image im;
            if (!t)
            {
                im = MakeRect(name, container, x, 67, 46, 64).gameObject.AddComponent<Image>();
                im.preserveAspect = true;
                CenterPivot(im.rectTransform);
                im.raycastTarget = false;
            }
            else
                im = t.GetComponent<Image>();
            im.gameObject.SetActive(!string.IsNullOrEmpty(id));
            if (string.IsNullOrEmpty(id))
                return;
            im.sprite = VectorAsset<Sprite>("Movements/Hands/" + id);
            im.color = VectorInteraction.Pink;
            im.transform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
        }

        void CueMotion(Transform node, float distance, float duration, float alpha)
        {
            var motion = node.GetComponent<VectorCueMotion>();
            if (!motion)
                motion = node.gameObject.AddComponent<VectorCueMotion>();
            motion.Begin(() => SongTime, distance, duration, alpha, save.settings.reducedMotion || state == ScreenState.Pause);
        }

        static void CenterPivot(RectTransform rect)
        {
            var center = new Vector2(.5f, .5f);
            rect.anchoredPosition += Vector2.Scale(center - rect.pivot, rect.rect.size);
            rect.pivot = center;
        }

        void UpdateHUD(double time, int visual, float accuracy)
        {
            float player = battle.playerHP / balance.hp, opponent = battle.robotHP / balance.hp;
            if (player < lastPlayer)
            {
                playerHitAt = (float)time;
            }

            lastPlayer = player;
            if (opponent < lastRobot)
            {
                robotHitAt = (float)time;
                lastRobot = opponent;
            }

            float lerp = 1 - Mathf.Exp(-Time.unscaledDeltaTime / .06f);
            hpPlayer.fillAmount = Mathf.Lerp(hpPlayer.fillAmount, player, lerp);
            hpRobot.fillAmount = Mathf.Lerp(hpRobot.fillAmount, opponent, lerp);
            if (time - playerHitAt > .09)
                playerDamage.fillAmount = Mathf.Lerp(playerDamage.fillAmount, player, 1 - Mathf.Exp(-Time.unscaledDeltaTime / .073f));
            if (time - robotHitAt > .09)
                robotDamage.fillAmount = Mathf.Lerp(robotDamage.fillAmount, opponent, 1 - Mathf.Exp(-Time.unscaledDeltaTime / .073f));
            timerText.text = Timer(Math.Max(0, clip.length - Math.Max(0, time)));
            scoreText.text = Number(battle.record.score);
            scoreText.fontSize = battle.record.score >= 10000000 ? 42 : 56;
            comboText.text = T("stats.combo").ToUpperInvariant() + " " + battle.combo;
            displayAccuracy = Mathf.Lerp(displayAccuracy, accuracy, 1 - Mathf.Exp(-Time.unscaledDeltaTime / .08f));
            accuracyText.text = T("hud.percent").Replace("{accuracy}", Mathf.RoundToInt(displayAccuracy * 100).ToString());
            windowBar.fillAmount = visual < battle.cues.Count ? Mathf.Clamp01((float)((battle.cues[visual].target - time) / selected.Window)) : 0;
            double beat = robot.Beat - Math.Floor(robot.Beat);
            beatFX.SampleSeconds(beat * 60 / selected.bpm);
            holdFX.target.enabled = accuracy >= balance.holdThreshold && visual < battle.cues.Count && time >= battle.cues[visual].start;
            if (holdFX.target.enabled)
                holdFX.SampleSeconds(time - battle.cues[visual].start);
            comboFX.SampleSeconds(time - comboAt);
            foreach (var pair in judgementFX)
                pair.Value.SampleSeconds(pair.Key == battle.LastJudgement.ToLowerInvariant() ? time - judgementAt : 999);
            if (time - judgementAt > .75)
            {
                assessmentText.text = "";
                healingText.text = "";
            }
        }

        void ShowJudgement()
        {
            judgementAt = SongTime;
            assessmentText.text = battle.LastJudgement + "  " + Percent(battle.LastAccuracy);
            healingText.text = battle.LastHealing > 0 ? T("hud.healing").Replace("{hp}", battle.LastHealing.ToString("0.#", ru)) : "";
            if (battle.combo > 0 && battle.combo % 10 == 0)
                comboAt = SongTime;
        }

        void LateUpdate()
        {
            if (!worldCamera || !robot || !canvas)
                return;
            // Align the 3D model to the safe 1920x1080 composition independently of
            // physical aspect ratio; extreme arm poses stay below the current cards.
            float aspect = worldCamera.aspect;
            float safeY = Mathf.Min(1, aspect / (1920f / 1080));
            float safeX = Mathf.Min(1, (1920f / 1080) / aspect);
            float depth = 8.4f;
            float worldHeight = 2 * depth * Mathf.Tan(worldCamera.fieldOfView * Mathf.Deg2Rad / 2);
            float scale = worldHeight * safeY * .49f / 2.5f;
            robot.transform.localScale = Vector3.one * scale;
            var point = worldCamera.ViewportToWorldPoint(new Vector3(.5f + (1080f / 1920 - .5f) * safeX, .5f + (.095f - .5f) * safeY, depth));
            robot.transform.position = point;
        }
    }

    public class VectorCueMotion : MonoBehaviour
    {
        RectTransform rect;
        CanvasGroup group;
        Vector2 rest;
        Func<double> clock;
        double began;
        float distance, duration, alpha;
        public void Begin(Func<double> time, float travel, float seconds, float opacity, bool reduced)
        {
            if (!rect)
            {
                rect = (RectTransform)transform;
                rest = rect.anchoredPosition;
                group = GetComponent<CanvasGroup>();
                if (!group)
                    group = gameObject.AddComponent<CanvasGroup>();
            }

            clock = time;
            began = time();
            distance = reduced ? 0 : travel;
            duration = reduced ? 0 : seconds;
            alpha = opacity;
        }

        void Update()
        {
            if (clock == null)
                return;
            float t = duration <= 0 ? 1 : Mathf.Clamp01((float)(clock() - began) / duration);
            float e = 1 - Mathf.Pow(1 - t, 3);
            rect.anchoredPosition = rest + Vector2.down * distance * (1 - e);
            group.alpha = alpha * e;
        }
    }
}
