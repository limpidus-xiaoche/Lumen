using System.Runtime.InteropServices;

namespace Launcher.Render;

/// <summary>
/// S1 世界层渲染器：天穹渐变、悬浮岛、传送门框与门内，带距离雾。
/// 只通过 <see cref="IGlApi"/> 访问 GL，因此不依赖 Avalonia。
/// </summary>
public sealed unsafe class SceneRenderer : IDisposable
{
    private const uint GlColorBufferBit = 0x4000;
    private const uint GlDepthBufferBit = 0x0100;
    private const uint GlDepthTest = 0x0B71;
    private const uint GlBlend = 0x0BE2;
    private const uint GlSrcAlpha = 0x0302;
    private const uint GlOneMinusSrcAlpha = 0x0303;
    private const uint GlLequal = 0x0203;
    private const uint GlArrayBuffer = 0x8892;
    private const uint GlStaticDraw = 0x88E4;
    private const uint GlFloat = 0x1406;
    private const uint GlTriangles = 0x0004;
    private const uint GlVertexShader = 0x8B31;
    private const uint GlFragmentShader = 0x8B30;
    private const uint GlCompileStatus = 0x8B81;
    private const uint GlLinkStatus = 0x8B82;
    private const uint GlTexture2D = 0x0DE1;
    private const uint GlRgba = 0x1908;
    private const uint GlUnsignedByte = 0x1401;
    private const int GlNearest = 0x2600;
    private const int GlClampToEdge = 0x812F;
    private const uint GlTextureMinFilter = 0x2801;
    private const uint GlTextureMagFilter = 0x2800;
    private const uint GlTextureWrapS = 0x2802;
    private const uint GlTextureWrapT = 0x2803;
    private const uint GlUnpackAlignment = 0x0CF5;

    private static readonly Vec3 BgDeep = new(0.020f, 0.012f, 0.039f);      // #05030A 末地虚空
    private static readonly Vec3 BgTop = new(0.086f, 0.047f, 0.141f);       // #160C24
    private static readonly Vec3 IslandColor = new(0.62f, 0.64f, 0.50f);   // 末地石 #DBDEB4 压暗
    private static readonly Vec3 PurpurColor = new(0.60f, 0.40f, 0.62f);   // 紫珀 #A16EA1
    private static readonly Vec3 ObsidianColor = new(0.125f, 0.095f, 0.205f); // 黑曜石（抬亮才能在虚空里看见）
    private static readonly Vec3 PillarColor = new(0.095f, 0.075f, 0.135f);
    private static readonly Vec3 PortalGlowColor = new(0.478f, 0.243f, 0.784f);

    private Gl? _gl;
    private uint _solid;
    private uint _sky;
    private uint _portal;
    private GpuMesh _skyMesh;
    private GpuMesh _island;
    private GpuMesh _pillars;
    private GpuMesh _frame;
    private GpuMesh _inner;
    private GpuMesh _glow;
    private SolidUniforms _solidU;
    private SkyUniforms _skyU;
    private PortalUniforms _portalU;

    private readonly List<Part> _islandParts = new();
    private readonly List<Part> _frameParts = new();
    private readonly Dictionary<string, uint> _textureIds = new(StringComparer.OrdinalIgnoreCase);
    private SceneAssetSet? _pendingAssets;
    private bool _assetsDirty;

    public bool IsReady { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>GLES 3.0 及以上才继续；ANGLE 报的字符串形如 "OpenGL ES 3.2 ANGLE ..."。</summary>
    public static bool IsSupportedVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return version.Contains("OpenGL ES 3.", StringComparison.OrdinalIgnoreCase);
    }

