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
    private readonly StickDirection[] _leftStick = new StickDirection[4];
    private readonly StickDirection[] _rightStick = new StickDirection[4];
    private readonly long[,] _lastStickEvent = new long[4, 2];
    private readonly long[] _nextDisconnectedPoll = new long[4];
    private bool _available = true;
    private int _polling;
    private int _disposed;
    public event EventHandler<KeyPressedEventArgs>? KeyPressed;

    public GamepadHook() { _timer = new ThreadingTimer(Poll, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
    internal void BeginPolling() => _timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(50));

    private void Poll(object? state)
    {
        if (!_available || Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            for (uint index = 0; index < 4; index++)
            {
                try
                {
                    var now = Environment.TickCount64;
                    if (now < _nextDisconnectedPoll[index]) continue;
                    var result = XInputGetState(index, out var current);
                    if (result != ErrorSuccess) { _previous[index] = 0; _nextDisconnectedPoll[index] = now + 1000; continue; }
                    _nextDisconnectedPoll[index] = 0;
                    var pressed = (uint)current.Gamepad.Buttons;
                    var old = _previous[index];
                    var rising = (ushort)(pressed & ~old);
                    foreach (var (mask, name) in ButtonNames) if ((rising & mask) != 0) Raise(index, name);
                    if (current.Gamepad.LeftTrigger >= 128 && (old & 0x10000) == 0) Raise(index, "LT");
                    if (current.Gamepad.RightTrigger >= 128 && (old & 0x20000) == 0) Raise(index, "RT");
                    UpdateStick(index, 0, current.Gamepad.ThumbLX, current.Gamepad.ThumbLY, _leftStick, "LS");
                    UpdateStick(index, 1, current.Gamepad.ThumbRX, current.Gamepad.ThumbRY, _rightStick, "RS");
                    _previous[index] = pressed | (current.Gamepad.LeftTrigger >= 128 ? 0x10000u : 0) | (current.Gamepad.RightTrigger >= 128 ? 0x20000u : 0);
                }
                catch (DllNotFoundException) { _available = false; return; }
                catch (EntryPointNotFoundException) { _available = false; return; }
            }
        }
        finally { Volatile.Write(ref _polling, 0); }
    }

    private void UpdateStick(uint index, int stickIndex, short x, short y, StickDirection[] states, string name)
    {
        var previous = states[index];
        var current = ResolveDirection(x, y, previous);
        states[index] = current;
        if (current == StickDirection.None || current == previous) return;
        var now = Environment.TickCount64;
        if (now - _lastStickEvent[index, stickIndex] < 200) return;
        _lastStickEvent[index, stickIndex] = now;
        Raise(index, $"{name} {DirectionNames[current]}");
    }

    internal static StickDirection ResolveDirection(short x, short y, StickDirection previous)
    {
        const int enter = 16000;
        const int exit = 9000;
        var magnitude = Math.Sqrt((double)x * x + (double)y * y);
        if (magnitude < (previous == StickDirection.None ? enter : exit)) return StickDirection.None;
        var angle = Math.Atan2(y, x) * 180d / Math.PI;
        if (angle < 0) angle += 360;
        return ((int)Math.Round(angle / 45d, MidpointRounding.AwayFromZero) % 8) switch
        {
            0 => StickDirection.Right,
            1 => StickDirection.UpRight,
            2 => StickDirection.Up,
            3 => StickDirection.UpLeft,
            4 => StickDirection.Left,
            5 => StickDirection.DownLeft,
            6 => StickDirection.Down,
            _ => StickDirection.DownRight
        };
    }

    internal static bool TryReadSnapshot(uint index, out GamepadSnapshot snapshot)
    {
        if (index >= 4 || XInputGetState(index, out var state) != ErrorSuccess)
        {
            snapshot = default;
            return false;
        }
        snapshot = new GamepadSnapshot(state.Gamepad.Buttons, state.Gamepad.LeftTrigger, state.Gamepad.RightTrigger,
            state.Gamepad.ThumbLX, state.Gamepad.ThumbLY, state.Gamepad.ThumbRX, state.Gamepad.ThumbRY);
        return true;
    }

    private void Raise(uint index, string name) { if (Volatile.Read(ref _disposed) != 0) return; KeyPressed?.Invoke(this, new KeyPressedEventArgs($"手柄 {index + 1} · {name}", $"手柄 {index + 1}", string.Empty, $"手柄 {index + 1} · {name}")); }
    private static readonly (ushort Mask, string Name)[] ButtonNames = [(DPadUp, "上"), (DPadDown, "下"), (DPadLeft, "左"), (DPadRight, "右"), (Start, "Start"), (Back, "Back"), (LeftThumb, "LS"), (RightThumb, "RS"), (LeftShoulder, "LB"), (RightShoulder, "RB"), (A, "A"), (B, "B"), (X, "X"), (Y, "Y")];
    private static readonly Dictionary<StickDirection, string> DirectionNames = new()
    {
        [StickDirection.Up] = "↑", [StickDirection.UpRight] = "↗", [StickDirection.Right] = "→",
        [StickDirection.DownRight] = "↘", [StickDirection.Down] = "↓", [StickDirection.DownLeft] = "↙",
        [StickDirection.Left] = "←", [StickDirection.UpLeft] = "↖"
    };
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; _timer.Change(Timeout.Infinite, Timeout.Infinite); using var stopped = new ManualResetEvent(false); if (_timer.Dispose(stopped)) stopped.WaitOne(1000); }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
    [StructLayout(LayoutKind.Sequential)] private struct XInputState { public uint PacketNumber; public XInputGamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)] private struct XInputGamepad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short ThumbLX, ThumbLY, ThumbRX, ThumbRY; }
}

internal enum StickDirection { None, Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }
internal readonly record struct GamepadSnapshot(ushort Buttons, byte LeftTrigger, byte RightTrigger, short LeftX, short LeftY, short RightX, short RightY);
