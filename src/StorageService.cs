using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace PingTester;

internal sealed partial class StorageService
{
    private readonly string _settingsPath;
    private readonly string _metadataPath;
    private readonly object _gate = new();
    private Dictionary<string, RunMetadata> _metadata;

    public StorageService()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PingTester");
        ResultsDirectory = Path.Combine(RootDirectory, "results");
        TemporaryRoot = Path.Combine(Path.GetTempPath(), "PingTester");
        WebViewDataDirectory = Path.Combine(RootDirectory, "webview");
        _settingsPath = Path.Combine(RootDirectory, "settings.json");
        _metadataPath = Path.Combine(RootDirectory, "runs.json");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ResultsDirectory);
        Directory.CreateDirectory(TemporaryRoot);
        _metadata = LoadMetadata();
        RemoveAbandonedTemporaryDirectories();
    }

    public string RootDirectory { get; }
    public string ResultsDirectory { get; }
    public string TemporaryRoot { get; }
    public string WebViewDataDirectory { get; }

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), JsonDefaults.Compact)
                ?? new AppSettings();
            settings.Language = settings.Language == "es" ? "es" : "en";
            settings.DurationMinutes = settings.DurationMinutes > 0 ? settings.DurationMinutes : 30;
            settings.IntervalSeconds = settings.IntervalSeconds > 0 ? settings.IntervalSeconds : 1;
            settings.Targets = SanitizeTargets(settings.Targets);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.Language = settings.Language == "es" ? "es" : "en";
        settings.Targets = SanitizeTargets(settings.Targets);
        WriteAtomic(_settingsPath, JsonSerializer.Serialize(settings, JsonDefaults.Indented));
    }

    public IReadOnlyList<HistoryItem> GetHistory()
    {
        lock (_gate)
        {
            var files = Directory.EnumerateFiles(ResultsDirectory, "ping_test_*.*")
                .Where(path => Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .Select(path => (path, match: ResultNameRegex().Match(Path.GetFileName(path))))
                .Where(item => item.match.Success)
                .GroupBy(item => item.match.Groups[1].Value, StringComparer.OrdinalIgnoreCase);

            var history = new List<HistoryItem>();
            foreach (var group in files)
            {
                var csvPath = group.Select(item => item.path)
                    .FirstOrDefault(path => Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase));
                var jsonPath = group.Select(item => item.path)
                    .FirstOrDefault(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase));
                var preferredPath = jsonPath ?? csvPath;
                IReadOnlyList<PingRecord> records = [];
                if (preferredPath is not null)
                {
                    try
                    {
                        records = ResultFiles.Read(preferredPath);
                    }
                    catch
                    {
                        // A partially written or externally damaged result remains visible as incomplete.
                    }
                }

                var metadata = _metadata.GetValueOrDefault(group.Key);
                var state = metadata?.State ?? (jsonPath is not null ? RunState.Completed : RunState.Incomplete);
                var timestamp = metadata?.StartedAt ?? TimestampFromId(group.Key);
                var failures = records.Count(record => !record.Success);
                history.Add(new HistoryItem
                {
                    Id = group.Key,
                    Timestamp = timestamp,
                    State = preferredPath is not null && records.Count == 0 && FileLength(preferredPath) > 4
                        ? RunState.Incomplete
                        : state,
                    Samples = records.Count,
                    Failures = failures,
                    LossPercent = records.Count == 0 ? 0 : failures * 100d / records.Count,
                    Targets = records.Select(record => record.Target)
                        .Where(target => !string.IsNullOrWhiteSpace(target))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    HasCsv = csvPath is not null,
                    HasJson = jsonPath is not null
                });
            }

            return history.OrderByDescending(item => item.Timestamp).ToArray();
        }
    }

    public IReadOnlyList<PingRecord> LoadRun(string id)
    {
        EnsureValidId(id);
        var jsonPath = Path.Combine(ResultsDirectory, $"ping_test_{id}.json");
        var csvPath = Path.Combine(ResultsDirectory, $"ping_test_{id}.csv");
        if (File.Exists(jsonPath))
        {
            try
            {
                return ResultFiles.ReadJson(jsonPath);
            }
            catch when (File.Exists(csvPath))
            {
                return ResultFiles.ReadCsv(csvPath);
            }
        }

        if (File.Exists(csvPath))
        {
            return ResultFiles.ReadCsv(csvPath);
        }

        throw new FileNotFoundException("The selected run no longer exists.");
    }

    public void RecordRun(RunMetadata metadata)
    {
        EnsureValidId(metadata.Id);
        lock (_gate)
        {
            _metadata[metadata.Id] = metadata;
            WriteAtomic(_metadataPath, JsonSerializer.Serialize(_metadata, JsonDefaults.Indented));
        }
    }

    public void DeleteRun(string id)
    {
        EnsureValidId(id);
        lock (_gate)
        {
            foreach (var extension in new[] { ".csv", ".json" })
            {
                var path = Path.Combine(ResultsDirectory, $"ping_test_{id}{extension}");
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.ThrowException);
                }
            }

            if (_metadata.Remove(id))
            {
                WriteAtomic(_metadataPath, JsonSerializer.Serialize(_metadata, JsonDefaults.Indented));
            }
        }
    }

    public void OpenResultsDirectory()
    {
        Directory.CreateDirectory(ResultsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { ResultsDirectory },
            UseShellExecute = true
        });
    }

    public string? FindNewCsv(ISet<string> filesBefore, DateTimeOffset startedAt)
    {
        return Directory.EnumerateFiles(ResultsDirectory, "ping_test_*.csv")
            .Where(path => !filesBefore.Contains(Path.GetFileName(path)))
            .Where(path => File.GetLastWriteTimeUtc(path) >= startedAt.UtcDateTime.AddSeconds(-3))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public HashSet<string> CsvSnapshot() => Directory.EnumerateFiles(ResultsDirectory, "ping_test_*.csv")
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Cast<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string IdFromCsvPath(string path)
    {
        var match = ResultNameRegex().Match(Path.GetFileName(path));
        return match.Success ? match.Groups[1].Value : throw new InvalidDataException("Unexpected result filename.");
    }

    private Dictionary<string, RunMetadata> LoadMetadata()
    {
        try
        {
            return File.Exists(_metadataPath)
                ? JsonSerializer.Deserialize<Dictionary<string, RunMetadata>>(File.ReadAllText(_metadataPath), JsonDefaults.Compact)
                    ?? new Dictionary<string, RunMetadata>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, RunMetadata>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, RunMetadata>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void RemoveAbandonedTemporaryDirectories()
    {
        foreach (var directory in Directory.EnumerateDirectories(TemporaryRoot))
        {
            try
            {
                if (Directory.GetCreationTimeUtc(directory) < DateTime.UtcNow.AddDays(-1))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // A currently running or protected temporary directory is not ours to remove.
            }
        }
    }

    private static string[] SanitizeTargets(IEnumerable<string>? targets)
    {
        var sanitized = (targets ?? [])
            .Select(target => target.Trim())
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return sanitized.Length > 0 ? sanitized : ["1.1.1.1", "8.8.8.8"];
    }

    private static void EnsureValidId(string id)
    {
        if (!IdRegex().IsMatch(id))
        {
            throw new ArgumentException("Invalid run identifier.", nameof(id));
        }
    }

    private static DateTimeOffset TimestampFromId(string id)
    {
        return DateTime.TryParseExact(
            id,
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var timestamp)
            ? new DateTimeOffset(timestamp)
            : DateTimeOffset.MinValue;
    }

    private static long FileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }

    [GeneratedRegex(@"^ping_test_(\d{8}_\d{6})\.(?:csv|json)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResultNameRegex();

    [GeneratedRegex(@"^\d{8}_\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
