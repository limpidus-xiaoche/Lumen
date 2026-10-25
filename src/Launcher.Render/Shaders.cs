namespace Launcher.Render;

internal static class Shaders
{
    public const string SkyVertex = """
#version 300 es
precision mediump float;
out vec2 vUv;
void main()
{
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    vUv = p;
    gl_Position = vec4(p * 2.0 - 1.0, 0.9999, 1.0);
}
""";

    // 末地虚空：近黑底 + 极淡紫灰带 + 星点
    public const string SkyFragment = """
#version 300 es
precision mediump float;
in vec2 vUv;
uniform vec3 uTop;
uniform vec3 uBottom;
uniform float uTime;
out vec4 fragColor;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void main()
{
    float y = clamp(vUv.y, 0.0, 1.0);
    vec3 c = mix(uBottom, uTop, pow(y, 2.2));

    // 地平线附近一层很淡的紫灰雾
    float haze = exp(-pow((y - 0.22) * 7.0, 2.0)) * 0.10;
    c += vec3(0.16, 0.10, 0.26) * haze;

    // 星点（稀疏、带轻微闪烁）
    vec2 cell = floor(vUv * 520.0);
    float h = hash(cell);
    float star = step(0.9982, h);
    float twinkle = 0.55 + 0.45 * sin(uTime * 1.7 + h * 90.0);
    c += vec3(0.85, 0.82, 1.0) * star * twinkle * 0.85;

    // 四周压暗
    vec2 d = vUv - vec2(0.5);
    float v = 1.0 - dot(d, d) * 0.55;
    fragColor = vec4(c * v, 1.0);
}
""";

    public const string SolidVertex = """
#version 300 es
precision mediump float;
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aColor;
layout(location = 2) in vec2 aUv;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
out vec3 vColor;
out float vDepth;
out vec2 vUv;
void main()
{
    vec4 wp = uModel * vec4(aPos, 1.0);
    vec4 vp = uView * wp;
    vDepth = -vp.z;
    vColor = aColor;
    vUv = aUv;
    gl_Position = uProj * vp;
}
""";

    public const string SolidFragment = """
#version 300 es
precision mediump float;
in vec3 vColor;
in float vDepth;
in vec2 vUv;
uniform sampler2D uTex;
uniform float uTextured;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;
uniform float uAlpha;
out vec4 fragColor;
void main()
{
    vec3 tint = texture(uTex, vUv).rgb;
    vec3 base = mix(vColor, vColor * tint * 1.08, uTextured);
    float f = clamp((vDepth - uFogStart) / max(uFogEnd - uFogStart, 0.001), 0.0, 1.0);
    vec3 c = mix(base, uFogColor, f);
    fragColor = vec4(c, uAlpha);
}
""";

    // 门内：下界传送门漩涡
    public const string PortalVertex = """
#version 300 es
precision mediump float;
layout(location = 0) in vec3 aPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
out vec2 vUv;
out float vDepth;
void main()
{
    vUv = vec2(aPos.x / 0.90 + 0.5, (aPos.y - 0.10) / 1.70);
    vec4 wp = uModel * vec4(aPos, 1.0);
    vec4 vp = uView * wp;
    vDepth = -vp.z;
    gl_Position = uProj * vp;
}
""";

    public const string PortalFragment = """
#version 300 es
precision mediump float;
in vec2 vUv;
in float vDepth;
uniform float uTime;
uniform float uAlpha;
out vec4 fragColor;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void main()
{
    vec2 q = vUv - vec2(0.5);
    float r = length(q * vec2(1.6, 1.0));
    float angle = atan(q.y, q.x);

    // 极角随半径旋转，形成漩涡
    float swirl = angle + r * 7.0 - uTime * 1.15;
    vec2 p = vec2(cos(swirl), sin(swirl)) * r * 3.2 + vec2(uTime * 0.11, -uTime * 0.37);

    float n = noise(p * 2.0) * 0.55 + noise(p * 5.3) * 0.30 + noise(p * 12.0) * 0.15;

    vec3 deep = vec3(0.16, 0.05, 0.30);
    vec3 hot = vec3(0.86, 0.46, 1.00);
    vec3 col = mix(deep, hot, smoothstep(0.20, 0.78, n));

    // 中心更亮，边缘收束
    col += vec3(0.45, 0.20, 0.75) * pow(clamp(1.0 - r * 1.2, 0.0, 1.0), 2.2);
    col *= 1.35;

    float edge = smoothstep(0.0, 0.05, vUv.x) * smoothstep(1.0, 0.95, vUv.x)
               * smoothstep(0.0, 0.04, vUv.y) * smoothstep(1.0, 0.96, vUv.y);
    float alpha = uAlpha * (0.72 + 0.28 * edge);

    fragColor = vec4(col, alpha);
}
""";
}
