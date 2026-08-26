using Microsoft.Win32;
using System.IO;

namespace GlyphEcho;

internal static class StartupRegistration
{
    internal const string ValueName = "GlyphEcho";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    internal static string BuildCommand(string executablePath) => $"\"{Path.GetFullPath(executablePath)}\" --background";

    internal static bool TryApply(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (!enabled)
            {
                using var existingKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
                if (existingKey?.GetValue(ValueName) is not null) throw new InvalidOperationException("注册表自启项未能删除。");
                return true;
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath)) throw new InvalidOperationException("无法确定 GlyphEcho 的可执行文件路径。");
            var command = BuildCommand(executablePath);
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true) ?? throw new InvalidOperationException("无法打开当前用户的开机启动项。");
            if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.Ordinal))
                key.SetValue(ValueName, command, RegistryValueKind.String);
            if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.Ordinal))
                throw new InvalidOperationException("注册表自启项写入后校验失败。");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
