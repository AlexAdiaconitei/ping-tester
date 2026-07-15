using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PingTester;

internal sealed class PowerShellRunner : IDisposable
{
    private const string ScriptResourceName = "PingTester.Resources.ping_test.ps1";
    private readonly StorageService _storage;
    private readonly object _gate = new();
    private Process? _process;
    private Task? _runTask;
    private string? _stopSignalPath;
    private bool _stopRequested;
    private bool _disposed;

    public PowerShellRunner(StorageService storage)
    {
        _storage = storage;
    }

    public event Action<RunParameters>? Started;
    public event Action<IReadOnlyList<PingRecord>>? RecordsAdded;
    public event Action<RunOutcome>? Finished;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    public void Start(RunParameters parameters)
    {
        Validate(parameters);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_runTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("A ping test is already running.");
            }

            _stopRequested = false;
            _runTask = ExecuteAsync(parameters);
        }
    }

    public async Task StopAsync()
    {
        Task? runTask;
        Process? process;
        lock (_gate)
        {
            if (_runTask is not { IsCompleted: false })
            {
                return;
            }

            _stopRequested = true;
            if (_stopSignalPath is not null)
            {
                File.WriteAllText(_stopSignalPath, "stop");
            }

            runTask = _runTask;
            process = _process;
        }

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
        if (completed != runTask)
        {
            try
            {
                process?.Kill(entireProcessTree: true);
            }
            catch
            {
                // The wrapper may have exited between the timeout and the kill request.
            }
        }

        await runTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_runTask is { IsCompleted: false })
            {
                _stopRequested = true;
                try
                {
                    if (_stopSignalPath is not null)
                    {
                        File.WriteAllText(_stopSignalPath, "stop");
                    }
                }
                catch
                {
                    _process?.Kill(entireProcessTree: true);
                }
            }
        }
    }

    private async Task ExecuteAsync(RunParameters parameters)
    {
        var startedAt = DateTimeOffset.Now;
        var filesBefore = _storage.CsvSnapshot();
        var temporaryDirectory = Path.Combine(_storage.TemporaryRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var scriptPath = Path.Combine(temporaryDirectory, "ping_test.ps1");
        var wrapperPath = Path.Combine(temporaryDirectory, "run.ps1");
        var configPath = Path.Combine(temporaryDirectory, "config.json");
        var stopPath = Path.Combine(temporaryDirectory, "stop.signal");
        var liveRecords = new List<PingRecord>();
        var exitCode = -1;
        string? failure = null;

        lock (_gate)
        {
            _stopSignalPath = stopPath;
        }

        Started?.Invoke(parameters);

        try
        {
            ExtractScript(scriptPath);
            File.WriteAllText(wrapperPath, WrapperScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            File.WriteAllText(configPath, JsonSerializer.Serialize(parameters, JsonDefaults.Compact), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            using var process = new Process
            {
                StartInfo = BuildStartInfo(wrapperPath, scriptPath, configPath, stopPath)
            };
            lock (_gate)
            {
                _process = process;
            }

            if (!process.Start())
            {
                throw new InvalidOperationException("Windows PowerShell could not be started.");
            }

            var standardOutput = ReadLiveOutputAsync(process.StandardOutput, liveRecords);
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            exitCode = process.ExitCode;

            var lastOutputLine = await standardOutput.ConfigureAwait(false);
            var errorOutput = await standardError.ConfigureAwait(false);
            if (exitCode != 0)
            {
                failure = LastMeaningfulLine(errorOutput) ?? lastOutputLine ?? $"PowerShell exited with code {exitCode}.";
            }
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            try
            {
                _process?.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process cleanup is best effort; result files remain recoverable.
            }
        }
        finally
        {
            lock (_gate)
            {
                _process = null;
                _stopSignalPath = null;
            }
        }

        var csvPath = _storage.FindNewCsv(filesBefore, startedAt);
        if (csvPath is not null)
        {
            var id = StorageService.IdFromCsvPath(csvPath);
            var jsonPath = Path.ChangeExtension(csvPath, ".json");
            var csvRecords = SafeReadCsv(csvPath);
            if (_stopRequested && !File.Exists(jsonPath))
            {
                try
                {
                    ResultFiles.WriteJson(jsonPath, csvRecords);
                }
                catch (Exception exception)
                {
                    failure ??= exception.Message;
                }
            }

            IReadOnlyList<PingRecord> finalRecords;
            try
            {
                finalRecords = File.Exists(jsonPath) ? ResultFiles.ReadJson(jsonPath) : csvRecords;
            }
            catch
            {
                finalRecords = csvRecords;
            }

            var delivered = liveRecords
                .Select(RecordKey)
                .ToHashSet(StringComparer.Ordinal);
            var missing = finalRecords
                .Where(record => delivered.Add(RecordKey(record)))
                .ToArray();
            if (missing.Length > 0)
            {
                RecordsAdded?.Invoke(missing);
            }

            var state = _stopRequested
                ? RunState.Stopped
                : exitCode == 0 && File.Exists(jsonPath)
                    ? RunState.Completed
                    : RunState.Incomplete;
            _storage.RecordRun(new RunMetadata
            {
                Id = id,
                State = state,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now,
                Parameters = parameters
            });

            CleanupTemporaryDirectory(temporaryDirectory);
            Finished?.Invoke(new RunOutcome
            {
                Id = id,
                State = state,
                ExitCode = exitCode,
                Error = failure
            });
            return;
        }

        CleanupTemporaryDirectory(temporaryDirectory);
        Finished?.Invoke(new RunOutcome
        {
            State = RunState.Incomplete,
            ExitCode = exitCode,
            Error = failure ?? "The script did not create a result file."
        });
    }

    private async Task<string?> ReadLiveOutputAsync(StreamReader reader, ICollection<PingRecord> records)
    {
        string? lastMeaningfulLine = null;
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lastMeaningfulLine = line.Trim();
            }

            var record = ParseOutputRecord(line);
            if (record is null)
            {
                continue;
            }

            records.Add(record);
            RecordsAdded?.Invoke([record]);
        }

        return lastMeaningfulLine;
    }

    private static PingRecord? ParseOutputRecord(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 4
            || !ValueParsing.TryTimestamp(parts[0], out var timestamp))
        {
            return null;
        }

        var status = parts[2].Trim();
        if (status is not ("OK" or "FALLO"))
        {
            return null;
        }

        var detail = parts[3].Trim();
        return new PingRecord
        {
            Timestamp = timestamp,
            Target = parts[1].Trim(),
            Success = status == "OK",
            LatencyMs = status == "OK"
                ? ValueParsing.NullableDouble(detail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                : null,
            ErrorMessage = status == "FALLO" ? detail : ""
        };
    }

    private static string RecordKey(PingRecord record) =>
        $"{record.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\u001F{record.Target}";

    private static IReadOnlyList<PingRecord> SafeReadCsv(string path)
    {
        try
        {
            return ResultFiles.ReadCsv(path);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private ProcessStartInfo BuildStartInfo(string wrapperPath, string scriptPath, string configPath, string stopPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            WorkingDirectory = _storage.ResultsDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[]
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-ScriptPath",
            scriptPath,
            "-ConfigPath",
            configPath,
            "-StopPath",
            stopPath,
            "-WorkingDirectory",
            _storage.ResultsDirectory
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void ExtractScript(string destination)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScriptResourceName)
            ?? throw new InvalidOperationException("The embedded ping script is missing.");
        using var target = File.Create(destination);
        source.CopyTo(target);
    }

    private static string? LastMeaningfulLine(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault();

    private static void CleanupTemporaryDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Startup cleanup will retry abandoned directories after one day.
        }
    }

    private static void Validate(RunParameters parameters)
    {
        if (parameters.DurationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "Duration must be a positive whole number of minutes.");
        }

        if (!double.IsFinite(parameters.IntervalSeconds) || parameters.IntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "Interval must be a positive number of seconds.");
        }

        parameters.Targets = parameters.Targets
            .Select(target => target.Trim())
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (parameters.Targets.Length == 0)
        {
            throw new ArgumentException("At least one target is required.", nameof(parameters));
        }

        if (parameters.Targets.Any(target => target.Any(char.IsControl) || target.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException("Targets cannot contain spaces or control characters.", nameof(parameters));
        }
    }

    private const string WrapperScript = """
param(
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$StopPath,
    [Parameter(Mandatory = $true)][string]$WorkingDirectory
)

$job = $null
$stopRequested = $false
try {
    $job = Start-Job -ScriptBlock {
        param($InnerScriptPath, $InnerConfigPath, $InnerWorkingDirectory)
        Set-Location -LiteralPath $InnerWorkingDirectory
        $config = Get-Content -LiteralPath $InnerConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
        [string[]]$targets = @($config.targets | ForEach-Object { [string]$_ })
        # Windows PowerShell 5.1 declares Start-Sleep -Seconds as an integer.
        # Keep the original script untouched while honoring its documented decimal interval.
        function Start-Sleep {
            [CmdletBinding(DefaultParameterSetName = 'Seconds')]
            param(
                [Parameter(ParameterSetName = 'Seconds', Position = 0)][double]$Seconds,
                [Parameter(ParameterSetName = 'Milliseconds')][int]$Milliseconds
            )
            $delay = if ($PSCmdlet.ParameterSetName -eq 'Milliseconds') {
                $Milliseconds
            } else {
                [int][Math]::Round($Seconds * 1000)
            }
            [Threading.Thread]::Sleep([Math]::Max(0, $delay))
        }

        & $InnerScriptPath `
            -DurationMinutes ([int]$config.durationMinutes) `
            -IntervalSeconds ([double]$config.intervalSeconds) `
            -Targets $targets
    } -ArgumentList $ScriptPath, $ConfigPath, $WorkingDirectory

    while ($job.State -eq 'Running' -or $job.State -eq 'NotStarted') {
        Receive-Job -Job $job
        if (Test-Path -LiteralPath $StopPath) {
            $stopRequested = $true
            Stop-Job -Job $job -ErrorAction SilentlyContinue
            break
        }
        Start-Sleep -Milliseconds 200
    }

    Wait-Job -Job $job | Out-Null
    Receive-Job -Job $job
    if (-not $stopRequested -and $job.State -eq 'Failed') { exit 1 }
    exit 0
}
finally {
    if ($job) {
        if ($job.State -eq 'Running') { Stop-Job -Job $job -ErrorAction SilentlyContinue }
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}
""";
}
