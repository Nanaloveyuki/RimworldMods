#version 450

layout(location = 0) in VertexData
{
    vec4 Color;
    vec2 UV;
} vertex;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0, std140) uniform FrostParams
{
    vec4 _Shape; // UI width, height, corner radius, border width.
    vec4 _Frost; // Enabled, blur sigma, transparency, native saturation.
    vec4 _Fill;
};
layout(set = 2, binding = 1) uniform sampler2D _SceneTex;

void main()
{
    // Material draws provide bottom-origin UVs; scene targets and gl_FragCoord
    // both use the native Vulkan top origin. No Unity backbuffer flip or crop.
    vec2 local = vec2(vertex.UV.x, 1.0 - vertex.UV.y) * _Shape.xy;
    float radius = min(_Shape.z, min(_Shape.x, _Shape.y) * 0.5);
    vec2 q = abs(local - _Shape.xy * 0.5) - (_Shape.xy * 0.5 - radius);
    float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    float aa = max(fwidth(distance), 0.001);
    float coverage = 1.0 - smoothstep(-aa, aa, distance);
    float border = smoothstep(-_Shape.w - aa, -_Shape.w + aa, distance);
    vec3 rgb = _Fill.rgb;
    float alpha = 1.0 - _Frost.z;
    if (_Frost.x > 0.5)
    {
        vec2 size = vec2(textureSize(_SceneTex, 0));
        vec2 uv = gl_FragCoord.xy / size;
        vec3 scene = textureLod(_SceneTex, uv, 0.0).rgb;
        if (_Frost.y > 0.01)
        {
            // Fixed symmetric Gaussian footprint, 25 bilinear samples. Half-texel
            // offsets keep integer radii from repeatedly sampling one checker phase.
            scene = vec3(0.0);
            const float weights[5] = float[5](0.06136, 0.24477, 0.38774, 0.24477, 0.06136);
            for (int y = 0; y < 5; ++y)
                for (int x = 0; x < 5; ++x)
                {
                    vec2 tap = vec2(x - 2, y - 2);
                    vec2 offset = tap * (_Frost.y * 2.0) + sign(tap) * min(0.5, _Frost.y);
                    scene += textureLod(_SceneTex, uv + offset / size, 0.0).rgb * weights[x] * weights[y];
                }
        }
        float luminance = dot(scene, vec3(0.22, 0.707, 0.071));
        scene = clamp(mix(vec3(luminance), scene, _Frost.w), 0.0, 1.0);
        rgb = mix(scene, _Fill.rgb, alpha);
        alpha = 1.0;
    }
    float borderAlpha = min(1.0, (1.0 - _Frost.z) * 1.6) * border;
    float resultAlpha = borderAlpha + alpha * (1.0 - borderAlpha);
    vec3 premultiplied = _Fill.rgb * 0.55 * borderAlpha + rgb * alpha * (1.0 - borderAlpha);
    outColor = vec4(premultiplied / max(resultAlpha, 0.00001), resultAlpha * coverage) * vertex.Color;
}
