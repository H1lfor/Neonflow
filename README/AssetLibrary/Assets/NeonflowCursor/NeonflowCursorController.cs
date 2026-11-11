using UnityEngine;

namespace Neonflow
{
    public class NeonflowCursorController : MonoBehaviour
    {
        public Texture2D defaultTexture;
        public Texture2D hoverTexture;
        public Texture2D pressedTexture;
        public Vector2 hotspot = new Vector2(10, 8);
        void OnEnable() { SetDefault(); }
        void OnDisable() { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
        public void SetDefault() { Apply(defaultTexture); }
        public void SetHover() { Apply(hoverTexture != null ? hoverTexture : defaultTexture); }
        public void SetPressed() { Apply(pressedTexture != null ? pressedTexture : defaultTexture); }
        void Apply(Texture2D texture) { Cursor.SetCursor(texture, texture != null ? hotspot : Vector2.zero, CursorMode.Auto); }
    }
}
