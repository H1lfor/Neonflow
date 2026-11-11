using UnityEngine;
using UnityEngine.UI;
namespace Neonflow.VectorUI {
// Presentation demo only. Replace these actions with game navigation.
public class VectorPreviewNavigator : MonoBehaviour {
 public GameObject[] screens; public string current="MainMenu";
 void Start(){Show(current);}
 public void Show(string id){if(id=="QUIT")return;foreach(var screen in screens)if(screen)screen.SetActive(screen.name==id);current=id;}
}
}
