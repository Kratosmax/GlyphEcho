using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace GlyphEcho;

internal sealed record UpdateInfo(UpdateManifest Manifest, Version Version, Uri DownloadUri, string RawManifest)
{
    internal long Size => Manifest.Size;
}
internal sealed record PreparedUpdate(string PackagePath, string ManifestPath, string LauncherPath);

internal static class UpdateService
{
    private const long MaximumExpandedSize = 1024L * 1024 * 1024;
    private const int MaximumEntries = 10000;
    private const int MaximumManifestSize = UpdateManifestValidator.MaximumManifestSize;
    private const string ManifestBase = "https://github.com/Kratosmax/GlyphEcho/releases/latest/download/";
    private static readonly SemaphoreSlim DownloadGate = new(1, 1);
    private static readonly HashSet<string> AllowedRedirectHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"
    };
    internal const string PublicKeyPem = UpdateManifestValidator.PublicKeyPem;

    internal static Version CurrentVersion
    {
        get
        {
            var value = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(value.Major, value.Minor, Math.Max(0, value.Build));
        }
    }

    internal static bool CanInstallInPlace => File.Exists(Path.Combine(AppContext.BaseDirectory, "GlyphEcho.Updater.exe"));

    internal static bool IsNewer(UpdateInfo update) => update.Version > CurrentVersion;

    internal static async Task ApplyAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        var prepared = await DownloadAsync(update, new Progress<int>(), App.Settings.UpdateNetwork, cancellationToken).ConfigureAwait(false);
        LaunchUpdater(prepared, App.Settings.UpdateChannel);
        App.ExitApplication();
    }

    internal static async Task<UpdateInfo?> CheckAsync(string channel, UpdateNetworkSettings? networkSettings = null, CancellationToken cancellationToken = default)
    {
        var settings = (networkSettings ?? UpdateNetworkSettings.Default).Normalize();
        var original = new Uri(ManifestBase + $"update-{channel}.json");
        using var client = CreateClient(settings);
        Exception? lastError = null;
        string? lastRoute = null;
        foreach (var route in UpdateRouteBuilder.Build(original, settings))
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                using var response = await client.GetAsync(route.RequestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                EnsureAllowedResponse(response, route);
                if (response.Content.Headers.ContentLength is > MaximumManifestSize) throw new InvalidDataException("更新清单超过允许大小。");
                await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var target = new MemoryStream();
                await CopyBoundedAsync(source, target, MaximumManifestSize, null, timeout.Token).ConfigureAwait(false);
                var json = Encoding.UTF8.GetString(target.ToArray());
                var info = ParseAndVerify(json, channel);
                return info.Version > CurrentVersion ? info : null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or CryptographicException)
            {
                lastError = ex;
                lastRoute = route.DisplayName;
            }
        }
        throw RoutesFailed(lastRoute, lastError);
    }

    internal static async Task<PreparedUpdate> DownloadAsync(UpdateInfo update, IProgress<int> progress, UpdateNetworkSettings? networkSettings = null, CancellationToken cancellationToken = default)
    {
        await DownloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!CanInstallInPlace) throw new InvalidOperationException("当前运行目录不是完整发布包，不能执行就地更新。");
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphEcho", "updates", update.Version.ToString(3));
            Directory.CreateDirectory(root);
            var package = Path.Combine(root, "package.zip");
            var temporary = package + ".download";
            TryDelete(temporary);
            var settings = (networkSettings ?? UpdateNetworkSettings.Default).Normalize();
            using var client = CreateClient(settings);
            Exception? lastError = null;
            string? lastRoute = null;
            var downloaded = false;
            foreach (var route in UpdateRouteBuilder.Build(update.DownloadUri, settings))
            {
                try
                {
                    TryDelete(temporary);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromMinutes(10));
                    using var response = await client.GetAsync(route.RequestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    EnsureAllowedResponse(response, route);
                    if (response.Content.Headers.ContentLength is { } length && length != update.Manifest.Size) throw new InvalidDataException("服务器返回的更新包大小与签名清单不一致。");
                    await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
                    await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        await CopyBoundedAsync(source, target, update.Manifest.Size, progress, timeout.Token).ConfigureAwait(false);
                    VerifyPackageFile(temporary, update.Manifest);
                    VerifyPackageStructure(temporary, update.Manifest.Channel);
                    File.Move(temporary, package, true);
                    downloaded = true;
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { TryDelete(temporary); throw; }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or CryptographicException)
                {
                    lastError = ex;
                    lastRoute = route.DisplayName;
                    TryDelete(temporary);
                }
            }
            if (!downloaded) throw RoutesFailed(lastRoute, lastError);

            var manifestPath = Path.Combine(root, "update.json");
            await File.WriteAllTextAsync(manifestPath, update.RawManifest, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            var launcherRoot = Path.Combine(root, "launcher");
            Directory.CreateDirectory(launcherRoot);
            var launcher = Path.Combine(launcherRoot, "GlyphEcho.Updater.exe");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "GlyphEcho.Updater.exe"), launcher, true);
            progress.Report(100);
            return new PreparedUpdate(package, manifestPath, launcher);
        }
        finally { DownloadGate.Release(); }
    }

    internal static void LaunchUpdater(PreparedUpdate prepared, string channel)
    {
        var start = new ProcessStartInfo(prepared.LauncherPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(prepared.LauncherPath)! };
        using var current = Process.GetCurrentProcess();
        foreach (var value in new[] { "--package", prepared.PackagePath, "--manifest", prepared.ManifestPath, "--target", AppContext.BaseDirectory, "--pid", Environment.ProcessId.ToString(), "--process-start-ticks", current.StartTime.ToUniversalTime().Ticks.ToString(), "--channel", channel }) start.ArgumentList.Add(value);
        _ = Process.Start(start) ?? throw new InvalidOperationException("无法启动 GlyphEcho 更新器。");
    }

    internal static UpdateInfo ParseAndVerify(string json, string expectedChannel) =>
        ParseAndVerify(json, expectedChannel, PublicKeyPem);

    internal static UpdateInfo ParseAndVerify(string json, string expectedChannel, string publicKeyPem)
    {
        var verified = UpdateManifestValidator.ParseAndVerify(json, expectedChannel, publicKeyPem);
        return new UpdateInfo(verified.Manifest, verified.Version, verified.DownloadUri, json);
    }

    internal static string LegacyPayload(UpdateManifest manifest) => UpdateManifestValidator.LegacyPayload(manifest);
    internal static string V2Payload(UpdateManifest manifest) => UpdateManifestValidator.V2Payload(manifest);

    private static HttpClient CreateClient(UpdateNetworkSettings settings)
    {
        var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, ConnectTimeout = TimeSpan.FromSeconds(10), PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        if (settings.HttpProxy is not null) { handler.Proxy = new WebProxy(settings.HttpProxy); handler.UseProxy = true; }
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GlyphEcho", CurrentVersion.ToString(3)));
        return client;
    }

    private static void EnsureAllowedResponse(HttpResponseMessage response, UpdateRequestRoute route)
    {
        var uri = response.RequestMessage?.RequestUri;
        var allowed = uri is not null && (route.IsDirect
            ? uri.Scheme == Uri.UriSchemeHttps && AllowedRedirectHosts.Contains(uri.Host)
            : (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.Host.Equals(route.RequestUri.Host, StringComparison.OrdinalIgnoreCase));
        if (!allowed) throw new InvalidDataException("更新请求被重定向到不受信任的地址。");
    }

    private static async Task CopyBoundedAsync(Stream source, Stream target, long maximum, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > maximum) throw new InvalidDataException("下载内容超过允许大小。");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (progress is not null && maximum > 0) progress.Report(Math.Min(99, (int)(total * 100 / maximum)));
        }
        if (progress is not null && total != maximum) throw new InvalidDataException("下载内容大小与签名清单不一致。");
    }

    private static void VerifyPackageFile(string path, UpdateManifest manifest) => UpdateManifestValidator.VerifyPackageFile(path, manifest);

    internal static void VerifyPackageStructure(string path, string expectedChannel)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries) throw new InvalidDataException("更新包条目数量无效。");
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GlyphEcho-package-validation")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long expanded = 0;
        var hasApplication = false;
        var hasUpdater = false;
        ZipArchiveEntry? channelEntry = null;
        foreach (var entry in archive.Entries)
        {
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("更新包包含不允许的符号链接。");
            expanded += entry.Length;
            if (expanded > MaximumExpandedSize) throw new InvalidDataException("更新包展开大小超出限制。");
            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包包含越界路径。");
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.Equals("GlyphEcho.exe", StringComparison.OrdinalIgnoreCase)) hasApplication = true;
            if (normalized.Equals("GlyphEcho.Updater.exe", StringComparison.OrdinalIgnoreCase)) hasUpdater = true;
            if (normalized.Equals(".glyph-echo-channel", StringComparison.OrdinalIgnoreCase)) channelEntry = entry;
        }
        if (!hasApplication || !hasUpdater || channelEntry is null) throw new InvalidDataException("更新包缺少应用入口、更新器或通道标记。");
        using var reader = new StreamReader(channelEntry.Open(), Encoding.UTF8, true, 128, false);
        var actualChannel = reader.ReadToEnd().Trim();
        if (!actualChannel.Equals(expectedChannel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包通道与签名清单不匹配。");
    }

    private static HttpRequestException RoutesFailed(string? route, Exception? error) => new($"所有更新线路均失败。最后线路：{route ?? "无可用线路"}。{error?.Message ?? "请检查网络设置。"}", error, (error as HttpRequestException)?.StatusCode);
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
