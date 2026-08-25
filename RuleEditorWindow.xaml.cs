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
        _rule = rule; _defaultRule = defaultRule; _catalog.AddRange(globalCatalog?.Select(x => x.Clone()) ?? []); _useGlobalCatalog = !defaultRule && rule.UseGlobalCatalog;
        InitializeComponent();
        NameBox.Text = rule.Name; EnabledBox.IsChecked = rule.Enabled; SingleBox.IsChecked = rule.ShowSingleKeys; UseGlobalCatalogBox.IsChecked = _useGlobalCatalog; DescriptionBox.Text = rule.Description; LevelBox.SelectedIndex = Math.Clamp(rule.Level - 1, 0, 2); PriorityBox.SelectedIndex = Math.Clamp(rule.Priority, 0, 2);
        LoadKeys();
        if (defaultRule) { ProcessSelect.ItemsSource = new[] { new ProcessTarget("默认规则（不绑定进程）", "") }; ProcessSelect.SelectedIndex = 0; ProcessSelect.IsEnabled = false; PriorityBox.IsEnabled = false; UseGlobalCatalogBox.IsEnabled = false; }
        else Loaded += async (_, _) => await LoadProcessesAsync();
        RefreshKeys();
    }

    private void LoadKeys()
    {
        _keys.Clear();
        if (_useGlobalCatalog && _catalog.Count > 0)
        {
            _keys.AddRange(_catalog.Select(x => x.Clone()));
            foreach (var overrideRule in _rule.KeyRules)
            {
                var existing = _keys.FirstOrDefault(x => KeyboardHook.NormalizeForRule(x.Key).Equals(KeyboardHook.NormalizeForRule(overrideRule.Key), StringComparison.OrdinalIgnoreCase));
                if (existing is null) _keys.Add(overrideRule.Clone()); else { existing.Enabled = overrideRule.Enabled; existing.Description = string.IsNullOrWhiteSpace(overrideRule.Description) ? existing.Description : overrideRule.Description; }
            }
        }
        else _keys.AddRange(_rule.KeyRules.Select(x => x.Clone()));
    }

    private async Task LoadProcessesAsync() { await Task.Yield(); var current = KeyboardHook.GetForegroundProcessPath(); var processes = new List<ProcessTarget>(); foreach (var p in Process.GetProcesses().OrderBy(x => x.ProcessName)) { try { var path = p.MainModule?.FileName; if (!string.IsNullOrWhiteSpace(path) && processes.All(x => !x.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) processes.Add(new ProcessTarget($"{p.ProcessName} · {path}", path)); } catch { } } if (current.Length > 0 && processes.All(x => !x.Path.Equals(current, StringComparison.OrdinalIgnoreCase))) processes.Insert(0, new ProcessTarget($"当前前台 · {current}", current)); _processes = processes; ProcessSelect.DisplayMemberPath = nameof(ProcessTarget.Label); ProcessSelect.SelectedValuePath = nameof(ProcessTarget.Path); ProcessSelect.SelectedValue = _rule.ProcessPath; RefreshProcesses(); }
    private void RefreshProcesses() { var query = ProcessSearchBox?.Text?.Trim() ?? ""; ProcessSelect.ItemsSource = _processes.Where(x => query.Length == 0 || x.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Path.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList(); }
    private void ProcessSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshProcesses();
    private void RefreshKeys() { KeysList.ItemsSource = null; KeysList.ItemsSource = _keys; }
    private void CatalogMode_Changed(object sender, RoutedEventArgs e) { if (!IsInitialized || _defaultRule) return; _useGlobalCatalog = UseGlobalCatalogBox.IsChecked == true; LoadKeys(); RefreshKeys(); }
    private void DeleteKey_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is not KeyRule key) return; if (_useGlobalCatalog && _catalog.Any(x => KeyboardHook.NormalizeForRule(x.Key).Equals(KeyboardHook.NormalizeForRule(key.Key), StringComparison.OrdinalIgnoreCase))) key.Enabled = false; else _keys.Remove(key); RefreshKeys(); }
    private List<KeyRule> BuildOverrides() { var overrides = new List<KeyRule>(); foreach (var current in _keys) { var baseline = _catalog.FirstOrDefault(x => KeyboardHook.NormalizeForRule(x.Key).Equals(KeyboardHook.NormalizeForRule(current.Key), StringComparison.OrdinalIgnoreCase)); if (baseline is null || baseline.Enabled != current.Enabled || !string.Equals(baseline.Description, current.Description, StringComparison.Ordinal)) overrides.Add(current.Clone()); } return overrides; }
    private void Save_Click(object sender, RoutedEventArgs e) { var path = ProcessSelect.SelectedValue as string ?? string.Empty; if (!_defaultRule && path.Length == 0) { System.Windows.MessageBox.Show("请选择一个带绝对路径的目标进程。", "规则不完整", MessageBoxButton.OK, MessageBoxImage.Warning); return; } _rule.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? Path.GetFileNameWithoutExtension(path) : NameBox.Text.Trim(); _rule.ProcessPath = path; _rule.Process = path.Length == 0 ? string.Empty : Path.GetFileNameWithoutExtension(path); _rule.Description = DescriptionBox.Text.Trim(); _rule.Enabled = EnabledBox.IsChecked == true; _rule.ShowSingleKeys = SingleBox.IsChecked == true; _rule.UseGlobalCatalog = _defaultRule || _useGlobalCatalog; _rule.Level = LevelBox.SelectedIndex + 1; _rule.Priority = _defaultRule ? 0 : PriorityBox.SelectedIndex; _rule.KeyRules = _useGlobalCatalog ? BuildOverrides() : [.. _keys]; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
public sealed record ProcessTarget(string Label, string Path);
