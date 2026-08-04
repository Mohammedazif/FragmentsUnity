Shader "FragmentsUnity/VertexColor"
{
    Properties
    {
        _Cull ("Cull", Float) = 2
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull [_Cull]
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        CGPROGRAM
        #pragma surface surf Lambert keepalpha
        #pragma multi_compile_instancing

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
            o.Alpha = IN.color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
