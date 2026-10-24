# 发布打包：publish → 生成 integrity.json → 校验 → vpk pack（Setup.exe + delta + Portable.zip）
# 不签名（§12.1），不产 MSI（--msi 从不使用）。
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Channel = 'win',
    [string]$Runtime = 'win-x64',
    [string]$RepositoryUrl = 'https://github.com/limpidus-xiaoche/Lumen',
    [string]$Tag,
    [string]$MinSupported = '0.0.0',
    [string]$Notes = '',
    [string]$OutputRoot = 'build'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root (Join-Path $OutputRoot 'publish')
$releasesDir = Join-Path $root (Join-Path $OutputRoot 'releases')

function Get-RelativePath([string]$BaseDir, [string]$FullPath) {
    $base = (Resolve-Path -LiteralPath $BaseDir).Path.TrimEnd('\') + '\'
    $full = (Resolve-Path -LiteralPath $FullPath).Path
    if ($full.StartsWith($base, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($base.Length)
    }
    return $full
}

if (-not $Tag) { $Tag = "v$Version" }

Write-Host "== Lumen 打包：version=$Version channel=$Channel tag=$Tag"

if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
if (Test-Path $releasesDir) { Remove-Item -LiteralPath $releasesDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$env:AVALONIA_TELEMETRY_OPTOUT = '1'

Write-Host '== dotnet publish（self-contained / R2R / 不裁剪 / 不单文件）'
dotnet publish (Join-Path $root 'src/Launcher.App/Launcher.App.csproj') `
    -c Release -r $Runtime --self-contained true `
    -p:PublishReadyToRun=true -p:PublishSingleFile=false -p:PublishTrimmed=false `
    -p:DebugType=none -p:DebugSymbols=false `
    -p:Version=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }

Write-Host '== 生成 integrity.json'
$excluded = @('data', 'logs', 'exports', 'cache')
$files = @()
Get-ChildItem -LiteralPath $publishDir -Recurse -File | ForEach-Object {
    $relative = (Get-RelativePath $publishDir $_.FullName) -replace '\\', '/'
    $top = ($relative -split '/')[0]
    if ($relative -eq 'integrity.json' -or $excluded -contains $top) { return }
    $files += [ordered]@{
        path   = $relative
        size   = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if ($files.Count -eq 0) { throw 'publish 目录为空，无法生成完整性清单' }

$manifest = [ordered]@{
    version     = $Version
    algorithm   = 'sha256'
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    files       = $files
}
$manifestPath = Join-Path $publishDir 'integrity.json'
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host '== 校验 integrity.json ↔ 产物'
& (Join-Path $PSScriptRoot 'verify-integrity.ps1') -PublishDir $publishDir -ManifestPath $manifestPath
if ($LASTEXITCODE -ne 0) { throw '完整性清单校验失败' }

Write-Host '== vpk pack'
dotnet vpk pack `
    --packId Lumen `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe Lumen.exe `
    --packTitle 'Lumen Launcher' `
    --packAuthors 'Lumen' `
    --runtime $Runtime `
    --channel $Channel `
    --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw 'vpk pack 失败' }

$msi = Get-ChildItem -LiteralPath $releasesDir -Filter '*.msi' -File
if ($msi) { throw "禁止生成 MSI 产物：$($msi.Name -join ', ')" }

$setup = Get-ChildItem -LiteralPath $releasesDir -Filter '*Setup*.exe' -File | Select-Object -First 1
if (-not $setup) { throw 'vpk pack 未生成 Setup.exe' }
$portable = Get-ChildItem -LiteralPath $releasesDir -Filter '*Portable*.zip' -File | Select-Object -First 1
if (-not $portable) { throw 'vpk pack 未生成 Portable.zip' }

Write-Host '== 生成 Lumen 更新清单（固定字段，§12.1）'
$velopackFeed = Join-Path $releasesDir "releases.$Channel.json"
if (Test-Path $velopackFeed) {
    Rename-Item -LiteralPath $velopackFeed -NewName "velopack.$Channel.json"
}

$setupUrl = "$($RepositoryUrl.TrimEnd('/'))/releases/download/$Tag/$($setup.Name)"
$updateManifest = [ordered]@{
    version     = $Version
    minSupported = $MinSupported
    url         = $setupUrl
    sha256      = (Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    size        = $setup.Length
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    notes       = $Notes
}
$updateJson = $updateManifest | ConvertTo-Json -Depth 2
[System.IO.File]::WriteAllText(
    (Join-Path $releasesDir "releases.$Channel.json"),
    $updateJson,
    (New-Object System.Text.UTF8Encoding($false)))

Write-Host '== 完成，产物：'
Get-ChildItem -LiteralPath $releasesDir -File |
    Select-Object Name, Length |
    Format-Table -AutoSize | Out-String -Width 200 | Write-Host
