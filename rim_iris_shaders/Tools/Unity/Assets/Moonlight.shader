Shader "Hidden/RimIrisShaders/Moonlight"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _RimIris_SunGlow;
            // Framework options are globals, not material Properties.
            float _RimIris_Pack_rimirisshaders_moonlight_strength;
            float _RimIris_Pack_rimirisshaders_moonlight_brightness;
            float _RimIris_Pack_rimirisshaders_moonlight_coolness;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float night = 1.0 - smoothstep(0.05, 0.55, _RimIris_SunGlow);
                float amount = night * saturate(_RimIris_Pack_rimirisshaders_moonlight_strength);
                float3 c = saturate(source.rgb);
                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                float shadow = 1.0 - smoothstep(0.25, 0.85, luma);
                // Lift existing detail without lifting true black or clipping lamps.
                float gain = 1.0 + max(0.0, _RimIris_Pack_rimirisshaders_moonlight_brightness) * shadow;
                float3 lifted = c * gain / (1.0 + c * (gain - 1.0));
                float cool = saturate(_RimIris_Pack_rimirisshaders_moonlight_coolness) * shadow;
                float3 graded = lifted * lerp(float3(1, 1, 1), float3(0.86, 0.96, 1.0), cool);
                graded.b += (1.0 - graded.b) * luma * cool * 0.18;
                return float4(lerp(source.rgb, graded, amount), source.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
