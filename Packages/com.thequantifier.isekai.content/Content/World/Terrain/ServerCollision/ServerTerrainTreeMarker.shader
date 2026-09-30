Shader "Nature/Soft Occlusion Leaves"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0)
        _MainTex ("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TreeLeaf"
        }

        Pass
        {
            ColorMask 0
            ZWrite Off
            Cull Off
        }
    }

    Fallback Off
}
