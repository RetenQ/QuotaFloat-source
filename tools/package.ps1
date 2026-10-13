param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'QuotaFloat-Setup-win-x64'
$bin = Join-Path $stage 'bin'
$zip = Join-Path $artifacts 'QuotaFloat-Setup-win-x64.zip'
$env:DOTNET_CLI_HOME = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = if($env:QUOTAFLOAT_NUGET_PACKAGES){$env:QUOTAFLOAT_NUGET_PACKAGES}else{Join-Path $PSScriptRoot 'packages'}
$sdk = $env:QUOTAFLOAT_DOTNET
if (-not $sdk) { $sdk = Join-Path $PSScriptRoot 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }

if ($Test) {
    & $sdk run --project (Join-Path $root 'tests\QuotaFloat.Tests\QuotaFloat.Tests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

# Restrict cleanup to this fixed package staging directory.
$resolvedArtifacts = [IO.Path]::GetFullPath($artifacts).TrimEnd('\')
$resolvedStage = [IO.Path]::GetFullPath($stage)
if ([IO.Path]::GetDirectoryName($resolvedStage) -ine $resolvedArtifacts) { throw 'Unsafe staging path.' }
if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
New-Item -ItemType Directory -Path $bin -Force | Out-Null

$publishArgs = @('-c','Release','-r','win-x64','--self-contained','true','-p:PublishReadyToRun=false','-p:DebugType=none','-p:DebugSymbols=false','-o',$bin)
& $sdk publish (Join-Path $root 'src\QuotaFloat\QuotaFloat.csproj') @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Widget publish failed.' }
& $sdk publish (Join-Path $root 'src\QuotaFloat.Launcher\QuotaFloat.Launcher.csproj') @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed.' }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.cmd') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination $bin
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.cmd') -Destination $bin
Copy-Item -LiteralPath (Join-Path $root '安装说明.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $bin '使用说明.md')
Copy-Item -LiteralPath (Join-Path $root '验证报告.md') -Destination (Join-Path $bin '验证报告.md')
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $bin
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $bin
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $bin -Recurse -Force

foreach ($required in @('QuotaFloat.exe','QuotaFloat.Launcher.exe','QuotaFloat.runtimeconfig.json','QuotaFloat.Launcher.runtimeconfig.json','e_sqlite3.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bin $required) -PathType Leaf)) { throw ('Missing package file: ' + $required) }
}
$runtime = Get-Content -LiteralPath (Join-Path $bin 'QuotaFloat.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $runtime.runtimeOptions.includedFrameworks) { throw 'Widget is not self-contained.' }

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    if (-not ($entryNames -contains 'Install.cmd') -or -not ($entryNames -contains 'bin/QuotaFloat.exe')) {
        throw 'Archive integrity check failed.'
    }
} finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName,Length,LastWriteTime

