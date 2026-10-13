$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'QuotaFloat-source'
$zip = Join-Path $artifacts 'QuotaFloat-source.zip'
$resolvedArtifacts = [IO.Path]::GetFullPath($artifacts).TrimEnd('\')
$resolvedStage = [IO.Path]::GetFullPath($stage)
if ([IO.Path]::GetDirectoryName($resolvedStage) -ine $resolvedArtifacts) { throw 'Unsafe staging path.' }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$rootFiles = @('.gitignore','LICENSE','README.md','CONTRIBUTING.md','THIRD_PARTY_NOTICES.md','Directory.Build.props','安装说明.md','验证报告.md')
foreach ($name in $rootFiles) {
    $file = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Missing source file: ' + $name) }
    Copy-Item -LiteralPath $file -Destination $stage
}

foreach ($directory in @('src','tests')) {
    $directoryRoot = Join-Path $root $directory
    foreach ($file in Get-ChildItem -LiteralPath $directoryRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in @('.cs','.csproj','.xaml','.ico','.manifest')
    }) {
        $relative = $file.FullName.Substring($root.Length + 1)
        $destination = Join-Path $stage $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}

$tools = @('build.ps1','package.ps1','source-package.ps1','Install.ps1','Install.cmd','Uninstall.ps1','Uninstall.cmd','make-icon.py','probe.py')
New-Item -ItemType Directory -Path (Join-Path $stage 'tools') -Force | Out-Null
foreach ($name in $tools) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'tools')
}
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $stage -Recurse

$forbidden = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($stage.Length + 1)
    $_.Name -match '^(auth\.json|settings\.json|.*\.db(-wal|-shm)?)$' -or $relative -match '(^|\\)(bin|obj|artifacts|packages)\\'
})
if ($forbidden.Count) { throw ('Private or generated files in source package: ' + ($forbidden.Name -join ', ')) }

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    if (-not ($names -contains 'LICENSE') -or -not ($names -contains 'src/QuotaFloat/QuotaFloat.csproj')) {
        throw 'Source archive integrity check failed.'
    }
} finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName,Length,LastWriteTime
