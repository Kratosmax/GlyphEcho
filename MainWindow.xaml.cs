using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GlyphEcho;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<GithubProxyEditorRow> _githubProxies = [];
    private readonly DispatcherTimer _catalogSearchTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _loading;
    private bool _updateCheckRunning;
    private int _configuredDefaultLevel = 2;
    private int _gameModeLevel = 1;
    private string _displayedMode = ModePolicy.Normal;
    private string _displayedPosition = "右下";
    private List<Forms.Screen> _monitors = [];

    public MainWindow()
    {
        InitializeComponent();
        var version = UpdateService.CurrentVersion.ToString(3);
        Title = $"GlyphEcho {version}";
        SidebarVersionText.Text = version;
        CurrentVersionText.Text = $"当前版本 {version}";
        Closing += OnClosing;
        _catalogSearchTimer.Tick += (_, _) => { _catalogSearchTimer.Stop(); RefreshCatalog(); };
        LoadSettings();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => ApplyMaterial();

    internal void NavigateToNetwork() { Navigate("Network"); Show(); Activate(); }
    internal void OpenNetworkAndCheck() { NavigateToNetwork(); _ = RunUpdateCheckAsync(true); }
    private void Navigate_Click(object sender, RoutedEventArgs e) => Navigate((sender as FrameworkElement)?.Tag?.ToString() ?? "Default");

    private void Navigate(string tag)
    {
        OverviewPanel.Visibility = tag is "Default" or "Rules" ? Visibility.Visible : Visibility.Collapsed;
        CatalogPanel.Visibility = tag == "Keys" ? Visibility.Visible : Visibility.Collapsed;
        NetworkPanel.Visibility = tag == "Network" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = tag == "Keys" ? Visibility.Collapsed : Visibility.Visible;
        if (tag == "Keys") { PageTitle.Text = "按键目录"; PageSubtitle.Text = "自动记录 · 搜索、启用和删除全局按键"; RefreshCatalog(); }
        else if (tag == "Network") { PageTitle.Text = "网络与更新"; PageSubtitle.Text = "GitHub 访问线路 · HTTP 代理 · 签名更新"; }
        else if (tag == "Settings") { PageTitle.Text = "设置"; PageSubtitle.Text = "启动与退出 · 窗口外观"; }
        else { PageTitle.Text = tag == "Rules" ? "应用规则" : "按键展示规则"; PageSubtitle.Text = "全局监听 · 规则按前台进程绝对路径匹配"; Grid.SetColumn(RulesPanel, tag == "Rules" ? 0 : 2); Grid.SetColumnSpan(RulesPanel, tag == "Rules" ? 3 : 1); DefaultPanel.Visibility = tag == "Rules" ? Visibility.Collapsed : Visibility.Visible; }
    }

    private void LoadSettings()
    {
        _loading = true;
        var settings = App.Settings;
        DefaultEnabled.IsChecked = settings.DefaultRule.Enabled;
        DefaultSingle.IsChecked = settings.DefaultRule.ShowSingleKeys;
        CatalogDefaultEnabled.IsChecked = settings.NewKeysEnabled;
        StartWithWindowsCheck.IsChecked = settings.StartWithWindows;
        AutoUpdateCheck.IsChecked = settings.CheckForUpdates;
        MaterialCheck.IsChecked = settings.EnableMaterial;
        UpdateChannelSelect.SelectedIndex = settings.UpdateChannel.Equals("full", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ModeSelect.SelectedIndex = settings.Mode switch { "游戏模式" => 1, "演示模式" => 2, _ => 0 };
        _configuredDefaultLevel = settings.DefaultRule.Level;
        _gameModeLevel = settings.GameModeLevel;
        _displayedMode = settings.Mode;
        CloseBehavior.SelectedIndex = settings.CloseToTray ? 0 : 1;
        SelectOverlayPalette(settings.OverlayPalette);
        PositionSelect.SelectedItem = PositionSelect.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Content == settings.OverlayPosition) ?? PositionSelect.Items[0];
        _displayedPosition = settings.OverlayPosition;
        LoadPositionOffset();
        RefreshMonitors();
        (settings.DefaultRule.Level switch { 1 => Low, 3 => High, _ => Medium }).IsChecked = true;
        RulesList.ItemsSource = settings.Rules;
        LoadNetworkSettings(settings.UpdateNetwork);
        GithubProxyGrid.ItemsSource = _githubProxies;
        RefreshCatalog();
        UpdateRuleButtons();
        _loading = false;
        UpdateModeUi();
    }

    private void LoadNetworkSettings(UpdateNetworkSettings network)
    {
        _githubProxies.Clear();
        var normalized = network.Normalize();
        foreach (var route in normalized.GithubProxies ?? []) _githubProxies.Add(new GithubProxyEditorRow(route));
        HttpProxyBox.Text = normalized.HttpProxy ?? string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadPositionOffset(out var offset)) return;
        if (!TryBuildNetworkSettings(out var network)) return;
        var settings = App.Settings;
        settings.DefaultRule.Enabled = DefaultEnabled.IsChecked == true;
        settings.DefaultRule.ShowSingleKeys = DefaultSingle.IsChecked == true;
        settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        settings.Mode = (ModeSelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "普通模式";
        settings.CloseToTray = CloseBehavior.SelectedIndex != 1;
        settings.MonitorIndex = Math.Max(0, MonitorSelect.SelectedIndex);
        settings.MonitorDeviceName = _monitors.ElementAtOrDefault(settings.MonitorIndex)?.DeviceName ?? string.Empty;
        settings.OverlayPosition = SelectedPosition;
        settings.OverlayPalette = SelectedOverlayPalette;
        settings.OverlayOffsets[SelectedPosition] = offset;
        if (SelectedMode == ModePolicy.Normal) _configuredDefaultLevel = SelectedLevel;
        else if (SelectedMode == ModePolicy.Game) _gameModeLevel = SelectedLevel == 2 ? 2 : 1;
        settings.DefaultRule.Level = _configuredDefaultLevel;
        settings.GameModeLevel = _gameModeLevel;
        settings.EnableMaterial = MaterialCheck.IsChecked == true;
        settings.CheckForUpdates = AutoUpdateCheck.IsChecked == true;
        settings.UpdateChannel = (UpdateChannelSelect.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "lite";
        settings.UpdateNetwork = network;
        var saved = App.SaveSettings();
        string? startupError = null;
        var startupApplied = App.IsVisualQa || StartupRegistration.TryApply(settings.StartWithWindows, out startupError);
        ApplyMaterial();
        App.Overlay?.RefreshPosition();
        App.Overlay?.RefreshStyle();
        FooterStatusText.Text = !saved ? App.SettingsWarning ?? "设置保存失败" : startupApplied ? "设置已保存" : $"其他设置已保存；开机自启失败：{startupError}";
    }

    private void ModeSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ModeSelect is null) return;
        App.Settings.Mode = SelectedMode;
        UpdateModeUi();
        App.Settings.GameModeLevel = _gameModeLevel;
        PersistSettings($"已切换到{SelectedMode}，当前输入立即按此模式显示");
    }

    private string SelectedMode => (ModeSelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "普通模式";
    private int SelectedLevel => High.IsChecked == true ? 3 : Low.IsChecked == true ? 1 : 2;
    private string SelectedPosition => (PositionSelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "右下";
    private string SelectedOverlayPalette => PaletteOptions.FirstOrDefault(option => option.IsChecked == true)?.Tag?.ToString() ?? OverlayPaletteCatalog.DefaultId;
    private System.Windows.Controls.RadioButton[] PaletteOptions => [PaletteDarkMint, PaletteDarkBlue, PaletteDarkAmber, PaletteLightTeal, PaletteLightBlue, PaletteLightRose];

    private void UpdateModeUi()
    {
        var mode = SelectedMode;
        if (_displayedMode == ModePolicy.Normal && mode != ModePolicy.Normal) _configuredDefaultLevel = SelectedLevel;
        else if (_displayedMode == ModePolicy.Game && mode != ModePolicy.Game) _gameModeLevel = SelectedLevel == 2 ? 2 : 1;
        var effectiveLevel = mode == ModePolicy.Game ? _gameModeLevel : mode == ModePolicy.Presentation ? 3 : _configuredDefaultLevel;
        (effectiveLevel == 1 ? Low : effectiveLevel == 3 ? High : Medium).IsChecked = true;
        var description = ModePolicy.Describe(mode);
        ModeSelect.ToolTip = description;
        ModeDescriptionText.Text = description;
        var followsRules = mode == ModePolicy.Normal;
        DefaultSingle.IsEnabled = followsRules;
        LevelOptionsPanel.IsEnabled = mode != ModePolicy.Presentation;
        Low.IsEnabled = mode != ModePolicy.Presentation;
        Medium.IsEnabled = mode != ModePolicy.Presentation;
        High.IsEnabled = mode == ModePolicy.Normal;
        DefaultSingle.ToolTip = followsRules ? null : $"{mode}强制显示单键；切回普通模式后恢复规则设置。";
        LevelOptionsPanel.ToolTip = mode == ModePolicy.Presentation ? "演示模式固定使用高级提示。" : null;
        High.ToolTip = mode == ModePolicy.Game ? "游戏模式只允许低级或中级；高级请使用演示模式。" : null;
        _displayedMode = mode;
    }

    private void PositionSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PositionSelect is null) return;
        if (TryReadPositionOffset(out var previousOffset)) App.Settings.OverlayOffsets[_displayedPosition] = previousOffset;
        _displayedPosition = SelectedPosition;
        App.Settings.OverlayPosition = SelectedPosition;
        LoadPositionOffset();
        PersistSettings($"已切换到{SelectedPosition}，并载入该位置的微调");
        App.Overlay?.RefreshPosition();
    }

    private void LoadPositionOffset()
    {
        var offset = App.Settings.GetOverlayOffset(SelectedPosition);
        OffsetXBox.Text = offset.X.ToString();
        OffsetYBox.Text = offset.Y.ToString();
    }

    private bool TryReadPositionOffset(out OverlayOffset offset)
    {
        if (!int.TryParse(OffsetXBox.Text, out var x) || !int.TryParse(OffsetYBox.Text, out var y) || x is < -5000 or > 5000 || y is < -5000 or > 5000)
        {
            FooterStatusText.Text = "位置微调必须是 -5000 到 5000 之间的整数像素";
            offset = new OverlayOffset();
            return false;
        }
        offset = new OverlayOffset { X = x, Y = y };
        return true;
    }

    private void ResetPositionOffset_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.OverlayOffsets[SelectedPosition] = new OverlayOffset();
        LoadPositionOffset();
        PersistSettings($"{SelectedPosition}的位置微调已重置");
        App.Overlay?.RefreshPosition();
    }

    private void SelectOverlayPalette(string id)
    {
        var normalized = OverlayPaletteCatalog.Normalize(id);
        PaletteOptions.First(option => option.Tag?.ToString() == normalized).IsChecked = true;
    }

    private void OverlayPalette_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not System.Windows.Controls.RadioButton { IsChecked: true } option) return;
        App.Settings.OverlayPalette = OverlayPaletteCatalog.Normalize(option.Tag?.ToString());
        PersistSettings($"提示色板已切换为{OverlayPaletteCatalog.Resolve(App.Settings.OverlayPalette).Label}");
        App.Overlay?.RefreshStyle();
    }

    private void ApplyMaterial() => _ = ApplyMaterialForVisualQa(App.Settings.EnableMaterial);
    internal BackdropResult ApplyMaterialForVisualQa(bool enabled) => NativeMethods.ApplyBackdrop(this, RootSurface, enabled);
    internal void NavigateForVisualQa(string tag) => Navigate(tag);
    internal void ShowSettingsForVisualQa() { Navigate("Settings"); StartWithWindowsCheck.BringIntoView(); }
    internal void ShowDefaultTopForVisualQa() { Navigate("Default"); DefaultEnabled.BringIntoView(); }
    internal void ShowPaletteSettingForVisualQa() { Navigate("Default"); PaletteDarkMint.BringIntoView(); }
    internal void SetOverlayPaletteForVisualQa(string id) => SelectOverlayPalette(id);
    internal void SelectAllCatalogForVisualQa() => CatalogList.SelectAll();
    internal void SetModeForVisualQa(string mode) => ModeSelect.SelectedIndex = mode == ModePolicy.Game ? 1 : mode == ModePolicy.Presentation ? 2 : 0;
    internal void SetGameLevelForVisualQa(int level) { _gameModeLevel = level == 2 ? 2 : 1; if (SelectedMode == ModePolicy.Game) (_gameModeLevel == 2 ? Medium : Low).IsChecked = true; App.Settings.GameModeLevel = _gameModeLevel; }
    internal void SetPositionOffsetForVisualQa(string position, int x, int y) { PositionSelect.SelectedItem = PositionSelect.Items.OfType<ComboBoxItem>().First(item => (string)item.Content == position); App.Settings.OverlayOffsets[position] = new OverlayOffset { X = x, Y = y }; LoadPositionOffset(); App.Settings.OverlayPosition = position; App.Overlay?.RefreshPosition(); }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { _catalogSearchTimer.Stop(); App.Settings.CloseToTray = CloseBehavior.SelectedIndex != 1; App.SaveSettings(); if (App.Settings.CloseToTray && !App.IsShuttingDown) { e.Cancel = true; Hide(); return; } if (!App.IsShuttingDown) { e.Cancel = true; App.ExitApplication(); } }

    private void AddRule_Click(object sender, RoutedEventArgs e) { var foreground = KeyboardHook.GetForegroundProcessInfo(); var rule = new DisplayRule { Name = "新应用规则", Process = foreground.Name, ProcessPath = foreground.Path, Level = App.Settings.DefaultRule.Level, Enabled = true, UseGlobalCatalog = true }; var editor = new RuleEditorWindow(rule, false, App.Settings.GlobalKeyCatalog) { Owner = this }; if (editor.ShowDialog() == true) { App.Settings.Rules.Add(rule); RefreshRules(); RulesList.SelectedItem = rule; PersistSettings("应用规则已添加"); } }
    private void DuplicateRule_Click(object sender, RoutedEventArgs e) { if (RulesList.SelectedItem is not DisplayRule source) return; var copy = source.Clone(); copy.Priority = source.Priority + 1; App.Settings.Rules.Add(copy); RefreshRules(); RulesList.SelectedItem = copy; PersistSettings("应用规则已复制"); }
    private void EditRule_Click(object sender, RoutedEventArgs e) { if (RulesList.SelectedItem is not DisplayRule rule) { FooterStatusText.Text = "请先选择一条应用规则"; return; } var editor = new RuleEditorWindow(rule, false, App.Settings.GlobalKeyCatalog) { Owner = this }; if (editor.ShowDialog() == true) { RefreshRules(); PersistSettings("应用规则已保存"); } }
    private void DeleteRule_Click(object sender, RoutedEventArgs e) { if (RulesList.SelectedItem is not DisplayRule rule) return; if (!AppDialog.ShowMessage(this, "确认删除", $"删除规则“{rule.Name}”？", true, true)) return; App.Settings.Rules.Remove(rule); RefreshRules(); PersistSettings("应用规则已删除"); }
    private void Preview_Click(object sender, RoutedEventArgs e) { var rule = App.ResolveRule(string.Empty); App.Overlay?.Present("Ctrl + C", "预览", rule); App.Overlay?.Present("Ctrl + C", "预览", rule); App.Overlay?.Present("Ctrl + V", "预览", rule); }
    private void RefreshRules() { RulesList.ItemsSource = null; RulesList.ItemsSource = App.Settings.Rules; UpdateRuleButtons(); }
    private void RulesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateRuleButtons();
    private void UpdateRuleButtons() { var selected = RulesList?.SelectedItem is DisplayRule; if (EditRuleButton is not null) EditRuleButton.IsEnabled = selected; if (DeleteRuleButton is not null) DeleteRuleButton.IsEnabled = selected; if (DuplicateRuleButton is not null) DuplicateRuleButton.IsEnabled = selected; }

    private void CatalogSearch_TextChanged(object sender, TextChangedEventArgs e) { _catalogSearchTimer.Stop(); _catalogSearchTimer.Start(); }
    private void CatalogDefaultEnabled_Changed(object sender, RoutedEventArgs e) { if (_loading || CatalogDefaultEnabled is null) return; App.Settings.NewKeysEnabled = CatalogDefaultEnabled.IsChecked == true; PersistSettings("新按键默认状态已保存"); }
    internal void RefreshCatalog() { if (CatalogList is null) return; var query = CatalogSearchBox?.Text?.Trim() ?? string.Empty; CatalogList.ItemsSource = App.Settings.GlobalKeyCatalog.Where(item => query.Length == 0 || item.Key.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList(); UpdateCatalogSelection(); }
    private void CatalogList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCatalogSelection();
    private void UpdateCatalogSelection() { if (CatalogList is null || CatalogSelectionText is null || DeleteSelectedCatalogButton is null) return; var count = CatalogList.SelectedItems.Count; CatalogSelectionText.Text = $"已选 {count} 项"; DeleteSelectedCatalogButton.IsEnabled = count > 0; }
    private void SelectAllCatalog_Click(object sender, RoutedEventArgs e) => CatalogList.SelectAll();
    private void DeleteSelectedCatalog_Click(object sender, RoutedEventArgs e)
    {
        var selected = CatalogList.SelectedItems.OfType<KeyRule>().ToList();
        if (selected.Count == 0 || !AppDialog.ShowMessage(this, "确认批量删除", $"删除选中的 {selected.Count} 个按键？删除后再次按下不会自动恢复。", true, true)) return;
        DeleteCatalogKeys(selected);
    }
    private void DeleteCatalogKey_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is KeyRule key) DeleteCatalogKeys([key]); }
    private void DeleteCatalogKeys(IEnumerable<KeyRule> keys)
    {
        var removed = App.Settings.DeleteCatalogKeys(keys);
        if (removed == 0) return;
        RefreshCatalog();
        PersistSettings($"已删除 {removed} 个按键");
    }
    private void CatalogKeyEnabled_Click(object sender, RoutedEventArgs e) => PersistSettings("按键状态已保存");

    private bool PersistSettings(string successMessage)
    {
        var saved = App.SaveSettings();
        FooterStatusText.Text = saved ? successMessage : App.SettingsWarning ?? "设置保存失败";
        return saved;
    }

    private void AddProxy_Click(object sender, RoutedEventArgs e) { var row = new GithubProxyEditorRow(new GithubProxySetting("https://", 5)); _githubProxies.Add(row); GithubProxyGrid.SelectedItem = row; GithubProxyGrid.ScrollIntoView(row); }
    private void RemoveProxy_Click(object sender, RoutedEventArgs e) { if (GithubProxyGrid.SelectedItem is not GithubProxyEditorRow row) return; if (row.IsDirect) { NetworkErrorText.Text = "GitHub 直连不可删除；可将优先级设为 0。"; return; } _githubProxies.Remove(row); NetworkErrorText.Text = string.Empty; }
    private void GithubProxyGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) { if (e.Row.Item is GithubProxyEditorRow { IsDirect: true } && e.Column.DisplayIndex == 0) e.Cancel = true; }
    private bool TryBuildNetworkSettings(out UpdateNetworkSettings settings)
    {
        GithubProxyGrid.CommitEdit(DataGridEditingUnit.Cell, true); GithubProxyGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var routes = new List<GithubProxySetting>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _githubProxies) { if (row.IsDirect) { routes.Add(new GithubProxySetting(string.Empty, row.Priority, true)); continue; } if (!UpdateNetworkSettings.TryNormalizeGithubProxy(row.Address, out var baseUrl)) { NetworkErrorText.Text = $"GitHub 前缀线路无效：{row.Address}"; settings = UpdateNetworkSettings.Default; return false; } if (!seen.Add(baseUrl)) { NetworkErrorText.Text = $"GitHub 前缀线路重复：{baseUrl}"; settings = UpdateNetworkSettings.Default; return false; } routes.Add(new GithubProxySetting(baseUrl, row.Priority)); }
        if (!UpdateNetworkSettings.TryNormalizeHttpProxy(HttpProxyBox.Text, out var httpProxy)) { NetworkErrorText.Text = "HTTP 网络代理无效，请使用 http://主机:端口，暂不支持账号密码。"; settings = UpdateNetworkSettings.Default; return false; }
        if (routes.All(route => route.Priority == 0)) { NetworkErrorText.Text = "至少启用一条 GitHub 访问线路。"; settings = UpdateNetworkSettings.Default; return false; }
        NetworkErrorText.Text = string.Empty; settings = new UpdateNetworkSettings(routes, httpProxy).Normalize(); return true;
    }

    private async void CheckUpdatePage_Click(object sender, RoutedEventArgs e) => await RunUpdateCheckAsync(true);
    internal async Task RunUpdateCheckAsync(bool showDialog, bool reportFailure = true, CancellationToken cancellationToken = default)
    {
        if (_updateCheckRunning) { if (reportFailure) UpdateStatusText.Text = "更新检查正在进行中，请稍候。"; return; }
        if (!TryBuildNetworkSettings(out var network)) return;
        _updateCheckRunning = true;
        CheckUpdateButton.IsEnabled = false; UpdateStatusText.Text = "正在按优先级检查更新线路…";
        try { var channel = (UpdateChannelSelect.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "lite"; var update = await UpdateService.CheckAsync(channel, network, cancellationToken); if (update is null) UpdateStatusText.Text = "当前已是最新版本，签名清单验证通过。"; else { UpdateStatusText.Text = $"发现 GlyphEcho {update.Version.ToString(3)}，签名验证通过。"; if (showDialog) new UpdateWindow(update, network, channel, MaterialCheck.IsChecked == true) { Owner = this }.ShowDialog(); } }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { App.WriteDiagnosticLog("update-check-failed.log", ex); UpdateStatusText.Text = reportFailure ? $"检查失败：{ex.Message}" : "后台检查失败，详细信息已写入日志。"; }
        finally { _updateCheckRunning = false; CheckUpdateButton.IsEnabled = true; }
    }

    internal void RefreshMonitors()
    {
        var settings = App.Settings;
        _monitors = Forms.Screen.AllScreens.ToList();
        var selected = string.IsNullOrWhiteSpace(settings.MonitorDeviceName) ? -1 : _monitors.FindIndex(screen => screen.DeviceName.Equals(settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        if (selected < 0) selected = Math.Clamp(settings.MonitorIndex, 0, Math.Max(0, _monitors.Count - 1));
        MonitorSelect.Items.Clear();
        for (var index = 0; index < _monitors.Count; index++)
        {
            var screen = _monitors[index];
            MonitorSelect.Items.Add(screen.Primary ? $"屏幕 {index + 1}（主）" : $"屏幕 {index + 1}");
        }
        settings.MonitorIndex = selected;
        settings.MonitorDeviceName = _monitors.ElementAtOrDefault(selected)?.DeviceName ?? string.Empty;
        MonitorSelect.SelectedIndex = selected;
        App.Overlay?.RefreshPosition();
    }
    internal void UpdateListenerStatus(bool running) { ListenerStatusText.Text = App.IsVisualQa ? "视觉验收" : running ? "监听中" : "监听失败"; TopListenerStatus.Text = App.IsVisualQa ? "● 视觉验收" : running ? "● 监听中" : "● 监听失败"; SidebarStatusText.Text = App.IsVisualQa ? "全局监听已隔离" : running ? "后台监听已启用" : "后台监听未启动"; }
}

internal sealed class GithubProxyEditorRow(GithubProxySetting setting)
{
    public string Address { get; set; } = setting.IsDirect ? "GitHub 直连（不拼接前缀）" : setting.BaseUrl;
    public int Priority { get; set; } = setting.Priority;
    public bool IsDirect { get; } = setting.IsDirect;
}
