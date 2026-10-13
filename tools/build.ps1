param([switch]$Test)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=$PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:NUGET_PACKAGES=if($env:QUOTAFLOAT_NUGET_PACKAGES){$env:QUOTAFLOAT_NUGET_PACKAGES}else{Join-Path $PSScriptRoot 'packages'}
$sdk=$env:QUOTAFLOAT_DOTNET
if (-not $sdk) { $sdk=Join-Path $PSScriptRoot 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $sdk)) { $sdk='dotnet' }
if ($Test) {
    & $sdk run --project (Join-Path $root 'tests\QuotaFloat.Tests\QuotaFloat.Tests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
& $sdk publish (Join-Path $root 'src\QuotaFloat\QuotaFloat.csproj') -c Release -r win-x64 --self-contained false -o (Join-Path $root 'artifacts\QuotaFloat-win-x64')
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
& $sdk publish (Join-Path $root 'src\QuotaFloat.Launcher\QuotaFloat.Launcher.csproj') -c Release -r win-x64 --self-contained false -o (Join-Path $root 'artifacts\QuotaFloat-win-x64')
if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $root 'artifacts\QuotaFloat-win-x64\使用说明.md')
Copy-Item -LiteralPath (Join-Path $root '验证报告.md') -Destination (Join-Path $root 'artifacts\QuotaFloat-win-x64\验证报告.md')
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $root 'artifacts\QuotaFloat-win-x64\THIRD_PARTY_NOTICES.md')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $root 'artifacts\QuotaFloat-win-x64\LICENSE')
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination (Join-Path $root 'artifacts\QuotaFloat-win-x64') -Recurse -Force
Compress-Archive -Path (Join-Path $root 'artifacts\QuotaFloat-win-x64\*') -DestinationPath (Join-Path $root 'artifacts\QuotaFloat-win-x64.zip') -Force
