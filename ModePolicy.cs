namespace GlyphEcho;

internal static class ModePolicy
{
    internal const string Normal = "普通模式";
    internal const string Game = "游戏模式";
    internal const string Presentation = "演示模式";

    internal static string Describe(string mode) => mode switch
    {
        Game => "强制显示单键，可选低级或中级提示，兼顾简洁与来源信息。",
        Presentation => "强制显示单键，并展示按键来源和功能说明。",
        _ => "遵循默认规则和应用规则的单键、展示级别设置。"
    };

    internal static DisplayRule Apply(DisplayRule source, string mode, int gameModeLevel = 1)
    {
        if (mode is not Game and not Presentation) return source;

        var result = new DisplayRule
        {
            Name = source.Name,
            Process = source.Process,
            ProcessPath = source.ProcessPath,
            Enabled = source.Enabled,
            ShowSingleKeys = source.ShowSingleKeys,
            UseGlobalCatalog = source.UseGlobalCatalog,
            Level = source.Level,
            Priority = source.Priority,
            Description = source.Description,
            HiddenKeys = [.. source.HiddenKeys],
            KeyRules = [.. source.KeyRules.Select(item => item.Clone())]
        };

        if (mode == Game)
        {
            result.ShowSingleKeys = true;
            result.Level = gameModeLevel == 2 ? 2 : 1;
        }
        else
        {
            result.ShowSingleKeys = true;
            result.Level = 3;
        }

        return result;
    }
}
