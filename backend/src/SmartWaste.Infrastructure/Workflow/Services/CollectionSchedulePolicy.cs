using System.Globalization;
using System.Text.RegularExpressions;
using SmartWaste.Application.Common.Exceptions;

namespace SmartWaste.Infrastructure.Workflow.Services;

/// <summary>Municipality-local operating hours for AI-approved collection starts only.</summary>
public sealed class CollectionScheduleOptions
{
    public string TimeZoneId { get; set; } = "Asia/Colombo";
    public string CollectionWindowStart { get; set; } = "08:00";
    public string CollectionWindowEnd { get; set; } = "16:00";
}

/// <summary>Pure schedule validation; does not change the proposed instant or write data.</summary>
public sealed class CollectionSchedulePolicy
{
    private static readonly Regex OffsetTimestamp = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly TimeZoneInfo _municipalTimeZone;
    private readonly TimeOnly _windowStart;
    private readonly TimeOnly _windowEnd;

    public CollectionSchedulePolicy(CollectionScheduleOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TimeZoneId))
        {
            throw new ArgumentException("Municipality:TimeZoneId is required.");
        }

        try
        {
            _municipalTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Municipality:TimeZoneId must name an available timezone.", ex);
        }

        if (!TimeOnly.TryParseExact(options.CollectionWindowStart, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _windowStart) ||
            !TimeOnly.TryParseExact(options.CollectionWindowEnd, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _windowEnd) || _windowStart >= _windowEnd)
        {
            throw new ArgumentException("Municipal collection window must have valid HH:mm times with start before end.");
        }
    }

    public static bool IsValidConfiguration(CollectionScheduleOptions options)
    {
        try
        {
            _ = new CollectionSchedulePolicy(options);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public DateTime ValidateProposedStart(string? scheduledAt, DateTimeOffset nowUtc)
    {
        if (scheduledAt is null || !OffsetTimestamp.IsMatch(scheduledAt) ||
            !DateTimeOffset.TryParse(scheduledAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var proposed))
        {
            throw new BusinessRuleConflictException(
                "The proposed collection time must be an ISO 8601 timestamp with an explicit UTC offset.");
        }

        if (proposed < nowUtc)
        {
            throw new BusinessRuleConflictException("The proposed collection time is in the past and cannot be executed.");
        }

        var local = TimeZoneInfo.ConvertTime(proposed, _municipalTimeZone);
        var localTime = TimeOnly.FromDateTime(local.DateTime);
        if (localTime < _windowStart || localTime >= _windowEnd)
        {
            throw new BusinessRuleConflictException(
                $"Proposed collection time must be between {_windowStart:HH:mm} inclusive and {_windowEnd:HH:mm} exclusive in the municipality timezone.");
        }

        return proposed.UtcDateTime;
    }
}
