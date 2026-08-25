using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlyphEcho;

internal sealed record UpdateManifest(string Product, string Channel, string Version, string DownloadUrl, long Size, string Sha256, string Signature, string ReleaseNotesUrl);

internal static class UpdateService
{
    private const string ManifestBase = "https://github.com/Kratosmax/GlyphEcho/releases/latest/download/";
    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxqcQFFVcy1q/hhK7Mxcc
7Vt1mzAZv7ZVl6eLmI6Yy0SGAXkPUJtqLuu8xm9d83m/OisTUO8kKtqxpLXgnXru
VAipKyX0b71dfEUWvO9gd5hYRcRqQmFTJ2SA/Ig7yjV44Dn1ieh38S1DuoB9vj5J
A91FAtJeE61prCM+J44z4cx07p9IPnY5yfpdn4UnjOv3kwDZVCcpRALdtBWwwiMH
FcSIUI732pEGJC/dKxeMiXbnVEaQpnuhhFDnle9ODEoI9OzcliUMa9aVRBrNUKYv
r/TXvsiLTt4i71UeFbMEQTx3RFFqeB097qHOJbB+JwEdEYUzzfDqqSx7RgkYTzq5
HQIDAQAB
-----END PUBLIC KEY-----
""";

    internal static async Task<UpdateManifest?> CheckAsync(string channel, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GlyphEcho/0.2.0");
        var json = await client.GetStringAsync(ManifestBase + $"update-{channel}.json", cancellationToken);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest is null) throw new InvalidDataException("更新清单为空");
        if (!string.Equals(manifest.Product, "GlyphEcho", StringComparison.Ordinal) || !string.Equals(manifest.Channel, channel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新清单产品或通道不匹配");
        if (!Verify(manifest)) throw new CryptographicException("更新清单签名无效");
        return manifest;
    }

    internal static bool IsNewer(UpdateManifest manifest)
        => Version.TryParse(manifest.Version, out var remote) && Version.TryParse(typeof(UpdateService).Assembly.GetName().Version?.ToString(), out var local) && remote > local;

    internal static async Task ApplyAsync(UpdateManifest manifest, CancellationToken cancellationToken = default)
    {
        if (manifest.Size <= 0 || manifest.Size > 512L * 1024 * 1024) throw new InvalidDataException("更新包大小超出允许范围");
        var updaterSource = Path.Combine(AppContext.BaseDirectory, "GlyphEcho.Updater.exe");
        if (!File.Exists(updaterSource)) throw new FileNotFoundException("更新器未随当前安装包提供");
        var package = Path.Combine(Path.GetTempPath(), $"GlyphEcho-{manifest.Version}-{manifest.Channel}.zip");
        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        await using (var source = await client.GetStreamAsync(manifest.DownloadUrl, cancellationToken))
        await using (var target = new FileStream(package, FileMode.Create, FileAccess.Write, FileShare.None)) await source.CopyToAsync(target, cancellationToken);
        if (new FileInfo(package).Length != manifest.Size || !string.Equals(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(package, cancellationToken))), manifest.Sha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(package); throw new CryptographicException("更新包大小或哈希校验失败"); }
        var updater = Path.Combine(Path.GetTempPath(), $"GlyphEcho.Updater-{Guid.NewGuid():N}.exe");
        File.Copy(updaterSource, updater, true);
        var psi = new System.Diagnostics.ProcessStartInfo(updater, $"--apply-update \"{package}\" \"{AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)}\" {Environment.ProcessId}") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
        System.Diagnostics.Process.Start(psi);
        App.ExitApplication();
    }

    private static bool Verify(UpdateManifest manifest)
    {
        var payload = $"{manifest.Version}\n{manifest.DownloadUrl}\n{manifest.Sha256}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PublicKeyPem);
        return rsa.VerifyData(Encoding.UTF8.GetBytes(payload), Convert.FromBase64String(manifest.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
