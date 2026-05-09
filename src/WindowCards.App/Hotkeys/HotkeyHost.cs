using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WindowCards.App.Hotkeys;

public sealed class HotkeyHost : IDisposable
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private const int WM_HOTKEY = 0x0312;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private HwndSource? _source;
    private int _nextId = 1;
    private readonly Dictionary<int, Action> _handlers = new();
    private bool _disposed;

    public HotkeyHost()
    {
        var parameters = new HwndSourceParameters("WindowCardsHotkeyHost")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public int Register(uint modifiers, uint virtualKey, Action onPressed)
    {
        if (_source is null) throw new ObjectDisposedException(nameof(HotkeyHost));

        int id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, modifiers | MOD_NOREPEAT, virtualKey))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"RegisterHotKey failed (Win32 error {err}).");
        }
        _handlers[id] = onPressed;
        return id;
    }

    public void Unregister(int id)
    {
        if (_source is null) return;
        UnregisterHotKey(_source.Handle, id);
        _handlers.Remove(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var h))
            {
                handled = true;
                try { h(); } catch { /* swallow to keep hook alive */ }
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_source is not null)
        {
            foreach (var id in _handlers.Keys.ToArray())
                UnregisterHotKey(_source.Handle, id);
            _handlers.Clear();
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
