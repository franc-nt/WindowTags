using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace WindowCards.App.Tray;

public sealed class TrayIconHost : IDisposable
{
    private readonly WinForms.NotifyIcon _notify;
    private readonly Icon _icon;
    private bool _disposed;

    public event Action? ShowInfoRequested;
    public event Action? CheckUpdateRequested;
    public event Action? ExitRequested;

    public TrayIconHost()
    {
        _icon = CreateRedCardIcon();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Mostrar atalhos", null, (_, _) => ShowInfoRequested?.Invoke());
        menu.Items.Add("Verificar atualização", null, (_, _) => CheckUpdateRequested?.Invoke());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());

        _notify = new WinForms.NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "WindowCards",
            ContextMenuStrip = menu
        };
        _notify.MouseClick += OnTrayMouseClick;
    }

    private void OnTrayMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
            ShowInfoRequested?.Invoke();
    }

    public void ShowBalloon(string title, string message)
    {
        _notify.BalloonTipTitle = title;
        _notify.BalloonTipText = message;
        _notify.BalloonTipIcon = WinForms.ToolTipIcon.Info;
        _notify.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notify.Visible = false;
        _notify.Dispose();
        _icon.Dispose();
    }

    private static Icon CreateRedCardIcon()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var fill = new SolidBrush(Color.FromArgb(211, 47, 47));
            using var border = new Pen(Color.FromArgb(122, 0, 0), 2f);
            var rect = new Rectangle(2, 6, size - 4, size - 12);
            g.FillRectangle(fill, rect);
            g.DrawRectangle(border, rect);

            using var font = new Font("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("WC", font, text, rect, fmt);
        }

        IntPtr h = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(h).Clone();
        }
        finally
        {
            DestroyIcon(h);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
