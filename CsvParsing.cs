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

        // ✅ REMOVE THIS LINE (you don't need a map for snake_case properties)
        // csv.Context.RegisterClassMap<InternetRecordMap>();

        return csv.GetRecords<InternetRecord>().ToList();
    }
}
