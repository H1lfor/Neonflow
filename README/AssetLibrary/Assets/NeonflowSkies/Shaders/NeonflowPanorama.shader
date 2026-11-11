Shader "Neonflow/Synthwave Skybox"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (2:1)", 2D) = "grey" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Exposure ("Exposure", Range(0,4)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
        _SeamBlend ("Rear seam blend", Range(0.005,0.15)) = 0.04
        _PoleBlend ("Pole smoothing", Range(0.01,0.3)) = 0.12
        _HorizonOffset ("Horizon alignment", Range(-0.2,0.2)) = -0.06
        _VerticalScale ("Vertical framing", Range(1,2.5)) = 1
        _HorizontalScale ("Horizontal framing", Range(1,4)) = 1
        [Toggle] _GridEnabled ("Geometric grid floor", Float) = 0
        _GridColor ("Grid color", Color) = (0.2,0.9,1,1)
        _GroundColor ("Ground color", Color) = (0.015,0.018,0.06,1)
        [Toggle] _RetroSun ("Round retro sun", Float) = 0
        _SunRadius ("Sun radius (degrees)", Range(4,25)) = 12
        _SunElevation ("Sun elevation (degrees)", Range(0,35)) = 12
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Tint;
            float _Exposure, _Rotation, _SeamBlend, _PoleBlend, _HorizonOffset;
            float _VerticalScale, _HorizontalScale, _GridEnabled;
            float4 _GridColor, _GroundColor;
            float _RetroSun, _SunRadius, _SunElevation;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o;
            }
            float3 sampleSky(float2 uv)
            {
                // Explicit LOD avoids derivative jumps at the rear wrap and poles.
                float2 q=clamp(uv,0.5*_MainTex_TexelSize.xy,1-0.5*_MainTex_TexelSize.xy);
                return tex2Dlod(_MainTex,float4(q,0,0)).rgb;
            }
            float triWave(float x)
            {
                return 1-abs(frac(x)*2-1);
            }
            float3 retroSky(float3 d)
            {
                // Direction-space billboard: one radius in both axes, no panorama distortion.
                float elevation=_SunElevation*UNITY_PI/180;
                float radius=tan(_SunRadius*UNITY_PI/180);
                float3 sunDirection=float3(0,sin(elevation),cos(elevation));
                float3 sunUp=float3(0,cos(elevation),-sin(elevation));
                float facing=dot(d,sunDirection);
                float2 s=float2(d.x,dot(d,sunUp))/max(facing,0.0001)/radius;
                float dist=length(s);
                float edge=max(fwidth(dist),0.001);
                float disc=(1-smoothstep(1-edge,1+edge,dist))*step(0,facing);
                float latitude=max(d.y,0);
                float3 sky=lerp(float3(1,.12,.4),float3(.24,.015,.39),smoothstep(0,.30,latitude));
                sky=lerp(sky,float3(.025,.025,.11),smoothstep(.20,.85,latitude));
                float glow=exp(-max(dist-1,0)*4)*step(0,facing)*.10;
                sky+=float3(.8,.12,.23)*glow;
                float sy=saturate(s.y*.5+.5);
                float3 sun=lerp(float3(1,.015,.47),float3(1,.91,.15),sy);
                float stripeCoord=(s.y+1)*8;
                float stripeAA=max(fwidth(stripeCoord),.005);
                float gapWidth=lerp(.48,.09,sy);
                float gapDistance=abs(frac(stripeCoord)-.5);
                float stripes=smoothstep(gapWidth*.5-stripeAA,gapWidth*.5+stripeAA,gapDistance);
                stripes=lerp(stripes,1,smoothstep(.4,.62,s.y));
                sky=lerp(sky,sun,disc*stripes);
                float angle=atan2(d.x,d.z)/(2*UNITY_PI);
                float ridge=.012+.025*triWave(angle*13+.2)+.013*triWave(angle*31+.7)+.007*triWave(angle*61+.1);
                float mountainAA=max(fwidth(d.y-ridge),.0006);
                float mountains=1-smoothstep(ridge-mountainAA,ridge+mountainAA,d.y);
                float3 rock=lerp(float3(.07,.016,.17),float3(.16,.025,.27),saturate(d.y/.06));
                sky=lerp(sky,rock,mountains);
                sky+=float3(.62,.025,.32)*exp(-abs(d.y)*150)*.3;
                return sky;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float ang=_Rotation*UNITY_PI/180.0;
                d.xz=float2(d.x*cos(ang)-d.z*sin(ang),d.x*sin(ang)+d.z*cos(ang));
                float u=frac(atan2(d.x*_HorizontalScale,d.z)/(2*UNITY_PI)+0.5);
                float v=asin(clamp(d.y,-1,1))/UNITY_PI+0.5;
                // Shift the horizon while preserving the top/bottom endpoints.
                float tv=saturate(0.5+(v-0.5)*_VerticalScale+_HorizonOffset*4*v*(1-v));
                float seam=1-smoothstep(0,max(_SeamBlend,0.001),min(u,1-u));
                float3 col=lerp(sampleSky(float2(u,tv)),sampleSky(float2(1-u,tv)),0.5*seam);
                // Longitude-independent color at the zenith/nadir removes pinching.
                float pv=v<0.5 ? 0.0 : 1.0;
                float3 pole=(sampleSky(float2(0.125,pv))+sampleSky(float2(0.375,pv))+sampleSky(float2(0.625,pv))+sampleSky(float2(0.875,pv)))*0.25;
                float poleWeight=1-smoothstep(0,max(_PoleBlend,0.001),min(v,1-v));
                col=lerp(col,pole,poleWeight);
                if (_RetroSun > .5) col=retroSky(d);
                if (_GridEnabled > 0.5 && d.y < 0)
                {
                    float2 plane=d.xz/max(-d.y,0.0001);
                    float2 g=plane*0.45;
                    float2 deriv=max(fwidth(g),0.00001);
                    float2 dist=abs(frac(g+0.5)-0.5)/deriv;
                    float lineAmount=1-smoothstep(0.45,1.45,min(dist.x,dist.y));
                    float farFade=1-smoothstep(35,110,length(plane));
                    float nearFade=smoothstep(0.3,2.2,length(plane));
                    float horizonHaze=exp(d.y*35)*0.14;
                    float3 ground=_GroundColor.rgb+_GridColor.rgb*(lineAmount*farFade*nearFade*0.8+horizonHaze);
                    col=lerp(col,ground,smoothstep(0,0.04,-d.y));
                }
                return float4(col*_Tint.rgb*_Exposure,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
