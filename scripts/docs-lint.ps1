#!/usr/bin/env pwsh
# 文档质量门禁：围栏配对、当前区段旧口径、第三方包登记。
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$excludeDirs = @('bin', 'obj', 'build', '_probe', '.trae', '.git')
$failures = [System.Collections.Generic.List[string]]::new()

function Get-RepoFiles {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$Filter
    )

    $files = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in Get-ChildItem -LiteralPath $Directory -Force) {
        if ($entry.PSIsContainer) {
            if ($excludeDirs -contains $entry.Name) { continue }
            $children = Get-RepoFiles -Directory $entry.FullName -Filter $Filter
            foreach ($child in $children) {
                $files.Add($child)
            }
        }
        elseif ($entry.Name -like $Filter) {
            $files.Add($entry.FullName)
        }
    }
    return $files
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$FullPath)

    $base = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $full = [System.IO.Path]::GetFullPath($FullPath)
    if ($full.StartsWith($base, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($base.Length).Replace('\', '/')
    }
    return [System.IO.Path]::GetFileName($FullPath)
}

# a) Markdown 围栏必须成对（奇数行即失败）
foreach ($file in Get-RepoFiles -Directory $repoRoot -Filter '*.md') {
    $count = @(Select-String -LiteralPath $file -Pattern '```' -SimpleMatch).Count
    if ($count % 2 -ne 0) {
        $failures.Add("$((Get-RelativePath -FullPath $file)): 围栏行数 $count（奇数，未配对）")
    }
}

# b) 当前区段不得出现旧口径（历史归档 docs/history 除外）
$legacyPatterns = @(
    @{ Name = 'Launcher.sln（应为 Launcher.slnx）'; Regex = 'Launcher\.sln(?!x)' },
    @{ Name = 'SystemDecorations=（Avalonia 已移除）'; Regex = 'SystemDecorations\s*=' },
    @{ Name = 'bangbang93.ForgeInstaller（已改用官方 maven）'; Regex = 'bangbang93\.ForgeInstaller' }
)

foreach ($file in Get-RepoFiles -Directory $repoRoot -Filter '*.md') {
    $relative = Get-RelativePath -FullPath $file
    if ($relative -like 'docs/history/*') { continue }
    foreach ($pattern in $legacyPatterns) {
        foreach ($match in Select-String -LiteralPath $file -Pattern $pattern.Regex) {
            $failures.Add("${relative}:$($match.LineNumber):$($match.Line.Trim())  [$($pattern.Name)]")
        }
    }
}

# c) 第三方包必须在 THIRD_PARTY_NOTICES.md 登记
$packageIds = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
$propsPath = Join-Path $repoRoot 'Directory.Packages.props'
if (Test-Path -LiteralPath $propsPath) {
    foreach ($match in Select-String -LiteralPath $propsPath -Pattern 'PackageVersion\s+Include="([^"]+)"') {
        [void]$packageIds.Add($match.Matches[0].Groups[1].Value)
    }
}

foreach ($csproj in Get-RepoFiles -Directory $repoRoot -Filter '*.csproj') {
    foreach ($match in Select-String -LiteralPath $csproj -Pattern 'PackageReference\s+Include="([^"]+)"') {
        [void]$packageIds.Add($match.Matches[0].Groups[1].Value)
    }
}

$noticesPath = Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md'
if (-not (Test-Path -LiteralPath $noticesPath)) {
    $failures.Add('THIRD_PARTY_NOTICES.md 不存在')
}
else {
    $notices = Get-Content -LiteralPath $noticesPath -Raw -Encoding utf8
    $missing = @($packageIds | Where-Object { -not $notices.Contains($_) })
    if ($missing.Count -gt 0) {
        $failures.Add("THIRD_PARTY_NOTICES.md 缺少包登记：$($missing -join ', ')")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "docs-lint 失败（$($failures.Count) 项）："
    foreach ($failure in $failures) {
        Write-Host "  - $failure"
    }
    exit 1
}

Write-Host 'docs-lint 通过'
exit 0
