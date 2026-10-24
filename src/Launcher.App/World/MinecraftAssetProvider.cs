using System.IO.Compression;
using Launcher.Render;
using Serilog;
using StbImageSharp;

namespace Launcher.App.World;

/// <summary>
/// 从用户本机已安装的 Minecraft 客户端 jar 读取方块模型与贴图。
/// 不随启动器分发任何 Mojang 资产；找不到游戏时返回 null，渲染层回退到程序化配色。
/// </summary>
public static class MinecraftAssetProvider
{
    public static SceneAssetSet? TryLoad()
    {
        foreach (var jar in CandidateJars())
        {
            try
            {
                using var zip = ZipFile.OpenRead(jar);
                if (zip.GetEntry("assets/minecraft/models/block/end_stone.json") is null)
                {
                    continue; // 模组/加载器 jar，不是原版客户端
                }

                var resolver = new ModelResolver(zip);
                var textures = new Dictionary<string, TextureData>(StringComparer.OrdinalIgnoreCase);

                var endStone = LoadTextured(zip, resolver, "block/end_stone", textures);
                var obsidian = LoadTextured(zip, resolver, "block/obsidian", textures);
                var purpur = LoadTextured(zip, resolver, "block/purpur_block", textures);

                if (endStone is null || obsidian is null)
                {
                    Log.Warning(
                        "方块资产解析失败：end_stone={EndStone} obsidian={Obsidian} purpur={Purpur}，回退到程序化配色：{Jar}",
                        endStone is not null, obsidian is not null, purpur is not null, jar);
                    return null;
                }

                var island = new List<SceneAsset> { Island(endStone.Value.Model, endStone.Value.TextureId) };
                _ = purpur; // 紫珀暂不铺整层（会与末地石面重合导致 z-fighting），留作后续镶边

                var frame = new List<SceneAsset> { Frame(obsidian.Value.Model, obsidian.Value.TextureId) };

                Log.Information("已从客户端 jar 加载方块资产：{Jar}，贴图 {Count} 张", jar, textures.Count);
                return new SceneAssetSet
                {
                    Island = Merge(island),
                    Frame = frame,
                    Textures = textures,
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "读取客户端 jar 资产失败：{Jar}", jar);
            }
        }

        Log.Information("未找到可用的原版客户端 jar，世界层使用程序化配色");
        return null;
    }

    /// <summary>同名贴图的网格合并成一个 draw call。</summary>
    private static List<SceneAsset> Merge(List<SceneAsset> parts)
    {
        var map = new Dictionary<string, MeshData>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            if (!map.TryGetValue(part.TextureName, out var mesh))
            {
                mesh = new MeshData();
                map[part.TextureName] = mesh;
            }

            mesh.Append(part.Mesh);
        }

        return map.Select(kv => new SceneAsset { TextureName = kv.Key, Mesh = kv.Value }).ToList();
    }

    private static SceneAsset Island(BlockModelLoader.Model model, string texture)
    {
        var mesh = new MeshData();
        const float s = 1f;
        var rng = new Random(20261024);

        // 两层厚度，边缘随机缺块，避免"薄板"观感
        for (var layer = 0; layer < 2; layer++)
        {
            for (var x = -3; x <= 3; x++)
            {
                for (var z = -3; z <= 2; z++)
                {
                    var edge = Math.Abs(x) == 3 || z == -3 || z == 2;
                    if (layer > 0 && edge && rng.NextDouble() < 0.35)
                    {
                        continue;
                    }

                    BlockModelLoader.Append(mesh, model, new Vec3(x, -1f - layer * s, z), s, out _, new Vec3(0.62f, 0.62f, 0.58f));
                }
            }
        }

        return new SceneAsset { TextureName = texture, Mesh = mesh };
    }

    private static SceneAsset Frame(BlockModelLoader.Model model, string texture)
    {
        var mesh = new MeshData();
        const float size = 0.45f;
        const float step = 0.45f;

        // 两根立柱（各 4 块）+ 顶横梁（2 块）
        for (var i = 0; i < 4; i++)
        {
            var y = 0.225f + i * step;
            BlockModelLoader.Append(mesh, model, new Vec3(-0.675f - size / 2, y - size / 2, -size / 2), size, out _);
            BlockModelLoader.Append(mesh, model, new Vec3(0.675f - size / 2, y - size / 2, -size / 2), size, out _);
        }

        for (var i = 0; i < 2; i++)
        {
            var x = (i == 0 ? -0.225f : 0.225f);
            BlockModelLoader.Append(mesh, model, new Vec3(x - size / 2, 2.025f - size / 2, -size / 2), size, out _);
        }

        return new SceneAsset { TextureName = texture, Mesh = mesh };
    }

    private static (BlockModelLoader.Model Model, string TextureId)? LoadTextured(
        ZipArchive zip,
        ModelResolver resolver,
        string modelPath,
        Dictionary<string, TextureData> textures)
    {
        var json = resolver.Get(modelPath);
        if (json is null)
        {
            Log.Information("模型缺失：{Path}", modelPath);
            return null;
        }

        var model = BlockModelLoader.Parse(json, resolver.Get);
        var textureName = BlockModelLoader.PickPrimaryTexture(model);
        if (textureName is null)
        {
            Log.Information("模型无可用贴图：{Path}（elements={Count}）", modelPath, model.Elements.Count);
            return null;
        }

        var png = ReadEntry(zip, $"assets/minecraft/textures/{textureName}.png");
        if (png is null)
        {
            Log.Information("贴图缺失：{Texture}", textureName);
            return null;
        }

        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        var id = textureName;
        if (!textures.ContainsKey(id))
        {
            textures[id] = new TextureData(id, image.Width, image.Height, image.Data);
        }

        return (model, id);
    }

    private sealed class ModelResolver(ZipArchive zip)
    {
        public string? Get(string modelPath)
        {
            var normalized = BlockModelLoader.Normalize(modelPath);
            return ReadEntry(zip, $"assets/minecraft/models/{normalized}.json") is { } bytes
                ? System.Text.Encoding.UTF8.GetString(bytes)
                : null;
        }
    }

    private static byte[]? ReadEntry(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path)
                    ?? zip.Entries.FirstOrDefault(x =>
                        x.FullName.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>只取 <c>versions/&lt;ver&gt;/&lt;ver&gt;.jar</c> 形态的候选，避免误取整合包里的模组 jar。</summary>
    private static IEnumerable<string> CandidateJars()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "shared", "versions"),
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                var jar = Path.Combine(dir, name + ".jar");
                if (File.Exists(jar))
                {
                    yield return jar;
                }
            }
        }
    }
}
