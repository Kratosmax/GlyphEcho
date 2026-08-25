using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlyphEcho;

internal static class VisualCaptureService
{
    internal static VisualCaptureResult Capture(Window window, string path)
    {
        window.UpdateLayout();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != 0 && GetWindowRect(hwnd, out var rect))
        {
            var width = Math.Max(1, rect.Right - rect.Left);
            var height = Math.Max(1, rect.Bottom - rect.Top);
            using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            bool printed;
            try { printed = PrintWindow(hwnd, hdc, 2); }
            finally { graphics.ReleaseHdc(hdc); }
            if (printed)
            {
                bitmap.Save(path, ImageFormat.Png);
                return Analyze(bitmap, path, "PrintWindow", rect.Left, rect.Top);
            }
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
        var rendered = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        rendered.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using (var stream = File.Create(path)) encoder.Save(stream);
        using var fallback = new Bitmap(path);
        return Analyze(fallback, path, "RenderTargetBitmap", null, null);
    }

    private static VisualCaptureResult Analyze(Bitmap bitmap, string path, string method, int? left, int? top)
    {
        var colors = new HashSet<int>();
        var stepX = Math.Max(1, bitmap.Width / 100);
        var stepY = Math.Max(1, bitmap.Height / 100);
        for (var y = 0; y < bitmap.Height; y += stepY)
            for (var x = 0; x < bitmap.Width; x += stepX)
                colors.Add(bitmap.GetPixel(x, y).ToArgb());
        return new VisualCaptureResult(path, bitmap.Width, bitmap.Height, colors.Count, method, left, top);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out WindowRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }
}

internal sealed record VisualCaptureResult(string Path, int Width, int Height, int SampledColors, string Method, int? Left, int? Top);
