using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace GlyphEcho;

internal static class NativeMethods
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);

    internal static BackdropResult ApplyBackdrop(Window window, System.Windows.Controls.Border surface, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
        {
            ApplyOpaqueFallback(window, surface);
            return new BackdropResult(false, null, null);
        }

        var rounded = 2;
        var nativeCorners = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            && DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref rounded, sizeof(int)) == 0;
        if (!enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            DisableBackdrop(hwnd);
            ApplyOpaqueFallback(window, surface, nativeCorners);
            return new BackdropResult(false, null, null);
        }

        if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var backdrop = 3; // DWMSBT_TRANSIENTWINDOW
        var backdropResult = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        var fullWindow = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        var frameResult = DwmExtendFrameIntoClientArea(hwnd, ref fullWindow);
        if (backdropResult != 0 || frameResult != 0)
        {
            DisableBackdrop(hwnd);
            ApplyOpaqueFallback(window, surface, nativeCorners);
            return new BackdropResult(false, backdropResult, frameResult);
        }

        var light = 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref light, sizeof(int));
        window.Background = MediaBrushes.Transparent;
        surface.Background = new SolidColorBrush(MediaColor.FromArgb(205, 248, 250, 250));
        surface.CornerRadius = new CornerRadius(0);
        surface.ClipToBounds = false;
        return new BackdropResult(true, backdropResult, frameResult);
    }

    internal static bool PositionOverlay(Window window, System.Drawing.Rectangle workingArea, string position, int gap, int offsetX, int offsetY)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0 || window.ActualWidth <= 0 || window.ActualHeight <= 0) return false;

        var center = new PointNative
        {
            X = workingArea.Left + workingArea.Width / 2,
            Y = workingArea.Top + workingArea.Height / 2
        };
        var scale = GetMonitorScale(center, window);
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * scale));
        var point = ResolveOverlayPosition(workingArea, position, width, height, gap, offsetX, offsetY);
        return SetWindowPos(hwnd, HwndTopmost, point.X, point.Y, width, height, SwpNoActivate | SwpShowWindow);
    }

    internal static System.Drawing.Point ResolveOverlayPosition(System.Drawing.Rectangle workingArea, string position, int width, int height, int gap, int offsetX, int offsetY)
    {
        var anchoredLeft = position is "左上" or "左下"
            ? workingArea.Left + gap
            : workingArea.Right - width - gap;
        var anchoredTop = position is "左上" or "右上"
            ? workingArea.Top + gap
            : workingArea.Bottom - height - gap;
        var maxLeft = Math.Max(workingArea.Left, workingArea.Right - width);
        var maxTop = Math.Max(workingArea.Top, workingArea.Bottom - height);
        return new System.Drawing.Point(
            Math.Clamp(anchoredLeft + offsetX, workingArea.Left, maxLeft),
            Math.Clamp(anchoredTop + offsetY, workingArea.Top, maxTop));
    }

    private static double GetMonitorScale(PointNative point, Visual visual)
    {
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (monitor != 0 && GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
        {
            return dpiX / 96d;
        }
        return VisualTreeHelper.GetDpi(visual).DpiScaleX;
    }

    private static void ApplyOpaqueFallback(Window window, System.Windows.Controls.Border surface, bool nativeCorners = false)
    {
        var fallback = new SolidColorBrush(MediaColor.FromRgb(244, 247, 248));
        window.Background = fallback;
        surface.Background = fallback;
        surface.CornerRadius = nativeCorners ? new CornerRadius(0) : new CornerRadius(8);
        surface.ClipToBounds = !nativeCorners;
    }

    private static void DisableBackdrop(nint hwnd)
    {
        var none = 1; // DWMSBT_NONE
        _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref none, sizeof(int));
        var reset = new Margins();
        _ = DwmExtendFrameIntoClientArea(hwnd, ref reset);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative { public int X, Y; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(PointNative point, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}

internal sealed record BackdropResult(bool Applied, int? BackdropHResult, int? FrameHResult);
