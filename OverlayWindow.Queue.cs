using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MediaBrushes = System.Windows.Media.Brushes;

namespace GlyphEcho;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly OverlayQueue _queue = new(TimeSpan.FromMilliseconds(1300));
    private readonly List<OverlayPresentation> _pending = [];

    public OverlayWindow()
    {
        InitializeComponent();
        _refresh.Tick += (_, _) => RefreshQueue();
        Loaded += (_, _) => Position();
    }

    internal void RefreshPosition() => Position();
    internal void RefreshStyle() => RefreshQueue();

    internal void Present(string display, string app, DisplayRule rule)
    {
        var key = display.Split(" + ", StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? display;
        var normalizedDisplay = KeyboardHook.NormalizeForRule(display);
        if (!rule.Enabled || rule.HiddenKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return;
        var matchedRule = rule.KeyRules.FirstOrDefault(x =>
            KeyboardHook.NormalizeForRule(x.Key).Equals(normalizedDisplay, StringComparison.OrdinalIgnoreCase));
        if (matchedRule is null || !matchedRule.Enabled) return;

        _pending.Add(new OverlayPresentation(
            display,
            app,
            rule.Level >= 3 ? $"{ShortcutSource(display)} · {app}" : rule.Level == 2 ? app : string.Empty,
            rule.Level >= 3 ? (string.IsNullOrWhiteSpace(rule.Description) ? "快捷键" : rule.Description) : string.Empty,
            rule.Level));
        if (!_refresh.IsEnabled) _refresh.Start();
    }

    private void RefreshQueue()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var presentation in _pending) _queue.Add(presentation, now);
        _pending.Clear();
        var items = _queue.Snapshot(now);
        if (items.Count == 0)
        {
            Hide();
            _refresh.Stop();
            return;
        }

        Render(items);
        if (!IsVisible) Show();
        UpdateLayout();
        Position();
    }

    private void Render(IReadOnlyList<OverlayQueueSnapshot> items)
    {
        var compact = items.All(item => item.Presentation.Level == 1);
        var palette = OverlayPaletteCatalog.Resolve(App.Settings.OverlayPalette);
        Surface.Background = compact ? MediaBrushes.Transparent : new SolidColorBrush(palette.Surface);
        Surface.BorderBrush = new SolidColorBrush(palette.Border);
        Surface.BorderThickness = compact ? new Thickness(0) : new Thickness(1);
        Surface.Padding = compact ? new Thickness(0) : new Thickness(10, 9, 10, 9);
        Surface.CornerRadius = new CornerRadius(compact ? 0 : 8);
        QueueRows.Children.Clear();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = new StackPanel { Margin = new Thickness(0, index == 0 ? 0 : 8, 0, 0) };
            if (item.Presentation.Level >= 2)
            {
                row.Children.Add(new TextBlock
                {
                    Text = item.Presentation.Source,
                    Foreground = new SolidColorBrush(palette.SourceText),
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 480
                });
            }

            var keys = new WrapPanel { Margin = new Thickness(0, item.Presentation.Level >= 2 ? 4 : 0, 0, 0) };
            foreach (var part in item.Presentation.Display.Split(" + ", StringSplitOptions.RemoveEmptyEntries))
            {
                keys.Children.Add(CreateKey(part, palette));
            }
            if (item.Count > 1)
            {
                keys.Children.Add(new TextBlock
                {
                    Text = $"× {item.Count}",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(palette.Accent),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 2, 0)
                });
            }
            row.Children.Add(keys);

            if (item.Presentation.Level >= 3)
            {
                row.Children.Add(new TextBlock
                {
                    Text = item.Presentation.Action,
                    Foreground = new SolidColorBrush(palette.Accent),
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
            QueueRows.Children.Add(row);
        }
    }

    private static Border CreateKey(string text, OverlayPalette palette) => new()
    {
        Background = new SolidColorBrush(palette.KeySurface),
        BorderBrush = new SolidColorBrush(palette.KeyBorder),
        BorderThickness = palette.IsDark ? new Thickness(0) : new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 4, 8, 4),
        Margin = new Thickness(0, 0, 4, 0),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(palette.KeyText)
        }
    };

    private void Position()
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0) return;
        var screen = screens[Math.Clamp(App.Settings.MonitorIndex, 0, screens.Length - 1)];
        var offset = App.Settings.GetOverlayOffset(App.Settings.OverlayPosition);
        _ = NativeMethods.PositionOverlay(this, screen.WorkingArea, App.Settings.OverlayPosition, 18, offset.X, offset.Y);
    }

    private static string ShortcutSource(string display) => display is "Ctrl + C" or "Ctrl + V" or "Ctrl + X" or "Ctrl + Z" or "Ctrl + A" or "Ctrl + S"
        ? "Windows 通用"
        : "前台应用";
}
