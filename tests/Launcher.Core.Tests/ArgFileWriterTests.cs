using System.Globalization;
using System.Text;
using Launcher.Core.Launch;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ArgFileWriterTests
{
    [Fact]
    public void Escapes_Spaces_And_Quotes()
    {
        Assert.Equal(@"""C:\\Program Files\\x""", ArgFileWriter.Escape(@"C:\Program Files\x"));
        Assert.Equal("\"a\\\"b\"", ArgFileWriter.Escape("a\"b"));
        Assert.Equal("plain", ArgFileWriter.Escape("plain"));
    }

    [Fact]
    public void Encoding_Uses_Ansi_For_Java17_And_Utf8_For_Java21()
    {
        var ansi = ArgFileWriter.GetEncoding(17);
        var utf8 = ArgFileWriter.GetEncoding(21);

        Assert.Equal(CultureInfo.CurrentCulture.TextInfo.ANSICodePage, ansi.CodePage);
        Assert.Equal(Encoding.UTF8.CodePage, utf8.CodePage);
    }

    [Fact]
    public void Writes_One_Argument_Per_Line()
    {
        var path = ArgFileWriter.Write(["a", "b c", "d\"e"], 21);
        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            Assert.Equal(3, lines.Length);
            Assert.Equal("a", lines[0]);
            Assert.Equal("\"b c\"", lines[1]);
        }
        finally
        {
            ArgFileWriter.DeleteWithRetry(path);
        }
    }
}