    public bool Initialize(IGlApi api)
    {
        try
        {
            if (!IsSupportedVersion(api.VersionString))
            {
                FailureReason = $"GL 版本不支持：{api.VersionString}";
                return false;
            }

            _gl = Gl.Load(api);
            _sky = BuildProgram(Shaders.SkyVertex, Shaders.SkyFragment);
            _solid = BuildProgram(Shaders.SolidVertex, Shaders.SolidFragment);
            _portal = BuildProgram(Shaders.PortalVertex, Shaders.PortalFragment);

            _skyU = new SkyUniforms(
                _gl.Uniform(_sky, "uTop"),
                _gl.Uniform(_sky, "uBottom"),
                _gl.Uniform(_sky, "uTime"));

            _portalU = new PortalUniforms(
                _gl.Uniform(_portal, "uModel"),
                _gl.Uniform(_portal, "uView"),
                _gl.Uniform(_portal, "uProj"),
                _gl.Uniform(_portal, "uTime"),
                _gl.Uniform(_portal, "uAlpha"));

            _solidU = new SolidUniforms(
                _gl.Uniform(_solid, "uModel"),
                _gl.Uniform(_solid, "uView"),
                _gl.Uniform(_solid, "uProj"),
                _gl.Uniform(_solid, "uFogColor"),
                _gl.Uniform(_solid, "uFogStart"),
                _gl.Uniform(_solid, "uFogEnd"),
                _gl.Uniform(_solid, "uAlpha"),
                _gl.Uniform(_solid, "uTextured"),
                _gl.Uniform(_solid, "uTex"));

            _skyMesh = Upload(new MeshData());
            _island = Upload(BuildIsland());
            _pillars = Upload(BuildPillars());
            _frame = Upload(BuildFrame());
            _inner = Upload(BuildInner());
            _glow = Upload(BuildGlow());

            IsReady = true;
            return true;
        }
        catch (Exception ex)
        {
            FailureReason = ex.Message;
            Dispose();
            return false;
        }
    }

    /// <summary>由 App 在后台线程解析完成后调用；真正上传发生在下一次 Render（此时 GL 上下文才是当前）。</summary>
    public void SetAssets(SceneAssetSet? assets)
    {
        _pendingAssets = assets;
        _assetsDirty = true;
    }

    private void ApplyPendingAssets()
    {
        var assets = _pendingAssets;
        if (assets is null || !IsReady || _gl is null)
        {
            return;
        }

        _assetsDirty = false;

        foreach (var part in _islandParts)
        {
            DeleteMesh(part.Mesh);
        }

        foreach (var part in _frameParts)
        {
            DeleteMesh(part.Mesh);
        }

        _islandParts.Clear();
        _frameParts.Clear();

        foreach (var tex in assets.Textures.Values)
        {
            _textureIds[tex.Name] = UploadTexture(tex);
        }

        foreach (var asset in assets.Island)
        {
            _islandParts.Add(new Part(TextureOrZero(asset.TextureName), Upload(asset.Mesh)));
        }

        foreach (var asset in assets.Frame)
        {
            _frameParts.Add(new Part(TextureOrZero(asset.TextureName), Upload(asset.Mesh)));
        }
    }

    private uint TextureOrZero(string name) =>
        _textureIds.TryGetValue(name, out var id) ? id : 0;

    private uint UploadTexture(TextureData tex)
    {
        var gl = _gl!;
        uint id = 0;
        gl.GenTextures(1, (nint*)&id);
        gl.BindTexture(GlTexture2D, id);
        gl.PixelStorei(GlUnpackAlignment, 1);
        fixed (byte* p = tex.Rgba)
        {
            gl.TexImage2D(GlTexture2D, 0, (int)GlRgba, tex.Width, tex.Height, 0, GlRgba, GlUnsignedByte, p);
        }

        gl.TexParameteri(GlTexture2D, GlTextureMinFilter, GlNearest);
        gl.TexParameteri(GlTexture2D, GlTextureMagFilter, GlNearest);
        gl.TexParameteri(GlTexture2D, GlTextureWrapS, GlClampToEdge);
        gl.TexParameteri(GlTexture2D, GlTextureWrapT, GlClampToEdge);
        return id;
    }

