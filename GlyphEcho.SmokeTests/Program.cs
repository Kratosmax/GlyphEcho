using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlyphEcho;

var tests = new (string Name, Action Run)[]
{
    ("摇杆方向与死区", TestStickDirections),
    ("提示队列合并与过期前移", TestOverlayQueue),
    ("提示色板与配置兼容", TestOverlayPalettes),
    ("模式覆盖规则", TestModePolicy),
    ("键盘单键与文本输入规范化", TestKeyboardFormatting),
    ("应用规则合并与按键说明", TestRuleResolution),
    ("配置空值迁移与保存失败信号", TestSettingsRecovery),
    ("提示位置微调与持久化", TestOverlayOffsets),
    ("按键目录查重与批量删除", TestCatalogIndex),
    ("开机自启命令引用", TestStartupCommand),
    ("后台启动参数", TestBackgroundStartup),
    ("更新线路规范化与排序", TestNetworkRoutes),
    ("Legacy/V2 清单签名", TestManifestSignatures),
    ("更新包结构与通道", TestPackageStructure)
};

var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
for (uint index = 0; index < 4; index++)
    if (GamepadHook.TryReadSnapshot(index, out var gamepad))
        Console.WriteLine($"INFO 手柄 {index + 1}: buttons=0x{gamepad.Buttons:X4}, LT={gamepad.LeftTrigger}, RT={gamepad.RightTrigger}, LS=({gamepad.LeftX},{gamepad.LeftY}), RS=({gamepad.RightX},{gamepad.RightY})");
return failed == 0 ? 0 : 1;

static void TestStickDirections()
{
    Equal(StickDirection.None, GamepadHook.ResolveDirection(8000, 0, StickDirection.None));
    Equal(StickDirection.Right, GamepadHook.ResolveDirection(20000, 0, StickDirection.None));
    Equal(StickDirection.Up, GamepadHook.ResolveDirection(0, 20000, StickDirection.None));
    Equal(StickDirection.UpLeft, GamepadHook.ResolveDirection(-20000, 20000, StickDirection.None));
    Equal(StickDirection.Right, GamepadHook.ResolveDirection(10000, 0, StickDirection.Right));
    Equal(StickDirection.None, GamepadHook.ResolveDirection(8000, 0, StickDirection.Right));
}

static void TestOverlayQueue()
{
    var queue = new OverlayQueue(TimeSpan.FromMilliseconds(1300));
    var start = DateTimeOffset.Parse("2026-08-25T00:00:00Z");
    var copy = new OverlayPresentation("Ctrl + C", "Test", "", "", 2);
    var paste = new OverlayPresentation("Ctrl + V", "Test", "", "", 2);
    queue.Add(copy, start);
    queue.Add(copy, start.AddMilliseconds(100));
    queue.Add(paste, start.AddMilliseconds(250));
    var initial = queue.Snapshot(start.AddMilliseconds(250));
    Equal(2, initial.Count);
    Equal(2, initial[0].Count);
    Equal("Ctrl + V", queue.Snapshot(start.AddMilliseconds(1450))[0].Presentation.Display);

    var lowQueue = new OverlayQueue(TimeSpan.FromMilliseconds(1300));
    lowQueue.Add(new OverlayPresentation("Ctrl + Alt + W", "AppA", "", "", 1), start);
    lowQueue.Add(new OverlayPresentation("Ctrl + Alt + W", "AppB", "", "", 1), start.AddMilliseconds(50));
    var lowItems = lowQueue.Snapshot(start.AddMilliseconds(50));
    Equal(1, lowItems.Count);
    Equal(2, lowItems[0].Count);

    var mediumQueue = new OverlayQueue(TimeSpan.FromMilliseconds(1300));
    mediumQueue.Add(new OverlayPresentation("Ctrl + C", "AppA", "来源 A", "", 2), start);
    mediumQueue.Add(new OverlayPresentation("Ctrl + C", "AppB", "来源 B", "", 2), start.AddMilliseconds(50));
    var mediumItems = mediumQueue.Snapshot(start.AddMilliseconds(50));
    Equal(1, mediumItems.Count);
    Equal(2, mediumItems[0].Count);
    Equal("来源 B", mediumItems[0].Presentation.Source);

    var highQueue = new OverlayQueue(TimeSpan.FromMilliseconds(1300));
    highQueue.Add(new OverlayPresentation("Ctrl + C", "AppA", "同一来源", "复制", 3), start);
    highQueue.Add(new OverlayPresentation("Ctrl + C", "AppA", "同一来源", "其他功能", 3), start.AddMilliseconds(50));
    var highItems = highQueue.Snapshot(start.AddMilliseconds(50));
    Equal(1, highItems.Count);
    Equal(2, highItems[0].Count);
    Equal("其他功能", highItems[0].Presentation.Action);
}

