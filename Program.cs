var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/step1"));

app.MapGet("/step1", () =>
{
    var body = """
        <h1>Phase 1: Import Internet Data</h1>

        <p>
            Click the button below to import the dataset from the Open Data link.
        </p>

        <form method="post" action="/import">
            <button type="submit">Import Data</button>
        </form>
    """;

    return Results.Content(
     RenderPage(
         title: "Step 1 - Import",
         bodyContent: body,
         backUrl: null,
         nextUrl: "/step2",
         showNext: false,
         showRestart: false
     ),
     "text/html"
     );
});

app.MapGet("/step2", () => Results.Content(
    RenderPage("Step 2 - Preview", "<h1>Phase 2: Preview</h1>", backUrl: "/step1", nextUrl: "/step3", showNext: true),
    "text/html"
));


app.MapPost("/import", async () =>
{
    try
    {
        var url = "https://data.cityofnewyork.us/resource/qz5fyx82.csv";
        using var httpClient = new HttpClient();
        var csvText = await httpClient.GetStringAsync(url);

        // store csvText / parsed data somewhere
        return Results.Redirect("/step2");
    }
    catch (Exception ex)
    {
        var body = $"""
            <h1>Import failed</h1>
            <p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p>
            <a href="/step1">Go back</a>
        """;
        return Results.Content(RenderPage("Import failed", body, showRestart: false), "text/html");
    }
});

app.MapPost("/restart", () =>
{
    // TODO:
    // Clear stored records / computed results

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
