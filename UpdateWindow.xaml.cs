using System.Diagnostics;
using System.Windows;

namespace GlyphEcho;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _update;
    private readonly UpdateNetworkSettings _network;
    private readonly string _channel;

    internal UpdateWindow(UpdateInfo update, UpdateNetworkSettings network, string channel, bool materialEnabled)
    {
        InitializeComponent();
        _update = update;
        _network = network;
        _channel = channel;
        Tag = materialEnabled;
        VersionText.Text = $"GlyphEcho {update.Version.ToString(3)} · {update.Manifest.Size / 1024d / 1024d:0.0} MB";
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(update.Manifest.ReleaseNotes) ? "此版本没有附加更新说明。" : update.Manifest.ReleaseNotes;
        if (!UpdateService.CanInstallInPlace)
        {
            InstallButton.Content = "打开下载页";
            StatusText.Text = "当前是开发构建或不完整目录，不能就地替换。";
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => NativeMethods.ApplyBackdrop(this, RootSurface, Tag is true);

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateService.CanInstallInPlace)
        {
            Process.Start(new ProcessStartInfo(_update.DownloadUri.ToString()) { UseShellExecute = true });
            return;
        }
        SetBusy(true);
        DownloadProgress.Visibility = Visibility.Visible;
        StatusText.Text = "正在下载更新…";
        try
        {
            var progress = new Progress<int>(value => { DownloadProgress.Value = value; StatusText.Text = value < 100 ? $"正在下载和验证… {value}%" : "验证完成，正在重启…"; });
            var prepared = await UpdateService.DownloadAsync(_update, progress, _network);
            UpdateService.LaunchUpdater(prepared, _channel);
            App.ExitApplication();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败，尚未修改现有安装：{ex.Message}";
            SetBusy(false);
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
    private void SetBusy(bool busy) { InstallButton.IsEnabled = !busy; LaterButton.IsEnabled = !busy; }
}
