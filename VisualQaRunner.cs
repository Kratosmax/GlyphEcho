using System.Text.Json;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace GlyphEcho;

internal static class VisualQaRunner
{
    internal static async Task RunAsync(MainWindow main, string outputDirectory)
    {
        var captures = new List<VisualCaptureResult>();
        var statusPath = Path.Combine(outputDirectory, "ui-capture-status.json");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            await Task.Delay(500);
            var backdrop = main.ApplyMaterialForVisualQa(true);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-overview-acrylic.png")));

            main.ShowStartupSettingForVisualQa();
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-startup-setting.png")));
            main.ShowPaletteSettingForVisualQa();
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-overlay-palettes.png")));
            main.ShowDefaultTopForVisualQa();

            main.SetGameLevelForVisualQa(1);
            main.SetModeForVisualQa(ModePolicy.Game);
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-game-mode-low.png")));
            main.SetGameLevelForVisualQa(2);
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-game-mode-medium.png")));
            main.SetModeForVisualQa(ModePolicy.Presentation);
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-presentation-mode.png")));
            main.SetModeForVisualQa(ModePolicy.Normal);
            main.SetPositionOffsetForVisualQa("右下", -10, 0);
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-position-offset.png")));

            main.NavigateForVisualQa("Network");
            await Task.Delay(150);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-network.png")));

            App.Settings.GlobalKeyCatalog =
            [
                new KeyRule { Key = "Ctrl + Shift + Space", Enabled = true, CreatedAt = DateTimeOffset.UtcNow },
                new KeyRule { Key = "Ctrl + C", Enabled = true, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-1) },
                new KeyRule { Key = "Alt + Tab", Enabled = false, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-2) }
            ];
            App.Settings.NormalizeCatalog();
            main.NavigateForVisualQa("Keys");
            main.SelectAllCatalogForVisualQa();
            await Task.Delay(150);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-catalog-multi-select.png")));

            main.Width = main.MinWidth;
            main.Height = main.MinHeight;
            main.NavigateForVisualQa("Default");
            await Task.Delay(150);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-minimum.png")));

            var fallback = main.ApplyMaterialForVisualQa(false);
            await Task.Delay(100);
            captures.Add(VisualCaptureService.Capture(main, Path.Combine(outputDirectory, "main-opaque-fallback.png")));
            _ = main.ApplyMaterialForVisualQa(true);

            var rule = new DisplayRule
            {
                Name = "长名称应用规则 · 演示与直播场景",
                Enabled = true,
                ShowSingleKeys = true,
                Level = 3,
                Description = "用于确认长文本、继承目录和按钮在最小窗口下不会重叠。",
                KeyRules =
                [
                    new KeyRule { Key = "Ctrl + Shift + Alt + F12", Enabled = true, Description = "较长组合键说明" },
                    new KeyRule { Key = "手柄 1 · LS ↖", Enabled = true, Description = "左摇杆方向" }
                ]
            };
            var editor = new RuleEditorWindow(rule, true, rule.KeyRules) { Owner = main, ShowActivated = false };
            editor.Show();
            await Task.Delay(250);
            captures.Add(VisualCaptureService.Capture(editor, Path.Combine(outputDirectory, "rule-editor.png")));
            editor.Close();

            var manifest = new UpdateManifest
            {
                Product = "GlyphEcho",
                Channel = "lite",
                Version = "0.3.0",
                DownloadUrl = "https://github.com/Kratosmax/GlyphEcho/releases/download/0.3.0/GlyphEcho-0.3.0-Lite.zip",
                Size = 8_388_608,
                Sha256 = new string('a', 64),
                Signature = "visual-qa",
                ReleaseNotes = "修复双屏边界跳动。\n\n新增左右摇杆八方向识别和 200ms 动态提示队列。\n\n统一 PinNote 风格毛玻璃、图标与网络更新设置。"
            };
            var update = new UpdateWindow(new UpdateInfo(manifest, new Version(0, 3, 0), new Uri(manifest.DownloadUrl), "{}"), UpdateNetworkSettings.Default, "lite", true) { Owner = main, ShowActivated = false };
            update.Show();
            await Task.Delay(250);
            captures.Add(VisualCaptureService.Capture(update, Path.Combine(outputDirectory, "update-window.png")));
            update.Close();

            var dialog = AppDialog.CreateForVisualQa(main, "确认删除", "删除规则“长名称应用规则 · 演示与直播场景”？");
            dialog.Show();
            await Task.Delay(200);
            captures.Add(VisualCaptureService.Capture(dialog, Path.Combine(outputDirectory, "confirmation-dialog.png")));
            dialog.Close();

            var overlayRule = new DisplayRule
            {
                Enabled = true,
                Level = 2,
                KeyRules =
                [
                    new KeyRule { Key = "Ctrl + C", Enabled = true },
                    new KeyRule { Key = "Ctrl + V", Enabled = true }
                ]
            };
            App.Overlay?.Present("Ctrl + C", "视觉验收", overlayRule);
            App.Overlay?.Present("Ctrl + C", "视觉验收", overlayRule);
            App.Overlay?.Present("Ctrl + C", "视觉验收", overlayRule);
            App.Overlay?.Present("Ctrl + V", "视觉验收", overlayRule);
            await Task.Delay(350);
            if (App.Overlay is { IsVisible: true } overlay)
            {
                var previousMonitor = App.Settings.MonitorIndex;
                var previousPosition = App.Settings.OverlayPosition;
                var previousPalette = App.Settings.OverlayPalette;
                var previousRightBottomOffset = App.Settings.GetOverlayOffset("右下").Clone();
                try
                {
                    App.Settings.OverlayPalette = "dark-mint";
                    App.Settings.OverlayOffsets["右下"] = new OverlayOffset { X = -10, Y = 0 };
                    App.Settings.MonitorIndex = 0;
                    App.Settings.OverlayPosition = "右下";
                    overlay.RefreshStyle();
                    overlay.RefreshPosition();
                    captures.Add(VisualCaptureService.Capture(overlay, Path.Combine(outputDirectory, "overlay-screen1-right-bottom-offset.png")));
                    App.Settings.OverlayPalette = "light-rose";
                    overlay.RefreshStyle();
                    captures.Add(VisualCaptureService.Capture(overlay, Path.Combine(outputDirectory, "overlay-light-rose.png")));
                    if (Forms.Screen.AllScreens.Length > 1)
                    {
                        App.Settings.MonitorIndex = 1;
                        App.Settings.OverlayPosition = "右下";
                        overlay.RefreshPosition();
                        captures.Add(VisualCaptureService.Capture(overlay, Path.Combine(outputDirectory, "overlay-screen2-right-bottom-offset.png")));
                    }
                }
                finally
                {
                    App.Settings.MonitorIndex = previousMonitor;
                    App.Settings.OverlayPosition = previousPosition;
                    App.Settings.OverlayPalette = previousPalette;
                    App.Settings.OverlayOffsets["右下"] = previousRightBottomOffset;
                    overlay.RefreshStyle();
                }
            }

            var status = new
            {
                success = true,
                capturedAt = DateTimeOffset.Now,
                backdrop,
                fallback,
                screens = Forms.Screen.AllScreens.Select(screen => new
                {
                    screen.DeviceName,
                    screen.Primary,
                    Bounds = new { screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height },
                    WorkingArea = new { screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height }
                }),
                captures
            };
            await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            var status = new { success = false, capturedAt = DateTimeOffset.Now, error = ex.ToString(), captures };
            await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            App.ExitApplication();
        }
    }
}
