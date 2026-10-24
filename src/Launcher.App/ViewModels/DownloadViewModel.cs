using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>模组分类（左侧分类栏，对应 Modrinth 的 categories 取值）。</summary>
public sealed record ModCategory(string Title, string? Slug);

/// <summary>原版版本下拉项：显示完整版本号与中文类型标识。</summary>
public sealed record VersionOption(VersionSummary Source)
{
    public string Id => Source.Id;

    public string TypeText => Source.Type switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "旧测试版",
        "old_alpha" => "远古版",
        _ => Source.Type,
    };
}

/// <summary>在线模组搜索结果的一行（带图标与中英文名）。</summary>
public sealed partial class OnlineModItem : ObservableObject
{
    public required OnlineModSummary Summary { get; init; }

    /// <summary>英文原名。</summary>
    public string Title => Summary.Title;

    /// <summary>中文名（命中内置译名表时显示，否则与英文名相同）。</summary>
    public string ChineseTitle => ModTranslations.Translate(Summary.Slug, Summary.Title);

    public string Description => Summary.Description;

    /// <summary>英文名与中文名不同时才并显，避免重复。</summary>
    public bool ShowEnglish => !string.Equals(ChineseTitle, Title, StringComparison.Ordinal);

    public string AuthorText => Summary.Author is null ? "未知作者" : $"by {Summary.Author}";
    public string DownloadsText => $"{Summary.Downloads / 10000.0:0.#} 万次下载";

    public Bitmap? Icon { get; init; }
    public bool HasIcon => Icon is not null;
    public string Initial => Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    private bool isInstalling;
}

/// <summary>下载页：原版游戏与模组两个栏目，模组来源为 Modrinth / CurseForge。</summary>
public partial class DownloadViewModel : ObservableObject
{
    public const string CategoryVanilla = "原版游戏";
    public const string CategoryMods = "模组";

    private readonly ILauncherFacade _facade;

    public ObservableCollection<string> Categories { get; } = [CategoryVanilla, CategoryMods];

    public ObservableCollection<string> ModSources { get; } = ["Modrinth", "CurseForge"];

    /// <summary>原版版本筛选：正式版 / 快照 / 全部。</summary>
    public ObservableCollection<string> VersionFilters { get; } = ["正式版", "快照", "全部"];

    /// <summary>可查询的加载器（Fabric / Quilt 支持一键安装）。</summary>
    public ObservableCollection<string> LoaderKinds { get; } = ["Fabric", "Quilt", "Forge", "NeoForge"];

    /// <summary>加载器可选的原版游戏版本（正式版，最新在前）。</summary>
    public ObservableCollection<string> LoaderGameVersions { get; } = [];

    /// <summary>左侧模组分类栏（PCL2 风格）。</summary>
    public ObservableCollection<ModCategory> ModCategories { get; } =
    [
        new("全部", null),
        new("性能优化", "optimization"),
        new("冒险", "adventure"),
        new("科技", "technology"),
        new("魔法", "magic"),
        new("装饰", "decoration"),
        new("工具", "utility"),
        new("世界生成", "worldgen"),
        new("存储", "storage"),
        new("生物", "mobs"),
        new("装备", "equipment"),
        new("前置库", "library"),
    ];

    public ObservableCollection<VersionOption> Versions { get; } = [];
    public ObservableCollection<LoaderVersionInfo> LoaderVersions { get; } = [];
    public ObservableCollection<OnlineModItem> ModResults { get; } = [];
    public ObservableCollection<InstanceSummary> Instances { get; } = [];

    private readonly List<VersionSummary> _allVersions = [];

    [ObservableProperty]
    private string selectedCategory = CategoryVanilla;

    [ObservableProperty]
    private string latestRelease = "-";

    [ObservableProperty]
    private string statusText = "尚未加载版本列表";

    [ObservableProperty]
    private bool isBusy;

    // —— 原版下载 ——
    [ObservableProperty]
    private string selectedVersionFilter = "正式版";

