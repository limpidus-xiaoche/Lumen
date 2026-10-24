# 校验 integrity.json 与产物目录一致（§12.1 规则 7）。
# 任何缺失 / size / sha256 不符都直接失败，供 CI 与本地打包复用。
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

if (-not $ManifestPath) {
    $ManifestPath = Join-Path $PublishDir 'integrity.json'
}

if (-not (Test-Path $ManifestPath)) {
    throw "完整性清单缺失：$ManifestPath"
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
if (-not $manifest.files -or $manifest.files.Count -eq 0) {
    throw "完整性清单没有文件条目：$ManifestPath"
}

$root = (Resolve-Path $PublishDir).Path
$failures = New-Object System.Collections.Generic.List[string]

function Get-RelativePath([string]$BaseDir, [string]$FullPath) {
    $base = (Resolve-Path -LiteralPath $BaseDir).Path.TrimEnd('\') + '\'
    $full = (Resolve-Path -LiteralPath $FullPath).Path
    if ($full.StartsWith($base, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($base.Length)
    }
    return $full
}

foreach ($entry in $manifest.files) {
    $full = Join-Path $root ($entry.path -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        $failures.Add("$($entry.path)：文件缺失")
        continue
    }

    $file = Get-Item -LiteralPath $full
    if ($file.Length -ne $entry.size) {
        $failures.Add("$($entry.path)：size $($file.Length) != $($entry.size)")
        continue
    }

    $hash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) {
        $failures.Add("$($entry.path)：sha256 不符")
    }
}

$excluded = @('data', 'logs', 'exports', 'cache')
$listed = @{}
foreach ($entry in $manifest.files) { $listed[$entry.path] = $true }

Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
    $relative = (Get-RelativePath $root $_.FullName) -replace '\\', '/'
    $top = ($relative -split '/')[0]
    if ($relative -eq 'integrity.json' -or $excluded -contains $top) { return }
    if (-not $listed.ContainsKey($relative)) {
        $failures.Add("$relative：产物存在但清单未登记")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "完整性清单与产物不一致（$($failures.Count) 项）。"
}

Write-Host "完整性清单校验通过：$($manifest.files.Count) 个文件，版本 $($manifest.version)"
