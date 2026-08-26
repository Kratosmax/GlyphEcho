using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
    private Forms.NotifyIcon? _tray;
    private static readonly object SettingsGate = new();
    private static readonly Dictionary<string, DisplayRule> ResolvedRules = new(StringComparer.OrdinalIgnoreCase);
    private static nint _cachedForegroundWindow;
    private static uint _cachedForegroundProcessId;
    private static (string Name, string Path) _cachedForegroundInfo;
    private static bool _hasCachedForegroundInfo;
    private static Mutex? _singleInstanceMutex;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _settingsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private bool _observedKeysDirty;
    internal static bool HookRunning { get; private set; }
    internal static string? SettingsWarning { get; private set; }
    internal static bool IsVisualQa { get; private set; }
    internal static bool IsBackgroundStartup { get; private set; }
    internal static string? CaptureOutputDirectory { get; private set; }
    internal static void ExitApplication() { IsShuttingDown = true; Current.Shutdown(); }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _settingsSaveTimer.Tick += (_, _) => { _settingsSaveTimer.Stop(); if (_observedKeysDirty) SaveSettings(); };
        var captureIndex = Array.FindIndex(e.Args, value => value.Equals("--capture-ui", StringComparison.OrdinalIgnoreCase));
        CaptureOutputDirectory = captureIndex >= 0 && captureIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[captureIndex + 1]) : null;
        IsVisualQa = CaptureOutputDirectory is not null || e.Args.Contains("--visual-qa", StringComparer.OrdinalIgnoreCase);
        IsBackgroundStartup = ShouldStartInBackground(e.Args, IsVisualQa);
        var mutexName = IsVisualQa ? "GlyphEcho.VisualQa.0.2" : "GlyphEcho.SingleInstance.0.1";
        _singleInstanceMutex = new Mutex(true, mutexName, out var created); if (!created) { AppDialog.ShowMessage(null, "GlyphEcho", "GlyphEcho 已经在运行中。"); Shutdown(); return; }
        Settings = LoadSettings();
        Settings.NormalizeCatalog();
        if (!IsVisualQa && !StartupRegistration.TryApply(Settings.StartWithWindows, out var startupError))
        {
            var warning = $"开机自启设置无法应用，程序仍可正常运行。\n原因：{startupError}";
            SettingsWarning = string.IsNullOrWhiteSpace(SettingsWarning) ? warning : $"{SettingsWarning}\n\n{warning}";
        }
        SaveSettings();
        MainWindowInstance = new MainWindow();
        Overlay = new OverlayWindow();
        if (!IsVisualQa)
        {
            _hook = new KeyboardHook();
            _hook.KeyPressed += (_, args) => Dispatcher.BeginInvoke(() => HandleKeyboardInput(args));
            HookRunning = _hook.Start();
            _gamepad = new GamepadHook();
            _gamepad.KeyPressed += (_, args) => Dispatcher.BeginInvoke(() => HandleGamepadInput(args));
            _gamepad.BeginPolling();
            _tray = new Forms.NotifyIcon { Icon = LoadApplicationIcon(), Visible = true, Text = "GlyphEcho" };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开设置", null, (_, _) => Dispatcher.Invoke(() => { MainWindowInstance?.Show(); MainWindowInstance?.Activate(); }));
            menu.Items.Add("检查更新", null, async (_, _) => await CheckForUpdatesAsync());
            menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => { IsShuttingDown = true; Shutdown(); }));
            _tray.ContextMenuStrip = menu;
        }
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        MainWindowInstance.UpdateListenerStatus(HookRunning);
        if (!string.IsNullOrWhiteSpace(SettingsWarning)) AppDialog.ShowMessage(MainWindowInstance, "GlyphEcho", SettingsWarning);
        if (CaptureOutputDirectory is not null) MainWindowInstance.ShowActivated = false;
        if (!IsBackgroundStartup || !string.IsNullOrWhiteSpace(SettingsWarning)) MainWindowInstance.Show();
        if (CaptureOutputDirectory is not null)
            _ = Dispatcher.BeginInvoke(async () => await VisualQaRunner.RunAsync(MainWindowInstance, CaptureOutputDirectory));
        else if (Settings.CheckForUpdates)
            _ = CheckForUpdatesInBackgroundAsync(_lifetimeCancellation.Token);
    }

    private Task CheckForUpdatesAsync()
    {
        MainWindowInstance?.OpenNetworkAndCheck();
        return Task.CompletedTask;
    }

    private static async Task CheckForUpdatesInBackgroundAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            await Current.Dispatcher.InvokeAsync(async () =>
            {
                if (!IsShuttingDown && MainWindowInstance is not null)
                    await MainWindowInstance.RunUpdateCheckAsync(true, false, cancellationToken);
            }).Task.Unwrap();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { WriteDiagnosticLog("background-update-check-failed.log", ex); }
    }

    internal static void WriteDiagnosticLog(string fileName, Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho", "logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}");
        }
        catch { }
    }

    private static System.Drawing.Icon LoadApplicationIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path) && System.Drawing.Icon.ExtractAssociatedIcon(path) is { } icon) return (System.Drawing.Icon)icon.Clone();
        }
        catch (Exception) { }
        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    internal static bool ShouldStartInBackground(IEnumerable<string> args, bool isVisualQa) => !isVisualQa && args.Contains("--background", StringComparer.OrdinalIgnoreCase);

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e) => Current.Dispatcher.BeginInvoke(() => MainWindowInstance?.RefreshMonitors());

    internal static DisplayRule ResolveRule(string appPath)
    {
        var cacheKey = appPath ?? string.Empty;
        if (ResolvedRules.TryGetValue(cacheKey, out var cached)) return cached;
        var special = Settings.FindRule(cacheKey);
        var source = special ?? Settings.DefaultRule;
        var resolved = new DisplayRule
        {
            Name = special is not null && string.IsNullOrWhiteSpace(source.Name) ? Settings.DefaultRule.Name : source.Name,
            Process = source.Process,
            ProcessPath = source.ProcessPath,
            Enabled = source.Enabled,
            ShowSingleKeys = source.ShowSingleKeys,
            UseGlobalCatalog = special is null || source.UseGlobalCatalog,
            Level = source.Level is >= 1 and <= 3 ? source.Level : Settings.DefaultRule.Level,
            Priority = source.Priority,
            Description = special is not null && string.IsNullOrWhiteSpace(source.Description) ? Settings.DefaultRule.Description : source.Description,
            InheritHiddenKeys = source.InheritHiddenKeys,
            HiddenKeys = special is not null && source.InheritHiddenKeys ? Settings.DefaultRule.HiddenKeys : source.HiddenKeys,
            KeyRules = special is null ? [] : source.KeyRules
        };
        if (special is null || source.UseGlobalCatalog) resolved.InheritedKeyRuleIndex = Settings.CatalogRuleIndex;
        resolved = ModePolicy.Apply(resolved, Settings.Mode, Settings.GameModeLevel);
        ResolvedRules[cacheKey] = resolved;
        return resolved;
    }
    private static void HandleKeyboardInput(KeyboardPressedEventArgs args)
    {
        var info = GetForegroundInfo(args.ForegroundWindow, args.ForegroundProcessId);
        var rule = ResolveRule(info.Path);
        var display = KeyboardHook.Format(args.Key, args.Modifiers, rule.ShowSingleKeys, rule.Level >= 3, args.ModifierSides);
        if (string.IsNullOrWhiteSpace(display)) return;
        var catalog = KeyboardHook.BuildCatalogKey(args.Key, args.Modifiers);
        if (RecordObservedKey(catalog)) { MainWindowInstance?.RefreshCatalog(); rule = ResolveRule(info.Path); }
        Overlay?.Present(display, info.Name, rule);
    }
    private static void HandleGamepadInput(KeyPressedEventArgs args)
    {
        var identity = KeyboardHook.GetForegroundIdentity();
        var info = GetForegroundInfo(identity.Window, identity.ProcessId);
        if (RecordObservedKey(args.CatalogKey)) MainWindowInstance?.RefreshCatalog();
        Overlay?.Present(args.Display, info.Name, ResolveRule(info.Path));
    }
    private static (string Name, string Path) GetForegroundInfo(nint window, uint processId)
    {
        if (_hasCachedForegroundInfo && window == _cachedForegroundWindow && processId == _cachedForegroundProcessId) return _cachedForegroundInfo;
        _cachedForegroundWindow = window;
        _cachedForegroundProcessId = processId;
        _hasCachedForegroundInfo = true;
        return _cachedForegroundInfo = KeyboardHook.GetProcessInfo(processId);
    }
    internal static bool RecordObservedKey(string display)
    {
        if (!Settings.TryAddObservedKey(display)) return false;
        ResolvedRules.Clear();
        if (Current is App app)
        {
            app._observedKeysDirty = true;
            app._settingsSaveTimer.Stop();
            app._settingsSaveTimer.Start();
        }
        else SaveSettings();
        return true;
    }
    internal static bool SaveSettings()
    {
        lock (SettingsGate)
        {
            string? temp = null;
            try
            {
                Settings.RebuildRuntimeIndexes();
                ResolvedRules.Clear();
                var path = TrySettingsPath();
                if (path is null) return false;
                temp = path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    JsonSerializer.Serialize(stream, Settings, new JsonSerializerOptions { WriteIndented = true });
                    stream.Flush(true);
                }
                File.Move(temp, path, true);
                if (Current is App app) app._observedKeysDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                SettingsWarning = $"设置无法保存，程序仍可运行，但本次修改不会持久化。\n原因：{ex.Message}";
                if (temp is not null) try { File.Delete(temp); } catch { }
                return false;
            }
        }
    }
    private static KeySettings LoadSettings() { try { var path = TrySettingsPath(); return path is not null && File.Exists(path) ? JsonSerializer.Deserialize<KeySettings>(File.ReadAllText(path)) ?? KeySettings.Default : KeySettings.Default; } catch (Exception ex) { var path = TrySettingsPath(); if (path is not null && File.Exists(path)) { var backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json"; try { File.Move(path, backup, true); SettingsWarning = $"设置文件无法读取，已备份为：{Path.GetFileName(backup)}\n原因：{ex.Message}"; } catch { SettingsWarning = $"设置文件无法读取，程序已使用默认设置。\n原因：{ex.Message}"; } } return KeySettings.Default; } }
    private static string? TrySettingsPath() { try { var dir = Environment.GetEnvironmentVariable("KEYOVERLAY_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho"); Directory.CreateDirectory(dir); return Path.Combine(dir, "settings.json"); } catch (Exception ex) { SettingsWarning ??= $"设置目录不可写，程序将使用临时设置运行。\n原因：{ex.Message}"; return null; } }
    protected override void OnExit(ExitEventArgs e) { _settingsSaveTimer.Stop(); if (_observedKeysDirty) SaveSettings(); _lifetimeCancellation.Cancel(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; _hook?.Dispose(); _gamepad?.Dispose(); _tray?.Dispose(); Overlay?.Close(); if (_singleInstanceMutex is not null) { try { _singleInstanceMutex.ReleaseMutex(); } catch { } _singleInstanceMutex.Dispose(); } _lifetimeCancellation.Dispose(); base.OnExit(e); }
}

public sealed class KeySettings
{
    private readonly HashSet<string> _catalogIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignoredIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KeyRule> _catalogRuleIndex = new(StringComparer.OrdinalIgnoreCase);
    public DisplayRule DefaultRule { get; set; } = new();
    public List<KeyRule> GlobalKeyCatalog { get; set; } = [];
    public List<DisplayRule> Rules { get; set; } = [];
    public string Mode { get; set; } = "普通模式";
    public int GameModeLevel { get; set; } = 1;
    public int MonitorIndex { get; set; }
    public string MonitorDeviceName { get; set; } = "";
    public string OverlayPosition { get; set; } = "右下";
    public Dictionary<string, OverlayOffset> OverlayOffsets { get; set; } = CreateDefaultOffsets();
    public string OverlayPalette { get; set; } = OverlayPaletteCatalog.DefaultId;
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "浅色";
    public bool CheckForUpdates { get; set; } = true;
    public bool EnableMaterial { get; set; } = true;
    public string UpdateChannel { get; set; } = "lite";
    public UpdateNetworkSettings UpdateNetwork { get; set; } = UpdateNetworkSettings.Default;
    public bool NewKeysEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public List<string> IgnoredKeys { get; set; } = [];
    private readonly Dictionary<string, DisplayRule> _ruleIndex = new(StringComparer.OrdinalIgnoreCase);
    public static KeySettings Default => new() { DefaultRule = new DisplayRule { Name = "默认规则", Level = 2, Enabled = true, HiddenKeys = ["CapsLock", "NumLock", "Scroll"] }, Rules = [] };
    public void NormalizeCatalog()
    {
        DefaultRule ??= new DisplayRule { Name = "默认规则", Level = 2, Enabled = true, HiddenKeys = ["CapsLock", "NumLock", "Scroll"] };
        GlobalKeyCatalog = GlobalKeyCatalog?.OfType<KeyRule>().ToList() ?? [];
        Rules = Rules?.OfType<DisplayRule>().ToList() ?? [];
        IgnoredKeys = IgnoredKeys?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        DefaultRule.Normalize();
        foreach (var rule in Rules) rule.Normalize();
        foreach (var key in GlobalKeyCatalog) key.Normalize();
        if (GlobalKeyCatalog.Count == 0 && DefaultRule.KeyRules.Count > 0) GlobalKeyCatalog = [.. DefaultRule.KeyRules.Select(x => x.Clone())];
        DefaultRule.KeyRules = [];
        DefaultRule.InvalidateKeyRuleIndex();
        GameModeLevel = GameModeLevel == 2 ? 2 : 1;
        Mode = Mode is ModePolicy.Game or ModePolicy.Presentation ? Mode : ModePolicy.Normal;
        OverlayPosition = OverlayPositions.Contains(OverlayPosition, StringComparer.Ordinal) ? OverlayPosition : "右下";
        MonitorDeviceName ??= "";
        Theme ??= "浅色";
        UpdateChannel = UpdateChannel?.Equals("full", StringComparison.OrdinalIgnoreCase) == true ? "full" : "lite";
        OverlayPalette = OverlayPaletteCatalog.Normalize(OverlayPalette);
        OverlayOffsets ??= [];
        foreach (var position in OverlayPositions) { if (!OverlayOffsets.TryGetValue(position, out var offset) || offset is null) OverlayOffsets[position] = new OverlayOffset(); else offset.Normalize(); }
        UpdateNetwork = (UpdateNetwork ?? UpdateNetworkSettings.Default).Normalize();
        RebuildRuntimeIndexes();
    }
    internal bool TryAddObservedKey(string display)
    {
        if (string.IsNullOrWhiteSpace(display)) return false;
        var normalized = KeyboardHook.NormalizeForRule(display);
        if (string.IsNullOrWhiteSpace(normalized) || _ignoredIndex.Contains(normalized) || !_catalogIndex.Add(normalized)) return false;
        var rule = new KeyRule { Key = display, Enabled = NewKeysEnabled, CreatedAt = DateTimeOffset.UtcNow };
        GlobalKeyCatalog.Add(rule);
        _catalogRuleIndex[normalized] = rule;
        return true;
    }
    internal int DeleteCatalogKeys(IEnumerable<KeyRule> keys)
    {
        var selected = keys.Select(key => (Key: key.Key, Normalized: KeyboardHook.NormalizeForRule(key.Key))).Where(item => !string.IsNullOrWhiteSpace(item.Normalized)).GroupBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        if (selected.Count == 0) return 0;
        var normalizedKeys = selected.Select(item => item.Normalized).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = GlobalKeyCatalog.RemoveAll(item => normalizedKeys.Contains(KeyboardHook.NormalizeForRule(item.Key)));
        if (removed == 0) return 0;
        foreach (var item in selected)
            if (_ignoredIndex.Add(item.Normalized)) IgnoredKeys.Add(item.Key);
        RebuildCatalogIndexes();
        return removed;
    }
    private void RebuildCatalogIndexes()
    {
        _catalogIndex.Clear();
        _ignoredIndex.Clear();
        _catalogRuleIndex.Clear();
        foreach (var item in GlobalKeyCatalog)
        {
            var normalized = KeyboardHook.NormalizeForRule(item.Key);
            _catalogIndex.Add(normalized);
            _catalogRuleIndex.TryAdd(normalized, item);
        }
        foreach (var item in IgnoredKeys) _ignoredIndex.Add(KeyboardHook.NormalizeForRule(item));
    }
    internal void RebuildRuntimeIndexes()
    {
        RebuildCatalogIndexes();
        _ruleIndex.Clear();
        foreach (var rule in Rules.Where(rule => rule.Enabled && !string.IsNullOrWhiteSpace(rule.ProcessPath)).OrderByDescending(rule => rule.Priority))
            _ruleIndex.TryAdd(rule.ProcessPath, rule);
    }
    internal DisplayRule? FindRule(string appPath) => _ruleIndex.GetValueOrDefault(appPath ?? string.Empty);
    internal IReadOnlyDictionary<string, KeyRule> CatalogRuleIndex => _catalogRuleIndex;
    internal OverlayOffset GetOverlayOffset(string position) { if (!OverlayOffsets.TryGetValue(position, out var offset) || offset is null) { offset = new OverlayOffset(); OverlayOffsets[position] = offset; } return offset; }
    private static Dictionary<string, OverlayOffset> CreateDefaultOffsets() => OverlayPositions.ToDictionary(position => position, _ => new OverlayOffset());
    private static readonly string[] OverlayPositions = ["右下", "右上", "左下", "左上"];
}
public sealed class OverlayOffset
{
    public int X { get; set; }
    public int Y { get; set; }
    internal void Normalize() { X = Math.Clamp(X, -5000, 5000); Y = Math.Clamp(Y, -5000, 5000); }
    internal OverlayOffset Clone() => new() { X = X, Y = Y };
}
public sealed class DisplayRule
{
    public string Name { get; set; } = "默认规则"; public string Process { get; set; } = ""; public string ProcessPath { get; set; } = ""; public bool Enabled { get; set; } = true; public bool ShowSingleKeys { get; set; } public bool UseGlobalCatalog { get; set; } = true; public int Level { get; set; } = 2; public int Priority { get; set; } = 0; public string Description { get; set; } = ""; public bool InheritHiddenKeys { get; set; } = true; public List<string> HiddenKeys { get; set; } = []; public List<KeyRule> KeyRules { get; set; } = [];
    [JsonIgnore] private Dictionary<string, KeyRule>? KeyRuleIndex { get; set; }
    [JsonIgnore] internal IReadOnlyDictionary<string, KeyRule>? InheritedKeyRuleIndex { get; set; }
    internal void Normalize() { Name ??= ""; Process ??= ""; ProcessPath ??= ""; Description ??= ""; HiddenKeys = HiddenKeys?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? []; KeyRules = KeyRules?.OfType<KeyRule>().ToList() ?? []; foreach (var key in KeyRules) key.Normalize(); KeyRuleIndex = null; }
    internal KeyRule? FindKeyRule(string normalizedKey)
    {
        KeyRuleIndex ??= BuildKeyRuleIndex();
        return KeyRuleIndex.GetValueOrDefault(normalizedKey) ?? InheritedKeyRuleIndex?.GetValueOrDefault(normalizedKey);
    }
    private Dictionary<string, KeyRule> BuildKeyRuleIndex()
    {
        var result = new Dictionary<string, KeyRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in KeyRules.GroupBy(item => KeyboardHook.NormalizeForRule(item.Key), StringComparer.OrdinalIgnoreCase))
        {
            var item = group.First();
            if (!item.HasDescriptionOverride && string.IsNullOrWhiteSpace(item.Description)
                && InheritedKeyRuleIndex?.GetValueOrDefault(group.Key) is { } inherited)
            {
                item = item.Clone();
                item.Description = inherited.Description;
            }
            result[group.Key] = item;
        }
        return result;
    }
    internal void InvalidateKeyRuleIndex() => KeyRuleIndex = null;
    public DisplayRule Clone() => new() { Name = Name + " 副本", Process = Process, ProcessPath = ProcessPath, Enabled = Enabled, ShowSingleKeys = ShowSingleKeys, UseGlobalCatalog = UseGlobalCatalog, Level = Level, Priority = Priority, Description = Description, InheritHiddenKeys = InheritHiddenKeys, HiddenKeys = [.. HiddenKeys], KeyRules = [.. KeyRules.Select(x => x.Clone())] };
}
public sealed class KeyRule
{
    public string Key { get; set; } = "Ctrl + C"; public bool Enabled { get; set; } = true; public string Description { get; set; } = ""; public bool HasDescriptionOverride { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;
    internal void Normalize() { Key ??= ""; Description ??= ""; }
    public KeyRule Clone() => new() { Key = Key, Enabled = Enabled, Description = Description, HasDescriptionOverride = HasDescriptionOverride, CreatedAt = CreatedAt };
}
