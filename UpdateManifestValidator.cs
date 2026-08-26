using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlyphEcho;

internal sealed class UpdateManifest
{
    [JsonPropertyName("product")] public string Product { get; init; } = string.Empty;
    [JsonPropertyName("channel")] public string Channel { get; init; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; init; } = string.Empty;
    [JsonPropertyName("size")] public long Size { get; init; }
    [JsonPropertyName("sha256")] public string Sha256 { get; init; } = string.Empty;
    [JsonPropertyName("signature")] public string Signature { get; init; } = string.Empty;
    [JsonPropertyName("signatureV2")] public string SignatureV2 { get; init; } = string.Empty;
    [JsonPropertyName("releaseNotes")] public string ReleaseNotes { get; init; } = string.Empty;
    [JsonPropertyName("releaseNotesUrl")] public string ReleaseNotesUrl { get; init; } = string.Empty;
}

internal static class UpdateManifestValidator
{
    internal const long MaximumPackageSize = 512L * 1024 * 1024;
    internal const int MaximumManifestSize = 64 * 1024;
    internal const string PublicKeyPem = """
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

    internal static (UpdateManifest Manifest, Version Version, Uri DownloadUri) ParseAndVerify(string json, string expectedChannel, string? publicKeyPem = null)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumManifestSize) throw new InvalidDataException("更新清单超过允许大小。");
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("更新清单为空。");
        if (!string.Equals(manifest.Product, "GlyphEcho", StringComparison.Ordinal) || !string.Equals(manifest.Channel, expectedChannel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新清单产品或通道不匹配。");
        if (!Version.TryParse(manifest.Version, out var version) || version.Build < 0 || version.Revision >= 0) throw new InvalidDataException("更新版本必须是三段数字版本。");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.AbsolutePath.StartsWith("/Kratosmax/GlyphEcho/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("更新下载地址不在允许的 GitHub Release 范围内。");
        if (manifest.Size <= 0 || manifest.Size > MaximumPackageSize) throw new InvalidDataException("更新包大小超出允许范围。");
        if (manifest.Sha256?.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("更新包 SHA-256 格式无效。");
        if (string.IsNullOrWhiteSpace(manifest.SignatureV2) && string.IsNullOrWhiteSpace(manifest.Signature)) throw new InvalidDataException("更新清单缺少签名。");
        if (Encoding.UTF8.GetByteCount(manifest.ReleaseNotes ?? string.Empty) > 16 * 1024) throw new InvalidDataException("更新说明超过允许大小。");
        var signatureText = string.IsNullOrWhiteSpace(manifest.SignatureV2) ? manifest.Signature : manifest.SignatureV2;
        byte[] signature;
        try { signature = Convert.FromBase64String(signatureText); }
        catch (FormatException ex) { throw new InvalidDataException("更新清单签名格式无效。", ex); }
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem ?? PublicKeyPem);
        var payload = string.IsNullOrWhiteSpace(manifest.SignatureV2) ? LegacyPayload(manifest) : V2Payload(manifest);
        if (!rsa.VerifyData(Encoding.UTF8.GetBytes(payload), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new CryptographicException("更新清单签名验证失败。");
        return (manifest, version, uri);
    }

    internal static string LegacyPayload(UpdateManifest manifest) => $"{manifest.Version}\n{manifest.DownloadUrl}\n{manifest.Sha256}";
    internal static string V2Payload(UpdateManifest manifest) => string.Join('\n', manifest.Product, manifest.Channel, manifest.Version, manifest.DownloadUrl, manifest.Size, manifest.Sha256.ToLowerInvariant(), manifest.ReleaseNotes);

    internal static void VerifyPackageFile(string path, UpdateManifest manifest)
    {
        var info = new FileInfo(path);
        if (info.Length != manifest.Size) throw new InvalidDataException("更新包大小与签名清单不一致。");
        using var stream = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new CryptographicException("更新包哈希校验失败。");
    }
}
