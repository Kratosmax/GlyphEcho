using System.Runtime.InteropServices;
using ThreadingTimer = System.Threading.Timer;

namespace GlyphEcho;

internal sealed class GamepadHook : IDisposable
{
    private const uint ErrorSuccess = 0;
    private const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
    private const ushort Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
    private readonly ThreadingTimer _timer;
    private readonly uint[] _previous = new uint[4];
    private bool _available = true;
    public event EventHandler<KeyPressedEventArgs>? KeyPressed;

    public GamepadHook() { _timer = new ThreadingTimer(Poll, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50)); }

    private void Poll(object? state)
    {
        if (!_available) return;
        for (uint index = 0; index < 4; index++)
        {
            try
            {
                var result = XInputGetState(index, out var current);
                if (result != ErrorSuccess) { _previous[index] = 0; continue; }
                var pressed = (uint)current.Gamepad.Buttons;
                var old = _previous[index];
                var rising = (ushort)(pressed & ~old);
                foreach (var (mask, name) in ButtonNames) if ((rising & mask) != 0) Raise(index, name);
                if (current.Gamepad.LeftTrigger >= 128 && (old & 0x10000) == 0) Raise(index, "LT");
                if (current.Gamepad.RightTrigger >= 128 && (old & 0x20000) == 0) Raise(index, "RT");
                _previous[index] = pressed | (current.Gamepad.LeftTrigger >= 128 ? 0x10000u : 0) | (current.Gamepad.RightTrigger >= 128 ? 0x20000u : 0);
            }
            catch (DllNotFoundException) { _available = false; return; }
            catch (EntryPointNotFoundException) { _available = false; return; }
        }
    }

    private void Raise(uint index, string name) => KeyPressed?.Invoke(this, new KeyPressedEventArgs($"手柄 {index + 1} · {name}", $"手柄 {index + 1}", string.Empty, $"手柄 {index + 1} · {name}"));
    private static readonly (ushort Mask, string Name)[] ButtonNames = [(DPadUp, "上"), (DPadDown, "下"), (DPadLeft, "左"), (DPadRight, "右"), (Start, "Start"), (Back, "Back"), (LeftThumb, "LS"), (RightThumb, "RS"), (LeftShoulder, "LB"), (RightShoulder, "RB"), (A, "A"), (B, "B"), (X, "X"), (Y, "Y")];
    public void Dispose() => _timer.Dispose();

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
    [StructLayout(LayoutKind.Sequential)] private struct XInputState { public uint PacketNumber; public XInputGamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)] private struct XInputGamepad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short ThumbLX, ThumbLY, ThumbRX, ThumbRY; }
}
