using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuotaFloat.Startup;

public sealed record DesktopWindow(int ProcessId, long CreatedAt, long Handle);

public sealed class ReopenTracker
{
    private HashSet<DesktopWindow> previous = [];
    public bool Observe(IEnumerable<DesktopWindow> windows)
    {
        var current = windows.ToHashSet();
        var reopened = current.Except(previous).Any();
        previous = current;
        return reopened;
    }
}

public static class CodexWindows
{
    public static IReadOnlyList<DesktopWindow> Read()
    {
        var candidates = new Dictionary<int, long>();
        foreach (var process in Process.GetProcessesByName("ChatGPT"))
        {
            using (process)
            {
                var handle = OpenProcess(0x1000, false, process.Id);
                if (handle == IntPtr.Zero) continue;
                try
                {
                    var path = new StringBuilder(32768); var length = path.Capacity;
                    if (QueryFullProcessImageName(handle, 0, path, ref length)
                        && path.ToString().Contains("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
                        && GetProcessTimes(handle, out var created, out _, out _, out _)) candidates[process.Id] = created;
                }
                finally { CloseHandle(handle); }
            }
        }
        var windows = new List<DesktopWindow>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var pid);
            if (candidates.TryGetValue((int)pid, out var created) && IsWindowVisible(window)
                && GetWindow(window, 4) == IntPtr.Zero && GetWindowRect(window, out var rect)
                && (IsIconic(window) || (rect.Right - rect.Left >= 200 && rect.Bottom - rect.Top >= 100)))
                windows.Add(new((int)pid, created, window.ToInt64()));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static bool InJob()
    { using var process = Process.GetCurrentProcess(); return IsProcessInJob(process.Handle, IntPtr.Zero, out var inJob) && inJob; }

    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll")] private static extern bool GetProcessTimes(IntPtr process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool inJob);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
}
