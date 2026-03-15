using System.Diagnostics;
using OpenTelemetry;

namespace MediMind.API.Diagnostics;

/// <summary>
/// Custom OpenTelemetry processor that captures completed spans and stores them
/// in the <see cref="InMemoryTraceCollector"/> for the in-app trace viewer.
/// </summary>
public sealed class InMemoryExportProcessor : BaseProcessor<Activity>
{
    private readonly InMemoryTraceCollector _collector;

    public InMemoryExportProcessor(InMemoryTraceCollector collector)
    {
        _collector = collector;
    }

    public override void OnEnd(Activity data)
    {
        _collector.Record(data);
    }
}
