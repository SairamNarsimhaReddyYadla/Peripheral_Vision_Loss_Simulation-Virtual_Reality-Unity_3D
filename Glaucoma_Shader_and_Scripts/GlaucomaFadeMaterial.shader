//Shader Code for Glaucoma simulation in Unity Built-in Render Pipeline :)

Shader "Custom/GlaucomaFadeMaterial"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
       
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
        LOD 200

     

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 screenPos;
            float3 viewDir;
        };

        float3 _SpherePosition;
        float _SphereRadius;
        float _SphereSmoothness;

        half _Glossiness;
        
        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;

            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness;
            
            
            //Projection of the Sphere position into viewspace
            float4 pointInScreen = UnityObjectToClipPos(mul(unity_WorldToObject, float4(_SpherePosition, 1)));
            pointInScreen.xyz /= pointInScreen.w;

            //Get centred screen position
            float3 centredScreenPos = (IN.screenPos.xyz / IN.screenPos.w) * 2 - 1;

            //subtract the sphere screen position from the screen position (screen UVs)
            float2 circleUV = float2(centredScreenPos.x - pointInScreen.x, centredScreenPos.y + pointInScreen.y);

            //remapping based on screen dimensions 
            circleUV.x *= (_ScreenParams.x / _ScreenParams.y);

            //Fade effect
            float circleG = saturate(length(circleUV)*10 / (_SphereRadius));
            float circle = 1 - smoothstep(1 - max(_SphereSmoothness, 0.001), 1, circleG); //For CAT and GLA 

            //Scotoma Flip depending on the impairment.

            //float circle = smoothstep(1 - max(_SphereSmoothness, 0.001), 1, circleG); //For AMD

            o.Alpha = c.a * circle;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
