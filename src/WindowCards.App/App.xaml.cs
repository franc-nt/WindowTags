using System.Windows;
using WindowCards.App.Hotkeys;
using WindowCards.App.Settings;
using WindowCards.App.Tray;
using WindowCards.App.Updates;
using WindowCards.Core.Tracking;
using WindowCards.Models;

namespace WindowCards.App;

public partial class App : Application
{
    private readonly Dictionary<IntPtr, CardWindow> _cards = new();
    private HotkeyHost? _hotkeys;
    private TrayIconHost? _tray;
    private InfoWindow? _infoWindow;
    private AppSettings _settings = new();

    private int _createOrEditId;
    private int _removeId;
    private int _updateCheckInProgress;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = SettingsStore.Load();
        StartupRegistration.Sync(_settings.StartWithWindows);
        UpdateInstaller.CleanupLeftoverOldExe();

        try
        {
            _hotkeys = new HotkeyHost();
            _createOrEditId = _hotkeys.Register(
                HotkeyFormat.ToWin32Modifiers(_settings.CreateOrEdit),
                _settings.CreateOrEdit.VirtualKey,
                OnHotkeyCreateOrEdit);
            _removeId = _hotkeys.Register(
                HotkeyFormat.ToWin32Modifiers(_settings.Remove),
                _settings.Remove.VirtualKey,
                OnHotkeyRemove);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao registrar atalhos globais.\n\n{ex.Message}\n\n" +
                            "Você pode alterar os atalhos pelo menu da bandeja após reiniciar o app.",
                "WindowCards", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _tray = new TrayIconHost();
        _tray.ShowInfoRequested += ShowInfoWindow;
        _tray.CheckUpdateRequested += async () => await CheckForUpdateAsync();
        _tray.ExitRequested += () => Shutdown();
        _tray.ShowBalloon("WindowCards está rodando",
            $"Use {HotkeyFormat.Display(_settings.CreateOrEdit)} sobre qualquer janela para criar um card. Clique no ícone para ver os atalhos.");
    }

    private void ShowInfoWindow()
    {
        if (_infoWindow is { IsLoaded: true })
        {
            _infoWindow.Activate();
            return;
        }

        _infoWindow = new InfoWindow(_settings, TryChangeHotkey, SetStartWithWindows, CheckForUpdateAsync);
        _infoWindow.Closed += (_, _) => _infoWindow = null;
        _infoWindow.Show();
    }

