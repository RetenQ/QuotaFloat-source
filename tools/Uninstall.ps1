param([switch]$RemoveData)
$ErrorActionPreference = 'Stop'
$installDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcher = Join-Path $installDir 'QuotaFloat.Launcher.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$registered = (Get-ItemProperty -Path $runKey -Name QuotaFloat -ErrorAction SilentlyContinue).QuotaFloat
if ($registered -and $registered.Trim('"') -ieq $launcher) {
    Remove-ItemProperty -Path $runKey -Name QuotaFloat
}
$scheduler = New-Object -ComObject 'Schedule.Service'
$scheduler.Connect()
$taskName = 'QuotaFloat.Startup.' + [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
try {
    $task = $scheduler.GetFolder('\').GetTask($taskName)
    if ($task.Definition.Actions.Item(1).Path -ieq $launcher) {
        $task.Stop(0)
        $scheduler.GetFolder('\').DeleteTask($taskName, 0)
    }
} catch { if ($_.Exception.HResult -ne -2147024894) { throw } }

Get-Process -Name 'QuotaFloat','QuotaFloat.Launcher' -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $runningPath = $_.MainModule.FileName
        if ($runningPath -and [IO.Path]::GetDirectoryName($runningPath).TrimEnd('\') -ieq $installDir.TrimEnd('\')) {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
        }
    } catch [System.ComponentModel.Win32Exception] { } catch [System.InvalidOperationException] { }
}

$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\QuotaFloat.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcutPath).TargetPath -ieq (Join-Path $installDir 'QuotaFloat.exe')) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}
Remove-Item -LiteralPath $installDir -Recurse -Force
if ($RemoveData) {
    Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'QuotaFloat') -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'QuotaFloat was removed. User data was kept unless -RemoveData was specified.'
