using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace GlyphEcho;

internal sealed record OverlayPalette(
    string Id,
    string Label,
    bool IsDark,
    MediaColor Surface,
    MediaColor Border,
    MediaColor KeySurface,
    MediaColor KeyBorder,
    MediaColor KeyText,
    MediaColor SourceText,
    MediaColor Accent);

internal static class OverlayPaletteCatalog
{
    internal const string DefaultId = "dark-mint";

    internal static IReadOnlyList<OverlayPalette> All { get; } =
    [
        new("dark-mint", "深色 · 薄荷绿", true, MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(76, 111, 113), MediaColor.FromRgb(35, 58, 62), MediaColors.Transparent, MediaColors.White, MediaColor.FromRgb(187, 208, 210), MediaColor.FromRgb(97, 210, 198)),
        new("dark-blue", "深色 · 天空蓝", true, MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(76, 111, 113), MediaColor.FromRgb(35, 58, 62), MediaColors.Transparent, MediaColors.White, MediaColor.FromRgb(187, 208, 210), MediaColor.FromRgb(130, 183, 255)),
        new("dark-amber", "深色 · 琥珀黄", true, MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(76, 111, 113), MediaColor.FromRgb(35, 58, 62), MediaColors.Transparent, MediaColors.White, MediaColor.FromRgb(187, 208, 210), MediaColor.FromRgb(255, 209, 102)),
        new("light-teal", "浅色 · 深青", false, MediaColor.FromRgb(248, 250, 250), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(232, 239, 240), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(83, 97, 104), MediaColor.FromRgb(15, 118, 110)),
        new("light-blue", "浅色 · 钴蓝", false, MediaColor.FromRgb(248, 250, 250), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(232, 239, 240), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(83, 97, 104), MediaColor.FromRgb(29, 78, 216)),
        new("light-rose", "浅色 · 玫红", false, MediaColor.FromRgb(248, 250, 250), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(232, 239, 240), MediaColor.FromRgb(198, 208, 211), MediaColor.FromRgb(24, 37, 41), MediaColor.FromRgb(83, 97, 104), MediaColor.FromRgb(190, 24, 93))
    ];

    internal static string Normalize(string? id) =>
        All.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ? All.First(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;

    internal static OverlayPalette Resolve(string? id)
    {
        var normalized = Normalize(id);
        return All.First(item => item.Id == normalized);
    }
}
