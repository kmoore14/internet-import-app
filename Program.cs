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

    var body = $"<h1>Phase 2: Preview</h1><p>Records loaded: {state.Records.Count}</p>";
    return Results.Content(RenderPage("Step 2 - Preview", body, backUrl: "/step1", nextUrl: "/step3", showNext: true), "text/html");
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


