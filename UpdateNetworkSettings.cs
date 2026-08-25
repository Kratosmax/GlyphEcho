namespace GlyphEcho;

public sealed record GithubProxySetting(string BaseUrl, int Priority, bool IsDirect = false);

public sealed record UpdateNetworkSettings(List<GithubProxySetting>? GithubProxies = null, string? HttpProxy = null)
{
    public static UpdateNetworkSettings Default => new([new GithubProxySetting(string.Empty, 10, true)]);

    public UpdateNetworkSettings Normalize()
    {
        var routes = new List<GithubProxySetting>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDirect = false;
        foreach (var route in GithubProxies ?? [])
        {
            if (route.IsDirect)
            {
                if (!hasDirect)
                {
                    routes.Add(new GithubProxySetting(string.Empty, Math.Clamp(route.Priority, 0, 10), true));
                    hasDirect = true;
                }
                continue;
            }
            if (TryNormalizeGithubProxy(route.BaseUrl, out var baseUrl) && seen.Add(baseUrl))
            {
                routes.Add(new GithubProxySetting(baseUrl, Math.Clamp(route.Priority, 0, 10)));
            }
        }
        if (!hasDirect) routes.Insert(0, new GithubProxySetting(string.Empty, routes.Count == 0 ? 10 : 1, true));
        return new UpdateNetworkSettings(routes, TryNormalizeHttpProxy(HttpProxy, out var proxy) ? proxy : null);
    }

    public static bool TryNormalizeGithubProxy(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!TryCreateHttpUri(value, true, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) return false;
        normalized = uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    public static bool TryNormalizeHttpProxy(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!TryCreateHttpUri(value, false, out var uri) || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0) return false;
        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static bool TryCreateHttpUri(string? value, bool allowHttps, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || allowHttps && parsed.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(parsed.Host))
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }
}

internal sealed record UpdateRequestRoute(Uri RequestUri, string DisplayName, bool IsDirect);

internal static class UpdateRouteBuilder
{
    internal static IReadOnlyList<UpdateRequestRoute> Build(Uri originalUri, UpdateNetworkSettings? settings)
    {
        var normalized = (settings ?? UpdateNetworkSettings.Default).Normalize();
        if (!originalUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return [new UpdateRequestRoute(originalUri, originalUri.Host, true)];
        return (normalized.GithubProxies ?? [])
            .Select((route, index) => new { Route = route, Index = index })
            .Where(item => item.Route.Priority > 0)
            .OrderByDescending(item => item.Route.Priority)
            .ThenBy(item => item.Index)
            .Select(item => item.Route.IsDirect
                ? new UpdateRequestRoute(originalUri, "GitHub 直连", true)
                : new UpdateRequestRoute(new Uri($"{item.Route.BaseUrl}/{originalUri.AbsoluteUri}"), new Uri(item.Route.BaseUrl).Host, false))
            .ToList();
    }
}
