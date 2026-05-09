using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowCards.Core.Tracking;

public readonly record struct TargetWindow(
    IntPtr Hwnd,
    string ProcessName,
    string Title,
    string ClassName);

public static class TargetWindowDetector
{
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "TaskListThumbnailWnd",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "ApplicationManager_DesktopShellWindow"
    };

    public static IntPtr GetForegroundWindow() => PInvoke.GetForegroundWindow();

    public static bool TryClassify(IntPtr hwnd, out TargetWindow tw)
    {
        tw = default;
        var h = (HWND)hwnd;

        if (!PInvoke.IsWindow(h) || !PInvoke.IsWindowVisible(h))
            return false;

        if (PInvoke.GetWindow(h, GET_WINDOW_CMD.GW_OWNER) != IntPtr.Zero)
            return false;

        if (IsOwnProcess(h))
            return false;

        var className = GetClassName(h);
        if (ExcludedClasses.Contains(className))
            return false;

        if (!PInvoke.GetWindowRect(h, out var rect) || rect.right <= rect.left || rect.bottom <= rect.top)
            return false;

        var title = GetWindowText(h);
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var process = GetProcessName(h) ?? string.Empty;
        tw = new TargetWindow(hwnd, process, title, className);
        return true;
    }

    private static bool IsOwnProcess(HWND hwnd)
    {
        uint pid = 0;
        unsafe { PInvoke.GetWindowThreadProcessId(hwnd, &pid); }
        return pid != 0 && (int)pid == Environment.ProcessId;
    }

    private static unsafe string GetClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        fixed (char* p = buffer)
        {
            int len = PInvoke.GetClassName(hwnd, (PWSTR)p, buffer.Length);
            return len > 0 ? new string(buffer[..len]) : string.Empty;
        }
    }

    private static unsafe string GetWindowText(HWND hwnd)
    {
        int len = PInvoke.GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;
        Span<char> buffer = stackalloc char[len + 1];
        fixed (char* p = buffer)
        {
            int copied = PInvoke.GetWindowText(hwnd, (PWSTR)p, buffer.Length);
            return copied > 0 ? new string(buffer[..copied]) : string.Empty;
        }
    }

    private static string? GetProcessName(HWND hwnd)
    {
        try
        {
            uint pid = 0;
            unsafe { PInvoke.GetWindowThreadProcessId(hwnd, &pid); }
            if (pid == 0) return null;
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