    [ObservableProperty]
    private string vanillaQuery = string.Empty;

    [ObservableProperty]
    private VersionOption? selectedVersion;

    [ObservableProperty]
    private string instanceName = string.Empty;

    [ObservableProperty]
    private bool isInstalling;

    /// <summary>安装总进度（0–100），用于进度条。</summary>
    [ObservableProperty]
    private double installPercent;

    /// <summary>总量未知（Percent = -1）时进度条走不确定态。</summary>
    [ObservableProperty]
    private bool isInstallIndeterminate;

    /// <summary>当前安装阶段文字，如「依赖库 · 32/78」。</summary>
    [ObservableProperty]
    private string installStageText = string.Empty;

    // —— 加载器下载 ——
    [ObservableProperty]
    private string selectedLoaderName = "Fabric";

    [ObservableProperty]
    private string selectedLoaderGameVersion = string.Empty;

    [ObservableProperty]
    private LoaderVersionInfo? selectedLoaderVersion;

    [ObservableProperty]
    private string loaderStatusText = "选择加载器与游戏版本后加载版本号";

    [ObservableProperty]
    private bool isLoaderBusy;

    /// <summary>当前是否可以从启动器直接安装（Fabric / Quilt 使用官方 profile JSON）。</summary>
    public bool CanInstallLoader => SelectedLoaderName is "Fabric" or "Quilt";

    public string LoaderSupportText => CanInstallLoader
        ? $"{SelectedLoaderName} 支持一键安装：自动下载 profile 与依赖库"
        : $"{SelectedLoaderName} 可查询版本号；一键安装需官方 installer，后续阶段接入";

    public LoaderKind SelectedLoader => SelectedLoaderName switch
    {
        "Quilt" => LoaderKind.Quilt,
        "Forge" => LoaderKind.Forge,
        "NeoForge" => LoaderKind.NeoForge,
        _ => LoaderKind.Fabric,
    };

    [ObservableProperty]
    private string modQuery = string.Empty;

    [ObservableProperty]
    private InstanceSummary? selectedInstance;

    [ObservableProperty]
    private string selectedSourceName = "Modrinth";

    [ObservableProperty]
    private ModCategory? selectedModCategory;

    /// <summary>右侧分类栏是否展开（可收纳，避免页面拥挤）。</summary>
    [ObservableProperty]
    private bool isCategoryOpen;

    /// <summary>分类开关按钮的文字。</summary>
    public string CategoryToggleText =>
        IsCategoryOpen ? "收起分类" : $"分类：{SelectedModCategory?.Title ?? "全部"}";

    partial void OnIsCategoryOpenChanged(bool value) => OnPropertyChanged(nameof(CategoryToggleText));

    partial void OnSelectedModCategoryChanged(ModCategory? value)
    {
        OnPropertyChanged(nameof(CategoryToggleText));
        ReloadMods();
    }

    /// <summary>当前页码（每页 20 条）。</summary>
    [ObservableProperty]
    private int currentPage = 1;

    public string PageText => $"第 {CurrentPage} 页";

    partial void OnCurrentPageChanged(int value) => OnPropertyChanged(nameof(PageText));

    [RelayCommand]
    private void NextPage()
    {
        if (!IsBusy)
        {
            CurrentPage++;
            ModResults.Clear();
            _ = SearchModsAsync();
        }
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (!IsBusy && CurrentPage > 1)
        {
            CurrentPage--;
            ModResults.Clear();
            _ = SearchModsAsync();
        }
    }

    [RelayCommand]
    private void ToggleCategories() => IsCategoryOpen = !IsCategoryOpen;

    public ModSource SelectedSource =>
        SelectedSourceName == "CurseForge" ? ModSource.CurseForge : ModSource.Modrinth;

    public bool IsVanilla => SelectedCategory == CategoryVanilla;

    public bool IsMods => SelectedCategory == CategoryMods;

