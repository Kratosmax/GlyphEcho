using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace GlyphEcho;
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(1300) };
    public OverlayWindow() { InitializeComponent(); _hide.Tick += (_, _) => { _hide.Stop(); Hide(); }; Loaded += (_, _) => Position(); }
    internal void RefreshPosition() => Position();
    internal void Present(string display, string app, DisplayRule rule)
    {
        var key = display.Split(" + ", StringSplitOptions.RemoveEmptyEntries).Last(); var normalizedDisplay = KeyboardHook.NormalizeForRule(display); if (!rule.Enabled || rule.HiddenKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return; var matchedRule = rule.KeyRules.FirstOrDefault(x => KeyboardHook.NormalizeForRule(x.Key).Equals(normalizedDisplay, StringComparison.OrdinalIgnoreCase)); if (matchedRule is null || !matchedRule.Enabled) return;
        var compact = rule.Level == 1;
        Surface.Background = compact ? MediaBrushes.Transparent : new SolidColorBrush(MediaColor.FromArgb(242, 24, 37, 41)); Surface.BorderThickness = compact ? new Thickness(0) : new Thickness(1); Surface.Padding = compact ? new Thickness(0) : new Thickness(10, 8, 10, 8); Surface.CornerRadius = new CornerRadius(compact ? 0 : 9); Source.Visibility = compact ? Visibility.Collapsed : Visibility.Visible; Action.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Keys.Children.Clear(); foreach (var part in display.Split(" + ")) { Keys.Children.Add(new Border { Background = new SolidColorBrush(MediaColor.FromRgb(35, 58, 62)), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 4, 0), Child = new TextBlock { Text = part, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = MediaBrushes.White } }); }
        Source.Text = rule.Level >= 3 ? $"{ShortcutSource(display)} · {app}" : rule.Level == 2 ? app : string.Empty; Action.Text = rule.Level >= 3 ? (string.IsNullOrWhiteSpace(rule.Description) ? "快捷键" : rule.Description) : string.Empty; Height = compact ? 38 : rule.Level >= 3 ? 96 : 70; Width = compact ? 1 : 260; SizeToContent = compact ? SizeToContent.Width : SizeToContent.Manual; Show(); UpdateLayout(); Position(); _hide.Stop(); _hide.Start();
    }
    private void Position() { var screens = Forms.Screen.AllScreens; if (screens.Length == 0) return; var screen = screens[Math.Clamp(App.Settings.MonitorIndex, 0, screens.Length - 1)]; var area = screen.WorkingArea; var gap = 18; var pixelLeft = App.Settings.OverlayPosition switch { "左上" or "左下" => area.Left + gap, _ => area.Right - (int)Math.Ceiling(Width) - gap }; var pixelTop = App.Settings.OverlayPosition switch { "左上" or "右上" => area.Top + gap, _ => area.Bottom - (int)Math.Ceiling(Height) - gap }; var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice; var point = transform?.Transform(new System.Windows.Point(pixelLeft, pixelTop)) ?? new System.Windows.Point(pixelLeft, pixelTop); Left = point.X; Top = point.Y; }
    private static string ShortcutSource(string display) => display is "Ctrl + C" or "Ctrl + V" or "Ctrl + X" or "Ctrl + Z" or "Ctrl + A" or "Ctrl + S" ? "Windows 通用" : "前台应用（未读取绑定表）";
}
