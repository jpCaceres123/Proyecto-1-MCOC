Shader "SQ4/Overlay"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; };
            fixed4 _Color;
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; return o; }
            fixed4 frag(Output i):SV_Target { return i.color; }
            ENDCG
        }
    }
}
