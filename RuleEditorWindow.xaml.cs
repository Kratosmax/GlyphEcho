using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GlyphEcho;
public partial class RuleEditorWindow : Window
{
    private readonly DisplayRule _rule;
    private readonly List<KeyRule> _keys = [];
    private readonly List<KeyRule> _catalog = [];
    private readonly bool _defaultRule;
    private List<ProcessTarget> _processes = [];
    private bool _useGlobalCatalog;

    public RuleEditorWindow(DisplayRule rule, bool defaultRule = false, IEnumerable<KeyRule>? globalCatalog = null)
    {
        _rule = rule; _defaultRule = defaultRule; _catalog.AddRange(globalCatalog ?? []); _useGlobalCatalog = !defaultRule && rule.UseGlobalCatalog;
        InitializeComponent();
        NameBox.Text = rule.Name; EnabledBox.IsChecked = rule.Enabled; SingleBox.IsChecked = rule.ShowSingleKeys; UseGlobalCatalogBox.IsChecked = _useGlobalCatalog; DescriptionBox.Text = rule.Description; LevelBox.SelectedIndex = Math.Clamp(rule.Level - 1, 0, 2); PriorityBox.SelectedIndex = Math.Clamp(rule.Priority, 0, 2);
        LoadKeys();
        if (defaultRule) { ProcessSelect.DisplayMemberPath = nameof(ProcessTarget.Label); ProcessSelect.SelectedValuePath = nameof(ProcessTarget.Path); ProcessSelect.ItemsSource = new[] { new ProcessTarget("默认规则（不绑定进程）", "") }; ProcessSelect.SelectedIndex = 0; ProcessSelect.IsEnabled = false; PriorityBox.IsEnabled = false; UseGlobalCatalogBox.IsEnabled = false; }
        else Loaded += async (_, _) => await LoadProcessesAsync();
        RefreshKeys();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyBackdrop(this, RootSurface, App.Settings.EnableMaterial);

    private void LoadKeys()
    {
        _keys.Clear();
        if (_useGlobalCatalog && _catalog.Count > 0)
        {
            _keys.AddRange(RuleCatalog.Merge(_catalog, _rule.KeyRules));
        }
        else _keys.AddRange(_rule.KeyRules.Select(x => x.Clone()));
    }

    private async Task LoadProcessesAsync()
    {
        try
        {
            var existingPath = _rule.ProcessPath;
            var snapshot = await Task.Run(() =>
            {
                var current = KeyboardHook.GetForegroundProcessInfo();
                var result = new List<ProcessTarget>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        try
                        {
                            var path = process.MainModule?.FileName;
                            var name = process.ProcessName;
                            if (!string.IsNullOrWhiteSpace(path) && seenPaths.Add(path))
                                result.Add(new ProcessTarget($"{name} · {path}", path));
                        }
                        catch { }
                    }
                }
                return (Current: current, Processes: result.OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToList());
            });
            var current = snapshot.Current;
            var processes = snapshot.Processes;
            if (existingPath.Length > 0 && processes.All(item => !item.Path.Equals(existingPath, StringComparison.OrdinalIgnoreCase)))
                processes.Insert(0, new ProcessTarget($"已配置（当前未运行） · {existingPath}", existingPath));
            if (current.Path.Length > 0 && processes.All(item => !item.Path.Equals(current.Path, StringComparison.OrdinalIgnoreCase)))
                processes.Insert(0, new ProcessTarget($"当前前台 · {current.Path}", current.Path));
            if (!IsLoaded) return;
            _processes = processes;
            ProcessSelect.DisplayMemberPath = nameof(ProcessTarget.Label);
            ProcessSelect.SelectedValuePath = nameof(ProcessTarget.Path);
            RefreshProcesses();
            ProcessSelect.SelectedValue = existingPath;
        }
        catch (Exception ex) { if (IsLoaded) AppDialog.ShowMessage(this, "进程读取失败", $"无法读取在线进程列表。\n原因：{ex.Message}"); }
    }
    private void RefreshProcesses() { var query = ProcessSearchBox?.Text?.Trim() ?? ""; ProcessSelect.ItemsSource = _processes.Where(x => query.Length == 0 || x.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Path.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList(); }
    private void ProcessSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshProcesses();
    private void RefreshKeys() { KeysList.ItemsSource = null; KeysList.ItemsSource = _keys; }
    private void CatalogMode_Changed(object sender, RoutedEventArgs e) { if (!IsInitialized || _defaultRule) return; var useGlobal = UseGlobalCatalogBox.IsChecked == true; if (useGlobal && !_useGlobalCatalog) { _useGlobalCatalog = true; LoadKeys(); } else _useGlobalCatalog = useGlobal; RefreshKeys(); }
    private void DeleteKey_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is not KeyRule key) return; if (_useGlobalCatalog && _catalog.Any(x => KeyboardHook.NormalizeForRule(x.Key).Equals(KeyboardHook.NormalizeForRule(key.Key), StringComparison.OrdinalIgnoreCase))) key.Enabled = false; else _keys.Remove(key); RefreshKeys(); }
    private List<KeyRule> BuildOverrides() => RuleCatalog.BuildOverrides(_keys, _catalog);
    private void Save_Click(object sender, RoutedEventArgs e) { var path = ProcessSelect.SelectedValue as string ?? string.Empty; if (!_defaultRule && path.Length == 0) { AppDialog.ShowMessage(this, "规则不完整", "请选择一个带绝对路径的目标进程。"); return; } _rule.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? Path.GetFileNameWithoutExtension(path) : NameBox.Text.Trim(); _rule.ProcessPath = path; _rule.Process = path.Length == 0 ? string.Empty : Path.GetFileNameWithoutExtension(path); _rule.Description = DescriptionBox.Text.Trim(); _rule.Enabled = EnabledBox.IsChecked == true; _rule.ShowSingleKeys = SingleBox.IsChecked == true; _rule.UseGlobalCatalog = _defaultRule || _useGlobalCatalog; _rule.Level = LevelBox.SelectedIndex + 1; _rule.Priority = _defaultRule ? 0 : PriorityBox.SelectedIndex; _rule.KeyRules = _useGlobalCatalog ? BuildOverrides() : [.. _keys]; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
public sealed record ProcessTarget(string Label, string Path);
