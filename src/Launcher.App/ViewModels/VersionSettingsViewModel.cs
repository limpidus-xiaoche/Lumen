using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>隔离方式下拉项。</summary>
public sealed record IsolationOption(string Title, IsolationLevel Value);

/// <summary>Java 环境下拉项（显示版本与文件位置）。</summary>
public sealed record JavaOption(string Title, string Path);

/// <summary>Mod 管理列表的一行（启停状态可变，带图标）。</summary>
public sealed partial class ModItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }

    /// <summary>从 jar 提取的图标，取不到时为 null，界面回退到首字母方块。</summary>
    public Bitmap? Icon { get; init; }

    public bool HasIcon => Icon is not null;

    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    private bool enabled;

    public string VersionText => Version is null ? "未标注版本" : Version;
}

/// <summary>版本设置页：横向分类（概览 / 启动 / Mod 管理）+ 分区内容。</summary>
public partial class VersionSettingsViewModel : ObservableObject
{
    public const string CategoryOverview = "概览";
    public const string CategoryLaunch = "启动";
    public const string CategoryMods = "Mod 管理";

    private readonly ILauncherFacade _facade;
    private readonly InstanceSummary _instance;
    private readonly Action _back;

    public ObservableCollection<string> Categories { get; } = [CategoryOverview, CategoryLaunch, CategoryMods];

    public ObservableCollection<IsolationOption> Isolations { get; } =
    [
        new IsolationOption("不隔离（共用游戏目录）", IsolationLevel.None),
        new IsolationOption("仅隔离 Mod 与配置", IsolationLevel.ModsConfig),
        new IsolationOption("完全隔离", IsolationLevel.Full),
    ];

    public ObservableCollection<ModItem> Mods { get; } = [];

    /// <summary>PCL 同款：已检测到的 Java 运行时（显示版本与路径）。</summary>
    public ObservableCollection<JavaOption> Javas { get; } = [];

    [ObservableProperty]
    private JavaOption? selectedJava;

    [ObservableProperty]
    private string selectedCategory = CategoryOverview;

    [ObservableProperty]
    private string versionName = string.Empty;

    [ObservableProperty]
    private string versionText = string.Empty;

    [ObservableProperty]
    private string gameDirText = "正在读取…";

    [ObservableProperty]
    private int memoryMb = 4096;

    [ObservableProperty]
    private IsolationOption? selectedIsolation;

    [ObservableProperty]
    private string statusText = string.Empty;

    public bool IsOverview => SelectedCategory == CategoryOverview;

    public bool IsLaunch => SelectedCategory == CategoryLaunch;

    public bool IsMods => SelectedCategory == CategoryMods;

    public VersionSettingsViewModel(ILauncherFacade facade, InstanceSummary instance, Action back)
    {
        _facade = facade;
        _instance = instance;
        _back = back;

        VersionName = instance.Name;
        VersionText = $"{instance.VersionId} · {LoaderLabel(instance.Loader)}" +
                      (instance.LoaderVersion is null ? string.Empty : $" {instance.LoaderVersion}");
        SelectedIsolation = Isolations.FirstOrDefault(o => o.Value == instance.Isolation) ?? Isolations[0];

        _ = LoadAsync();
    }

