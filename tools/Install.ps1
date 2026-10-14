param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\QuotaFloat'),
    [switch]$SkipRegistration,
    [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'
$packageDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $packageDir 'bin'
$widget = Join-Path $sourceDir 'QuotaFloat.exe'
$launcher = Join-Path $sourceDir 'QuotaFloat.Launcher.exe'
if (-not (Test-Path -LiteralPath $widget -PathType Leaf) -or -not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw 'Package is incomplete. Extract the entire ZIP before running Install.cmd.'
}

$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if ($InstallDir.TrimEnd('\') -eq $sourceDir.TrimEnd('\')) {
    throw 'Choose an install directory outside the extracted package.'
}

if (-not $SkipRegistration) {
    $scheduler = New-Object -ComObject 'Schedule.Service'
    $scheduler.Connect()
    $taskName = 'QuotaFloat.Startup.' + [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    try {
        $task = $scheduler.GetFolder('\').GetTask($taskName)
        if ($task.Definition.Actions.Item(1).Path -ieq (Join-Path $InstallDir 'QuotaFloat.Launcher.exe')) {
            $task.Enabled = $false
            $task.Stop(0)
        }
    } catch {
        if ($_.Exception.HResult -ne -2147024894) { throw }
    }
}

# Stop only this application's processes from the selected installation path.
Get-Process -Name 'QuotaFloat','QuotaFloat.Launcher' -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $runningPath = $_.MainModule.FileName
        if ($runningPath -and [IO.Path]::GetDirectoryName($runningPath).TrimEnd('\') -ieq $InstallDir.TrimEnd('\')) {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
            if (-not $_.WaitForExit(10000)) { throw 'Application did not exit; try again.' }
        }
    } catch [System.ComponentModel.Win32Exception] { } catch [System.InvalidOperationException] { }
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $sourceDir '*') -Destination $InstallDir -Recurse -Force

$installedWidget = Join-Path $InstallDir 'QuotaFloat.exe'
$installedLauncher = Join-Path $InstallDir 'QuotaFloat.Launcher.exe'
if (-not (Test-Path -LiteralPath $installedWidget -PathType Leaf) -or -not (Test-Path -LiteralPath $installedLauncher -PathType Leaf)) {
    throw 'The application files were not copied successfully.'
}

if (-not $SkipRegistration) {
    $registration = Start-Process -FilePath $installedLauncher -ArgumentList '--register-startup' -WindowStyle Hidden -Wait -PassThru
    if ($registration.ExitCode -ne 0) { throw 'Startup registration failed. See %LOCALAPPDATA%\QuotaFloat\launcher.log.' }

    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $shortcutPath = Join-Path $startMenu 'QuotaFloat.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedWidget
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = $installedWidget
    $shortcut.Save()
}

$codexDir = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
$codexFound = [bool](Get-Command codex.exe -ErrorAction SilentlyContinue)
if (-not $codexFound -and (Test-Path -LiteralPath $codexDir)) {
    $codexFound = [bool](Get-ChildItem -LiteralPath $codexDir -Filter codex.exe -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1)
}

if (-not $SkipLaunch) {
    if ($SkipRegistration) { Start-Process -FilePath $installedWidget | Out-Null }
    else { Start-Process -FilePath $installedLauncher -WindowStyle Hidden | Out-Null }
}

Write-Host ('Installed: ' + $InstallDir)
if (-not $SkipRegistration) { Write-Host 'Windows sign-in and Codex reopen startup: enabled.' }
if (-not $codexFound) { Write-Warning 'Codex was not found. Install Codex and sign in with a ChatGPT account to read official quota.' }
Write-Host 'Settings and usage data are stored separately in %LOCALAPPDATA%\QuotaFloat.'
