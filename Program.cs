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
        .Where(z => !string.IsNullOrWhiteSpace(z))
        .Distinct()
        .OrderBy(z => z)
        .ToList();

    var lowNoInternetZips = records
        .Where(r => r.no_internet_access_percentage.HasValue &&
                    r.no_internet_access_percentage.Value < 0.10 &&
                    !string.IsNullOrWhiteSpace(r.zip_code))
        .Select(r => r.zip_code!)
        .Distinct()
        .OrderBy(z => z)
        .ToList();

    // Build ZIP list HTML
    var zipListHtml = lowNoInternetZips.Count == 0
        ? "<p><em>No ZIP codes found under 10%.</em></p>"
        : "<ul>" + string.Join("", lowNoInternetZips.Select(z => $"<li>{z}</li>")) + "</ul>";

    // Table preview (first 50)
    var preview = records.Take(50).ToList();
    var rowsHtml = string.Join("", preview.Select(r =>
        $"<tr><td>{r.zip_code}</td><td>{(r.no_internet_access_percentage.HasValue ? (r.no_internet_access_percentage.Value * 100).ToString("0.00") + "%" : "")}</td></tr>"
    ));

    var tableHtml = $"""
        <table class="data">
            <thead>
                <tr>
                    <th>ZIP Code</th>
                    <th>No Internet Access (%)</th>
                </tr>
            </thead>
            <tbody>
                {rowsHtml}
            </tbody>
        </table>
        <p><em>Showing first {preview.Count} of {records.Count} rows.</em></p>
    """;

    var body = $"""
        <h1>Phase 2: Preview</h1>

        <p><strong>Total unique ZIP codes:</strong> {uniqueZips.Count}</p>

        <h2>ZIP codes with &lt; 10% homes with no internet access</h2>
        {zipListHtml}

        <h2>Raw data (preview)</h2>
        {tableHtml}
    """;

    return Results.Content(
        RenderPage("Step 2 - Preview", body, backUrl: "/step1", nextUrl: "/step3", showNext: true),
        "text/html; charset=utf-8"
    );
});


app.MapGet("/step3", (AppState state) =>
{
    if (!state.HasData)
        return Results.Redirect("/step1");

    var body = $"""
        <h1>Phase 3: Export</h1>
        <p>Placeholder export page.</p>
        <p>Records available: {state.Records.Count}</p>

        <ul>
            <li><a href="/export/json">Export JSON (todo)</a></li>
            <li><a href="/export/xml">Export XML (todo)</a></li>
            <li><a href="/export/raw">Export RAW (todo)</a></li>
        </ul>

        <p>You can restart anytime using the Restart button above.</p>
    """;

    return Results.Content(
        RenderPage(
            title: "Step 3 - Export",
            bodyContent: body,
            backUrl: "/step2",
            nextUrl: null,
            showNext: false,
            showRestart: true
        ),
        "text/html; charset=utf-8"
    );
});


app.MapPost("/import", async (AppState state) =>
{
    try
    {
        var url = "https://data.cityofnewyork.us/resource/qz5f-yx82.csv";

        using var httpClient = new HttpClient();
        var csvText = await httpClient.GetStringAsync(url);

        state.Records = CsvParsing.ParseInternetCsv(csvText);
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
        body {
            font-family: Arial, sans-serif;
            padding: 40px;
        }
        .nav {
            margin-bottom: 20px;
        }
        .nav a, .nav button {
            margin-right: 10px;
        }
        table.data {
  border-collapse: collapse;
  width: 100%;
  max-width: 900px;
}
table.data th, table.data td {
  border: 1px solid #ccc;
  padding: 8px;
  text-align: left;
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