    private static string LoaderLabel(LoaderKind loader) => loader switch
    {
        LoaderKind.Vanilla => "原版",
        LoaderKind.Fabric => "Fabric",
        LoaderKind.Forge => "Forge",
        LoaderKind.NeoForge => "NeoForge",
        _ => loader.ToString(),
    };

    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsLaunch));
        OnPropertyChanged(nameof(IsMods));

        if (value == CategoryMods)
        {
            _ = LoadModsAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var detail = await _facade.GetInstanceAsync(_instance.Id, CancellationToken.None);
            GameDirText = detail.GameDir;
            MemoryMb = detail.MemoryMb;

            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            Javas.Clear();
            foreach (var java in snapshot.Javas.OrderBy(j => j.MajorVersion))
            {
                var title = $"Java {java.MajorVersion}" +
                            (java.Vendor.Length > 0 ? $" · {java.Vendor}" : string.Empty) +
                            (java.Is64Bit ? " · 64 位" : string.Empty);
                Javas.Add(new JavaOption(title, java.Path));
            }

            SelectedJava = Javas.FirstOrDefault(j =>
                string.Equals(j.Path, snapshot.Settings.JavaPath, StringComparison.OrdinalIgnoreCase))
                ?? Javas.FirstOrDefault();
        }
        catch (Exception ex)
        {
            GameDirText = "读取失败";
            StatusText = $"读取实例信息失败：{ex.Message}";
        }
    }

    async Task LoadModsAsync()
    {
        try
        {
            var entries = await _facade.ScanModsAsync(_instance.Id, CancellationToken.None);
            Mods.Clear();
            foreach (var entry in entries)
            {
                Bitmap? icon = null;
                if (entry.IconPath is not null && File.Exists(entry.IconPath))
                {
                    try
                    {
                        icon = new Bitmap(entry.IconPath);
                    }
                    catch
                    {
                        icon = null;
                    }
                }

                Mods.Add(new ModItem
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Version = entry.Version,
                    Icon = icon,
                    Enabled = entry.Enabled,
                });
            }

            StatusText = Mods.Count == 0 ? "该版本没有 Mod" : $"共 {Mods.Count} 个 Mod";
        }
        catch (Exception ex)
        {
            StatusText = $"读取 Mod 失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private Task RefreshModsAsync() => LoadModsAsync();

    /// <summary>启用 / 禁用单个 Mod（通过门面重命名 .jar 与 .jar.disabled）。</summary>
    [RelayCommand]
    private async Task ToggleModAsync(ModItem? item)
    {
        if (item is null)
        {
            return;
        }

        var target = !item.Enabled;
        try
        {
            await _facade.SetModEnabledAsync(_instance.Id, item.Id, target, CancellationToken.None);
            item.Enabled = target;
            StatusText = $"{item.Name} 已{(target ? "启用" : "禁用")}";
        }
        catch (Exception ex)
        {
            StatusText = $"操作失败：{ex.Message}";
        }
    }

    /// <summary>删除单个 Mod 文件。</summary>
    [RelayCommand]
    private async Task DeleteModAsync(ModItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _facade.DeleteModAsync(_instance.Id, item.Id, CancellationToken.None);
            Mods.Remove(item);
            StatusText = $"已删除 {item.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"删除失败：{ex.Message}";
        }
    }

    /// <summary>把用户选择的 jar 复制进 mods 目录。</summary>
    public async Task AddModAsync(string sourcePath)
    {
        try
        {
            await _facade.ImportModAsync(_instance.Id, sourcePath, CancellationToken.None);
            StatusText = $"已添加 {Path.GetFileName(sourcePath)}";
            await LoadModsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"添加失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var name = VersionName.Trim();
        if (name.Length == 0)
        {
            StatusText = "版本名不能为空";
            return;
        }

        try
        {
            StatusText = "正在保存…";
            var progress = new Progress<InstallProgress>(p =>
                Dispatcher.UIThread.Post(() => StatusText = p.Message));

            if (SelectedJava is not null)
            {
                await _facade.UpdateSettingsAsync(
                    new AppSettingsPatch(JavaPath: SelectedJava.Path), CancellationToken.None);
            }

            await _facade.UpdateInstanceAsync(
                _instance.Id,
                new UpdateInstanceRequest(
                    DisplayName: name,
                    VersionId: null,
                    Loader: null,
                    LoaderVersion: null,
                    Isolation: SelectedIsolation?.Value,
                    MemoryMb: MemoryMb),
                progress,
                CancellationToken.None);

            StatusText = "已保存";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenVersionFolder() => OpenFolder(Path.Combine(GameDirText, "versions", _instance.VersionId), "版本文件夹");

    [RelayCommand]
    private void OpenSavesFolder() => OpenFolder(Path.Combine(GameDirText, "saves"), "存档文件夹");

    [RelayCommand]
    private void OpenModsFolder() => OpenFolder(Path.Combine(GameDirText, "mods"), "Mod 文件夹");

    private void OpenFolder(string path, string label)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                StatusText = $"{label}不存在";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            StatusText = $"已打开{label}";
        }
        catch (Exception ex)
        {
            StatusText = $"打开失败：{ex.Message}";
        }
    }

    /// <summary>返回启动页。</summary>
    [RelayCommand]
    private void Back() => _back();
}
