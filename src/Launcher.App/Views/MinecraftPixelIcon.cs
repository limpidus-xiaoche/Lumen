using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Launcher.App.Views;

/// <summary>导航栏使用的 Q 版原版 MC 图案。</summary>
public enum IconKind
{
    Enderman,
    Shulker,
    EndRod,
    EnchantedBook,
    EyeOfEnder,
}

/// <summary>16×16 像素画渲染控件：按原版配色逐像素绘制，放大后保持方正清晰。</summary>
public sealed class MinecraftPixelIcon : Control
{
    public static readonly StyledProperty<IconKind> KindProperty =
        AvaloniaProperty.Register<MinecraftPixelIcon, IconKind>(nameof(Kind));

    static MinecraftPixelIcon()
    {
        AffectsRender<MinecraftPixelIcon>(KindProperty);
    }

    public MinecraftPixelIcon()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    private static readonly Dictionary<string, Bitmap> BitmapCache = [];

    public IconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var art = IconArt.Get(Kind);
        if (art.Asset is not null)
        {
            var icon = LoadBitmap(art.Asset);
            if (icon is not null)
            {
                var side = Math.Min(Bounds.Width, Bounds.Height);
                var x = (Bounds.Width - side) / 2;
                var y = (Bounds.Height - side) / 2;
                context.DrawImage(icon, new Rect(x, y, side, side));
                return;
            }
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 16.0;
        if (scale <= 0)
        {
            return;
        }

        for (var y = 0; y < art.Rows.Length; y++)
        {
            var row = art.Rows[y];
            for (var x = 0; x < row.Length && x < 16; x++)
            {
                var ch = row[x];
                if (ch == '.' || !art.Palette.TryGetValue(ch, out var color))
                {
                    continue;
                }

                var brush = new SolidColorBrush(color);
                context.DrawRectangle(brush, null, new Rect(x * scale, y * scale, scale + 0.4, scale + 0.4));
            }
        }
    }

    private static Bitmap? LoadBitmap(string asset)
    {
        if (BitmapCache.TryGetValue(asset, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://Lumen/Assets/{asset}"));
            var bitmap = new Bitmap(stream);
            BitmapCache[asset] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>像素画数据：每个图标 16 行、每行 16 个字符，'.' 表示透明。</summary>
internal static class IconArt
{
    // 末影人、潜影贝使用原版皮肤纹理裁剪（脸部 + 发光眼睛），保留游戏原貌
    private static readonly IconArtData Enderman = new(
        new Dictionary<char, Color>(),
        [],
        "icon-enderman.png");

    private static readonly IconArtData Shulker = new(
        new Dictionary<char, Color>(),
        [],
        "icon-shulker.png");

    private static readonly IconArtData EndRod = new(
        new Dictionary<char, Color>
        {
            ['W'] = Color.Parse("#F5F2FA"),
            ['P'] = Color.Parse("#C08CFF"),
            ['C'] = Color.Parse("#8B5CF6"),
            ['G'] = Color.Parse("#E0CFFF"),
        },
        [
            "................",
            "................",
            ".....GWWWWG.....",
            ".....GWWWWG.....",
            "......WWWW......",
            "......WWWW......",
            "......WWWW......",
            "......WWWW......",
            "......WWWW......",
            "......WWWW......",
            "......WWWW......",
            "....PPPPPPPP....",
            "....PPPPPPPP....",
            ".....PPPPPP.....",
            "................",
            "................",
        ]);

    // 附魔书与末影之眼直接使用原版 16×16 物品纹理，保证还原度
    private static readonly IconArtData EnchantedBook = new(
        new Dictionary<char, Color>(),
        [],
        "icon-enchanted-book.png");

    private static readonly IconArtData EyeOfEnder = new(
        new Dictionary<char, Color>(),
        [],
        "icon-ender-eye.png");

    public static IconArtData Get(IconKind kind) => kind switch
    {
        IconKind.Enderman => Enderman,
        IconKind.Shulker => Shulker,
        IconKind.EndRod => EndRod,
        IconKind.EnchantedBook => EnchantedBook,
        _ => EyeOfEnder,
    };
}

internal sealed record IconArtData(Dictionary<char, Color> Palette, string[] Rows, string? Asset = null);
