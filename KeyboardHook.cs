using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace GlyphEcho;

internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13; private const int WmKeyDown = 0x100; private const int WmSysKeyDown = 0x104;
    private nint _hook; private HookProc? _proc; public event EventHandler<KeyboardPressedEventArgs>? KeyPressed;
    public bool Start() { _proc = Callback; using var p = Process.GetCurrentProcess(); using var m = p.MainModule; _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(m?.ModuleName), 0); return _hook != 0; }
    private nint Callback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown))
        {
            var data = Marshal.PtrToStructure<Kbd>(lParam);
            var hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out var processId);
            var modifiers = Keyboard.Modifiers;
            KeyPressed?.Invoke(this, new KeyboardPressedEventArgs(
                KeyInterop.KeyFromVirtualKey((int)data.VkCode), modifiers, CaptureModifierSides(modifiers), hwnd, processId));
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }
    internal static string Format(Key key, ModifierKeys mods, bool allowSingle, bool detailed, ModifierSideState sides = ModifierSideState.None)
    {
        if (IsModifierKey(key)) return string.Empty;
        if (IsTextInputCombination(key, mods)) mods &= ~ModifierKeys.Shift;
        var parts = new List<string>();
        AddModifier(parts, mods.HasFlag(ModifierKeys.Control), detailed, sides.HasFlag(ModifierSideState.LeftCtrl), sides.HasFlag(ModifierSideState.RightCtrl), "Ctrl", "LeftCtrl", "RightCtrl");
        AddModifier(parts, mods.HasFlag(ModifierKeys.Shift), detailed, sides.HasFlag(ModifierSideState.LeftShift), sides.HasFlag(ModifierSideState.RightShift), "Shift", "LeftShift", "RightShift");
        AddModifier(parts, mods.HasFlag(ModifierKeys.Alt), detailed, sides.HasFlag(ModifierSideState.LeftAlt), sides.HasFlag(ModifierSideState.RightAlt), "Alt", "LeftAlt", "RightAlt");
        AddModifier(parts, mods.HasFlag(ModifierKeys.Windows), detailed, sides.HasFlag(ModifierSideState.LeftWin), sides.HasFlag(ModifierSideState.RightWin), "Win", "LWin", "RWin");
        if (parts.Count == 0 && !allowSingle) return string.Empty;
        parts.Add(FriendlyName(key));
        return string.Join(" + ", parts);
    }
    internal static string BuildCatalogKey(Key key, ModifierKeys mods) =>
        !IsModifierKey(key) && mods != ModifierKeys.None && !IsTextInputCombination(key, mods) ? Format(key, mods, true, false) : string.Empty;
    private static void AddModifier(List<string> parts, bool active, bool detailed, bool left, bool right, string simple, string leftName, string rightName)
    {
        if (!active) return;
        if (!detailed || !left && !right) { parts.Add(simple); return; }
        if (left) parts.Add(leftName);
        if (right) parts.Add(rightName);
    }
    private static string FriendlyName(Key key) { var raw = key.ToString(); return raw switch { "Return" => "Enter", "Escape" => "Esc", "Back" => "Backspace", "Tab" => "Tab", "Capital" => "Caps Lock", "Space" => "Space", "Prior" => "Page Up", "Next" => "Page Down", "PageUp" => "Page Up", "PageDown" => "Page Down", "Left" => "←", "Right" => "→", "Up" => "↑", "Down" => "↓", "Insert" => "Insert", "Delete" => "Delete", "Home" => "Home", "End" => "End", "NumLock" => "Num Lock", "Scroll" => "Scroll Lock", "Snapshot" => "Print Screen", "Apps" => "Menu", "F13" => "M1", "F14" => "M2", "F15" => "M3", "F16" => "M4", "OemQuestion" or "Oem2" => "/", "Oem5" => "\\", "Oem1" => ";", "Oem7" => "'", "OemComma" => ",", "OemMinus" => "-", "OemPeriod" => ".", "OemPlus" => "+", "Oem3" => "`", "Oem4" => "[", "Oem6" => "]", "Oem8" => "OEM 8", "Oem102" => "<", "D0" => "0", "D1" => "1", "D2" => "2", "D3" => "3", "D4" => "4", "D5" => "5", "D6" => "6", "D7" => "7", "D8" => "8", "D9" => "9", "NumPad0" => "Num 0", "NumPad1" => "Num 1", "NumPad2" => "Num 2", "NumPad3" => "Num 3", "NumPad4" => "Num 4", "NumPad5" => "Num 5", "NumPad6" => "Num 6", "NumPad7" => "Num 7", "NumPad8" => "Num 8", "NumPad9" => "Num 9", "Add" => "Num +", "Subtract" => "Num -", "Multiply" => "Num ×", "Divide" => "Num ÷", "Decimal" => "Num .", "Separator" => "Num ,", "System" => "系统键", "None" => "未知按键", _ => raw }; }
    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;
    private static bool IsTextInputCombination(Key key, ModifierKeys mods) => mods == ModifierKeys.Shift && (key is >= Key.A and <= Key.Z || key is >= Key.D0 and <= Key.D9 || key is Key.Space or Key.Oem1 or Key.Oem2 or Key.Oem3 or Key.Oem4 or Key.Oem5 or Key.Oem6 or Key.Oem7 or Key.Oem8 or Key.OemQuotes or Key.OemBackslash or Key.OemComma or Key.OemMinus or Key.OemPeriod or Key.OemPlus or Key.OemQuestion);
    internal static string NormalizeForRule(string value)
    {
        var normalized = value.Replace("LeftAlt", "Alt", StringComparison.OrdinalIgnoreCase).Replace("RightAlt", "Alt", StringComparison.OrdinalIgnoreCase).Replace("LeftCtrl", "Ctrl", StringComparison.OrdinalIgnoreCase).Replace("RightCtrl", "Ctrl", StringComparison.OrdinalIgnoreCase).Replace("LeftShift", "Shift", StringComparison.OrdinalIgnoreCase).Replace("RightShift", "Shift", StringComparison.OrdinalIgnoreCase).Replace("LWin", "Win", StringComparison.OrdinalIgnoreCase).Replace("RWin", "Win", StringComparison.OrdinalIgnoreCase).Replace("OemQuestion", "/", StringComparison.OrdinalIgnoreCase).Replace("Oem2", "/", StringComparison.OrdinalIgnoreCase).Replace("Oem5", "\\", StringComparison.OrdinalIgnoreCase).Replace("OemBackslash", "\\", StringComparison.OrdinalIgnoreCase).Replace("Oem1", ";", StringComparison.OrdinalIgnoreCase).Replace("Oem7", "'", StringComparison.OrdinalIgnoreCase).Replace("OemQuotes", "'", StringComparison.OrdinalIgnoreCase);
        return string.Join(" + ", normalized.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }
    internal static (string Name, string Path) GetForegroundProcessInfo() { var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out var pid); return GetProcessInfo(pid); }
    internal static (nint Window, uint ProcessId) GetForegroundIdentity() { var hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out var pid); return (hwnd, pid); }
    internal static (string Name, string Path) GetProcessInfo(uint processId) { try { using var process = Process.GetProcessById((int)processId); return (process.ProcessName, process.MainModule?.FileName ?? ""); } catch { return ("未知应用", ""); } }
    private static ModifierSideState CaptureModifierSides(ModifierKeys modifiers)
    {
        var result = ModifierSideState.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) { if (IsDown(0xA2)) result |= ModifierSideState.LeftCtrl; if (IsDown(0xA3)) result |= ModifierSideState.RightCtrl; }
        if (modifiers.HasFlag(ModifierKeys.Shift)) { if (IsDown(0xA0)) result |= ModifierSideState.LeftShift; if (IsDown(0xA1)) result |= ModifierSideState.RightShift; }
        if (modifiers.HasFlag(ModifierKeys.Alt)) { if (IsDown(0xA4)) result |= ModifierSideState.LeftAlt; if (IsDown(0xA5)) result |= ModifierSideState.RightAlt; }
        if (modifiers.HasFlag(ModifierKeys.Windows)) { if (IsDown(0x5B)) result |= ModifierSideState.LeftWin; if (IsDown(0x5C)) result |= ModifierSideState.RightWin; }
        return result;
    }
    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    public void Dispose() { if (_hook != 0) UnhookWindowsHookEx(_hook); _hook = 0; }
    private delegate nint HookProc(int code, nint wParam, nint lParam); [StructLayout(LayoutKind.Sequential)] private struct Kbd { public uint VkCode, ScanCode, Flags, Time; public nint Extra; }
    [DllImport("user32.dll")] static extern nint SetWindowsHookEx(int id, HookProc proc, nint mod, uint thread); [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(nint h); [DllImport("user32.dll")] static extern nint CallNextHookEx(nint h, int c, nint w, nint l); [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern nint GetModuleHandle(string? name); [DllImport("user32.dll")] static extern nint GetForegroundWindow(); [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid); [DllImport("user32.dll")] static extern short GetAsyncKeyState(int virtualKey);
}
internal sealed class KeyboardPressedEventArgs(Key key, ModifierKeys modifiers, ModifierSideState modifierSides, nint foregroundWindow, uint foregroundProcessId) : EventArgs
{
    internal Key Key { get; } = key;
    internal ModifierKeys Modifiers { get; } = modifiers;
    internal ModifierSideState ModifierSides { get; } = modifierSides;
    internal nint ForegroundWindow { get; } = foregroundWindow;
    internal uint ForegroundProcessId { get; } = foregroundProcessId;
}
[Flags]
internal enum ModifierSideState { None = 0, LeftCtrl = 1, RightCtrl = 2, LeftShift = 4, RightShift = 8, LeftAlt = 16, RightAlt = 32, LeftWin = 64, RightWin = 128 }
internal sealed class KeyPressedEventArgs : EventArgs
{
    public KeyPressedEventArgs(string display, string foregroundApp, string foregroundPath, string catalogKey) { Display = display; ForegroundApp = foregroundApp; ForegroundPath = foregroundPath; CatalogKey = catalogKey; }
    public string Display { get; }
    public string ForegroundApp { get; }
    public string ForegroundPath { get; }
    public string CatalogKey { get; }
}
