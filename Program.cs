using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<AppState>(); // Register Services 
var app = builder.Build();


app.MapGet("/", () => Results.Redirect("/step1"));

app.MapGet("/step1", () =>
{
    var body = """
        <h1>Phase 1: Import Internet Data</h1>
        <form method="post" action="/import">
            <button type="submit">Import Data</button>
        </form>
    """;
    return Results.Content(RenderPage("Step 1 - Import", body, showRestart: false), "text/html");
});

app.MapGet("/step2", (AppState state) =>
{
    if (!state.HasData)
        return Results.Redirect("/step1");

    var records = state.Records;

    var uniqueZips = records
        .Select(r => r.zip_code)
        .Where(Utils.IsValidZip5)
        .Select(z => z!.Trim())
        .Distinct()
        .OrderBy(z => z)
        .ToList();

    var lowNoInternet = records
        .Where(r => r.no_internet_access_percentage.HasValue)
        .Where(r => Utils.IsValidZip5(r.zip_code))
        .Select(r => new
        {
            Zip = r.zip_code!.Trim(),
            NoInternet = r.no_internet_access_percentage!.Value
        })
        .GroupBy(x => x.Zip)
        .Select(g => new
        {
            Zip = g.Key,
            NoInternet = g.Min(x => x.NoInternet)
        })
        .Where(x => x.NoInternet < 0.10)
        .OrderBy(x => x.Zip)
        .ToList();

    string lowTableHtml;
    if (lowNoInternet.Count == 0)
    {
        lowTableHtml = "<p><em>No ZIP codes found under 10%.</em></p>";
    }
    else
    {
        var lowRows = string.Join("", lowNoInternet.Select(x =>
            $"<tr><td>{x.Zip}</td><td>{Utils.FormatPercent(x.NoInternet)}</td></tr>"
        ));

        lowTableHtml = $"""
            <table class="data">
                <thead>
                    <tr><th>ZIP Code</th><th>No Internet Access (%)</th></tr>
                </thead>
                <tbody>
                    {lowRows}
                </tbody>
            </table>
            <p><em>Count: {lowNoInternet.Count}</em></p>
        """;
    }

    string rawTableHtml;
    var previewCount = Math.Min(100, state.RawRows.Count);
    var previewRows = state.RawRows.Take(previewCount).ToList();

    if (state.RawHeaders.Count == 0 || state.RawRows.Count == 0)
    {
        rawTableHtml = "<p><em>No raw table data available. Try importing again.</em></p>";
    }
    else
    {
        var headerHtml = string.Join("", state.RawHeaders.Select(h =>
            $"<th>{Utils.HtmlEncode(h)}</th>"
        ));

        var bodyRowsHtml = string.Join("", previewRows.Select(row =>
  {
      var tds = string.Join("", state.RawHeaders.Select(h =>
          $"<td>{Utils.HtmlEncode(row.TryGetValue(h, out var v) ? v : "")}</td>"
      ));
      return $"<tr>{tds}</tr>";
  }));

        rawTableHtml = $"""
            <div class="table-wrap">
                <table class="data">
                    <thead>
                        <tr>{headerHtml}</tr>
                    </thead>
                    <tbody>
                        {bodyRowsHtml}
                    </tbody>
                </table>
            </div>
            <p><em>Showing first {previewCount} of {state.RawRows.Count} rows.</em></p>

        """;
    }

    var body = $"""
        <h1>Phase 2: Preview</h1>

        <p><strong>Total unique ZIP codes:</strong> {uniqueZips.Count}</p>
        <p><em>Note: ZIP-based calculations include only 5-digit ZIP codes.</em></p>

        <h2>ZIP codes with &lt; 10% homes with no internet access</h2>
        {lowTableHtml}

        <h2>Raw data (preview)</h2>
        {rawTableHtml}
    """;

    return Results.Content(
        RenderPage(
            title: "Step 2 - Preview",
            bodyContent: body,
            backUrl: "/step1",
            nextUrl: "/step3",
            showNext: true,
            showRestart: true
        ),
        "text/html; charset=utf-8"
    );
});


