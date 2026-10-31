using Launcher.Core.Launch;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class Java8ClasspathFarmTests
{
    [Fact]
    public void Prepare_Deploys_Jars_In_Classpath_Order()
    {
        var root = NewTempRoot();
        try
        {
            var jarsDir = Path.Combine(root, "jars");
            Directory.CreateDirectory(jarsDir);
            var jars = new[]
            {
                CreateJar(jarsDir, "first.jar", "one"),
                CreateJar(jarsDir, "second.jar", "two"),
                CreateJar(jarsDir, "third.jar", "three"),
            };
            var cacheRoot = Path.Combine(root, "cache");

            var (wildcard, count, missing) = Java8ClasspathFarm.Prepare(jars, cacheRoot);

            Assert.Equal(3, count);
            Assert.Equal(0, missing);
            Assert.EndsWith(Path.DirectorySeparatorChar + "*", wildcard);
            Assert.Equal(Java8ClasspathFarm.ComputeDirectoryName(jars), Path.GetFileName(Path.GetDirectoryName(wildcard)!));

            var entries = Directory.GetFiles(Path.GetDirectoryName(wildcard)!)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(3, entries.Length);
            Assert.Equal("0001-first.jar", Path.GetFileName(entries[0]));
            Assert.Equal("0002-second.jar", Path.GetFileName(entries[1]));
            Assert.Equal("0003-third.jar", Path.GetFileName(entries[2]));
            Assert.Equal("one", File.ReadAllText(entries[0]));
            Assert.Equal("two", File.ReadAllText(entries[1]));
            Assert.Equal("three", File.ReadAllText(entries[2]));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Prepare_Is_Idempotent()
    {
        var root = NewTempRoot();
        try
        {
            var jarsDir = Path.Combine(root, "jars");
            Directory.CreateDirectory(jarsDir);
            var jars = new[]
            {
                CreateJar(jarsDir, "a.jar", "a"),
                CreateJar(jarsDir, "b.jar", "b"),
            };
            var cacheRoot = Path.Combine(root, "cache");

            var first = Java8ClasspathFarm.Prepare(jars, cacheRoot);
            var firstDir = Path.GetDirectoryName(first.WildcardClasspath)!;

            var second = Java8ClasspathFarm.Prepare(jars, cacheRoot);

            Assert.Equal(first.WildcardClasspath, second.WildcardClasspath);
            Assert.Equal(first.JarCount, second.JarCount);
            Assert.Equal(Path.GetFileName(firstDir), Path.GetFileName(Path.GetDirectoryName(second.WildcardClasspath)!));
            Assert.Equal(2, Directory.GetFiles(firstDir).Length);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Prepare_Skips_Missing_Jars()
    {
        var root = NewTempRoot();
        try
        {
            var jarsDir = Path.Combine(root, "jars");
            Directory.CreateDirectory(jarsDir);
            var present = CreateJar(jarsDir, "present.jar", "present");
            var missing = Path.Combine(jarsDir, "missing.jar");
            var other = CreateJar(jarsDir, "other.jar", "other");

            var (wildcard, count, missingCount) =
                Java8ClasspathFarm.Prepare([present, missing, other], Path.Combine(root, "cache"));

            Assert.Equal(2, count);
            Assert.Equal(1, missingCount);
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(wildcard)!).Length);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Entry_Names_Are_Safe_And_Ordered()
    {
        Assert.Equal("0001-my mod 中文.jar", Java8ClasspathFarm.BuildEntryFileName(1, @"C:\libs\my mod 中文.jar"));
        Assert.Equal("0002-bad_name_.jar", Java8ClasspathFarm.BuildEntryFileName(2, @"C:\libs\bad:name?.jar"));
        Assert.StartsWith("0003-", Java8ClasspathFarm.BuildEntryFileName(3, "plain.jar"));

        var root = NewTempRoot();
        try
        {
            var jarsDir = Path.Combine(root, "jars");
            Directory.CreateDirectory(jarsDir);
            var jar = CreateJar(jarsDir, "a b 中文.jar", "content");

            var (wildcard, _, _) = Java8ClasspathFarm.Prepare([jar], Path.Combine(root, "cache"));

            var entry = Directory.GetFiles(Path.GetDirectoryName(wildcard)!)
                .Single(path => path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));
            var name = Path.GetFileName(entry);
            Assert.StartsWith("0001-", name);
            Assert.DoesNotContain(name, c => Path.GetInvalidFileNameChars().Contains(c));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Duplicate_Names_Stay_Distinct_By_Prefix()
    {
        var root = NewTempRoot();
        try
        {
            var firstDir = Path.Combine(root, "one");
            var secondDir = Path.Combine(root, "two");
            Directory.CreateDirectory(firstDir);
            Directory.CreateDirectory(secondDir);
            var jars = new[]
            {
                CreateJar(firstDir, "same.jar", "1"),
                CreateJar(secondDir, "same.jar", "2"),
            };

            var (wildcard, count, _) = Java8ClasspathFarm.Prepare(jars, Path.Combine(root, "cache"));

            Assert.Equal(2, count);
            var entries = Directory.GetFiles(Path.GetDirectoryName(wildcard)!)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal("0001-same.jar", Path.GetFileName(entries[0]));
            Assert.Equal("0002-same.jar", Path.GetFileName(entries[1]));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void ReplaceClasspathArgument_Swaps_Exact_Value()
    {
        var classpath = string.Join(Path.PathSeparator, ["a.jar", "b.jar"]);
        var wildcard = Path.Combine(@"C:\cache\cp8\abc", "*");
        string[] args = ["-Xmx2g", "-cp", classpath, "net.minecraft.client.main.Main", "--username", "Player"];

        var replaced = LaunchService.ReplaceClasspathArgument(args, classpath, wildcard);

        Assert.Equal(new[] { "-Xmx2g", "-cp", wildcard, "net.minecraft.client.main.Main", "--username", "Player" }, replaced);
    }

    [Fact]
    public void ReplaceClasspathArgument_Supports_Prefixed_Form()
    {
        var classpath = string.Join(Path.PathSeparator, ["a.jar", "b.jar"]);
        var wildcard = Path.Combine(@"C:\cache\cp8\abc", "*");
        string[] args = ["-cp=" + classpath, "net.minecraft.client.main.Main"];

        var replaced = LaunchService.ReplaceClasspathArgument(args, classpath, wildcard);

        Assert.Equal(new[] { "-cp=" + wildcard, "net.minecraft.client.main.Main" }, replaced);
    }

    [Fact]
    public void ReplaceClasspathArgument_Throws_When_Classpath_Missing()
    {
        var classpath = string.Join(Path.PathSeparator, ["a.jar", "b.jar"]);

        var ex = Assert.Throws<LaunchException>(() =>
            LaunchService.ReplaceClasspathArgument(["-cp", "other.jar", "Main"], classpath, "wildcard"));

        Assert.Equal("E5003", ex.ErrorCode);
    }

    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "lumen-tests", "cp8-" + Guid.NewGuid().ToString("N"));

    private static string CreateJar(string directory, string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
