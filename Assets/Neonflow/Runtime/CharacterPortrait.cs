using UnityEngine;
using UnityEngine.UI;

namespace Neonflow
{
    public sealed class CharacterPortrait : MonoBehaviour
    {
        GameObject stage;
        RenderTexture texture;
        public void Setup(string id, int slot)
        {
            stage = new GameObject("Portrait stage " + id);
            stage.transform.position = new Vector3(1000 + slot * 8, 0, 0);
            var model = Instantiate(Resources.Load<RobotRig>("Characters/" + id), stage.transform, false);
            model.transform.localRotation = Quaternion.Euler(0, 163, 0);
            model.Ready();
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            var camera = new GameObject("Portrait camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 1, -4);
            camera.orthographic = true; camera.orthographicSize = 1.03f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30; camera.nearClipPlane = .1f; camera.farClipPlane = 8;
            camera.allowHDR = false; camera.allowMSAA = true;
            texture = new RenderTexture(640, 896, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Portrait " + id };
            texture.Create(); camera.targetTexture = texture;
            var image = GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
        }
        void OnDisable() { if (stage) stage.SetActive(false); }
        void OnEnable() { if (stage) stage.SetActive(true); }
        void OnDestroy()
        {
            if (stage) Destroy(stage);
            if (texture) { texture.Release(); Destroy(texture); }
        }
    }
}
