using UnityEngine;
using UnityEngine.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        void PlayCharacterChoice()
        {
            var focused = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            ChooseCharacter(focused && focused.name.StartsWith("Choose_", System.StringComparison.Ordinal) ? focused.name.Substring(7) : SelectedCharacter.id);
        }
        void ChooseCharacter(string id)
        {
            if (NavigationBusy || ending || state != ScreenState.Characters) return;
            var choice = characters.Get(id);
            save.settings.characterId = choice.id;
            robot.SetCharacter(choice.id);
            Persist();
            cameraReturn = ScreenState.Songs;
            if (calibrated && pendingTutorial)
            {
                pendingTutorial = false;
                StartSong(true);
            }
            else Navigate(calibrated ? ScreenState.Songs : ScreenState.Camera);
        }

        RectTransform CharacterScreen()
        {
            var layout = MakeRect("Characters", screenLayer, 0, 0, 1920, 1080);
            layout.gameObject.AddComponent<VectorSafeRoot>().contentScale = .8f;
            void Plate(string name, float x, float y, float w, float h, Color color)
            {
                var g = MakeRect(name, layout, x, y, w, h).gameObject.AddComponent<VectorCutGraphic>();
                g.color = color; g.cutRatio = VectorCutGraphic.MenuSlope; g.raycastTarget = false;
            }
            Plate("Header", 40, 98, 1000, 110, VectorInteraction.Ivory);
            NewText(layout, "Heading", T("characters.title"), 96, 108, 880, 90, 60);
            NewText(layout, "Subtitle", T("characters.subtitle"), 135, 218, 1650, 48, 28, VectorInteraction.Ivory);
            for (int i = 0; i < characters.characters.Length; i++)
            {
                var profile = characters.characters[i];
                float x = 115 + i * 885;
                Plate(profile.id + "Card", x, 294, 815, 616, VectorInteraction.Ivory);
                NewText(layout, profile.id + "Name", T(profile.nameKey), x + 165, 317, 555, 58, 46);
                NewText(layout, profile.id + "Description", T(profile.descriptionKey), x + 355, 403, 350, 90, 25);
                var portrait = MakeRect(profile.id + "Portrait", layout, x + 40, 387, 300, 420).gameObject.AddComponent<RawImage>();
                portrait.gameObject.AddComponent<CharacterPortrait>().Setup(profile.id, i);
                NewText(layout, profile.id + "Poses", T("characters.poses"), x + 355, 511, 320, 36, 24);
                for (int p = 0; p < profile.poses.Length; p++)
                {
                    var icon = MakeRect(profile.id + "_" + profile.poses[p], layout, x + 355 + p % 3 * 88, 550 + p / 3 * 78, 68, 68).gameObject.AddComponent<Image>();
                    icon.sprite = VectorAsset<Sprite>("Movements/Poses/" + profile.poses[p]); icon.color = VectorInteraction.Ink; icon.preserveAspect = true; icon.raycastTarget = false;
                }
                NewText(layout, profile.id + "Hands", T("characters.hands"), x + 330, 710, 320, 34, 24);
                for (int h = 0; h < profile.hands.Length; h++)
                {
                    var icon = MakeRect(profile.id + "_" + profile.hands[h], layout, x + 330 + h * 60, 754, 52, 52).gameObject.AddComponent<Image>();
                    icon.sprite = VectorAsset<Sprite>("Movements/Hands/" + profile.hands[h]); icon.color = VectorInteraction.Ink; icon.preserveAspect = true; icon.raycastTarget = false;
                }
                var button = ExtraButton(layout, "Choose_" + profile.id, T("characters.choose"), x + 134, 831, 475, 62, () => ChooseCharacter(profile.id), 29);
                button.GetComponent<VectorInteraction>().normal = VectorInteraction.Pink;
                button.GetComponent<VectorCutGraphic>().color = VectorInteraction.Pink;
                button.GetComponentInChildren<Text>().color = VectorInteraction.Ivory;
            }
            ExtraButton(layout, "Back", T("common.back"), 80, 963, 330, 64, Back, 28);
            NewText(layout, "EqualRules", T("characters.rules"), 640, 958, 1160, 64, 26, VectorInteraction.Ivory, TextAnchor.MiddleRight);
            return layout;
        }
    }
}
