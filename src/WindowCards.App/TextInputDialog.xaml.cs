using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;

namespace WindowCards.App;

public partial class TextInputDialog : Window
{
    private static readonly string[] Palette =
    {
        "#D32F2F", // vermelho
        "#F57C00", // laranja
        "#FBC02D", // amarelo
        "#388E3C", // verde
        "#00897B", // teal
        "#1976D2", // azul
        "#7B1FA2", // roxo
        "#C2185B", // rosa
        "#424242", // cinza
        "#212121", // preto
    };

    private Border? _selectedSwatch;

    public string ResultText { get; private set; } = string.Empty;
    public string SelectedBackgroundHex { get; private set; } = Palette[0];
    public string SelectedForegroundHex { get; private set; } = "#FFFFFF";

    public TextInputDialog(string title, string prompt, string initialText, string initialBackgroundHex)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputBox.Text = initialText;
        BuildSwatches(initialBackgroundHex);
        Loaded += OnLoaded;
    }

    private void BuildSwatches(string initialBackgroundHex)
    {
        Border? matchSwatch = null;

        foreach (var hex in Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var swatch = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                Background = new SolidColorBrush(color),
                Cursor = Cursors.Hand,
                Tag = hex,
                ToolTip = hex,
            };
            swatch.MouseLeftButtonDown += OnSwatchClick;
            ColorSwatches.Children.Add(swatch);

            if (string.Equals(hex, initialBackgroundHex, StringComparison.OrdinalIgnoreCase))
                matchSwatch = swatch;
        }

        SelectSwatch(matchSwatch ?? (Border)ColorSwatches.Children[0]);
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b) SelectSwatch(b);
    }

    private void SelectSwatch(Border swatch)
    {
        if (_selectedSwatch != null)
        {
            _selectedSwatch.BorderThickness = new Thickness(1);
            _selectedSwatch.BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        _selectedSwatch = swatch;
        swatch.BorderThickness = new Thickness(3);
        swatch.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));

        var hex = (string)swatch.Tag!;
        SelectedBackgroundHex = hex;
        SelectedForegroundHex = PickForeground((Color)ColorConverter.ConvertFromString(hex));
    }

    private static string PickForeground(Color c)
    {
        double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        return lum > 0.6 ? "#212121" : "#FFFFFF";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hotkey global deixa o navegador como foreground. Forçar o dialog
        // para o foreground antes de tentar focar o TextBox, senão Focus()
        // não tem efeito enquanto a janela ativa estiver em outro processo.
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetForegroundWindow(hwnd);

        Activate();
        InputBox.Focus();
        Keyboard.Focus(InputBox);
        InputBox.SelectAll();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ResultText = InputBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(ResultText)) return;
        DialogResult = true;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnOkClick(sender, e);
            e.Handled = true;
        }
    }
}
