using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

if (args.Length != 4 || args[0] != "--apply-update")
{
    Console.Error.WriteLine("Usage: GlyphEcho.Updater.exe --apply-update <package.zip> <target-dir> <pid>");
    return 2;
}

var package = Path.GetFullPath(args[1]);
var target = Path.GetFullPath(args[2]);
if (!File.Exists(package) || !Directory.Exists(target) || !int.TryParse(args[3], out var pid)) return 3;
try
{
    try { using var process = Process.GetProcessById(pid); if (!process.WaitForExit(30000)) return 4; } catch (ArgumentException) { }
    var staging = Path.Combine(Path.GetTempPath(), "GlyphEcho-stage-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging);
    ZipFile.ExtractToDirectory(package, staging, overwriteFiles: true);
    var entry = Path.Combine(staging, "GlyphEcho.exe");
    if (!File.Exists(entry) || !IsSafeTree(staging)) return 5;
    var backup = target + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    Directory.Move(target, backup);
    try
    {
        Directory.Move(staging, target);
        Directory.Delete(backup, true);
    }
    catch
    {
        if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.Move(backup, target);
        throw;
    }
    File.Delete(package);
    Process.Start(new ProcessStartInfo(Path.Combine(target, "GlyphEcho.exe")) { WorkingDirectory = target, UseShellExecute = true });
    return 0;
}
catch (Exception ex)
{
    File.WriteAllText(Path.Combine(Path.GetTempPath(), "GlyphEcho-update-failed.log"), ex.ToString());
    return 6;
}

static bool IsSafeTree(string root)
{
    var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) return false;
    }
    return true;
}
