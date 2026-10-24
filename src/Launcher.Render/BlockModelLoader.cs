using System.Text.Json;

namespace Launcher.Render;

public sealed record TextureData(string Name, int Width, int Height, byte[] Rgba);

public sealed class SceneAsset
{
    public required string TextureName { get; init; }

    public required MeshData Mesh { get; init; }
}

/// <summary>由 App 从本地 Minecraft 客户端 jar 解析出的场景资产；为空时回退到程序化配色。</summary>
public sealed class SceneAssetSet
{
    public IReadOnlyList<SceneAsset> Island { get; init; } = [];

    public IReadOnlyList<SceneAsset> Frame { get; init; } = [];

    public IReadOnlyDictionary<string, TextureData> Textures { get; init; } =
        new Dictionary<string, TextureData>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>极简 Blockbench/原版方块模型解析：只处理 elements + textures，支持 parent 合并。</summary>
public static class BlockModelLoader
{
    public sealed class Model
    {
        public Dictionary<string, string> Textures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Element> Elements { get; } = new();
    }

    public sealed class Element
    {
        public float X1, Y1, Z1, X2, Y2, Z2;
        public Dictionary<string, Face> Faces { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class Face
    {
        public string TextureKey { get; set; } = "";
        public float U0, V0, U1, V1;
    }

    public static Model Parse(string json, Func<string, string?>? parentResolver = null, int depth = 0)
    {
        var model = new Model();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (depth < 8 && root.TryGetProperty("parent", out var parentEl))
        {
            var parentId = parentEl.GetString();
            var parentJson = parentId is null ? null : parentResolver?.Invoke(Normalize(parentId));
            if (parentJson is not null)
            {
                var parent = Parse(parentJson, parentResolver, depth + 1);
                foreach (var kv in parent.Textures)
                {
                    model.Textures[kv.Key] = kv.Value;
                }

                model.Elements.AddRange(parent.Elements);
            }
        }

        if (root.TryGetProperty("textures", out var textures) && textures.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in textures.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    model.Textures[p.Name] = Normalize(p.Value.GetString() ?? "");
                }
            }
        }

        if (root.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
        {
            model.Elements.Clear();
            foreach (var e in elements.EnumerateArray())
            {
                model.Elements.Add(ParseElement(e));
            }
        }

        return model;
    }

    /// <summary>解析贴图引用链：#all → all → 具体路径。父模型的 particle 常写成 "#all"。</summary>
    public static string? ResolveTexture(Model model, string key, int depth = 0)
    {
        if (depth > 8)
        {
            return null;
        }

        var name = key.TrimStart('#');
        if (!model.Textures.TryGetValue(name, out var value))
        {
            return null;
        }

        return value.StartsWith('#') ? ResolveTexture(model, value[1..], depth + 1) : value;
    }

