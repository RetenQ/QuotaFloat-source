using System.Diagnostics;
using System.Text.Json;
using QuotaFloat.Startup;

var launcherPath = Path.Combine(AppContext.BaseDirectory, "QuotaFloat.Launcher.exe");
var widgetPath = Path.Combine(AppContext.BaseDirectory, "QuotaFloat.exe");
try
{
    if (args.Length == 2 && args[0] == "--probe")
    {
        File.WriteAllText(args[1], JsonSerializer.Serialize(new { windows = CodexWindows.Read(), inWindowsJob = CodexWindows.InJob(), widgetExists = File.Exists(widgetPath), startupEnabled = StartupRegistration.Enabled, scheduledTask = StartupRegistration.TaskName }));
        return;
    }
    if (args is ["--register-startup"]) { StartupRegistration.Enable(launcherPath, false); return; }
    if (args is ["--disable-startup"]) { StartupRegistration.Disable(); return; }
    if (args is not ["--monitor"])
    {
        // Windows owns the long-lived process instead of the app that launched it.
        if (StartupRegistration.Enabled) StartupRegistration.StartMonitor(launcherPath);
        else StartOrShowWidget();
        return;
    }

    using var singleton = new Mutex(false, @"Local\QuotaFloat.Launcher", out var created);
    if (!created || !StartupRegistration.Enabled) return;
    using var stopMonitor = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\QuotaFloat.StopMonitor");
    stopMonitor.Reset();
    Log("monitor started; job=" + CodexWindows.InJob());
    var tracker = new ReopenTracker();
    tracker.Observe(CodexWindows.Read());
    var pending = !StartOrShowWidget();
    var lastPreferenceCheck = DateTime.UtcNow;
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (stopMonitor.WaitOne(0)) break;
        if (DateTime.UtcNow - lastPreferenceCheck > TimeSpan.FromSeconds(30))
        {
            if (!StartupRegistration.Enabled) break;
            lastPreferenceCheck = DateTime.UtcNow;
        }
        if (tracker.Observe(CodexWindows.Read())) { pending = true; Log("Codex window reopened"); }
        if (pending) pending = !StartOrShowWidget();
    }
    Log("monitor stopped; startup disabled");
}
catch (Exception e) when (e is not OutOfMemoryException)
{
    Log("launcher error: " + e.GetType().Name + " (" + e.HResult + ")");
    Environment.ExitCode = 1;
}

bool StartOrShowWidget()
{
    if (!File.Exists(widgetPath)) return false;
    try
    {
        try
        {
            using var existing = Mutex.OpenExisting(@"Local\QuotaFloat.Main");
            try { using var show = EventWaitHandle.OpenExisting(@"Local\QuotaFloat.Show"); show.Set(); }
            catch (WaitHandleCannotBeOpenedException) { return false; }
            return true;
        }
        catch (WaitHandleCannotBeOpenedException) { }
        using var process = Process.Start(new ProcessStartInfo(widgetPath) { UseShellExecute = true });
        return process != null;
    }
    catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
    { Log("widget launch failed: " + e.GetType().Name); return false; }
}

void Log(string message)
{
    try
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaFloat");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "launcher.log");
        if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Delete(path);
        File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
}
