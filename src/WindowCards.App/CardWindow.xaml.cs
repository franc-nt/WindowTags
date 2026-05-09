using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowCards.Core.Interop;
using WindowCards.Core.Tracking;
using WindowCards.Models;

namespace WindowCards.App;

public partial class CardWindow : Window
{
    private readonly CardConfig _config;
    private readonly WindowTracker _tracker;
    private IntPtr _hwnd;

    private bool _dragging;
    private POINT _dragStartCursor;
    private int _dragStartCardX;
    private int _dragStartCardY;

    public IntPtr TargetHwnd => _tracker.TargetHwnd;
    public string CurrentText => CardText.Text;
    public string CurrentBackgroundHex => _config.BackgroundHex;

    public event Action<CardWindow>? EditRequested;
    public event Action<CardWindow>? RemoveRequested;
    public event Action? ExitRequested;

    public CardWindow(IntPtr targetHwnd, CardConfig config)
    {
        InitializeComponent();

        _config = config;
        _tracker = new WindowTracker(targetHwnd);

        Width = _config.Width;
        Height = _config.Height;
        CardText.Text = _config.Text;
        ApplyColors();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;

        _tracker.BoundsChanged += OnTargetBoundsChanged;
        _tracker.Minimized += OnTargetMinimized;
        _tracker.Restored += OnTargetRestored;
        _tracker.Destroyed += OnTargetDestroyed;
    }

    private void ApplyColors()
    {
        try
        {
            var bg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.BackgroundHex);
            var fg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.ForegroundHex);
            CardBorder.Background = new SolidColorBrush(bg);
            CardText.Foreground = new SolidColorBrush(fg);
        }
        catch
        {
            // keep XAML defaults on bad hex
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        CardWindowStyler.ApplyOverlayStyles(_hwnd);
        CardWindowStyler.SetOwner(_hwnd, _tracker.TargetHwnd);

        var src = HwndSource.FromHwnd(_hwnd);
        src?.AddHook(WndProc);
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int SC_MOVE = 0xF010;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_MAXIMIZE = 0xF030;
    private const int SC_RESTORE = 0xF120;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_SYSCOMMAND:
            {
                int cmd = wParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MOVE || cmd == SC_MAXIMIZE ||
                    cmd == SC_MINIMIZE || cmd == SC_RESTORE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                break;
            }

            case WM_MOUSEACTIVATE:
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_tracker.IsCurrentlyMinimized())
        {
            Hide();
        }
        else
        {
            PositionOverTarget(_tracker.CurrentBounds());
        }
    }

    private void OnTargetBoundsChanged(WindowBounds bounds)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_hwnd == IntPtr.Zero) return;
            if (_dragging) return;
            if (!IsVisible) Show();
            PositionOverTarget(bounds);
        });
    }

    private void OnTargetMinimized()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_hwnd == IntPtr.Zero) return;
            CardWindowStyler.Hide(_hwnd);
        });
    }

    private void OnTargetRestored()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_hwnd == IntPtr.Zero) return;
            PositionOverTarget(_tracker.CurrentBounds());
        });
    }

    private void OnTargetDestroyed()
    {
        Dispatcher.BeginInvoke(new Action(Close));
    }

    private void PositionOverTarget(WindowBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (_hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(this);

        double widthDip = ActualWidth > 0 ? ActualWidth : _config.Width;
        double heightDip = ActualHeight > 0 ? ActualHeight : _config.Height;

        int width = (int)(widthDip * dpi.DpiScaleX);
        int height = (int)(heightDip * dpi.DpiScaleY);
        int offsetX = (int)(_config.OffsetX * dpi.DpiScaleX);
        int offsetY = (int)(_config.OffsetY * dpi.DpiScaleY);

        int x = bounds.Left + offsetX;
        int y = bounds.Top + offsetY;

        _config.Width = widthDip;
        _config.Height = heightDip;

        CardWindowStyler.MoveTo(_hwnd, x, y, width, height, show: true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _tracker.Dispose();
    }

    public void SetText(string text)
    {
        _config.Text = text;
        CardText.Text = text;
    }

    public void SetColors(string backgroundHex, string foregroundHex)
    {
        _config.BackgroundHex = backgroundHex;
        _config.ForegroundHex = foregroundHex;
        ApplyColors();
    }

    private void OnEditMenuClick(object sender, RoutedEventArgs e) => EditRequested?.Invoke(this);
    private void OnRemoveMenuClick(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this);
    private void OnExitMenuClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

    private void OnBorderMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (_hwnd == IntPtr.Zero) return;

        GetCursorPos(out _dragStartCursor);
        var card = WindowGeometry.GetWindowRectBounds(_hwnd);
        _dragStartCardX = card.Left;
        _dragStartCardY = card.Top;

        _dragging = true;
        CardBorder.CaptureMouse();
        e.Handled = true;
    }

    private void OnBorderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if (_hwnd == IntPtr.Zero) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishDrag();
            return;
        }

        if (!GetCursorPos(out var cur)) return;
        int dx = cur.X - _dragStartCursor.X;
        int dy = cur.Y - _dragStartCursor.Y;
        CardWindowStyler.Move(_hwnd, _dragStartCardX + dx, _dragStartCardY + dy);
    }

    private void OnBorderMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        FinishDrag();
        e.Handled = true;
    }

    private void FinishDrag()
    {
        _dragging = false;
        if (CardBorder.IsMouseCaptured)
            CardBorder.ReleaseMouseCapture();

        var target = _tracker.CurrentBounds();
        var card = WindowGeometry.GetWindowRectBounds(_hwnd);
        if (target.Width <= 0 || card.Width <= 0) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        _config.OffsetX = (card.Left - target.Left) / dpi.DpiScaleX;
        _config.OffsetY = (card.Top - target.Top) / dpi.DpiScaleY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
