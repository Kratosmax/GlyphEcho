namespace GlyphEcho;

internal static class RuleCatalog
{
    internal static List<KeyRule> Merge(IEnumerable<KeyRule> catalog, IEnumerable<KeyRule> overrides)
    {
        var merged = catalog.Select(item => item.Clone()).ToList();
        var index = merged
            .Select((item, position) => (Key: KeyboardHook.NormalizeForRule(item.Key), Position: position))
            .Where(item => item.Key.Length > 0)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Position, StringComparer.OrdinalIgnoreCase);

        foreach (var overrideRule in overrides)
        {
            var normalized = KeyboardHook.NormalizeForRule(overrideRule.Key);
            if (!index.TryGetValue(normalized, out var position))
            {
                index[normalized] = merged.Count;
                merged.Add(overrideRule.Clone());
                continue;
            }

            var existing = merged[position];
            existing.Enabled = overrideRule.Enabled;
            if (overrideRule.HasDescriptionOverride || !string.IsNullOrWhiteSpace(overrideRule.Description))
                existing.Description = overrideRule.Description;
        }
        return merged;
    }

    internal static List<KeyRule> BuildOverrides(IEnumerable<KeyRule> current, IEnumerable<KeyRule> catalog)
    {
        var baseline = catalog
            .GroupBy(item => KeyboardHook.NormalizeForRule(item.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<KeyRule>();
        foreach (var item in current)
        {
            var normalized = KeyboardHook.NormalizeForRule(item.Key);
            if (!baseline.TryGetValue(normalized, out var original))
            {
                var added = item.Clone();
                added.HasDescriptionOverride = true;
                result.Add(added);
                continue;
            }
            if (original.Enabled == item.Enabled && string.Equals(original.Description, item.Description, StringComparison.Ordinal)) continue;
            var changed = item.Clone();
            changed.HasDescriptionOverride = !string.Equals(original.Description, item.Description, StringComparison.Ordinal);
            result.Add(changed);
        }
        return result;
    }
}
