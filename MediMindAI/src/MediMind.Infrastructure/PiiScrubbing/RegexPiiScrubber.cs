using System.Diagnostics;
using System.Text.RegularExpressions;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.PiiScrubbing;

/// <summary>
/// Regex-based PII scrubber for local development.
/// In production, this would delegate to Microsoft Presidio sidecar (FR-36).
/// </summary>
public partial class RegexPiiScrubber : IPiiScrubber
{
    private static readonly ActivitySource _activitySource = new("MediMind.Data", "1.0.0");

    private readonly ILogger<RegexPiiScrubber> _logger;

    // Pre-compiled regex patterns for common PII types
    private static readonly (string Type, Regex Pattern, string Replacement)[] PiiPatterns =
    {
        ("SSN", SsnRegex(), "[SSN_REDACTED]"),
        ("PHONE", PhoneRegex(), "[PHONE_REDACTED]"),
        ("EMAIL", EmailRegex(), "[EMAIL_REDACTED]"),
        ("DOB", DobRegex(), "[DOB_REDACTED]"),
        ("MRN", MrnRegex(), "[MRN_REDACTED]"),
    };

    public RegexPiiScrubber(ILogger<RegexPiiScrubber> logger)
    {
        _logger = logger;
    }

    public Task<PiiScrubResult> ScrubAsync(string text, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.PiiScrubber.Scrub", ActivityKind.Internal);
        activity?.SetTag("pii.input_length", text.Length);

        var detectedEntities = new List<PiiEntity>();
        var scrubbedText = text;

        foreach (var (type, pattern, replacement) in PiiPatterns)
        {
            var matches = pattern.Matches(scrubbedText);
            foreach (Match match in matches)
            {
                detectedEntities.Add(new PiiEntity
                {
                    Type = type,
                    OriginalValue = match.Value,
                    Replacement = replacement,
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length
                });
            }
            scrubbedText = pattern.Replace(scrubbedText, replacement);
        }

        if (detectedEntities.Count > 0)
        {
            var types = string.Join(", ", detectedEntities.Select(e => e.Type).Distinct());
            activity?.SetTag("pii.entities_found", detectedEntities.Count);
            activity?.SetTag("pii.entity_types", types);

            _logger.LogWarning("PII scrubber detected {Count} entities: {Types}, TraceId: {TraceId}",
                detectedEntities.Count, types,
                Activity.Current?.TraceId.ToString() ?? "none");
        }
        else
        {
            activity?.SetTag("pii.entities_found", 0);
        }

        activity?.SetTag("pii.output_length", scrubbedText.Length);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Task.FromResult(new PiiScrubResult
        {
            ScrubedText = scrubbedText,
            EntitiesDetected = detectedEntities.Count,
            DetectedEntities = detectedEntities
        });
    }

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b(\+?1[-.]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(0[1-9]|1[0-2])/(0[1-9]|[12]\d|3[01])/(\d{4})\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    [GeneratedRegex(@"\bMRN[-:]?\s*\d{6,10}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex MrnRegex();
}