    private async Task CheckForUpdateAsync()
    {
        if (Interlocked.Exchange(ref _updateCheckInProgress, 1) == 1) return;
        try
        {
            var result = await UpdateChecker.CheckAsync();
            var owner = _infoWindow is { IsLoaded: true } ? (Window)_infoWindow : null;

            switch (result.Outcome)
            {
                case UpdateCheckOutcome.UpToDate:
                    ShowMessage(owner,
                        $"Você já está na versão mais recente ({result.Info!.CurrentVersion.ToString(3)}).",
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckOutcome.NoReleaseFound:
                    ShowMessage(owner,
                        "Não foi possível encontrar uma release publicada no GitHub.",
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckOutcome.NetworkError:
                    ShowMessage(owner,
                        $"Falha ao consultar o GitHub:\n\n{result.ErrorMessage}",
                        MessageBoxImage.Warning);
                    break;

                case UpdateCheckOutcome.NewerAvailable:
                    var dlg = new UpdateAvailableDialog(result.Info!);
                    if (owner is not null) dlg.Owner = owner;
                    if (dlg.ShowDialog() == true)
                    {
                        await Task.Delay(300);
                        Shutdown();
                    }
                    break;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _updateCheckInProgress, 0);
        }
    }

    private static void ShowMessage(Window? owner, string text, MessageBoxImage icon)
    {
        if (owner is not null)
            MessageBox.Show(owner, text, "WindowCards", MessageBoxButton.OK, icon);
        else
            MessageBox.Show(text, "WindowCards", MessageBoxButton.OK, icon);
    }

    private void SetStartWithWindows(bool enabled)
    {
        _settings.StartWithWindows = enabled;
        StartupRegistration.Sync(enabled);
        SettingsStore.Save(_settings);
    }

    private bool TryChangeHotkey(HotkeySlot slot, HotkeyBinding newBinding, out string error)
    {
        error = string.Empty;
        if (_hotkeys is null) { error = "Hotkey host indisponível."; return false; }

        var oldBinding = slot == HotkeySlot.CreateOrEdit ? _settings.CreateOrEdit : _settings.Remove;
        int oldId = slot == HotkeySlot.CreateOrEdit ? _createOrEditId : _removeId;
        Action handler = slot == HotkeySlot.CreateOrEdit ? OnHotkeyCreateOrEdit : OnHotkeyRemove;

        _hotkeys.Unregister(oldId);

        try
        {
            int newId = _hotkeys.Register(
                HotkeyFormat.ToWin32Modifiers(newBinding),
                newBinding.VirtualKey,
                handler);

            if (slot == HotkeySlot.CreateOrEdit)
            {
                _createOrEditId = newId;
                _settings.CreateOrEdit = newBinding;
            }
            else
            {
                _removeId = newId;
                _settings.Remove = newBinding;
            }

            SettingsStore.Save(_settings);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            // rollback to old binding
            try
            {
                int restoredId = _hotkeys.Register(
                    HotkeyFormat.ToWin32Modifiers(oldBinding),
                    oldBinding.VirtualKey,
                    handler);
                if (slot == HotkeySlot.CreateOrEdit) _createOrEditId = restoredId;
                else _removeId = restoredId;
            }
            catch
            {
                // even rollback failed; the slot is now unbound until next change
            }
            return false;
        }
    }

    private void OnHotkeyCreateOrEdit()
    {
        var fg = TargetWindowDetector.GetForegroundWindow();
        if (fg == IntPtr.Zero) return;

        if (_cards.TryGetValue(fg, out var existing))
        {
            EditCardText(existing);
            return;
        }

        if (!TargetWindowDetector.TryClassify(fg, out var tw))
        {
            _tray?.ShowBalloon("WindowCards",
                "A janela em foco não pode receber um card (sem título ou é parte da shell).");
            return;
        }

        CreateCardForWindow(tw);
    }

    private void OnHotkeyRemove()
    {
        var fg = TargetWindowDetector.GetForegroundWindow();
        if (fg == IntPtr.Zero) return;
        if (_cards.TryGetValue(fg, out var card))
            card.Close();
    }

    private void CreateCardForWindow(TargetWindow tw)
    {
        var dlg = new TextInputDialog(
            title: "Novo card",
            prompt: $"Texto para o card em \"{Truncate(tw.Title, 50)}\":",
            initialText: "",
            initialBackgroundHex: "#D32F2F");

        if (dlg.ShowDialog() != true) return;

        var card = new CardWindow(tw.Hwnd, new CardConfig
        {
            Text = dlg.ResultText,
            BackgroundHex = dlg.SelectedBackgroundHex,
            ForegroundHex = dlg.SelectedForegroundHex,
            Width = 240,
            Height = 40,
            OffsetX = 12,
            OffsetY = 6
        });

        WireCard(card);
        _cards[tw.Hwnd] = card;
        card.Show();
    }

    private void WireCard(CardWindow card)
    {
        card.EditRequested += EditCardText;
        card.RemoveRequested += c => c.Close();
        card.ExitRequested += () => Shutdown();
        card.Closed += (_, _) =>
        {
            _cards.Remove(card.TargetHwnd);
        };
    }

    private void EditCardText(CardWindow card)
    {
        var dlg = new TextInputDialog(
            title: "Editar card",
            prompt: "Novo texto:",
            initialText: card.CurrentText,
            initialBackgroundHex: card.CurrentBackgroundHex);

        if (dlg.ShowDialog() != true) return;

        card.SetText(dlg.ResultText);
        card.SetColors(dlg.SelectedBackgroundHex, dlg.SelectedForegroundHex);
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? "(sem título)" : (s.Length <= max ? s : s[..max] + "…");

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        foreach (var c in _cards.Values.ToArray())
            c.Close();
        _cards.Clear();
        base.OnExit(e);
    }
}
