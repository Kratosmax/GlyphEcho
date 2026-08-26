using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using GlyphEcho;

const long MaximumExpandedSize = 1024L * 1024 * 1024;
const int MaximumEntries = 10000;

if (args is ["--self-test"]) return RunSelfTest();

string? recoveryTarget = null;
int? originalProcessId = null;
long? originalProcessStartTicks = null;

try
{
    var options = ParseArguments(args);
    var package = Path.GetFullPath(Required(options, "package"));
    var manifestPath = Path.GetFullPath(Required(options, "manifest"));
    var target = Path.GetFullPath(Required(options, "target")).TrimEnd(Path.DirectorySeparatorChar);
    recoveryTarget = target;
    var channel = Required(options, "channel");
    if (!int.TryParse(Required(options, "pid"), out var pid)) return 2;
    originalProcessId = pid;
    var expectedStartTicks = long.TryParse(Required(options, "process-start-ticks"), out var ticks) ? ticks : throw new ArgumentException("原进程启动时间无效。");
    originalProcessStartTicks = expectedStartTicks;
    ValidateInputs(package, manifestPath, target);
    var manifest = ReadAndVerifyManifest(manifestPath, channel);
    VerifyPackage(package, manifest);
    try
    {
        using var process = Process.GetProcessById(pid);
        if (process.StartTime.ToUniversalTime().Ticks == expectedStartTicks && !process.WaitForExit(30000)) throw new TimeoutException("等待 GlyphEcho 退出超时。");
    }
    catch (ArgumentException) { }

    var transactionId = Guid.NewGuid().ToString("N");
    var (stage, backup) = TransactionPaths(target, transactionId);
    Directory.CreateDirectory(stage);
    try
    {
        ExtractValidated(package, stage);
        ValidateStagedChannel(stage, channel);
        ValidateStagedVersion(stage, manifest.Version);
        Directory.Move(target, backup);
        try
        {
            Directory.Move(stage, target);
        }
        catch
        {
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(backup, target);
            throw;
        }
        try { Directory.Delete(backup, true); } catch { }
    }
    finally
    {
        if (Directory.Exists(stage)) try { Directory.Delete(stage, true); } catch { }
    }

    TryDelete(package);
    TryDelete(manifestPath);
    var start = new ProcessStartInfo(Path.Combine(target, "GlyphEcho.exe")) { WorkingDirectory = target, UseShellExecute = true };
    start.ArgumentList.Add("--updated-from");
    start.ArgumentList.Add(manifest.Version);
    _ = Process.Start(start);
    return 0;
}
catch (Exception ex)
{
    try
    {
        var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho", "logs");
        Directory.CreateDirectory(logRoot);
        File.WriteAllText(Path.Combine(logRoot, "update-failed.log"), ex.ToString());
    }
    catch { }
    TryRestart(recoveryTarget, originalProcessId, originalProcessStartTicks);
    return 4;
}
static void TryRestart(string? target, int? originalProcessId, long? originalProcessStartTicks)
{
    if (string.IsNullOrWhiteSpace(target)) return;
    if (originalProcessId is { } pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (originalProcessStartTicks == process.StartTime.ToUniversalTime().Ticks && !process.WaitForExit(30000)) return;
        }
        catch (ArgumentException) { }
    }
    var executable = Path.Combine(target, "GlyphEcho.exe");
    if (!File.Exists(executable)) return;
    try
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = target, UseShellExecute = true };
        start.ArgumentList.Add("--update-failed");
        _ = Process.Start(start);
    }
    catch { }
}

static void TryDelete(string path) { try { File.Delete(path); } catch { } }

static (string Stage, string Backup) TransactionPaths(string target, string transactionId)
{
    var fullTarget = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
    var parent = Path.GetDirectoryName(fullTarget) ?? throw new InvalidOperationException("更新目标目录缺少父目录。");
    var name = Path.GetFileName(fullTarget);
    return (
        Path.Combine(parent, $"{name}.stage-{transactionId}"),
        Path.Combine(parent, $"{name}.backup-{transactionId}"));
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index + 1 < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("更新器参数格式无效。");
        result[values[index][2..]] = values[index + 1];
    }
    return result;
}

static string Required(IReadOnlyDictionary<string, string> values, string name) =>
    values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"缺少参数：{name}");

static void ValidateInputs(string package, string manifest, string target)
{
    if (!File.Exists(package) || !File.Exists(manifest) || !Directory.Exists(target)) throw new FileNotFoundException("更新输入文件或目标目录不存在。");
    if (!File.Exists(Path.Combine(target, "GlyphEcho.exe"))) throw new InvalidOperationException("目标目录不是 GlyphEcho 安装目录。");
    var updateRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho", "updates")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!package.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase) || !manifest.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新文件不在 GlyphEcho 受控缓存目录中。");
}

