Shader "Custom/CyberGrid"
{
    Properties
    {
        _BgColor          ("Background Color", Color)    = (0.0, 0.0, 0.0, 1.0)
        [HDR] _GridColor  ("Grid Line Color",  Color)    = (0.0, 0.8, 1.0, 1.0)
        _LineThick        ("Line Thickness",   Range(0.001, 0.2)) = 0.02
        _GridTiling       ("Grid Tiling (X,Y)", Vector)  = (20, 20, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Blend Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BgColor;
            float4 _GridColor;
            float  _LineThick;
            float4 _GridTiling;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            float calcLine(float coord, float tiling, float thick)
            {
                float cell = coord * tiling;
                float d = abs(frac(cell) - 0.5);
                float w = fwidth(cell);
                return 1.0 - smoothstep(thick - w, thick + w, d);
            }

            float4 frag(v2f i) : SV_Target
            {
                float lx = calcLine(i.uv.x, _GridTiling.x, _LineThick);
                float ly = calcLine(i.uv.y, _GridTiling.y, _LineThick);
                float mask = max(lx, ly);
                float3 col = lerp(_BgColor.rgb, _GridColor.rgb, mask);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
