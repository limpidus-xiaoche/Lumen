using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>隔离方式下拉项。</summary>
public sealed record IsolationOption(string Title, IsolationLevel Value);

/// <summary>可切换的加载器下拉项（M5：原版 / Fabric / Forge / NeoForge）。</summary>
public sealed record LoaderChoice(string Title, LoaderKind Value);

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
    private InstanceSummary _instance;
    private readonly Action _back;
    private readonly Action? _onInstanceChanged;

    public ObservableCollection<string> Categories { get; } = [CategoryOverview, CategoryLaunch, CategoryMods];

    public ObservableCollection<IsolationOption> Isolations { get; } =
    [
        new IsolationOption("不隔离（共用游戏目录）", IsolationLevel.None),
        new IsolationOption("仅隔离 Mod 与配置", IsolationLevel.ModsConfig),
        new IsolationOption("完全隔离", IsolationLevel.Full),
    ];

    /// <summary>M5：可切换的加载器。</summary>
    public ObservableCollection<LoaderChoice> Loaders { get; } =
    [
        new LoaderChoice("原版", LoaderKind.Vanilla),
        new LoaderChoice("Fabric", LoaderKind.Fabric),
        new LoaderChoice("Forge", LoaderKind.Forge),
        new LoaderChoice("NeoForge", LoaderKind.NeoForge),
    ];

    /// <summary>当前加载器对应的可选版本（原版为空）。</summary>
    public ObservableCollection<LoaderVersionInfo> LoaderVersions { get; } = [];

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVanillaLoader))]
    [NotifyPropertyChangedFor(nameof(CanSwitchLoader))]
    private LoaderChoice? selectedLoader;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSwitchLoader))]
    private LoaderVersionInfo? selectedLoaderVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSwitchLoader))]
    private bool isLoaderBusy;

    [ObservableProperty]
    private double loaderPercent;

    [ObservableProperty]
    private bool isLoaderIndeterminate;

    [ObservableProperty]
    private string loaderStatusText = string.Empty;

    public bool IsOverview => SelectedCategory == CategoryOverview;

    public bool IsLaunch => SelectedCategory == CategoryLaunch;

    public bool IsMods => SelectedCategory == CategoryMods;

    public bool IsVanillaLoader => SelectedLoader?.Value == LoaderKind.Vanilla;

    public bool CanSwitchLoader => !IsLoaderBusy && SelectedLoader is not null &&
                                   (SelectedLoader.Value == LoaderKind.Vanilla || SelectedLoaderVersion is not null);

    public string LoaderCurrentText => _instance.Loader == LoaderKind.Vanilla
        ? "当前：原版"
        : $"当前：{LoaderLabel(_instance.Loader)} {_instance.LoaderVersion}".TrimEnd();

    public VersionSettingsViewModel(
        ILauncherFacade facade, InstanceSummary instance, Action back, Action? onInstanceChanged = null)
    {
        _facade = facade;
        _instance = instance;
        _back = back;
        _onInstanceChanged = onInstanceChanged;

        VersionName = instance.Name;
        VersionText = $"{instance.VersionId} · {LoaderLabel(instance.Loader)}" +
                      (instance.LoaderVersion is null ? string.Empty : $" {instance.LoaderVersion}");
        SelectedIsolation = Isolations.FirstOrDefault(o => o.Value == instance.Isolation) ?? Isolations[0];
        SelectedLoader = Loaders.FirstOrDefault(l => l.Value == instance.Loader) ?? Loaders[0];

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

    partial void OnSelectedLoaderChanged(LoaderChoice? value) => _ = LoadLoaderVersionsAsync();

    /// <summary>按当前加载器 + 实例游戏版本查询可选加载器版本。</summary>
    private async Task LoadLoaderVersionsAsync()
    {
        LoaderVersions.Clear();
        SelectedLoaderVersion = null;

        if (SelectedLoader is not { } choice)
        {
            return;
        }

        if (choice.Value == LoaderKind.Vanilla)
        {
            LoaderStatusText = "原版不依赖加载器";
            return;
        }

        LoaderStatusText = $"正在获取 {choice.Title} {_instance.VersionId} 版本…";
        try
        {
            var versions = await _facade.GetLoaderVersionsAsync(
                choice.Value, _instance.VersionId, CancellationToken.None);
            if (!Equals(SelectedLoader, choice))
            {
                return;
            }

            foreach (var version in versions)
            {
                LoaderVersions.Add(version);
            }

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(v => v.Version == _instance.LoaderVersion)
                                    ?? LoaderVersions.FirstOrDefault(v => v.Stable)
                                    ?? LoaderVersions.FirstOrDefault();
            LoaderStatusText = LoaderVersions.Count == 0
                ? $"{choice.Title} 没有适用于 {_instance.VersionId} 的版本"
                : $"{choice.Title} {_instance.VersionId} 共 {LoaderVersions.Count} 个版本";
        }
        catch (Exception ex)
        {
            LoaderStatusText = $"获取加载器版本失败：{ex.Message}";
        }
    }

    /// <summary>M5：更换加载器（原版可清除加载器；Forge/NeoForge 走官方 installer）。</summary>
    [RelayCommand]
    private async Task ChangeLoaderAsync()
    {
        if (!CanSwitchLoader || SelectedLoader is not { } choice)
        {
            return;
        }

        var loaderVersion = choice.Value == LoaderKind.Vanilla
            ? string.Empty
            : SelectedLoaderVersion?.Version;
        if (choice.Value != LoaderKind.Vanilla && string.IsNullOrWhiteSpace(loaderVersion))
        {
            LoaderStatusText = "请先选择加载器版本";
            return;
        }

        IsLoaderBusy = true;
        LoaderPercent = 0;
        IsLoaderIndeterminate = false;
        LoaderStatusText = $"正在更换为 {choice.Title}…";

        try
        {
            var progress = new Progress<InstallProgress>(p => UiThread.Post(() =>
            {
                IsLoaderIndeterminate = p.Percent < 0;
                LoaderPercent = p.Percent < 0 ? 0 : p.Percent;
                LoaderStatusText = $"更换加载器：{p.Message}";
            }));

            await _facade.UpdateInstanceAsync(
                _instance.Id,
                new UpdateInstanceRequest(null, null, choice.Value, loaderVersion, null, null),
                progress,
                CancellationToken.None);

            var detail = await _facade.GetInstanceAsync(_instance.Id, CancellationToken.None);
            _instance = detail.Summary;
            VersionText = $"{_instance.VersionId} · {LoaderLabel(_instance.Loader)}" +
                          (string.IsNullOrWhiteSpace(_instance.LoaderVersion) ? string.Empty : $" {_instance.LoaderVersion}");
            OnPropertyChanged(nameof(LoaderCurrentText));
            LoaderStatusText = choice.Value == LoaderKind.Vanilla
                ? "已更换为原版"
                : $"已更换为 {choice.Title} {loaderVersion}";
            _onInstanceChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LoaderStatusText = $"更换加载器失败：{ex.Message}";
        }
        finally
        {
            IsLoaderBusy = false;
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