    public void Render(double seconds, IReadOnlyList<PortalSlot> portals, Camera camera)
    {
        if (!IsReady || _gl is null)
        {
            return;
        }

        var gl = _gl;
        var w = _width;
        var h = _height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        if (_assetsDirty)
        {
            ApplyPendingAssets();
        }

        gl.Viewport(0, 0, w, h);
        gl.ClearColor(BgDeep.X, BgDeep.Y, BgDeep.Z, 1f);
        gl.Clear(GlColorBufferBit | GlDepthBufferBit);

        // 天穹
        gl.Disable(GlDepthTest);
        gl.Disable(GlBlend);
        gl.UseProgram(_sky);
        gl.Uniform3f(_skyU.Top, BgTop.X, BgTop.Y, BgTop.Z);
        gl.Uniform3f(_skyU.Bottom, BgDeep.X, BgDeep.Y, BgDeep.Z);
        gl.Uniform1f(_skyU.Time, (float)seconds);
        gl.BindVertexArray(_skyMesh.Vao);
        gl.DrawArrays(GlTriangles, 0, 3);

        // 固体几何
        gl.Enable(GlDepthTest);
        gl.DepthFunc(GlLequal);
        gl.UseProgram(_solid);

        var aspect = (float)w / h;
        var view = camera.ViewMatrix();
        var proj = camera.ProjectionMatrix(aspect);
        SetMatrix(_solidU.View, view);
        SetMatrix(_solidU.Proj, proj);
        gl.Uniform3f(_solidU.FogColor, BgDeep.X, BgDeep.Y, BgDeep.Z);
        gl.Uniform1f(_solidU.FogStart, 9f);
        gl.Uniform1f(_solidU.FogEnd, 30f);

        SetMatrix(_solidU.Model, Mat4.Identity);
        gl.Uniform1f(_solidU.Alpha, 1f);
        gl.Uniform1i(_solidU.Tex, 0);

        if (_islandParts.Count > 0)
        {
            gl.Uniform1f(_solidU.Textured, 1f);
            foreach (var part in _islandParts)
            {
                gl.BindTexture(GlTexture2D, part.Texture);
                DrawMesh(part.Mesh);
            }

            gl.Uniform1f(_solidU.Textured, 0f);
        }
        else
        {
            DrawMesh(_island);
        }

        DrawMesh(_pillars);

        // 黑曜石门框
        foreach (var slot in portals)
        {
            var model = Mat4.Translation(slot.Position) *
                        Mat4.RotationY(slot.YawRadians) *
                        Mat4.Scale(slot.Scale);
            SetMatrix(_solidU.Model, model);
            gl.Uniform1f(_solidU.Alpha, slot.Opacity);

            if (_frameParts.Count > 0)
            {
                gl.Uniform1f(_solidU.Textured, 1f);
                foreach (var part in _frameParts)
                {
                    gl.BindTexture(GlTexture2D, part.Texture);
                    DrawMesh(part.Mesh);
                }

                gl.Uniform1f(_solidU.Textured, 0f);
            }
            else
            {
                DrawMesh(_frame);
            }
        }

        // 门内漩涡（标准 alpha 混合，避免透出过亮的地面）
        gl.Enable(GlBlend);
        gl.BlendFunc(GlSrcAlpha, GlOneMinusSrcAlpha);
        gl.UseProgram(_portal);
        SetMatrix(_portalU.View, view);
        SetMatrix(_portalU.Proj, proj);
        gl.Uniform1f(_portalU.Time, (float)seconds);
        foreach (var slot in portals)
        {
            var model = Mat4.Translation(slot.Position) *
                        Mat4.RotationY(slot.YawRadians) *
                        Mat4.Scale(slot.Scale);
            SetMatrix(_portalU.Model, model);
            gl.Uniform1f(_portalU.Alpha, slot.Opacity);
            DrawMesh(_inner);
        }

        gl.Disable(GlBlend);
        gl.BindVertexArray(0);
    }

    private int _width;
    private int _height;

    public void Resize(int widthPx, int heightPx)
    {
        _width = widthPx;
        _height = heightPx;
    }

    public void Dispose()
    {
        if (_gl is not null)
        {
            DeleteMesh(_skyMesh);
            DeleteMesh(_island);
            DeleteMesh(_pillars);
            DeleteMesh(_frame);
            DeleteMesh(_inner);
            DeleteMesh(_glow);
            _skyMesh = default;
            _island = default;
            _pillars = default;
            _frame = default;
            _inner = default;
            _glow = default;

            foreach (var part in _islandParts)
            {
                DeleteMesh(part.Mesh);
            }

            foreach (var part in _frameParts)
            {
                DeleteMesh(part.Mesh);
            }

            _islandParts.Clear();
            _frameParts.Clear();

            foreach (var id in _textureIds.Values)
            {
                var t = id;
                _gl.DeleteTextures(1, (nint*)&t);
            }

            _textureIds.Clear();
            if (_solid != 0)
            {
                _gl.DeleteProgram(_solid);
                _solid = 0;
            }

            if (_portal != 0)
            {
                _gl.DeleteProgram(_portal);
                _portal = 0;
            }

            if (_sky != 0)
            {
                _gl.DeleteProgram(_sky);
                _sky = 0;
            }
        }

        _gl = null;
        IsReady = false;
    }

