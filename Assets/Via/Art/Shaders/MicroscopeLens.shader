Shader "Via/UI/MicroscopeLens"
{
    Properties
    {
        _MainTex ("Specimen", 2D) = "black" {}
        _Dispersion ("Radial RGB separation", Range(0, 0.04)) = 0.012
        _EdgeSoftness ("Edge defocus (texels)", Range(0, 4)) = 1.8
        _Vignette ("Edge light falloff", Range(0, 0.6)) = 0.22
        [HideInInspector] _DispersionScale ("Dispersion breathing", Float) = 1
    }
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
            float4 _MainTex_TexelSize;
            float _Dispersion, _EdgeSoftness, _Vignette, _DispersionScale;

            float3 Specimen(float2 uv)
            {
                float4 pixel = tex2D(_MainTex, uv);
                // The optical field is presented over black, including transparent source edges.
                return pixel.rgb * pixel.a;
            }

            float3 SoftSample(float2 uv, float2 blur)
            {
                return Specimen(uv) * 0.5 +
                    (Specimen(uv - blur) + Specimen(uv + blur)) * 0.25;
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 fromCenter = input.uv - 0.5;
                float radius = length(fromCenter) * 2;
                float edge = smoothstep(0.52, 0.94, radius);
                edge *= edge;
                // Different magnifications per wavelength give lateral chromatic aberration.
                // The center stays registered; specimen contours and the aperture split near the rim.
                float2 dispersion = fromCenter * (_Dispersion * _DispersionScale * edge);
                float2 radial = fromCenter / max(length(fromCenter), 0.001);
                float2 blur = radial * _MainTex_TexelSize.xy * (_EdgeSoftness * edge);
                float red = SoftSample(input.uv - dispersion, blur).r;
                float green = SoftSample(input.uv, blur).g;
                float blue = SoftSample(input.uv + dispersion, blur).b;
                float lightFalloff = 1 - _Vignette * smoothstep(0.76, 0.97, radius);
                return fixed4(float3(red, green, blue) * lightFalloff, 1);
            }
            ENDCG
        }
    }
}
