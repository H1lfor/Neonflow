using UnityEngine;
using UnityEngine.UI;
namespace Neonflow.VectorUI {
public class VectorPreviewAction:MonoBehaviour {
 public string target;
 void Start(){var b=GetComponent<Button>();if(b)b.onClick.AddListener(Invoke);}
 void Invoke(){var n=GetComponentInParent<VectorPreviewNavigator>();if(n)n.Show(target);}
}}
