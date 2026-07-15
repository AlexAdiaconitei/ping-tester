using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PingTester;

internal static class ResultFiles
{
    public static IReadOnlyList<PingRecord> Read(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? ReadJson(path)
            : ReadCsv(path);
    }

    public static IReadOnlyList<PingRecord> ReadCsv(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ParseCsv(reader.ReadToEnd());
    }

    public static IReadOnlyList<PingRecord> ParseCsv(string text)
    {
        var rows = ParseRows(text);
        if (rows.Count == 0)
        {
            return [];
        }

        var headers = rows[0]
            .Select((value, index) => (value: value.Trim().TrimStart('\uFEFF'), index))
            .ToDictionary(item => item.value, item => item.index, StringComparer.OrdinalIgnoreCase);

        string Cell(IReadOnlyList<string> row, string name) =>
            headers.TryGetValue(name, out var index) && index < row.Count ? row[index].Trim() : "";

        var records = new List<PingRecord>(Math.Max(0, rows.Count - 1));
        foreach (var row in rows.Skip(1))
        {
            if (!ValueParsing.TryTimestamp(Cell(row, "Timestamp"), out var timestamp))
            {
                continue;
            }

            records.Add(new PingRecord
            {
                Timestamp = timestamp,
                Target = Cell(row, "Target"),
                Success = ValueParsing.IsTrue(Cell(row, "Success")),
                LatencyMs = ValueParsing.NullableDouble(Cell(row, "LatencyMs")),
                ErrorMessage = Cell(row, "ErrorMessage")
            });
        }

        records.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        return records;
    }

    public static IReadOnlyList<PingRecord> ReadJson(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
        var records = new List<PingRecord>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                AddJsonRecord(element, records);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddJsonRecord(document.RootElement, records);
        }

        records.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        return records;
    }

    public static void WriteJson(string path, IReadOnlyList<PingRecord> records)
    {
        var payload = records.Select(record => new
        {
            Timestamp = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            record.Target,
            record.Success,
            record.LatencyMs,
            record.ErrorMessage
        });

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(payload, JsonDefaults.Indented), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(temporary, path, overwrite: true);
    }

    private static void AddJsonRecord(JsonElement element, ICollection<PingRecord> records)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !TryProperty(element, "Timestamp", out var timestampElement)
            || !ValueParsing.TryTimestamp(JsonText(timestampElement), out var timestamp))
        {
            return;
        }

        TryProperty(element, "Target", out var targetElement);
        TryProperty(element, "Success", out var successElement);
        TryProperty(element, "LatencyMs", out var latencyElement);
        TryProperty(element, "ErrorMessage", out var errorElement);

        records.Add(new PingRecord
        {
            Timestamp = timestamp,
            Target = JsonText(targetElement),
            Success = successElement.ValueKind == JsonValueKind.True
                || ValueParsing.IsTrue(JsonText(successElement)),
            LatencyMs = latencyElement.ValueKind == JsonValueKind.Number && latencyElement.TryGetDouble(out var latency)
                ? latency
                : ValueParsing.NullableDouble(JsonText(latencyElement)),
            ErrorMessage = JsonText(errorElement)
        });
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string JsonText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => element.GetRawText(),
        _ => ""
    };

    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when cell.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    if (row.Any(value => value.Length > 0))
                    {
                        rows.Add(row);
                    }
                    row = [];
                    break;
                default:
                    cell.Append(character);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            if (row.Any(value => value.Length > 0))
            {
                rows.Add(row);
            }
        }

        return rows;
    }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions Indented = new(Compact)
    {
        WriteIndented = true
    };
}
