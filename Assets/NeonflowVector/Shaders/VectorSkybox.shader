Shader "Neonflow/Vector Skybox" {
 Properties {
  _Zenith("Zenith",Color)=(0.56,0.28,0.96,1)
  _Horizon("Horizon",Color)=(1,0.22,0.68,1)
  _SunColor("Sun",Color)=(1,0,0.55,1)
  _MountainColor("Mountains",Color)=(0.32,0.06,0.47,1)
  _GroundColor("Ground",Color)=(0.16,0.02,0.25,1)
  _GridColor("Grid",Color)=(1,0,0.55,1)
  _SunRadius("Sun angular radius",Range(2,35))=19
  _SunHeight("Sun elevation",Range(-5,55))=16
  _Rotation("Rotation",Range(0,360))=18
  _GridScale("Grid cell size",Float)=7
  _Scroll("Grid travel",Float)=0
  _BeatPulse("Beat accent",Range(0,1))=0
 }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;};struct v2f {float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
   float4 _Zenith,_Horizon,_SunColor,_MountainColor,_GroundColor,_GridColor;
   float _SunRadius,_SunHeight,_Rotation,_GridScale,_Scroll,_BeatPulse;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
   float tri(float x){return abs(frac(x)*2-1);}
   float4 frag(v2f i):SV_Target {
    float3 d=normalize(i.dir);float a=radians(_Rotation);d.xz=float2(d.x*cos(a)-d.z*sin(a),d.x*sin(a)+d.z*cos(a));
    float3 col=lerp(_Horizon.rgb,_Zenith.rgb,saturate(d.y*1.3));
    float h=radians(_SunHeight);float sd=dot(d,float3(0,sin(h),cos(h)));float edge=cos(radians(_SunRadius));float sun=smoothstep(edge-fwidth(sd),edge+fwidth(sd),sd);col=lerp(col,_SunColor.rgb,sun);
    float az=atan2(d.x,d.z)/6.2831853;float mountain=.025+.034*tri(az*10+.3)+.045*tri(az*18-.2)+.016*tri(az*34);
    float farMountain=step(d.y,mountain+.027);col=lerp(col,lerp(_MountainColor.rgb,_Zenith.rgb,.34),farMountain);
    col=lerp(col,_MountainColor.rgb,step(d.y,mountain));
    if(d.y<-.035){float3 p=d*(-8/min(-.001,d.y));float2 cell=(p.xz+float2(0,_Scroll))/max(1,_GridScale);float2 width=max(fwidth(cell),.0001);float2 g=abs(frac(cell-.5)-.5)/width;float grid=1-min(1,min(g.x,g.y));float fade=1-smoothstep(70,240,length(p.xz));col=lerp(_GroundColor.rgb,_GridColor.rgb,grid*fade*(.7+.12*_BeatPulse));}
    return float4(col,1);
   }
   ENDCG }
 }
 Fallback Off
}