    private void DrawMesh(GpuMesh mesh)
    {
        _gl!.BindVertexArray(mesh.Vao);
        _gl.DrawArrays(GlTriangles, 0, mesh.Count);
    }

    private void DeleteMesh(GpuMesh mesh)
    {
        if (mesh.Vao == 0 || _gl is null)
        {
            return;
        }

        var vao = mesh.Vao;
        var vbo = mesh.Vbo;
        _gl.DeleteVertexArrays(1, (nint*)&vao);
        _gl.DeleteBuffers(1, (nint*)&vbo);
    }

    private void SetMatrix(int location, Mat4 matrix)
    {
        Span<float> buffer = stackalloc float[16];
        matrix.CopyTo(buffer);
        fixed (float* p = buffer)
        {
            _gl!.UniformMatrix4fv(location, 1, 0, p);
        }
    }

    private GpuMesh Upload(MeshData data)
    {
        var gl = _gl!;
        var values = data.ToArray();

        uint vao = 0;
        gl.GenVertexArrays(1, (nint*)&vao);
        gl.BindVertexArray(vao);

        if (values.Length == 0)
        {
            return new GpuMesh(vao, 0, 0);
        }

        uint vbo = 0;
        gl.GenBuffers(1, (nint*)&vbo);
        gl.BindBuffer(GlArrayBuffer, vbo);

        fixed (float* p = values)
        {
            gl.BufferData(GlArrayBuffer, values.Length * sizeof(float), (nint)p, GlStaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GlFloat, 0, MeshData.StrideBytes, 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, GlFloat, 0, MeshData.StrideBytes, 12);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, GlFloat, 0, MeshData.StrideBytes, 24);

        return new GpuMesh(vao, vbo, data.VertexCount);
    }

    private uint BuildProgram(string vertexSource, string fragmentSource)
    {
        var vs = CompileShader(GlVertexShader, vertexSource);
        var fs = CompileShader(GlFragmentShader, fragmentSource);
        var program = _gl!.CreateProgram();
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);
        _gl.LinkProgram(program);

