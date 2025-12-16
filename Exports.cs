using System.Text;
using System.Text.Json;
using System.Xml.Linq;

public static class Exports
{
    // JSON export: include summary + key outputs + (optional) all rows
    public static string ToJson(AppState state)
    {
        var summary = BuildSummary(state);

        var payload = new
        {
            exportedAtUtc = DateTime.UtcNow,
            summary = new
            {
                totalRows = summary.TotalRows,
                totalUniqueZipCodes = summary.TotalUniqueZipCodes,
                zipCodesUnder10PercentNoInternet = summary.ZipUnder10
            },
            // Include full raw rows so JSON can represent the raw dataset too
            raw = state.RawRows
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    // XML export: similar info, with raw rows included as elements
    public static string ToXml(AppState state)
    {
        var summary = BuildSummary(state);

        var doc = new XDocument(
            new XElement("Export",
                new XElement("ExportedAtUtc", DateTime.UtcNow.ToString("o")),
                new XElement("Summary",
                    new XElement("TotalRows", summary.TotalRows),
                    new XElement("TotalUniqueZipCodes", summary.TotalUniqueZipCodes),
                    new XElement("ZipCodesUnder10PercentNoInternet",
                        summary.ZipUnder10.Select(z => new XElement("ZipCode", z))
                    )
                ),
                new XElement("RawRows",
                    state.RawRows.Select(row =>
                        new XElement("Row",
                            state.RawHeaders.Select(h =>
                                new XElement(SafeXmlName(h), row.TryGetValue(h, out var v) ? v : "")
                            )
                        )
                    )
                )
            )
        );

        return doc.ToString();
    }

    // RAW export as CSV: export the raw data as imported  
    public static string ToCsv(AppState state)
    {
        var sb = new StringBuilder();

        // Header line
        sb.AppendLine(string.Join(",", state.RawHeaders.Select(EscapeCsv)));

        // Rows
        foreach (var row in state.RawRows)
        {
            var fields = state.RawHeaders.Select(h =>
            {
                var value = row.TryGetValue(h, out var v) ? v : "";
                return EscapeCsv(value);
            });
            sb.AppendLine(string.Join(",", fields));
        }

        return sb.ToString();
    }

    // --- helpers ---

    private static (int TotalRows, int TotalUniqueZipCodes, List<string> ZipUnder10) BuildSummary(AppState state)
    {
        // Same zip validation rule you use in Step 2
        var uniqueZips = state.Records
            .Select(r => r.zip_code)
            .Where(Utils.IsValidZip5)
            .Select(z => z!.Trim())
            .Distinct()
            .ToList();

        var zipUnder10 = state.Records
            .Where(r => r.no_internet_access_percentage.HasValue)
            .Where(r => Utils.IsValidZip5(r.zip_code))
            .Select(r => new { Zip = r.zip_code!.Trim(), NoInternet = r.no_internet_access_percentage!.Value })
            .GroupBy(x => x.Zip)
            .Select(g => new { Zip = g.Key, NoInternet = g.Min(x => x.NoInternet) })
            .Where(x => x.NoInternet < 0.10)
            .OrderBy(x => x.Zip)
            .Select(x => x.Zip)
            .ToList();

        return (state.Records.Count, uniqueZips.Count, zipUnder10);
    }

    private static string EscapeCsv(string? value)
    {
        value ??= "";
        var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (value.Contains('"'))
            value = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{value}\"" : value;
    }

    private static string SafeXmlName(string header)
    {
        // XML element names cannot contain spaces, start with digits, etc.
        // Simple approach: replace invalid chars with underscores.
        var sb = new StringBuilder();
        foreach (var ch in header)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');

        var name = sb.ToString();
        if (string.IsNullOrWhiteSpace(name)) name = "Field";
        if (char.IsDigit(name[0])) name = "_" + name;
        return name;
    }
}
