Shader "Custom/SBSSplit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SBSMode ("SBS Mode", Float) = 0
        _SwapEyes ("Swap Eyes", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _SBSMode;
            float _SwapEyes;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float2 ApplySbsUv(float2 uv)
            {
                #if defined(UNITY_SINGLE_PASS_STEREO)
                uint eyeIndex = unity_StereoEyeIndex;
                #else
                uint eyeIndex = 0;
                #endif

                if (_SwapEyes > 0.5)
                {
                    eyeIndex = 1 - eyeIndex;
                }

                float2 sbsUv = uv;
                sbsUv.x = uv.x * 0.5 + (eyeIndex == 0 ? 0.0 : 0.5);
                return sbsUv;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                if (_SBSMode > 0.5)
                {
                    uv = ApplySbsUv(uv);
                }
                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
