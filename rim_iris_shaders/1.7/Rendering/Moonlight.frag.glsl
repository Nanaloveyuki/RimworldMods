#version 450

// Graphics.Blit uses the engine's SceneDefault mesh vertex shader.
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0, std140) uniform MoonlightParams
{
    // No authored defaults: values come from RimIris global uniforms.
    float _RimIris_SunGlow;
    float _RimIris_Pack_rimirisshaders_moonlight_strength;
    float _RimIris_Pack_rimirisshaders_moonlight_brightness;
    float _RimIris_Pack_rimirisshaders_moonlight_coolness;
};
layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    // Native render textures use a top origin; the blit mesh uses bottom-origin UVs.
    vec4 source = texture(_MainTex, vec2(vUv.x, 1.0 - vUv.y));
    float night = 1.0 - smoothstep(0.05, 0.55, _RimIris_SunGlow);
    float amount = night * clamp(_RimIris_Pack_rimirisshaders_moonlight_strength, 0.0, 1.0);
    vec3 c = clamp(source.rgb, 0.0, 1.0);
    float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));
    float shadow = 1.0 - smoothstep(0.25, 0.85, luma);
    float gain = 1.0 + max(0.0, _RimIris_Pack_rimirisshaders_moonlight_brightness) * shadow;
    vec3 lifted = c * gain / (1.0 + c * (gain - 1.0));
    float cool = clamp(_RimIris_Pack_rimirisshaders_moonlight_coolness, 0.0, 1.0) * shadow;
    vec3 graded = lifted * mix(vec3(1.0), vec3(0.86, 0.96, 1.0), cool);
    graded.b += (1.0 - graded.b) * luma * cool * 0.18;
    outColor = vec4(mix(source.rgb, graded, amount), source.a);
}
