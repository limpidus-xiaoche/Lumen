namespace Launcher.Core.Game;

public static class GameOptions
{
    public static void EnsureDefaultLanguage(string gameDir, string language = "zh_cn")
    {
        var path = Path.Combine(gameDir, "options.txt");
        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(gameDir);
        File.WriteAllText(path, $"lang:{language}{Environment.NewLine}");
    }
}
