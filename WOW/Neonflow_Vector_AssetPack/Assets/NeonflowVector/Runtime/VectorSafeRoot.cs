using UnityEngine;
namespace Neonflow.VectorUI {
[ExecuteAlways] public class VectorSafeRoot : MonoBehaviour {
 public Vector2 logicalSize=new Vector2(1920,1080);
 void LateUpdate(){var r=transform as RectTransform;var p=transform.parent as RectTransform;if(!r||!p)return;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=logicalSize;r.anchoredPosition=Vector2.zero;float s=Mathf.Min(p.rect.width/logicalSize.x,p.rect.height/logicalSize.y);r.localScale=Vector3.one*Mathf.Max(.001f,s);}
}}
