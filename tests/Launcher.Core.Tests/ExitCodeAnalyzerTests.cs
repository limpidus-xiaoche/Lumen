using Launcher.Core.Launch;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ExitCodeAnalyzerTests
{
    [Fact]
    public void Zero_Returns_Null()
    {
        Assert.Null(ExitCodeAnalyzer.Analyze(0, @"C:\game"));
    }

    [Fact]
    public void Access_Violation_Is_Mapped()
    {
        var hint = ExitCodeAnalyzer.Analyze(unchecked((int)0xC0000005), @"C:\game");
        Assert.Contains("访问冲突", hint);
    }

    [Fact]
    public void Crash_Report_Hint_Is_Used()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-crash", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "crash-reports"));
        File.WriteAllText(
            Path.Combine(root, "crash-reports", "crash-2026-10-24.txt"),
            "java.lang.OutOfMemoryError: Java heap space");

        var hint = ExitCodeAnalyzer.Analyze(1, root);
        Assert.Contains("内存不足", hint);

        Directory.Delete(root, recursive: true);
    }
}
