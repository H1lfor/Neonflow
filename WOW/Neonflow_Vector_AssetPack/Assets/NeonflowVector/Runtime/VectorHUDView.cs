using UnityEngine;
using UnityEngine.UI;
namespace Neonflow.VectorUI {
public class VectorHUDView:MonoBehaviour {
 public Image playerHP,robotHP,window;public RawImage webcam;
 public Text score,combo,timer,accuracy,judgement;public VectorFlipbook beatFX,judgementFX;
 public void SetHealth(float player,float robot){if(playerHP)playerHP.fillAmount=Mathf.Clamp01(player);if(robotHP)robotHP.fillAmount=Mathf.Clamp01(robot);}
 public void SetScore(long points,int streak){if(score)score.text=points.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));if(combo)combo.text="КОМБО "+streak;}
 public void SetRemaining(double seconds){int s=Mathf.Max(0,Mathf.CeilToInt((float)seconds));if(timer)timer.text=(s/60).ToString("00")+":"+(s%60).ToString("00");}
 public void SetAccuracy(float percent){if(accuracy)accuracy.text="Совпадение: "+Mathf.RoundToInt(Mathf.Clamp(percent,0,100))+"%";}
 public void SetWindow(float phase){if(window)window.fillAmount=Mathf.Clamp01(phase);}
 public void ShowJudgement(string grade,float percent){if(judgement)judgement.text=grade+"  "+Mathf.RoundToInt(percent)+"%";}
 // Host owns WebCamTexture lifecycle. Disable the Webcam/Unavailable group only once frames really arrive.
 public void SetCameraTexture(Texture frame){if(webcam)webcam.texture=frame;}
}}
