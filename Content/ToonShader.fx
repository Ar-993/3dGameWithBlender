#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix World;
matrix View;
matrix Projection;
float3 LightDirection;
float3 CameraPosition;
texture ModelTexture;

#define MAX_BONES 72
matrix Bones[MAX_BONES];

sampler2D TextureSampler = sampler_state
{
    Texture = <ModelTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;
    int4 BoneIndices : BLENDINDICES0;
    float4 BoneWeights : BLENDWEIGHT0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
    float3 WorldPosition : TEXCOORD2;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    matrix skinTransform = 0;
    skinTransform += Bones[input.BoneIndices.x] * input.BoneWeights.x;
    skinTransform += Bones[input.BoneIndices.y] * input.BoneWeights.y;
    skinTransform += Bones[input.BoneIndices.z] * input.BoneWeights.z;
    skinTransform += Bones[input.BoneIndices.w] * input.BoneWeights.w;
    float4 skinnedPosition = mul(input.Position, skinTransform);
    float3 skinnedNormal = mul(input.Normal, (float3x3)skinTransform);
    float4 worldPosition = mul(skinnedPosition, World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldPosition = worldPosition.xyz;
    output.Normal = mul(skinnedNormal, (float3x3)World);
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float3 N = normalize(input.Normal);
    float3 L = normalize(-LightDirection);
    float3 V = normalize(CameraPosition - input.WorldPosition);
    float NdotL = max(0.0, dot(N, L));
    float lightIntensity = 0.3;
    if (NdotL > 0.6) lightIntensity = 1.0;
    else if (NdotL > 0.2) lightIntensity = 0.6;
    float rim = 1.0 - max(0.0, dot(V, N));
    rim = smoothstep(0.6, 1.0, rim) * 0.5;
    float4 texColor = tex2D(TextureSampler, input.TextureCoordinate);
    float3 finalColor = texColor.rgb * lightIntensity + rim.xxx;
    return float4(finalColor, texColor.a);
}

technique ToonTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
