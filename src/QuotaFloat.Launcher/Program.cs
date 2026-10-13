using System.Diagnostics;

// A small, windowless companion. It starts the widget at Windows sign-in, then
// starts it again when the Codex desktop app is fully closed and later reopened.
if (args.Length == 2 && args[0] == "--probe")
{
    File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { codexIsRunning = CodexIsRunning(), widgetExists = File.Exists(Path.Combine(AppContext.BaseDirectory, "QuotaFloat.exe")) }));
    return;
}

using var singleton = new Mutex(true, @"Local\QuotaFloat.Launcher", out var ownsMutex);
if (!ownsMutex) return;

var widgetPath = Path.Combine(AppContext.BaseDirectory, "QuotaFloat.exe");
StartWidgetIfAbsent();
var codexWasRunning = CodexIsRunning();

while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(5));
    if (!AutoStartIsEnabled()) break;
    var codexIsRunning = CodexIsRunning();
    if (codexIsRunning && !codexWasRunning) StartWidgetIfAbsent();
    codexWasRunning = codexIsRunning;
}

void StartWidgetIfAbsent()
{
    if (!File.Exists(widgetPath)) return;
    try
    {
        using var existing = new Mutex(false, @"Local\QuotaFloat.Main", out var newlyCreated);
        if (!newlyCreated) return;
        using var process = Process.Start(new ProcessStartInfo(widgetPath) { UseShellExecute = true });
    }
    catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException) { }
}

bool CodexIsRunning()
{
    foreach (var process in Process.GetProcessesByName("ChatGPT"))
    {
        using (process)
        {
            try
            {
                if (process.MainModule?.FileName?.Contains("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true) return true;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException) { }
        }
    }
    return false;
}

bool AutoStartIsEnabled()
{
    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
    var command = key?.GetValue("QuotaFloat") as string;
    return command?.Contains("QuotaFloat.Launcher.exe", StringComparison.OrdinalIgnoreCase) == true;
}
