using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Neonflow.VectorUI;

namespace Neonflow
{
    // Keeps keyboard focus independent of mouse hover; uses supplied frame effects.
    public class VectorInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public VectorFlipbook hoverFX, pressFX;
        public Action Hover;
        public Action FocusEntered;
        public NeonflowCursorController cursor;
        public bool reducedMotion, songSelected, songRow;
        public float entranceDelay = -1;
        public Color normal;
        public static readonly Color Ink = new Color32(37, 7, 51, 255), Ivory = new Color32(255, 242, 225, 255), Pink = new Color32(255, 0, 140, 255), Cyan = new Color32(0, 232, 242, 255);
        RectTransform rect;
        Selectable control;
        VectorCutGraphic plate;
        CanvasGroup group;
        Vector2 rest;
        Vector2 restSize;
        bool inside, focused, pressed;
        float entered;
        public void Configure()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
            restSize = rect.sizeDelta;
            control = GetComponent<Selectable>();
            plate = GetComponent<VectorCutGraphic>();
            if (plate)
                normal = plate.color;
            group = GetComponent<CanvasGroup>();
            if (!group)
                group = gameObject.AddComponent<CanvasGroup>();
            entered = Time.unscaledTime;
            if (control)
            {
                control.transition = Selectable.Transition.None;
                if (control.targetGraphic)
                    control.targetGraphic.CrossFadeColor(Color.white, 0, true, true);
            }
        }

        bool Enabled => control && control.IsInteractable();

        bool Active => inside || focused;

        public void SetSelected(bool value)
        {
            songSelected = value;
        }

        void Accent(bool old)
        {
            if (Enabled && Active && !old)
            {
                Hover?.Invoke();
                if (!reducedMotion && hoverFX)
                    hoverFX.Play();
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            bool old = Active;
            inside = true;
            Accent(old);
            if (Enabled)
                cursor?.SetHover();
        }

        public void OnPointerExit(PointerEventData e)
        {
            inside = false;
            pressed = false;
            cursor?.SetDefault();
        }

        public void OnSelect(BaseEventData e)
        {
            bool old = Active;
            focused = true;
            Accent(old);
            FocusEntered?.Invoke();
        }

        public void OnDeselect(BaseEventData e)
        {
            focused = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Enabled)
                return;
            pressed = true;
            cursor?.SetPressed();
            if (!reducedMotion && pressFX)
                pressFX.Play();
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            if (inside)
                cursor?.SetHover();
            else
                cursor?.SetDefault();
        }

        void Update()
        {
            if (!rect)
                return;
            float enter = entranceDelay < 0 || reducedMotion ? 1 : Mathf.Clamp01((Time.unscaledTime - entered - entranceDelay) / .22f);
            float eased = 1 - Mathf.Pow(1 - enter, 3);
            float shift = songRow && songSelected ? -80 : 0;
            if (!reducedMotion && Enabled)
                shift -= pressed ? 4 : Active ? 14 : 0;
            Vector2 target = rest + new Vector2(shift + 48 * (1 - eased), 0);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 24));
            if (songRow)
                rect.sizeDelta = Vector2.Lerp(rect.sizeDelta, restSize + new Vector2(songSelected ? 80 : 0, 0), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 24));
            group.alpha = eased;
            if (plate)
            {
                Color value = songRow ? (songSelected ? Pink : Ivory) : normal;
                if (!Enabled)
                    value = new Color32(222, 201, 228, 255);
                else if (pressed)
                    value = Color.Lerp(value, Ink, .18f);
                else if (Active)
                    value = Color.Lerp(value, Cyan, .12f);
                plate.color = value;
                plate.accent = Enabled && (songSelected || Active || normal == Pink);
                if (songRow)
                    foreach (var t in GetComponentsInChildren<Text>())
                        t.color = songSelected ? Ivory : Ink;
            }
        }

        void OnDisable()
        {
            cursor?.SetDefault();
            inside = focused = pressed = false;
        }
    }
}
