using UnityEngine;
using UnityEngine.EventSystems;

namespace Neonflow.VectorUI
{
    public class VectorButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        public VectorFlipbook hoverFX, pressFX;
        public bool reducedMotion;
        public float hoverOffset = 14;
        RectTransform rect;
        Vector2 rest;
        bool selected, pressed;
        void Awake()
        {
            rect = transform as RectTransform;
            rest = rect.anchoredPosition;
        }

        void OnDisable()
        {
            if (rect)
                rect.anchoredPosition = rest;
            selected = pressed = false;
        }

        void Update()
        {
            if (!rect)
                return;
            var goal = rest + Vector2.left * (reducedMotion ? 0 : pressed ? 4 : selected ? hoverOffset : 0);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, goal, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 24));
        }

        void Select(bool value)
        {
            if (value && !selected && !reducedMotion && hoverFX)
                hoverFX.Play();
            selected = value;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            Select(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Select(false);
            pressed = false;
        }

        public void OnSelect(BaseEventData e)
        {
            Select(true);
        }

        public void OnDeselect(BaseEventData e)
        {
            Select(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            if (!reducedMotion && pressFX)
                pressFX.Play();
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
        }
    }
}