    public DownloadViewModel(ILauncherFacade facade)
    {
        _facade = facade;
        SelectedModCategory = ModCategories[0];
        _ = LoadInstancesAsync();
        _ = LoadAsync();
    }

    private async Task LoadInstancesAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            Instances.Clear();
            foreach (var instance in snapshot.Instances)
            {
                Instances.Add(instance);
            }

            SelectedInstance = Instances.FirstOrDefault();
        }
        catch
        {
            // 实例列表读取失败不影响原版下载。
        }
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsVanilla));
        OnPropertyChanged(nameof(IsMods));

        // 进入模组栏目时自动加载热门列表，无需手动搜索
        if (value == CategoryMods && ModResults.Count == 0 && !IsBusy)
        {
            _ = SearchModsAsync();
        }

        // 进入原版栏目时若清单为空则自动加载，已有清单则重建筛选结果（恢复版本状态文字）
        if (value == CategoryVanilla && !IsBusy)
        {
            if (_allVersions.Count == 0)
            {
                _ = LoadAsync();
            }
            else
            {
                ApplyVersionFilter();
            }
        }
    }

    partial void OnSelectedSourceNameChanged(string value)
    {
        ReloadMods();
    }

    private void ReloadMods()
    {
        if (IsMods && !IsBusy)
        {
            CurrentPage = 1;
            ModResults.Clear();
            _ = SearchModsAsync();
        }
    }

    private string SourceName => SelectedSource == ModSource.CurseForge ? "CurseForge" : "Modrinth";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var manifest = await _facade.RefreshManifestAsync(ManifestRefreshMode.IfStale, CancellationToken.None);
            LatestRelease = manifest.LatestRelease;
            _allVersions.Clear();
            _allVersions.AddRange(manifest.Versions);

            LoaderGameVersions.Clear();
            foreach (var release in _allVersions.Where(v => v.Type == "release").Select(v => v.Id))
            {
                LoaderGameVersions.Add(release);
            }

            ApplyVersionFilter();
            if (string.IsNullOrWhiteSpace(SelectedLoaderGameVersion))
            {
                SelectedLoaderGameVersion = LoaderGameVersions.FirstOrDefault() ?? LatestRelease;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedVersionFilterChanged(string value) => ApplyVersionFilter();

    partial void OnVanillaQueryChanged(string value) => ApplyVersionFilter();

    partial void OnSelectedVersionChanged(VersionOption? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(InstanceName))
        {
            InstanceName = value.Id;
        }
    }

    partial void OnSelectedLoaderNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanInstallLoader));
        OnPropertyChanged(nameof(LoaderSupportText));
        if (!string.IsNullOrWhiteSpace(SelectedLoaderGameVersion))
        {
            _ = LoadLoaderVersionsAsync();
        }
    }

    partial void OnSelectedLoaderGameVersionChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _ = LoadLoaderVersionsAsync();
        }
    }

    /// <summary>按筛选与关键字重建版本列表（列表过大时只取前 300 条）。</summary>
    private void ApplyVersionFilter()
    {
        if (_allVersions.Count == 0)
        {
            return;
        }

        var query = VanillaQuery.Trim();
        IEnumerable<VersionSummary> filtered = _allVersions;
        filtered = SelectedVersionFilter switch
        {
            "正式版" => filtered.Where(v => v.Type == "release"),
            "快照" => filtered.Where(v => v.Type != "release"),
            _ => filtered,
        };

        if (query.Length > 0)
        {
            filtered = filtered.Where(v => v.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Versions.Clear();
        foreach (var version in filtered.Take(300))
        {
            Versions.Add(new VersionOption(version));
        }

        SelectedVersion = Versions.FirstOrDefault(v => v.Id == LatestRelease) ?? Versions.FirstOrDefault();
        StatusText = Versions.Count == 0
            ? "没有匹配的版本"
            : $"共 {Versions.Count} 个版本（最新正式版 {LatestRelease}）";
    }

    /// <summary>安装选中的原版版本（含资源文件完整下载）。</summary>
    [RelayCommand]
    private async Task InstallVanillaAsync()
    {
        if (SelectedVersion is null || IsInstalling)
        {
            StatusText = "请先选择要安装的版本";
            return;
        }

        IsInstalling = true;
        InstallPercent = 0;
        IsInstallIndeterminate = false;
        InstallStageText = string.Empty;
        var versionId = SelectedVersion.Id;
        var displayName = InstanceName.Trim();
        var instanceId = $"vanilla-{versionId}";

        try
        {
            var progress = new Progress<InstallProgress>(p => Dispatcher.UIThread.Post(() =>
            {
                IsInstallIndeterminate = p.Percent < 0;
                InstallPercent = p.Percent < 0 ? 0 : p.Percent;
                InstallStageText = $"{StageLabel(p.Stage)} · {p.Message}";
                StatusText = $"正在安装 {versionId}：{InstallStageText}";
            }));

            var summary = await _facade.InstallAsync(
                new InstallRequest(
                    instanceId,
                    displayName.Length == 0 ? versionId : displayName,
                    versionId,
                    LoaderKind.Vanilla,
                    null,
                    IsolationLevel.ModsConfig,
                    InstallMode.New),
                progress,
                CancellationToken.None);

            InstallPercent = 100;
            IsInstallIndeterminate = false;
            InstallStageText = "安装完成";
            StatusText = $"已安装 {summary.Name}（{summary.VersionId}），可在启动页启动";
            await LoadInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(i => i.Id == summary.Id) ?? SelectedInstance;
        }
        catch (Exception ex)
        {
            StatusText = $"安装失败：{ex.Message}";
            InstallStageText = "失败";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>加载选中加载器 + 游戏版本的可用版本号（完整版本号，最新在前）。</summary>
    private async Task LoadLoaderVersionsAsync()
    {
        if (IsLoaderBusy)
        {
            return;
        }

        IsLoaderBusy = true;
        var loaderName = SelectedLoaderName;
        var gameVersion = SelectedLoaderGameVersion;
        LoaderStatusText = $"正在获取 {loaderName} {gameVersion} 版本列表…";
        LoaderVersions.Clear();
        try
        {
            var versions = await _facade.GetLoaderVersionsAsync(
                SelectedLoader, gameVersion, CancellationToken.None);
            foreach (var version in versions)
            {
                LoaderVersions.Add(version);
            }

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(v => v.Stable) ?? LoaderVersions.FirstOrDefault();
            LoaderStatusText = LoaderVersions.Count == 0
                ? $"{loaderName} 没有适用于 {gameVersion} 的版本"
                : $"{loaderName} {gameVersion} 共 {LoaderVersions.Count} 个版本，当前选中 {SelectedLoaderVersion?.Version}";
        }
        catch (Exception ex)
        {
            LoaderStatusText = $"获取版本失败：{ex.Message}";
        }
        finally
        {
            IsLoaderBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReloadLoaderVersionsAsync() => await LoadLoaderVersionsAsync();

    /// <summary>安装带加载器的实例：原版 + Fabric/Quilt profile 合并后下载依赖库。</summary>
    [RelayCommand]
    private async Task InstallLoaderAsync()
    {
        if (IsInstalling)
        {
            return;
        }

        if (!CanInstallLoader)
        {
            LoaderStatusText = $"{SelectedLoaderName} 暂不支持一键安装（仅可查询版本号）";
            return;
        }

        if (SelectedLoaderVersion is null || string.IsNullOrWhiteSpace(SelectedLoaderGameVersion))
        {
            LoaderStatusText = "请先选择游戏版本与加载器版本";
            return;
        }

        var gameVersion = SelectedLoaderGameVersion;
        var loaderName = SelectedLoaderName;
        var loaderVersion = SelectedLoaderVersion.Version;
        var instanceId = $"{gameVersion}-{loaderName.ToLowerInvariant()}-{loaderVersion}";
        var displayName = $"{gameVersion} · {loaderName} {loaderVersion}";

        IsInstalling = true;
        InstallPercent = 0;
        IsInstallIndeterminate = false;
        InstallStageText = string.Empty;
        try
        {
            var progress = new Progress<InstallProgress>(p => Dispatcher.UIThread.Post(() =>
            {
                IsInstallIndeterminate = p.Percent < 0;
                InstallPercent = p.Percent < 0 ? 0 : p.Percent;
                InstallStageText = $"{StageLabel(p.Stage)} · {p.Message}";
                LoaderStatusText = $"正在安装 {displayName}：{InstallStageText}";
            }));

            var summary = await _facade.InstallAsync(
                new InstallRequest(
                    instanceId,
                    displayName,
                    gameVersion,
                    SelectedLoader,
                    loaderVersion,
                    IsolationLevel.ModsConfig,
                    InstallMode.New),
                progress,
                CancellationToken.None);

            InstallPercent = 100;
            IsInstallIndeterminate = false;
            InstallStageText = "安装完成";
            LoaderStatusText = $"已安装 {summary.Name}（{loaderName} {loaderVersion}），可在启动页启动";
            await LoadInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(i => i.Id == summary.Id) ?? SelectedInstance;
        }
        catch (Exception ex)
        {
            LoaderStatusText = $"安装失败：{ex.Message}";
            InstallStageText = "失败";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private static string StageLabel(InstallStage stage) => stage switch
    {
        InstallStage.VersionJson => "版本清单",
        InstallStage.Loader => "加载器",
        InstallStage.ClientJar => "客户端",
        InstallStage.Libraries => "依赖库",
        InstallStage.Natives => "本地库",
        InstallStage.Assets => "资源文件",
        InstallStage.Finalizing => "收尾",
        InstallStage.Done => "完成",
        _ => stage.ToString(),
    };

    /// <summary>点击搜索：回到第一页重新检索。</summary>
    [RelayCommand]
    private async Task SearchFirstAsync()
    {
        if (IsBusy)
        {
            return;
        }

        CurrentPage = 1;
        ModResults.Clear();
        await SearchModsAsync();
    }

    /// <summary>按关键字搜索；关键字为空时按下载量浏览热门模组。</summary>
    private async Task SearchModsAsync()
    {
        var query = ModQuery.Trim();

        IsBusy = true;
        StatusText = query.Length == 0 ? $"正在加载热门模组（{SourceName}）…" : $"正在搜索 {SourceName}…";
        try
        {
            var loader = SelectedInstance?.Loader;
            var gameVersion = SelectedInstance?.VersionId;
            var results = await _facade.SearchOnlineModsAsync(
                SelectedSource, query, SelectedModCategory?.Slug, loader, gameVersion, CurrentPage, CancellationToken.None);

            ModResults.Clear();
            foreach (var summary in results)
            {
                ModResults.Add(new OnlineModItem { Summary = summary, Icon = LoadIcon(summary.IconPath) });
            }

            StatusText = ModResults.Count == 0 ? "没有找到匹配的模组" : $"找到 {ModResults.Count} 个模组";
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>安装搜索结果到当前实例的 mods 目录。</summary>
    [RelayCommand]
    private async Task InstallModAsync(OnlineModItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (SelectedInstance is null)
        {
            StatusText = "请先在版本设置中创建或导入一个实例";
            return;
        }

        item.IsInstalling = true;
        try
        {
            var progress = new Progress<InstallProgress>(p =>
                Dispatcher.UIThread.Post(() => StatusText = p.Message));

            var fileName = await _facade.InstallOnlineModAsync(
                SelectedInstance.Id,
                SelectedSource,
                item.Summary.ProjectId,
                SelectedInstance.VersionId,
                SelectedInstance.Loader,
                progress,
                CancellationToken.None);

            StatusText = $"已安装 {fileName} 到 {SelectedInstance.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"安装失败：{ex.Message}";
        }
        finally
        {
            item.IsInstalling = false;
        }
    }
}
