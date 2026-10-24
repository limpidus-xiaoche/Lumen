using System.Text;

namespace Launcher.Core.Storage;

public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, content, Utf8NoBom);
        File.Move(temp, path, overwrite: true);
    }

    public static string ReadAllTextOrEmpty(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : string.Empty;
    }
}
