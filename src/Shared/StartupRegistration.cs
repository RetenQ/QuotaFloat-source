using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace QuotaFloat.Startup;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string TaskName => "QuotaFloat.Startup." + WindowsIdentity.GetCurrent().User!.Value;
    public static bool Enabled
    {
        get
        {
            dynamic service = Connect();
            try { return (bool)service.GetFolder("\\").GetTask(TaskName).Enabled; }
            catch (Exception e) when ((uint)e.HResult == 0x80070002) { return false; }
            finally { Marshal.FinalReleaseComObject(service); }
        }
    }

    public static void Enable(string launcher, bool startNow)
    {
        EnsureTask(launcher);
        // Packaged and unpackaged callers can see different HKCU views; the task is authoritative.
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true)) key?.DeleteValue("QuotaFloat", false);
        if (startNow) StartMonitor(launcher);
    }

    public static void Disable()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true)) key?.DeleteValue("QuotaFloat", false);
        try { using var signal = EventWaitHandle.OpenExisting(@"Local\QuotaFloat.StopMonitor"); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { }
        dynamic service = Connect();
        try { service.GetFolder("\\").DeleteTask(TaskName, 0); }
        catch (Exception e) when ((uint)e.HResult == 0x80070002) { }
        finally { Marshal.FinalReleaseComObject(service); }
    }

    public static void StartMonitor(string launcher)
    {
        if (!Enabled) return;
        EnsureTask(launcher);
        dynamic service = Connect();
        try { service.GetFolder("\\").GetTask(TaskName).Run(null); }
        finally { Marshal.FinalReleaseComObject(service); }
    }

    private static object Connect()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        service.Connect();
        return service;
    }

    private static void EnsureTask(string launcher)
    {
        dynamic service = Connect();
        try
        {
            dynamic folder = service.GetFolder("\\");
            try
            {
                dynamic existing = folder.GetTask(TaskName);
                dynamic action = existing.Definition.Actions.Item(1);
                if (existing.Enabled && string.Equals((string)action.Path, launcher, StringComparison.OrdinalIgnoreCase)
                    && (string)action.Arguments == "--monitor" && (string)existing.Definition.RegistrationInfo.Version == "1.4.1") return;
            }
            catch (Exception e) when ((uint)e.HResult == 0x80070002) { }

            dynamic task = service.NewTask(0);
            task.RegistrationInfo.Description = "QuotaFloat: Windows sign-in, Codex window reopening and monitor recovery.";
            task.RegistrationInfo.Version = "1.4.1";
            var sid = WindowsIdentity.GetCurrent().User!.Value;
            task.Principal.UserId = sid;
            task.Principal.LogonType = 3;
            task.Principal.RunLevel = 0;
            task.Settings.MultipleInstances = 2;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.ExecutionTimeLimit = "PT0S";
            task.Settings.StartWhenAvailable = true;
            task.Settings.RestartCount = 3;
            task.Settings.RestartInterval = "PT1M";
            dynamic login = task.Triggers.Create(9);
            login.UserId = sid;
            dynamic recovery = task.Triggers.Create(1);
            recovery.StartBoundary = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss");
            recovery.Repetition.Interval = "PT1M";
            recovery.Repetition.StopAtDurationEnd = false;
            dynamic launch = task.Actions.Create(0);
            launch.Path = launcher;
            launch.Arguments = "--monitor";
            launch.WorkingDirectory = Path.GetDirectoryName(launcher);
            folder.RegisterTaskDefinition(TaskName, task, 6, sid, null, 3, null);
        }
        finally { Marshal.FinalReleaseComObject(service); }
    }
}
