namespace GlyphEcho;

internal sealed record OverlayPresentation(string Display, string App, string Source, string Action, int Level);

internal sealed class OverlayQueue
{
    private readonly List<OverlayQueueItem> _items = [];
    private readonly TimeSpan _lifetime;
    private readonly int _maximumItems;

    internal OverlayQueue(TimeSpan lifetime, int maximumItems = 12)
    {
        _lifetime = lifetime;
        _maximumItems = maximumItems;
    }

    internal void Add(OverlayPresentation presentation, DateTimeOffset now)
    {
        Prune(now);
        var normalized = KeyboardHook.NormalizeForRule(presentation.Display);
        var existing = _items.FirstOrDefault(item =>
            KeyboardHook.NormalizeForRule(item.Presentation.Display).Equals(normalized, StringComparison.OrdinalIgnoreCase)
            && item.Presentation.Level == presentation.Level
            && string.Equals(item.Presentation.App, presentation.App, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Count++;
            existing.ExpiresAt = now + _lifetime;
            return;
        }

        _items.Add(new OverlayQueueItem(presentation, 1, now + _lifetime));
        while (_items.Count > _maximumItems) _items.RemoveAt(0);
    }

    internal IReadOnlyList<OverlayQueueSnapshot> Snapshot(DateTimeOffset now)
    {
        Prune(now);
        return _items.Select(item => new OverlayQueueSnapshot(item.Presentation, item.Count)).ToList();
    }

    internal void Clear() => _items.Clear();

    private void Prune(DateTimeOffset now) => _items.RemoveAll(item => item.ExpiresAt <= now);

    private sealed class OverlayQueueItem(OverlayPresentation presentation, int count, DateTimeOffset expiresAt)
    {
        internal OverlayPresentation Presentation { get; } = presentation;
        internal int Count { get; set; } = count;
        internal DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    }
}

internal sealed record OverlayQueueSnapshot(OverlayPresentation Presentation, int Count);