        var ok = 0;
        _gl.GetProgramiv(program, GlLinkStatus, (nint*)&ok);
        if (ok == 0)
        {
            var log = ReadProgramLog(program);
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);
            throw new InvalidOperationException($"着色器链接失败：{log}");
        }

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        return program;
    }

    private uint CompileShader(uint type, string source)
    {
        var gl = _gl!;
        var shader = gl.CreateShader(type);
        var srcPtr = Marshal.StringToHGlobalAnsi(source);
        try
        {
            var ptrs = stackalloc nint[1];
            ptrs[0] = srcPtr;
            gl.ShaderSource(shader, 1, ptrs, null);
        }
        finally
        {
            Marshal.FreeHGlobal(srcPtr);
        }

        gl.CompileShader(shader);
        var ok = 0;
        gl.GetShaderiv(shader, GlCompileStatus, (nint*)&ok);
        if (ok == 0)
        {
            throw new InvalidOperationException($"着色器编译失败：{ReadShaderLog(shader)}");
        }

        return shader;
    }

    private string ReadShaderLog(uint shader)
    {
        var buffer = stackalloc byte[1024];
        var length = 0;
        _gl!.GetShaderInfoLog(shader, 1024, (nint*)&length, buffer);
        return length <= 0 ? "(无日志)" : Marshal.PtrToStringAnsi((nint)buffer, length) ?? "(无日志)";
    }

    private string ReadProgramLog(uint program)
    {
        var buffer = stackalloc byte[1024];
        var length = 0;
        _gl!.GetProgramInfoLog(program, 1024, (nint*)&length, buffer);
        return length <= 0 ? "(无日志)" : Marshal.PtrToStringAnsi((nint)buffer, length) ?? "(无日志)";
    }

    private static MeshData BuildIsland()
    {
        var mesh = new MeshData();
        mesh.AddBox(new Vec3(0, -0.45f, -0.4f), new Vec3(3.6f, 0.45f, 3.2f), IslandColor);

        // 紫珀镶边，让地面有材质变化
        mesh.AddBox(new Vec3(0, -0.44f, -3.48f), new Vec3(3.6f, 0.06f, 0.12f), PurpurColor);
        mesh.AddBox(new Vec3(0, -0.44f, 2.68f), new Vec3(3.6f, 0.06f, 0.12f), PurpurColor);
        mesh.AddBox(new Vec3(-3.48f, -0.44f, -0.4f), new Vec3(0.12f, 0.06f, 3.2f), PurpurColor);
        mesh.AddBox(new Vec3(3.48f, -0.44f, -0.4f), new Vec3(0.12f, 0.06f, 3.2f), PurpurColor);

        // 末地石碎块
        var rng = new Random(20261024);
        for (var i = 0; i < 12; i++)
        {
            var angle = i / 12f * MathF.PI * 2f;
            var r = 2.7f + (float)rng.NextDouble() * 0.55f;
            var x = MathF.Cos(angle) * r;
            var z = -0.4f + MathF.Sin(angle) * r;
            var h = 0.10f + (float)rng.NextDouble() * 0.22f;
            mesh.AddBox(new Vec3(x, h, z), new Vec3(0.30f, h, 0.30f), IslandColor * 0.92f);
        }

        return mesh;
    }

    /// <summary>远景黑曜石柱，撑起末地纵深。</summary>
    private static MeshData BuildPillars()
    {
        var mesh = new MeshData();
        var positions = new (float X, float Z, float H, float W)[]
        {
            (-5.6f, -8.5f, 5.6f, 0.55f),
            (-3.1f, -11.5f, 4.2f, 0.45f),
            (4.9f, -9.2f, 6.2f, 0.60f),
            (6.4f, -13.0f, 3.6f, 0.40f),
            (0.8f, -15.5f, 4.8f, 0.50f),
        };

        foreach (var p in positions)
        {
            mesh.AddBox(
                new Vec3(p.X, -0.45f + p.H * 0.5f, p.Z),
                new Vec3(p.W, p.H * 0.5f, p.W),
                PillarColor);
        }

        return mesh;
    }

    private static MeshData BuildFrame()
    {
        var mesh = new MeshData();
        mesh.AddBox(new Vec3(-0.78f, 1.10f, 0f), new Vec3(0.14f, 1.10f, 0.14f), ObsidianColor);
        mesh.AddBox(new Vec3(0.78f, 1.10f, 0f), new Vec3(0.14f, 1.10f, 0.14f), ObsidianColor);
        mesh.AddBox(new Vec3(0f, 2.32f, 0f), new Vec3(0.92f, 0.16f, 0.14f), ObsidianColor * 1.25f);
        mesh.AddBox(new Vec3(0f, 0.06f, 0f), new Vec3(0.92f, 0.10f, 0.14f), ObsidianColor);
        return mesh;
    }

    private static MeshData BuildInner()
    {
        var mesh = new MeshData();
        mesh.AddQuad(
            new Vec3(-0.45f, 0.10f, 0f),
            new Vec3(0.45f, 0.10f, 0f),
            new Vec3(0.45f, 1.80f, 0f),
            new Vec3(-0.45f, 1.80f, 0f),
            PortalGlowColor);
        return mesh;
    }

    /// <summary>地面上的椭圆光斑（XZ 平面），加色混合。</summary>
    private static MeshData BuildGlow()
    {
        var mesh = new MeshData();
        mesh.AddQuad(
            new Vec3(-1.7f, 0.015f, 1.25f),
            new Vec3(1.7f, 0.015f, 1.25f),
            new Vec3(1.7f, 0.015f, -1.15f),
            new Vec3(-1.7f, 0.015f, -1.15f),
            PortalGlowColor);
        return mesh;
    }

    private readonly record struct GpuMesh(uint Vao, uint Vbo, int Count);

    private readonly record struct SolidUniforms(
        int Model,
        int View,
        int Proj,
        int FogColor,
        int FogStart,
        int FogEnd,
        int Alpha,
        int Textured,
        int Tex);

    private readonly record struct Part(uint Texture, GpuMesh Mesh);

    private readonly record struct SkyUniforms(int Top, int Bottom, int Time);

    private readonly record struct PortalUniforms(
        int Model,
        int View,
        int Proj,
        int Time,
        int Alpha);
}
