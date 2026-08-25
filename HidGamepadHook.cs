using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GlyphEcho;

// Generic Raw Input bridge. Device-specific HID reports are intentionally not
// assumed; buttons beyond the standard range are exposed as M1-M4.
internal sealed class HidGamepadHook : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const uint RimmTypeHid = 2;
    private readonly Window _window;
    private HwndSource? _source;
    private readonly Dictionary<nint, uint> _previous = [];
    public event EventHandler<KeyPressedEventArgs>? KeyPressed;

    public HidGamepadHook(Window window) { _window = window; _window.SourceInitialized += Attach; }

    private void Attach(object? sender, EventArgs e)
    {
        _source = (HwndSource)PresentationSource.FromVisual(_window)!;
        _source.AddHook(WndProc);
        RegisterRawInputDevices([new RawInputDevice { UsagePage = 0x01, Usage = 0x05, Flags = RidevInputSink, Target = _source.Handle }], 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WmInput) return nint.Zero;
        uint size = 0;
        if (GetRawInputData(lParam, RidInput, nint.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue || size is 0 or > 4096) return nint.Zero;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RidInput, buffer, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue) return nint.Zero;
            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimmTypeHid) return nint.Zero;
            var hid = Marshal.PtrToStructure<RawInputHid>(buffer + Marshal.SizeOf<RawInputHeader>());
            var length = Math.Min((int)(hid.SizeHid * hid.Count), 4096);
            if (length <= 0) return nint.Zero;
            var bytes = new byte[length];
            Marshal.Copy(buffer + Marshal.SizeOf<RawInputHeader>() + Marshal.SizeOf<RawInputHid>(), bytes, 0, length);
            uint state = 0;
            for (var bit = 16; bit < 20; bit++) if ((bytes[bit / 8] & (1 << (bit % 8))) != 0) state |= 1u << (bit - 16);
            var old = _previous.TryGetValue(header.Device, out var previous) ? previous : 0;
            for (var i = 0; i < 4; i++) if ((state & (1u << i)) != 0 && (old & (1u << i)) == 0)
                KeyPressed?.Invoke(this, new KeyPressedEventArgs($"M{i + 1}", "HID 手柄", string.Empty, $"M{i + 1}"));
            _previous[header.Device] = state;
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return nint.Zero;
    }

    public void Dispose() { if (_source is not null) _source.RemoveHook(WndProc); _window.SourceInitialized -= Attach; _previous.Clear(); }

    [StructLayout(LayoutKind.Sequential)] private struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInputHeader { public uint Type, Size; public nint Device, WParam; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInputHid { public uint SizeHid, Count; public byte Data; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
}