static void TestOverlayPalettes()
{
    Equal(6, OverlayPaletteCatalog.All.Count);
    Equal(6, OverlayPaletteCatalog.All.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    Equal(OverlayPaletteCatalog.DefaultId, OverlayPaletteCatalog.Normalize("unknown"));
    Equal("light-rose", OverlayPaletteCatalog.Normalize("LIGHT-ROSE"));

    var settings = KeySettings.Default;
    settings.OverlayPalette = "LIGHT-BLUE";
    settings.NormalizeCatalog();
    Equal("light-blue", settings.OverlayPalette);

    foreach (var palette in OverlayPaletteCatalog.All)
    {
        True(ContrastRatio(palette.Accent, palette.Surface) >= 4.5);
        True(ContrastRatio(palette.KeyText, palette.KeySurface) >= 4.5);
    }
}

static void TestModePolicy()
{
    var configured = new DisplayRule { ShowSingleKeys = false, Level = 2, KeyRules = [new KeyRule { Key = "Ctrl + C", Enabled = true }] };
    var normal = ModePolicy.Apply(configured, ModePolicy.Normal);
    Equal(false, normal.ShowSingleKeys);
    Equal(2, normal.Level);
    var game = ModePolicy.Apply(configured, ModePolicy.Game);
    Equal(true, game.ShowSingleKeys);
    Equal(1, game.Level);
    Equal(2, ModePolicy.Apply(configured, ModePolicy.Game, 2).Level);
    Equal(1, ModePolicy.Apply(configured, ModePolicy.Game, 3).Level);
    var presentation = ModePolicy.Apply(configured, ModePolicy.Presentation);
    Equal(true, presentation.ShowSingleKeys);
    Equal(3, presentation.Level);
    Equal(false, configured.ShowSingleKeys);
    Equal(2, configured.Level);
}

static void TestOverlayOffsets()
{
    var area = new System.Drawing.Rectangle(100, 50, 1000, 700);
    var moved = NativeMethods.ResolveOverlayPosition(area, "右下", 160, 100, 18, -10, 6);
    Equal(new System.Drawing.Point(912, 638), moved);
    var clamped = NativeMethods.ResolveOverlayPosition(area, "右下", 160, 100, 18, 500, 500);
    Equal(new System.Drawing.Point(940, 650), clamped);

    var settings = KeySettings.Default;
    settings.GetOverlayOffset("右下").X = -10;
    settings.GetOverlayOffset("右下").Y = 6;
    var restored = JsonSerializer.Deserialize<KeySettings>(JsonSerializer.Serialize(settings))!;
    restored.NormalizeCatalog();
    Equal(-10, restored.GetOverlayOffset("右下").X);
    Equal(6, restored.GetOverlayOffset("右下").Y);
    Equal(0, restored.GetOverlayOffset("右上").X);
}

static void TestKeyboardFormatting()
{
    Equal("A", KeyboardHook.Format(System.Windows.Input.Key.A, System.Windows.Input.ModifierKeys.None, true, false));
    Equal(string.Empty, KeyboardHook.BuildCatalogKey(System.Windows.Input.Key.A, System.Windows.Input.ModifierKeys.None));
    Equal("1", KeyboardHook.Format(System.Windows.Input.Key.D1, System.Windows.Input.ModifierKeys.Shift, true, false));
    Equal(string.Empty, KeyboardHook.BuildCatalogKey(System.Windows.Input.Key.D1, System.Windows.Input.ModifierKeys.Shift));
    Equal(string.Empty, KeyboardHook.Format(System.Windows.Input.Key.LeftAlt, System.Windows.Input.ModifierKeys.Alt, true, true, ModifierSideState.LeftAlt));
    Equal("LeftAlt + C", KeyboardHook.Format(System.Windows.Input.Key.C, System.Windows.Input.ModifierKeys.Alt, false, true, ModifierSideState.LeftAlt));
    Equal("Alt + C", KeyboardHook.NormalizeForRule("LeftAlt + C"));
}

static void TestRuleResolution()
{
    var settings = KeySettings.Default;
    settings.DefaultRule.ShowSingleKeys = false;
    settings.GlobalKeyCatalog =
    [
        new KeyRule { Key = "Ctrl + C", Enabled = true, Description = "复制" },
        new KeyRule { Key = "Ctrl + V", Enabled = true, Description = "粘贴" }
    ];
    settings.Rules.Add(new DisplayRule
    {
        Name = "Target", ProcessPath = @"C:\Target\app.exe", Enabled = true, ShowSingleKeys = true,
        UseGlobalCatalog = true, KeyRules = [new KeyRule { Key = "Ctrl + C", Enabled = true, Description = "", HasDescriptionOverride = true }]
    });
    settings.NormalizeCatalog();
    SetAppSettings(settings);
    var resolved = App.ResolveRule(@"C:\Target\app.exe");
    Equal(true, resolved.ShowSingleKeys);
    Equal(1, resolved.KeyRules.Count);
    True(ReferenceEquals(settings.CatalogRuleIndex, resolved.InheritedKeyRuleIndex));
    Equal(string.Empty, resolved.FindKeyRule("Ctrl + C")!.Description);
    Equal("粘贴", resolved.FindKeyRule("Ctrl + V")!.Description);

    var defaultResolved = App.ResolveRule(@"C:\Other\app.exe");
    Equal(0, defaultResolved.KeyRules.Count);
    True(ReferenceEquals(settings.CatalogRuleIndex, defaultResolved.InheritedKeyRuleIndex));
    Equal("复制", defaultResolved.FindKeyRule("Ctrl + C")!.Description);
}

static void TestSettingsRecovery()
{
    var settings = JsonSerializer.Deserialize<KeySettings>("""{"DefaultRule":null,"GlobalKeyCatalog":null,"Rules":null,"IgnoredKeys":null,"OverlayOffsets":null,"UpdateNetwork":null}""")!;
    settings.NormalizeCatalog();
    True(settings.DefaultRule is not null);
    Equal(0, settings.GlobalKeyCatalog.Count);
    Equal(0, settings.Rules.Count);
    True(settings.OverlayOffsets.Count >= 4);

    var root = Path.Combine(Path.GetTempPath(), "GlyphEcho-save-test-" + Guid.NewGuid().ToString("N"));
    File.WriteAllText(root, "not a directory");
    var previous = Environment.GetEnvironmentVariable("KEYOVERLAY_DATA_DIR");
    try
    {
        Environment.SetEnvironmentVariable("KEYOVERLAY_DATA_DIR", root);
        SetAppSettings(settings);
        Equal(false, App.SaveSettings());
    }
    finally
    {
        Environment.SetEnvironmentVariable("KEYOVERLAY_DATA_DIR", previous);
        File.Delete(root);
    }
}

static void TestCatalogIndex()
{
    var settings = new KeySettings
    {
        GlobalKeyCatalog = [new KeyRule { Key = "Ctrl + C", Enabled = true }],
        IgnoredKeys = ["Alt + F4"]
    };
    settings.NormalizeCatalog();
    Equal(false, settings.TryAddObservedKey("Ctrl+C"));
    Equal(false, settings.TryAddObservedKey("LeftAlt + F4"));
    Equal(true, settings.TryAddObservedKey("Ctrl + V"));
    Equal(2, settings.GlobalKeyCatalog.Count);
    Equal(1, settings.DeleteCatalogKeys([settings.GlobalKeyCatalog[0]]));
    Equal(false, settings.TryAddObservedKey("Ctrl+C"));
    Equal(1, settings.GlobalKeyCatalog.Count);
    Equal(0, settings.DefaultRule.KeyRules.Count);
}

static void TestStartupCommand()
{
    var path = Path.Combine("C:\\", "Program Files", "GlyphEcho", "GlyphEcho.exe");
    Equal("\"C:\\Program Files\\GlyphEcho\\GlyphEcho.exe\" --background", StartupRegistration.BuildCommand(path));
}

static void TestBackgroundStartup()
{
    Equal(true, App.ShouldStartInBackground(["--background"], false));
    Equal(true, App.ShouldStartInBackground(["--BACKGROUND"], false));
    Equal(false, App.ShouldStartInBackground([], false));
    Equal(false, App.ShouldStartInBackground(["--background"], true));
}

static void TestNetworkRoutes()
{
    var settings = new UpdateNetworkSettings(
    [
        new GithubProxySetting("", 1, true),
        new GithubProxySetting("https://proxy.example/", 8),
        new GithubProxySetting("https://proxy.example", 5),
        new GithubProxySetting("https://backup.example/github", 8)
    ], "http://127.0.0.1:7890").Normalize();
    Equal(3, settings.GithubProxies!.Count);
    Equal("http://127.0.0.1:7890", settings.HttpProxy);
    var routes = UpdateRouteBuilder.Build(new Uri("https://github.com/Kratosmax/GlyphEcho/releases/latest/download/update-lite.json"), settings);
    Equal("proxy.example", routes[0].DisplayName);
    Equal("backup.example", routes[1].DisplayName);
    Equal("GitHub 直连", routes[2].DisplayName);
    True(routes[0].RequestUri.AbsoluteUri.StartsWith("https://proxy.example/https://github.com/", StringComparison.Ordinal));
    var untouched = UpdateRouteBuilder.Build(new Uri("https://example.com/file"), settings);
    Equal("https://example.com/file", untouched[0].RequestUri.AbsoluteUri);
}

static void TestManifestSignatures()
{
    using var rsa = RSA.Create(2048);
    var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
    var manifest = new UpdateManifest
    {
        Product = "GlyphEcho",
        Channel = "lite",
        Version = "0.3.0",
        DownloadUrl = "https://github.com/Kratosmax/GlyphEcho/releases/download/0.3.0/GlyphEcho-0.3.0-Lite.zip",
        Size = 1234,
        Sha256 = new string('a', 64),
        ReleaseNotes = "本次更新"
    };
    var legacySignature = Sign(rsa, UpdateService.LegacyPayload(manifest));
    var legacyJson = JsonSerializer.Serialize(new
    {
        product = manifest.Product, channel = manifest.Channel, version = manifest.Version,
        downloadUrl = manifest.DownloadUrl, size = manifest.Size, sha256 = manifest.Sha256,
        signature = legacySignature, releaseNotes = manifest.ReleaseNotes, releaseNotesUrl = ""
    });
    Equal(new Version(0, 3, 0), UpdateService.ParseAndVerify(legacyJson, "lite", publicKey).Version);

    var v2Signature = Sign(rsa, UpdateService.V2Payload(manifest));
    var v2Json = JsonSerializer.Serialize(new
    {
        product = manifest.Product, channel = manifest.Channel, version = manifest.Version,
        downloadUrl = manifest.DownloadUrl, size = manifest.Size, sha256 = manifest.Sha256,
        signature = legacySignature, signatureV2 = v2Signature, releaseNotes = manifest.ReleaseNotes, releaseNotesUrl = ""
    });
    Equal(new Version(0, 3, 0), UpdateService.ParseAndVerify(v2Json, "lite", publicKey).Version);
    var tamperedJson = JsonSerializer.Serialize(new
    {
        product = manifest.Product, channel = manifest.Channel, version = manifest.Version,
        downloadUrl = manifest.DownloadUrl, size = manifest.Size, sha256 = manifest.Sha256,
        signature = legacySignature, signatureV2 = v2Signature, releaseNotes = "已被篡改", releaseNotesUrl = ""
    });
    Throws<CryptographicException>(() => UpdateService.ParseAndVerify(tamperedJson, "lite", publicKey));
}

static void TestPackageStructure()
{
    var root = Path.Combine(Path.GetTempPath(), "GlyphEcho-package-test-" + Guid.NewGuid().ToString("N"));
    var package = Path.Combine(root, "package.zip");
    Directory.CreateDirectory(root);
    try
    {
        using (var archive = System.IO.Compression.ZipFile.Open(package, System.IO.Compression.ZipArchiveMode.Create))
        {
            WriteEntry(archive, "GlyphEcho.exe", "app");
            WriteEntry(archive, "GlyphEcho.Updater.exe", "updater");
            WriteEntry(archive, ".glyph-echo-channel", "lite");
        }
        UpdateService.VerifyPackageStructure(package, "lite");
        Throws<InvalidDataException>(() => UpdateService.VerifyPackageStructure(package, "full"));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static void WriteEntry(System.IO.Compression.ZipArchive archive, string name, string value)
{
    using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
    writer.Write(value);
}

static string Sign(RSA rsa, string payload) => Convert.ToBase64String(
    rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

static double ContrastRatio(System.Windows.Media.Color foreground, System.Windows.Media.Color background)
{
    static double Luminance(System.Windows.Media.Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    var first = Luminance(foreground);
    var second = Luminance(background);
    return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"期望 {expected}，实际 {actual}");
}

static void True(bool value)
{
    if (!value) throw new InvalidOperationException("条件不成立");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"预期抛出 {typeof(T).Name}");
}

static void SetAppSettings(KeySettings settings)
{
    typeof(App).GetField("<Settings>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, settings);
    typeof(App).GetField("ResolvedRules", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!.GetType().GetMethod("Clear")!.Invoke(typeof(App).GetField("ResolvedRules", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null), null);
}
