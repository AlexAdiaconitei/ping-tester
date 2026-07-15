using System.Globalization;
using System.Text.Json.Serialization;

namespace PingTester;

internal sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public int DurationMinutes { get; set; } = 30;
    public double IntervalSeconds { get; set; } = 1;
    public string[] Targets { get; set; } = ["1.1.1.1", "8.8.8.8"];
}

internal sealed class RunParameters
{
    public int DurationMinutes { get; set; }
    public double IntervalSeconds { get; set; }
    public string[] Targets { get; set; } = [];
}

internal enum RunState
{
    Completed,
    Stopped,
    Incomplete
}

internal sealed class RunMetadata
{
    public string Id { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunState State { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public RunParameters Parameters { get; set; } = new();
}

internal sealed class PingRecord
{
    public DateTime Timestamp { get; set; }
    public string Target { get; set; } = "";
    public bool Success { get; set; }
    public double? LatencyMs { get; set; }
    public string ErrorMessage { get; set; } = "";
}

internal sealed class HistoryItem
{
    public string Id { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunState State { get; set; }
    public int Samples { get; set; }
    public int Failures { get; set; }
    public double LossPercent { get; set; }
    public string[] Targets { get; set; } = [];
    public bool HasCsv { get; set; }
    public bool HasJson { get; set; }
}

internal sealed class RunOutcome
{
    public string? Id { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunState State { get; set; }
    public int ExitCode { get; set; }
    public string? Error { get; set; }
}

internal static class ValueParsing
{
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
        "O"
    ];

    public static bool TryTimestamp(string? value, out DateTime timestamp) =>
        DateTime.TryParseExact(
            value?.Trim(),
            TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
            out timestamp)
        || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out timestamp);

    public static bool IsTrue(string? value) =>
        bool.TryParse(value?.Trim(), out var result) && result;

    public static double? NullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