    /// <summary>选一个可用于整块方块的贴图（优先 all / texture / side / particle）。</summary>
    public static string? PickPrimaryTexture(Model model)
    {
        foreach (var key in new[] { "all", "texture", "side", "particle" })
        {
            var resolved = ResolveTexture(model, key);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        foreach (var kv in model.Textures)
        {
            var resolved = ResolveTexture(model, kv.Key);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>把模型展开成方块空间（1 单位 = 1 方块）的网格，平移到 <paramref name="origin"/> 并按 <paramref name="scale"/> 缩放。</summary>
    public static void Append(MeshData mesh, Model model, Vec3 origin, float scale, out string textureName, Vec3? tint = null)
    {
        textureName = "";
        var color = tint ?? new Vec3(1f, 1f, 1f);
        foreach (var e in model.Elements)
        {
            foreach (var (dir, face) in e.Faces)
            {
                var tex = ResolveTexture(model, face.TextureKey) ?? PickPrimaryTexture(model) ?? "block/stone";
                textureName = tex;
                AppendFace(mesh, e, dir, face, origin, scale, color);
            }
        }
    }

    public static string Normalize(string id)
    {
        // "minecraft:block/end_stone" 或 "block/end_stone" → "block/end_stone"
        var idx = id.IndexOf(':');
        return idx >= 0 ? id[(idx + 1)..] : id;
    }

    private static Element ParseElement(JsonElement e)
    {
        var el = new Element();
        var from = e.GetProperty("from");
        var to = e.GetProperty("to");
        el.X1 = from[0].GetSingle();
        el.Y1 = from[1].GetSingle();
        el.Z1 = from[2].GetSingle();
        el.X2 = to[0].GetSingle();
        el.Y2 = to[1].GetSingle();
        el.Z2 = to[2].GetSingle();

        if (e.TryGetProperty("faces", out var faces))
        {
            foreach (var f in faces.EnumerateObject())
            {
                var face = new Face();
                if (f.Value.TryGetProperty("texture", out var t))
                {
                    face.TextureKey = t.GetString() ?? "";
                }

                if (f.Value.TryGetProperty("uv", out var uv) && uv.ValueKind == JsonValueKind.Array)
                {
                    face.U0 = uv[0].GetSingle();
                    face.V0 = uv[1].GetSingle();
                    face.U1 = uv[2].GetSingle();
                    face.V1 = uv[3].GetSingle();
                }
                else
                {
                    face.U1 = 16;
                    face.V1 = 16;
                }

                el.Faces[f.Name] = face;
            }
        }

        return el;
    }

    private static Vec3 P(Element e, float x, float y, float z, Vec3 origin, float s) =>
        new(origin.X + (x / 16f - 0.5f) * s, origin.Y + y / 16f * s, origin.Z + (z / 16f - 0.5f) * s);

    private static void AppendFace(MeshData mesh, Element e, string dir, Face f, Vec3 origin, float s, Vec3 color)
    {
        Vec3 a, b, c, d;
        switch (dir)
        {
            case "north":
                a = P(e, e.X1, e.Y2, e.Z1, origin, s);
                b = P(e, e.X1, e.Y1, e.Z1, origin, s);
                c = P(e, e.X2, e.Y1, e.Z1, origin, s);
                d = P(e, e.X2, e.Y2, e.Z1, origin, s);
                break;
            case "south":
                a = P(e, e.X2, e.Y2, e.Z2, origin, s);
                b = P(e, e.X2, e.Y1, e.Z2, origin, s);
                c = P(e, e.X1, e.Y1, e.Z2, origin, s);
                d = P(e, e.X1, e.Y2, e.Z2, origin, s);
                break;
            case "west":
                a = P(e, e.X1, e.Y2, e.Z2, origin, s);
                b = P(e, e.X1, e.Y1, e.Z2, origin, s);
                c = P(e, e.X1, e.Y1, e.Z1, origin, s);
                d = P(e, e.X1, e.Y2, e.Z1, origin, s);
                break;
            case "east":
                a = P(e, e.X2, e.Y2, e.Z1, origin, s);
                b = P(e, e.X2, e.Y1, e.Z1, origin, s);
                c = P(e, e.X2, e.Y1, e.Z2, origin, s);
                d = P(e, e.X2, e.Y2, e.Z2, origin, s);
                break;
            case "up":
                a = P(e, e.X1, e.Y2, e.Z1, origin, s);
                b = P(e, e.X1, e.Y2, e.Z2, origin, s);
                c = P(e, e.X2, e.Y2, e.Z2, origin, s);
                d = P(e, e.X2, e.Y2, e.Z1, origin, s);
                break;
            default: // down
                a = P(e, e.X1, e.Y1, e.Z2, origin, s);
                b = P(e, e.X1, e.Y1, e.Z1, origin, s);
                c = P(e, e.X2, e.Y1, e.Z1, origin, s);
                d = P(e, e.X2, e.Y1, e.Z2, origin, s);
                break;
        }

        mesh.AddTexturedQuad(a, b, c, d, color, f.U0 / 16f, f.V0 / 16f, f.U1 / 16f, f.V1 / 16f);
    }
}
