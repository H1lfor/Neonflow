using UnityEngine;
using UnityEngine.UI;
namespace Neonflow.VectorUI {
// A resizeable slanted plate. Camera images remain unskewed behind a matching Mask.
[AddComponentMenu("Neonflow/Vector Cut Graphic")]
[RequireComponent(typeof(CanvasRenderer),typeof(RectTransform))]
public class VectorCutGraphic : MaskableGraphic, ICanvasRaycastFilter {
 [Range(0, .45f)] public float cutRatio=.36f;
 public bool accent;
 public Color accentColor=new Color(0, .91f, .95f, 1);
 protected override void OnPopulateMesh(VertexHelper vh) {
  vh.Clear();var r=rectTransform.rect;float c=Mathf.Min(r.height*cutRatio,r.width*.24f);
  Quad(vh,new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMin,r.yMin),color);
  if(accent){float w=Mathf.Min(12,r.width*.025f);Quad(vh,new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMin+c+w,r.yMax),new Vector2(r.xMin+w,r.yMin),new Vector2(r.xMin,r.yMin),accentColor);}
 }
 static void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color col){int i=v.currentVertCount;v.AddVert(a,col,Vector2.zero);v.AddVert(b,col,Vector2.right);v.AddVert(c,col,Vector2.one);v.AddVert(d,col,Vector2.up);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);}
 public bool IsRaycastLocationValid(Vector2 p,Camera cam){Vector2 q;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,p,cam,out q))return false;var r=rectTransform.rect;float c=Mathf.Min(r.height*cutRatio,r.width*.24f),u=Mathf.InverseLerp(r.yMin,r.yMax,q.y);return q.y>=r.yMin&&q.y<=r.yMax&&q.x>=r.xMin+c*u&&q.x<=r.xMax-c+c*u;}
}}
