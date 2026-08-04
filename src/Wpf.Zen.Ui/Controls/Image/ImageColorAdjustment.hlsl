// Compile with: fxc /T ps_2_0 /E main /Fo ImageColorAdjustment.ps ImageColorAdjustment.hlsl
//
// c0..c2 contain affine RGB rows. The fourth component is an offset which is multiplied by source
// alpha. WPF effect textures use premultiplied alpha, so this preserves transparent pixels and the
// anti-aliased edge produced by the rounded clip.

sampler2D InputSampler : register(s0);
float4 RedTransform : register(c0);
float4 GreenTransform : register(c1);
float4 BlueTransform : register(c2);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 source = tex2D(InputSampler, uv);
    float4 premultiplied = float4(source.rgb, source.a);
    float3 adjusted = float3(
        dot(premultiplied, RedTransform),
        dot(premultiplied, GreenTransform),
        dot(premultiplied, BlueTransform)
    );

    return float4(saturate(adjusted), source.a);
}