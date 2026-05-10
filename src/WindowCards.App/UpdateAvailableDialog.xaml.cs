using System.Windows;
using WindowCards.App.Updates;

namespace WindowCards.App;

public partial class UpdateAvailableDialog : Window
{
    private readonly UpdateInfo _info;
    private CancellationTokenSource? _cts;
    private bool _installing;

    public UpdateAvailableDialog(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;
        CurrentVersionLabel.Text = info.CurrentVersion.ToString(3);
        NewVersionLabel.Text = info.LatestVersion.ToString(3);
        ChangelogText.Text = string.IsNullOrWhiteSpace(info.ReleaseBody)
            ? "(Sem notas de versão.)"
            : info.ReleaseBody;
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_installing) return;
        _installing = true;

        UpdateButton.IsEnabled = false;
        CancelButton.Content = "Cancelar download";
        ProgressPanel.Visibility = Visibility.Visible;

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p => DownloadProgress.Value = p);

        var result = await UpdateInstaller.InstallAsync(_info, progress, _cts.Token);

        if (result.Success)
        {
            DialogResult = true;
            Close();
            return;
        }

        ProgressPanel.Visibility = Visibility.Collapsed;
        UpdateButton.IsEnabled = true;
        CancelButton.Content = "Cancelar";
        _installing = false;

        MessageBox.Show(this,
            $"Falha ao atualizar:\n\n{result.Error}",
            "WindowCards", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
        Close();
    }
}