app.MapGet("/step3", (AppState state) =>
{
    if (!state.HasData)
        return Results.Redirect("/step1");

    var body = """
        <h1>Phase 3: Export</h1>
        <p>Choose an export format:</p>

        <ul>
            <li><a href="/export/json">Export JSON</a></li>
            <li><a href="/export/xml">Export XML</a></li>
            <li><a href="/export/raw">Export RAW (CSV)</a></li>
        </ul>
    """;

    return Results.Content(
        RenderPage("Step 3 - Export", body, backUrl: "/step2", showNext: false, showRestart: true),
        "text/html; charset=utf-8"
    );
});


app.MapGet("/export/json", (AppState state) =>
{
    if (!state.HasData) return Results.Redirect("/step1");

    var json = Exports.ToJson(state);
    return Results.File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", "internet_export_JSON.json");
});

app.MapGet("/export/xml", (AppState state) =>
{
    if (!state.HasData) return Results.Redirect("/step1");

    var xml = Exports.ToXml(state);
    return Results.File(Encoding.UTF8.GetBytes(xml), "application/xml; charset=utf-8", "internet_export_XML.xml");
});

app.MapGet("/export/raw", (AppState state) =>
{
    if (!state.HasData) return Results.Redirect("/step1");

    var csv = Exports.ToCsv(state);
    return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "internet_raw_CSV.csv");
});


app.MapPost("/import", async (AppState state) =>
{
    try
    {
        var url = "https://data.cityofnewyork.us/resource/qz5f-yx82.csv";
        using var httpClient = new HttpClient();
        var csvText = await httpClient.GetStringAsync(url);

        // Typed records (for calculations)
        state.Records = CsvParsing.ParseInternetCsv(csvText);

        var raw = CsvParsing.ParseRawCsv(csvText); // no limit
        state.RawHeaders = raw.Headers;
        state.RawRows = raw.Rows;

        state.HasData = true;
        return Results.Redirect("/step2");
    }
    catch (HttpRequestException ex)
    {
        var body = $"""
            <h1>Import failed</h1>
            <p>Could not download the dataset.</p>
            <pre>{System.Net.WebUtility.HtmlEncode(ex.Message)}</pre>
            <a href="/step1">Go back</a>
        """;

        return Results.Content(
            RenderPage("Import Error", body, showRestart: false),
            "text/html"
        );
    }
    catch (Exception ex)
    {
        var body = $"""
            <h1>Unexpected error</h1>
            <pre>{System.Net.WebUtility.HtmlEncode(ex.Message)}</pre>
            <a href="/step1">Go back</a>
        """;

        return Results.Content(
            RenderPage("Error", body, showRestart: false),
            "text/html"
        );
    }
});

app.MapPost("/restart", (AppState state) =>
{
    state.HasData = false;
    state.Records.Clear();
    return Results.Redirect("/step1");
});


app.Run();

// Template for Pages
string RenderPage(
    string title,
    string bodyContent,
    string? backUrl = null,
    string? nextUrl = null,
    bool showNext = false,
    bool showRestart = true
)
{
    var navButtons = "";


    if (backUrl != null)
        navButtons += $"<a href=\"{backUrl}\">⬅ Back</a> ";

    if (showNext && nextUrl != null)
        navButtons += $"<a href=\"{nextUrl}\">Next ➡</a> ";

    if (showRestart)
    {
        navButtons += """
            <form method="post" action="/restart" style="display:inline;">
                <button type="submit"
                        onclick="return confirm('Restart and clear imported data?')">
                    Restart
                </button>
            </form>
        """;
    }

    return """
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>__TITLE__</title>
    <style>
      .nav {
    margin-bottom: 20px;
  }

  .nav a,
  .nav button {
    margin-right: 10px;
  }
  .table-wrap {
  overflow-x: auto;
  max-width: 100%;
}
table.data {
  border-collapse: collapse;
  width: 100%;
  min-width: max-content; /* helps keep columns readable */
}
table.data th, table.data td {
  border: 1px solid #ccc;
  padding: 8px;
  text-align: left;
  white-space: nowrap; /* prevents ugly wrapping */
}
table.data th {
  background: #f3f3f3;
}

    </style>
</head>
<body>

<div class="nav">
__NAV__
</div>

<hr />

__BODY__

</body>
</html>
"""
    .Replace("__TITLE__", title)
    .Replace("__NAV__", navButtons)
    .Replace("__BODY__", bodyContent);
}


