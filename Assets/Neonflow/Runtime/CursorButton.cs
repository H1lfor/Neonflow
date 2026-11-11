using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Neonflow
{
    public class CursorButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public NeonflowCursorController controller;
        public System.Action Hover;
        bool inside;
        public void OnPointerEnter(PointerEventData e)
        {
            inside = true;
            if (GetComponent<Button>().interactable)
            {
                controller?.SetHover();
                Hover?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            inside = false;
            controller?.SetDefault();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (GetComponent<Button>().interactable)
                controller?.SetPressed();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (inside)
                controller?.SetHover();
            else
                controller?.SetDefault();
        }

        void OnDisable()
        {
            controller?.SetDefault();
        }
    }

    public class StagePulse : MonoBehaviour
    {
        public RobotView robot;
        public Material material;
        public Color accent;
        Vector3 basis;
        void Start()
        {
            basis = transform.localScale;
        }

        void Update()
        {
            if (!robot || !material)
                return;
            float p = Mathf.Pow(Mathf.Max(0, 1 - (float)(robot.Beat - Mathf.Floor((float)robot.Beat))), 4);
            material.SetColor("_EmissionColor", accent * (.65f + p));
            transform.localScale = new Vector3(basis.x, basis.y * (1 + .4f * p), basis.z);
        }
    }
}
