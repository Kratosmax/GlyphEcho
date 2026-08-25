using System.Text.Json;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace GlyphEcho;

public partial class App : System.Windows.Application
{
    internal static bool IsShuttingDown { get; private set; }
    internal static KeySettings Settings { get; private set; } = KeySettings.Default;
    internal static MainWindow? MainWindowInstance { get; private set; }
    internal static OverlayWindow? Overlay { get; private set; }
    private KeyboardHook? _hook;
    private GamepadHook? _gamepad;
    private HidGamepadHook? _hidGamepad;
    private Forms.NotifyIcon? _tray;
    private static readonly object SettingsGate = new();
    private static Mutex? _singleInstanceMutex;
    internal static bool HookRunning { get; private set; }
    internal static string? SettingsWarning { get; private set; }
    internal static void ExitApplication() { IsShuttingDown = true; Current.Shutdown(); }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(true, "GlyphEcho.SingleInstance.0.1", out var created); if (!created) { System.Windows.MessageBox.Show("GlyphEcho 已经在运行中。", "GlyphEcho", MessageBoxButton.OK, MessageBoxImage.Information); Shutdown(); return; }
        Settings = LoadSettings();
        Settings.NormalizeCatalog();
        SaveSettings();
        MainWindowInstance = new MainWindow();
        Overlay = new OverlayWindow();
        _hook = new KeyboardHook();
        _hook.KeyPressed += (_, args) => Dispatcher.BeginInvoke(() => { RecordObservedKey(args.CatalogKey); MainWindowInstance?.RefreshCatalog(); Overlay?.Present(args.Display, args.ForegroundApp, ResolveRule(args.ForegroundPath)); });
        HookRunning = _hook.Start();
        _gamepad = new GamepadHook();
        _gamepad.KeyPressed += (_, args) => Dispatcher.BeginInvoke(() => { RecordObservedKey(args.CatalogKey); MainWindowInstance?.RefreshCatalog(); Overlay?.Present(args.Display, args.ForegroundApp, ResolveRule(args.ForegroundPath)); });
        _hidGamepad = new HidGamepadHook(MainWindowInstance);
        _hidGamepad.KeyPressed += (_, args) => Dispatcher.BeginInvoke(() => { RecordObservedKey(args.CatalogKey); MainWindowInstance?.RefreshCatalog(); Overlay?.Present(args.Display, args.ForegroundApp, ResolveRule(args.ForegroundPath)); });
        _tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Visible = true, Text = "GlyphEcho" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => Dispatcher.Invoke(() => { MainWindowInstance?.Show(); MainWindowInstance?.Activate(); }));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => { IsShuttingDown = true; Shutdown(); }));
        _tray.ContextMenuStrip = menu;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        MainWindowInstance.UpdateListenerStatus(HookRunning);
        if (!string.IsNullOrWhiteSpace(SettingsWarning)) System.Windows.MessageBox.Show(SettingsWarning, "GlyphEcho", MessageBoxButton.OK, MessageBoxImage.Warning);
        MainWindowInstance.Show();
    }

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e) => Current.Dispatcher.BeginInvoke(() => MainWindowInstance?.RefreshMonitors());

    internal static DisplayRule ResolveRule(string appPath)
    {
        var special = Settings.Rules.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.ProcessPath) && x.ProcessPath.Equals(appPath, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Priority).FirstOrDefault();
        if (special is null) return Settings.DefaultRule;
        return new DisplayRule
        {
            Name = string.IsNullOrWhiteSpace(special.Name) ? Settings.DefaultRule.Name : special.Name,
            Process = special.Process,
            ProcessPath = special.ProcessPath,
            Enabled = special.Enabled,
            Level = special.Level is >= 1 and <= 3 ? special.Level : Settings.DefaultRule.Level,
            Description = string.IsNullOrWhiteSpace(special.Description) ? Settings.DefaultRule.Description : special.Description,
            HiddenKeys = special.HiddenKeys.Count == 0 ? [.. Settings.DefaultRule.HiddenKeys] : [.. special.HiddenKeys],
            KeyRules = MergeKeyRules(special)
        };
    }
    private static List<KeyRule> MergeKeyRules(DisplayRule special) { if (!special.UseGlobalCatalog) return [.. special.KeyRules.Select(x => x.Clone())]; var merged = Settings.GlobalKeyCatalog.Select(x => x.Clone()).ToList(); foreach (var overrideRule in special.KeyRules) { var normalized = KeyboardHook.NormalizeForRule(overrideRule.Key); var existing = merged.FirstOrDefault(x => KeyboardHook.NormalizeForRule(x.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase)); if (existing is null) merged.Add(overrideRule.Clone()); else { existing.Enabled = overrideRule.Enabled; existing.Description = string.IsNullOrWhiteSpace(overrideRule.Description) ? existing.Description : overrideRule.Description; } } return merged; }
    internal static void RecordObservedKey(string display) { var normalized = KeyboardHook.NormalizeForRule(display); if (string.IsNullOrWhiteSpace(display) || Settings.IgnoredKeys.Any(x => KeyboardHook.NormalizeForRule(x).Equals(normalized, StringComparison.OrdinalIgnoreCase)) || Settings.GlobalKeyCatalog.Any(x => KeyboardHook.NormalizeForRule(x.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))) return; Settings.GlobalKeyCatalog.Add(new KeyRule { Key = display, Enabled = Settings.NewKeysEnabled, CreatedAt = DateTimeOffset.UtcNow }); Settings.DefaultRule.KeyRules = [.. Settings.GlobalKeyCatalog.Select(x => x.Clone())]; SaveSettings(); }
    internal static void SaveSettings() { lock (SettingsGate) { try { var path = TrySettingsPath(); if (path is null) return; var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); } catch (Exception ex) { SettingsWarning ??= $"设置无法保存，程序仍可运行，但本次修改不会持久化。\n原因：{ex.Message}"; } } }
    private static KeySettings LoadSettings() { try { var path = TrySettingsPath(); return path is not null && File.Exists(path) ? JsonSerializer.Deserialize<KeySettings>(File.ReadAllText(path)) ?? KeySettings.Default : KeySettings.Default; } catch (Exception ex) { var path = TrySettingsPath(); if (path is not null && File.Exists(path)) { var backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json"; try { File.Move(path, backup, true); SettingsWarning = $"设置文件无法读取，已备份为：{Path.GetFileName(backup)}\n原因：{ex.Message}"; } catch { SettingsWarning = $"设置文件无法读取，程序已使用默认设置。\n原因：{ex.Message}"; } } return KeySettings.Default; } }
    private static string? TrySettingsPath() { try { var dir = Environment.GetEnvironmentVariable("KEYOVERLAY_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho"); Directory.CreateDirectory(dir); return Path.Combine(dir, "settings.json"); } catch (Exception ex) { SettingsWarning ??= $"设置目录不可写，程序将使用临时设置运行。\n原因：{ex.Message}"; return null; } }
    protected override void OnExit(ExitEventArgs e) { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; _hook?.Dispose(); _gamepad?.Dispose(); _hidGamepad?.Dispose(); _tray?.Dispose(); Overlay?.Close(); if (_singleInstanceMutex is not null) { try { _singleInstanceMutex.ReleaseMutex(); } catch { } _singleInstanceMutex.Dispose(); } base.OnExit(e); }
}

public sealed class KeySettings
{
    public DisplayRule DefaultRule { get; set; } = new();
    public List<KeyRule> GlobalKeyCatalog { get; set; } = [];
    public List<DisplayRule> Rules { get; set; } = [];
    public string Mode { get; set; } = "普通模式";
    public int MonitorIndex { get; set; }
    public string OverlayPosition { get; set; } = "右下";
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "浅色";
    public bool NewKeysEnabled { get; set; } = true;
    public List<string> IgnoredKeys { get; set; } = [];
    public static KeySettings Default => new() { DefaultRule = new DisplayRule { Name = "默认规则", Level = 2, Enabled = true, HiddenKeys = ["CapsLock", "NumLock", "Scroll"] }, Rules = [] };
    public void NormalizeCatalog() { if (GlobalKeyCatalog.Count == 0 && DefaultRule.KeyRules.Count > 0) GlobalKeyCatalog = [.. DefaultRule.KeyRules.Select(x => x.Clone())]; DefaultRule.KeyRules = [.. GlobalKeyCatalog.Select(x => x.Clone())]; }
}
public sealed class DisplayRule
{
    public string Name { get; set; } = "默认规则"; public string Process { get; set; } = ""; public string ProcessPath { get; set; } = ""; public bool Enabled { get; set; } = true; public bool ShowSingleKeys { get; set; } public bool UseGlobalCatalog { get; set; } = true; public int Level { get; set; } = 2; public int Priority { get; set; } = 0; public string Description { get; set; } = ""; public List<string> HiddenKeys { get; set; } = []; public List<KeyRule> KeyRules { get; set; } = [];
    public DisplayRule Clone() => new() { Name = Name + " 副本", Process = Process, ProcessPath = ProcessPath, Enabled = Enabled, ShowSingleKeys = ShowSingleKeys, UseGlobalCatalog = UseGlobalCatalog, Level = Level, Priority = Priority, Description = Description, HiddenKeys = [.. HiddenKeys], KeyRules = [.. KeyRules.Select(x => x.Clone())] };
}
public sealed class KeyRule
{
    public string Key { get; set; } = "Ctrl + C"; public bool Enabled { get; set; } = true; public string Description { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;
    public KeyRule Clone() => new() { Key = Key, Enabled = Enabled, Description = Description, CreatedAt = CreatedAt };
}
