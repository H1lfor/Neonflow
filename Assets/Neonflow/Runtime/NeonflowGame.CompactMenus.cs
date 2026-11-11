using UnityEngine;
using UnityEngine.UI;
using Neonflow.VectorUI;

namespace Neonflow
{
    public partial class NeonflowGame
    {
        void CompactMenu(RectTransform layout, string screen)
        {
            if (screen == "Gameplay")
                return;
            layout.GetComponent<VectorSafeRoot>().contentScale = screen == "ConfirmExit" ? .72f : screen == "Pause" ? .75f : screen == "TrackSelect" ? .82f : .8f;
            var dim = layout.Find("Dim") as RectTransform;
            if (dim)
            {
                dim.SetParent(screenLayer, false);
                Stretch(dim);
                dim.SetSiblingIndex(layout.GetSiblingIndex());
            }

            var webcam = layout.Find("Webcam");
            foreach (var graphic in layout.GetComponentsInChildren<VectorCutGraphic>(true))
                if (!webcam || !graphic.transform.IsChildOf(webcam))
                    graphic.cutRatio = VectorCutGraphic.MenuSlope;
            void Rect(string name, float x, float y, float w, float h)
            {
                var r = layout.Find(name) as RectTransform;
                if (!r)
                    return;
                r.anchoredPosition = new Vector2(x, -y);
                r.sizeDelta = new Vector2(w, h);
            }

            void Font(string name, int size)
            {
                var t = layout.Find(name)?.GetComponent<Text>();
                if (t)
                    t.fontSize = size;
            }

            if (layout.Find("Header"))
            {
                Rect("Header", 40, 98, 900, 110);
                Rect("Heading", 96, 108, 800, 90);
                Font("Heading", 60);
                Font("Brand", 28);
            }

            switch (screen)
            {
                case "MainMenu":
                    Rect("LogoPlate", 120, 120, 1000, 219);
                    Rect("Logo", 175, 141, 890, 149);
                    var names = new[]
                    {
                        "Play",
                        "Settings",
                        "Tutorial",
                        "Exit"
                    };
                    for (int i = 0; i < names.Length; i++)
                    {
                        float w = 780 - 40 * i;
                        Rect(names[i], 900 - 28 * i, 430 + 118 * i, w, 96);
                        layout.Find(names[i]).localRotation = Quaternion.identity;
                        Rect(names[i] + "/Label", 48, 0, w - 85, 96);
                        Font(names[i] + "/Label", 60);
                        foreach (var fx in layout.Find(names[i]).GetComponentsInChildren<VectorFlipbook>(true))
                            Stretch((RectTransform)fx.transform);
                    }

                    break;
                case "TrackSelect":
                    Rect("ResultsPanel", -70, 268, 860, 630);
                    break;
                case "Settings":
                case "CameraSettings":
                case "ControlSettings":
                    Rect("SettingsPanel", 480, 338, 1400, 570);
                    break;
                case "Calibration":
                    Rect("InstructionPanel", 1140, 294, 720, 610);
                    Rect("Instruction", 1310, 340, 450, 130);
                    Font("Instruction", 36);
                    Rect("Framing", 1280, 495, 430, 110);
                    Font("Framing", 30);
                    Font("Ready/Label", 32);
                    break;
                case "Tutorial":
                    Rect("ExplanationPanel", -20, 300, 1140, 590);
                    break;
                case "Pause":
                    Rect("PausePanel", 480, 180, 960, 720);
                    Rect("Heading", 710, 230, 550, 100);
                    Font("Heading", 64);
                    break;
                case "ResultsVictory":
                case "ResultsDefeat":
                    for (int i = 0; i < 3; i++)
                        Rect("Value" + i, 1190, 520 + 88 * i, 280, 60);
                    break;
            }
        }
    }
}
