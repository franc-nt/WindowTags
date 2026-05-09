using System.Windows;
using WindowCards.App.Hotkeys;
using WindowCards.Models;

namespace WindowCards.App;

public enum HotkeySlot
{
    CreateOrEdit,
    Remove
}

public partial class InfoWindow : Window
{
    public delegate bool TryChangeHotkey(HotkeySlot slot, HotkeyBinding newBinding, out string error);

    private readonly AppSettings _settings;
    private readonly TryChangeHotkey _changer;

    public InfoWindow(AppSettings settings, TryChangeHotkey changer)
    {
        InitializeComponent();
        _settings = settings;
        _changer = changer;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        CreateOrEditLabel.Text = HotkeyFormat.Display(_settings.CreateOrEdit);
        RemoveLabel.Text = HotkeyFormat.Display(_settings.Remove);
    }

    private void OnChangeCreateOrEdit(object sender, RoutedEventArgs e)
        => PromptAndApply(HotkeySlot.CreateOrEdit, _settings.CreateOrEdit,
            "Pressione a nova combinação para adicionar/editar card:");

    private void OnChangeRemove(object sender, RoutedEventArgs e)
        => PromptAndApply(HotkeySlot.Remove, _settings.Remove,
            "Pressione a nova combinação para remover card:");

    private void PromptAndApply(HotkeySlot slot, HotkeyBinding current, string prompt)
    {
        var dlg = new HotkeyCaptureDialog(prompt, current) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null) return;

        if (_changer(slot, dlg.Result, out var error))
        {
            RefreshLabels();
        }
        else
        {
            MessageBox.Show(this,
                $"Não foi possível registrar o atalho:\n\n{error}\n\nProvavelmente outra aplicação já está usando essa combinação.",
                "WindowCards",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
