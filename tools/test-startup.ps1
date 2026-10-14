$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixtureExe = Join-Path $root 'artifacts\OpenAI.Codex_Fixture\ChatGPT.exe'
$control = Join-Path $root 'artifacts\startup-fixture-command.txt'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\QuotaFloat\QuotaFloat.exe'
if (-not (Test-Path -LiteralPath $fixtureExe)) { throw 'Publish tests/CodexWindowFixture to artifacts/OpenAI.Codex_Fixture first.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QuotaWindowTest {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr first, IntPtr second);
}
'@
function Widget {
    return @(Get-Process -Name QuotaFloat -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $installed })
}
function Assert($condition, $message) {
    if (-not $condition) { throw $message }
    Write-Host ('PASS ' + $message)
}
function Handle($id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    if ($windows.Count -eq 0) { throw 'No widget window found.' }
    return [IntPtr]$windows[0].Current.NativeWindowHandle
}
$fixture = $null
try {
    $monitor = @(Get-Process -Name QuotaFloat.Launcher -ErrorAction SilentlyContinue)
    Assert ($monitor.Count -eq 1) 'independent monitor is running'
    foreach ($widget in (Widget)) { Stop-Process -Id $widget.Id -Force }
    Start-Sleep -Seconds 3
    Assert ((Widget).Count -eq 0) 'manual widget exit does not immediately relaunch it'
    Set-Content -LiteralPath $control -Value 'show' -Encoding ASCII
    $fixture = Start-Process -FilePath $fixtureExe -ArgumentList $control -WindowStyle Normal -PassThru
    Start-Sleep -Seconds 5
    $widgets = Widget
    Assert ($widgets.Count -eq 1) 'opening a Codex desktop window starts exactly one widget'
    $widgetId = $widgets[0].Id
    $handle = Handle $widgetId
    Set-Content -LiteralPath $control -Value 'hide' -Encoding ASCII
    [QuotaWindowTest]::PostMessage($handle, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 3
    Assert (-not [QuotaWindowTest]::IsWindowVisible($handle)) 'widget was minimized to tray'
    Set-Content -LiteralPath $control -Value 'show' -Encoding ASCII
    Start-Sleep -Seconds 4
    Assert ([QuotaWindowTest]::IsWindowVisible($handle) -and (Widget)[0].Id -eq $widgetId) 'reopening the same process restores the existing widget'
    Set-Content -LiteralPath $control -Value 'close' -Encoding ASCII
    Assert ($fixture.WaitForExit(5000)) 'first disposable Codex window closed'
    foreach ($widget in (Widget)) { Stop-Process -Id $widget.Id -Force }
    Set-Content -LiteralPath $control -Value 'show' -Encoding ASCII
    $fixture = Start-Process -FilePath $fixtureExe -ArgumentList $control -WindowStyle Normal -PassThru
    Start-Sleep -Seconds 5
    Assert ((Widget).Count -eq 1 -and (Widget)[0].Id -ne $widgetId) 'a replacement Codex process starts a new widget'
    Assert (@(Get-Process -Name QuotaFloat.Launcher).Count -eq 1) 'monitor remains a single instance'
} finally {
    if ($fixture -and -not $fixture.HasExited) {
        Set-Content -LiteralPath $control -Value 'close' -Encoding ASCII
        if (-not $fixture.WaitForExit(5000)) { Stop-Process -Id $fixture.Id -Force }
    }
}
