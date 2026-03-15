using System.Collections.Concurrent;
using System.Diagnostics;

namespace MediMind.API.Diagnostics;

/// <summary>
/// In-memory trace collector that captures completed Activity spans for the Trace Viewer UI.
/// Stored traces are capped to prevent unbounded memory growth.
/// </summary>
public sealed class InMemoryTraceCollector
{
    private readonly ConcurrentQueue<TraceSpan> _spans = new();
    private const int MaxSpans = 500;

    public void Record(Activity activity)
    {
        var span = new TraceSpan
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            ParentSpanId = activity.ParentSpanId.ToString(),
            OperationName = activity.OperationName,
            DisplayName = activity.DisplayName,
            Source = activity.Source.Name,
            Kind = activity.Kind.ToString(),
            Status = activity.Status.ToString(),
            StatusDescription = activity.StatusDescription,
            StartTime = activity.StartTimeUtc,
            Duration = activity.Duration,
            Tags = activity.Tags
                .Where(t => t.Value is not null)
                .ToDictionary(t => t.Key, t => t.Value!)
        };

        _spans.Enqueue(span);

        // Trim old spans
        while (_spans.Count > MaxSpans)
        {
            _spans.TryDequeue(out _);
        }
    }

    public IReadOnlyList<TraceSpan> GetRecentSpans(int count = 200)
        => _spans.Reverse().Take(count).ToList();

    public IReadOnlyList<TraceSpan> GetByTraceId(string traceId)
        => _spans.Where(s => s.TraceId == traceId).OrderBy(s => s.StartTime).ToList();

    public IReadOnlyList<TraceGroup> GetRecentTraces(int count = 50)
    {
        return _spans
            .GroupBy(s => s.TraceId)
            .OrderByDescending(g => g.Max(s => s.StartTime))
            .Take(count)
            .Select(g =>
            {
                var spans = g.OrderBy(s => s.StartTime).ToList();
                var root = spans.FirstOrDefault(s => s.ParentSpanId == "0000000000000000")
                    ?? spans.First();
                return new TraceGroup
                {
                    TraceId = g.Key,
                    RootOperation = root.DisplayName,
                    Source = root.Source,
                    StartTime = root.StartTime,
                    TotalDuration = root.Duration,
                    SpanCount = spans.Count,
                    HasErrors = spans.Any(s => s.Status == "Error"),
                    Spans = spans
                };
            })
            .ToList();
    }

    public void Clear() 
    {
        while (_spans.TryDequeue(out _)) { }
    }
}

public record TraceSpan
{
    public string TraceId { get; init; } = "";
    public string SpanId { get; init; } = "";
    public string ParentSpanId { get; init; } = "";
    public string OperationName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Source { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Status { get; init; } = "";
    public string? StatusDescription { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

public record TraceGroup
{
    public string TraceId { get; init; } = "";
    public string RootOperation { get; init; } = "";
    public string Source { get; init; } = "";
    public DateTime StartTime { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int SpanCount { get; init; }
    public bool HasErrors { get; init; }
    public List<TraceSpan> Spans { get; init; } = new();
}