static UpdateManifest ReadAndVerifyManifest(string path, string expectedChannel)
{
    var json = File.ReadAllText(path, Encoding.UTF8);
    return UpdateManifestValidator.ParseAndVerify(json, expectedChannel).Manifest;
}

static void VerifyPackage(string path, UpdateManifest manifest) => UpdateManifestValidator.VerifyPackageFile(path, manifest);

static void ExtractValidated(string package, string stage)
{
    using var archive = ZipFile.OpenRead(package);
    if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries) throw new InvalidDataException("更新包条目数量无效。");
    var root = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    long expanded = 0;
    foreach (var entry in archive.Entries)
    {
        if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("更新包包含不允许的符号链接。");
        expanded += entry.Length;
        if (expanded > MaximumExpandedSize) throw new InvalidDataException("更新包展开大小超出限制。");
        var destination = Path.GetFullPath(Path.Combine(stage, entry.FullName));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包包含越界路径。");
        if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(destination); continue; }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        entry.ExtractToFile(destination, true);
    }
}

static void ValidateStagedVersion(string stage, string expectedVersion)
{
    var executable = Path.Combine(stage, "GlyphEcho.exe");
    if (!File.Exists(executable)) throw new InvalidDataException("更新包缺少 GlyphEcho.exe。");
    var actual = FileVersionInfo.GetVersionInfo(executable).ProductVersion?.Split('+')[0];
    if (!string.Equals(actual, expectedVersion, StringComparison.Ordinal)) throw new InvalidDataException($"更新包版本不匹配：{actual ?? "未知"}。");
}

static void ValidateStagedChannel(string stage, string expectedChannel)
{
    var marker = Path.Combine(stage, ".glyph-echo-channel");
    if (!File.Exists(marker)) throw new InvalidDataException("更新包缺少通道标记。");
    var actual = File.ReadAllText(marker, Encoding.UTF8).Trim();
    if (!actual.Equals(expectedChannel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包通道不匹配。");
}

static int RunSelfTest()
{
    var root = Path.Combine(Path.GetTempPath(), "GlyphEcho-updater-self-test-" + Guid.NewGuid().ToString("N"));
    var safeZip = Path.Combine(root, "safe.zip");
    var unsafeZip = Path.Combine(root, "unsafe.zip");
    var stage = Path.Combine(root, "stage");
    Directory.CreateDirectory(root);
    try
    {
        using (var archive = ZipFile.Open(safeZip, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("GlyphEcho.exe").Open(), Encoding.UTF8);
            writer.Write("test");
            writer.Dispose();
            using var channelWriter = new StreamWriter(archive.CreateEntry(".glyph-echo-channel").Open(), Encoding.UTF8);
            channelWriter.Write("lite");
        }
        ExtractValidated(safeZip, stage);
        if (!File.Exists(Path.Combine(stage, "GlyphEcho.exe"))) throw new InvalidOperationException("安全 ZIP 未正确展开。");
        ValidateStagedChannel(stage, "lite");
        try
        {
            ValidateStagedChannel(stage, "full");
            throw new InvalidOperationException("跨通道包未被拒绝。");
        }
        catch (InvalidDataException) { }
        Directory.Delete(stage, true);

        var transactionTarget = Path.Combine(root, "portable-drive", "GlyphEcho");
        var (transactionStage, transactionBackup) = TransactionPaths(transactionTarget, "selftest");
        if (!string.Equals(Path.GetDirectoryName(transactionTarget), Path.GetDirectoryName(transactionStage), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetPathRoot(transactionTarget), Path.GetPathRoot(transactionStage), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(transactionTarget), Path.GetDirectoryName(transactionBackup), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("更新事务目录未与目标目录保持同卷同父级。");
        Directory.CreateDirectory(transactionStage);
        File.WriteAllText(Path.Combine(transactionStage, "marker.txt"), "same-volume");
        Directory.Move(transactionStage, transactionTarget);
        if (!File.Exists(Path.Combine(transactionTarget, "marker.txt"))) throw new InvalidOperationException("同卷 stage 未能原子移动到目标目录。");
        Directory.Delete(transactionTarget, true);
        Console.WriteLine("PASS 更新事务目录同卷替换");

        using (var archive = ZipFile.Open(unsafeZip, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open(), Encoding.UTF8);
            writer.Write("blocked");
        }
        try
        {
            ExtractValidated(unsafeZip, stage);
            throw new InvalidOperationException("越界 ZIP 未被拒绝。");
        }
        catch (InvalidDataException) { }
        Console.WriteLine("PASS 更新包路径穿越保护");
        return 0;
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
