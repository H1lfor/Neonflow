using UnityEngine;
namespace Neonflow.VectorUI {
public class VectorSkyMotion:MonoBehaviour {
 public Material material;public bool menuMode=true,reducedMotion;public float menuSpeed=.8f;
 Material instance,previous;double songTime;
 void OnEnable(){if(!material)return;previous=RenderSettings.skybox;instance=new Material(material);RenderSettings.skybox=instance;}
 public void SampleSong(double time,float beatPhase){songTime=time;if(instance){instance.SetFloat("_Scroll",reducedMotion?0:(float)(songTime*.8));instance.SetFloat("_BeatPulse",reducedMotion?0:Mathf.Exp(-Mathf.Clamp01(beatPhase)*12));}}
 void Update(){if(menuMode&&instance)instance.SetFloat("_Scroll",reducedMotion?0:(float)Time.unscaledTimeAsDouble*menuSpeed);}
 void OnDisable(){if(RenderSettings.skybox==instance)RenderSettings.skybox=previous;if(instance)Destroy(instance);}
}}
