using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WindowCards.App.Hotkeys;
using WindowCards.Models;

namespace WindowCards.App;

public partial class HotkeyCaptureDialog : Window
{
    public HotkeyBinding? Result { get; private set; }

    public HotkeyCaptureDialog(string prompt, HotkeyBinding current)
    {
        InitializeComponent();
        PromptLabel.Text = prompt;
        ComboLabel.Text = HotkeyFormat.Display(current);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetForegroundWindow(hwnd);
        Activate();
        Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (IsModifierOrIgnored(key))
        {
            // While the user is still holding only modifiers, render them live
            UpdateLiveModifierHint();
            e.Handled = true;
            return;
        }

        var binding = new HotkeyBinding
        {
            Ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0,
            Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
            Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0,
            Win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin),
            VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key)
        };

        if (!binding.HasModifier)
        {
            HintLabel.Text = "Combinação inválida — use ao menos um modificador.";
            HintLabel.Foreground = System.Windows.Media.Brushes.IndianRed;
            e.Handled = true;
            return;
        }

        Result = binding;
        ComboLabel.Text = HotkeyFormat.Display(binding);
        ComboLabel.Foreground = System.Windows.Media.Brushes.Black;
        HintLabel.Text = "Pressione OK para confirmar, ou outra combinação para substituir.";
        HintLabel.Foreground = System.Windows.Media.Brushes.Gray;
        OkButton.IsEnabled = true;
        e.Handled = true;
    }

    private void UpdateLiveModifierHint()
    {
        var parts = new List<string>(4);
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) parts.Add("Win");
        if (parts.Count == 0) return;

        parts.Add("…");
        ComboLabel.Text = string.Join(" + ", parts);
        ComboLabel.Foreground = System.Windows.Media.Brushes.Gray;
    }

    private static bool IsModifierOrIgnored(Key k) =>
        k is Key.LeftCtrl or Key.RightCtrl
          or Key.LeftAlt or Key.RightAlt
          or Key.LeftShift or Key.RightShift
          or Key.LWin or Key.RWin
          or Key.System
          or Key.Capital or Key.NumLock or Key.Scroll
          or Key.None or Key.DeadCharProcessed;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (Result is null) return;
        DialogResult = true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
