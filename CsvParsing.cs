using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

public static class CsvParsing
{
    public static List<InternetRecord> ParseInternetCsv(string csvText)
    {
        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        });

        return csv.GetRecords<InternetRecord>().ToList();
    }

    public static (List<string> Headers, List<Dictionary<string, string>> Rows)
 ParseRawCsv(string csvText, int? maxRows = null)
    {
        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        });

        csv.Read();
        csv.ReadHeader();

        var headers = (csv.HeaderRecord ?? Array.Empty<string>()).ToList();
        var rows = new List<Dictionary<string, string>>();

        while (csv.Read())
        {
            var row = new Dictionary<string, string>(headers.Count);
            foreach (var h in headers)
                row[h] = csv.GetField(h) ?? "";

            rows.Add(row);

            if (maxRows.HasValue && rows.Count >= maxRows.Value)
                break;
        }

        return (headers, rows);
    }

}
