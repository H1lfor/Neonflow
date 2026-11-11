using System;
using UnityEngine;
using UnityEngine.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        void UpdateTrackDetails()
        {
            var title = Find<Text>("SongTitle");
            title.text = selected.title;
            title.fontSize = 36;
            title.rectTransform.anchoredPosition = new Vector2(140, -316);
            title.rectTransform.sizeDelta = new Vector2(550, 142);
            title.alignment = TextAnchor.UpperLeft;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            float titleHeight = Mathf.Clamp(Mathf.Ceil(title.preferredHeight) + 12, 58, 142);
            title.rectTransform.sizeDelta = new Vector2(550, titleHeight);
            float artistY = 316 + titleHeight + 10;
            var artist = Find<Text>("Artist");
            artist.text = selected.artist;
            artist.rectTransform.anchoredPosition = new Vector2(140, -artistY);
            artist.rectTransform.sizeDelta = new Vector2(530, 38);
            artist.fontSize = 27;
            artist.color = new Color32(113, 80, 123, 255);
            var bpm = Find<Text>("BPM");
            bpm.text = selected.bpm.ToString("0.##", ru) + " BPM  ·  " + Timer(assets.Duration(selected.fileName));
            bpm.fontSize = 26;
            bpm.rectTransform.anchoredPosition = new Vector2(140, -artistY - 47);
            bpm.rectTransform.sizeDelta = new Vector2(460, 38);
            float stars = Mathf.Clamp(1 + ((float)selected.bpm - 100) * 4 / 50, 1, 5);
            root.Find("DifficultyStar").gameObject.SetActive(false);
            root.Find("Difficulty").gameObject.SetActive(false);
            foreach (string name in new[]
            {
                "RatingStars",
                "RealResults"
            }

            )
            {
                var old = root.Find(name);
                if (!old)
                    continue;
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            float starsY = artistY + 99;
            var rating = MakeRect("RatingStars", root, 140, starsY, 460, 36);
            NewText(rating, "Label", T("songs.difficulty"), 0, 0, 165, 36, 23);
            for (int i = 0; i < 5; i++)
            {
                var back = MakeRect("Star", rating, 176 + i * 34, 4, 28, 28).gameObject.AddComponent<Image>();
                back.sprite = VectorAsset<Sprite>("UI/Icons/star");
                back.color = new Color32(180, 150, 187, 255);
                back.raycastTarget = false;
                var fill = MakeRect("Fraction", back.transform, 0, 0, 28, 28).gameObject.AddComponent<Image>();
                fill.sprite = back.sprite;
                fill.color = VectorInteraction.Pink;
                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillAmount = Mathf.Clamp01(stars - i);
            }

            NewText(rating, "Rating", stars.ToString("0.0", ru), 365, 0, 90, 36, 25);
            float headingY = Mathf.Max(550, starsY + 65);
            root.Find("ResultsHeading").gameObject.SetActive(false);
            var heading = Find<Text>("LocalResults");
            heading.rectTransform.anchoredPosition = new Vector2(110, -headingY);
            heading.rectTransform.sizeDelta = new Vector2(490, 44);
            heading.fontSize = 30;
            var records = save.runs.FindAll(r => r.trackId == selected.id && !r.practice && r.completed);
            records.Sort((a, b) => b.score.CompareTo(a.score));
            heading.text = T("songs.results") + (records.Count > 0 ? "  ·  " + records.Count : "");
            var empty = Find<Text>("EmptyResults");
            empty.text = T("songs.no_results");
            empty.fontSize = 27;
            empty.rectTransform.anchoredPosition = new Vector2(140, -headingY - 100);
            empty.rectTransform.sizeDelta = new Vector2(430, 80);
            empty.gameObject.SetActive(records.Count == 0);
            var section = MakeRect("RealResults", root, 110, headingY + 49, 490, 884 - headingY - 49);
            var rule = MakeRect("Rule", section, 0, 0, 490, 2).gameObject.AddComponent<Image>();
            rule.color = new Color32(225, 181, 211, 255);
            rule.raycastTarget = false;
            if (records.Count == 0)
                return;
            var muted = new Color32(113, 80, 123, 255);
            NewText(section, "ScoreHeading", T("stats.score"), 100, 7, 185, 28, 20, muted);
            NewText(section, "AccuracyHeading", T("stats.accuracy"), 305, 7, 155, 28, 20, muted, TextAnchor.MiddleRight);
            float available = section.rect.height - 43;
            float visibleRows = Mathf.Max(1, Mathf.Floor((available + 10) / 98));
            var viewport = MakeRect("Viewport", section, 0, 43, 490, Mathf.Min(available, visibleRows * 98 - 10));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            resultsContent = MakeRect("Records", viewport, 0, 0, 490, Mathf.Max(viewport.rect.height, records.Count * 98 - 10));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = resultsContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                DateTime.TryParse(r.date, out var date);
                var row = DetailPlate(resultsContent, "Result" + i, 0, i * 98, 490, 88, i == 0 ? new Color32(255, 223, 234, 255) : new Color32(241, 229, 233, 255));
                var rank = DetailPlate(row, "Rank", 24, 14, 60, 60, i == 0 ? VectorInteraction.Pink : VectorInteraction.Ink);
                NewText(rank, "Value", r.rank, 0, 0, 60, 60, 32, VectorInteraction.Ivory, TextAnchor.MiddleCenter);
                NewText(row, "Score", Number(r.score), 100, 7, 205, 42, r.score >= 10000000 ? 26 : 32);
                NewText(row, "Accuracy", Percent(r.accuracy), 305, 7, 155, 42, 29, VectorInteraction.Ink, TextAnchor.MiddleRight);
                string character = r.characterId == "light" ? "Light · " : r.characterId == "solid" ? "Solid · " : "";
                NewText(row, "Combo", character + T("stats.combo") + " " + r.maxCombo, 100, 51, 225, 30, 20, muted);
                NewText(row, "Date", date.ToString("dd.MM.yyyy"), 295, 51, 165, 30, 21, muted, TextAnchor.MiddleRight);
            }
        }

        static RectTransform DetailPlate(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rect = MakeRect(name, parent, x, y, w, h);
            var plate = rect.gameObject.AddComponent<VectorCutGraphic>();
            plate.color = color;
            plate.cutRatio = VectorCutGraphic.MenuSlope;
            plate.raycastTarget = false;
            return rect;
        }
    }
}
