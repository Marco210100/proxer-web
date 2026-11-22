using System.Text.RegularExpressions;
using Proxer.Api.Proxer;

var builder = WebApplication.CreateBuilder(args);

var root = FindRepoRoot();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(new ProxerOptions(Path.Combine(root, ".proxer-session.json")));
builder.Services.AddSingleton<ProxerClient>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ProxerException e) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = e.Status;
        await context.Response.WriteAsJsonAsync(new { error = e.Message });
    }
});

var api = app.MapGroup("/api");

TimeSpan Minutes(double m) => TimeSpan.FromMinutes(m);
bool Slug(string s) => Regex.IsMatch(s, "^[a-z0-9-]{1,30}$");

api.MapGet("/session", (ProxerClient proxer, CancellationToken ct) => proxer.GetSessionAsync(ct));

// Both take a JSON body, so plain cross-site form posts from other pages cannot trigger them.
api.MapPost("/session/login", async (ProxerClient proxer, CancellationToken ct, LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { error = "Benutzername und Passwort fehlen." });
    return Results.Ok(await proxer.LoginAsync(request.Username.Trim(), request.Password, request.SecretKey, ct));
});

api.MapPost("/session/logout", async (ProxerClient proxer, CancellationToken ct, LogoutRequest _) =>
{
    await proxer.LogoutAsync(ct);
    return Results.Ok(new SessionInfo(false, null));
});

api.MapGet("/news", async (ProxerClient proxer, CancellationToken ct) =>
    Parsers.News(await proxer.GetPageAsync("/?format=raw", Minutes(5), ct)));

api.MapGet("/entries", async (ProxerClient proxer, CancellationToken ct,
    string category = "anime", string medium = "animeseries", string sort = "rating", string letter = "all", int page = 1) =>
{
    if (category is not ("anime" or "manga") || !Slug(medium) || sort is not ("rating" or "clicks" or "title")
        || !Regex.IsMatch(letter, "^(all|0-9|[A-Z])$") || page < 1)
        return Results.BadRequest(new { error = "Ungültige Filter." });
    var html = await proxer.GetPageAsync($"/{category}/{medium}/{sort}/{letter}/{page}?format=raw", Minutes(15), ct);
    return Results.Ok(Parsers.EntryList(html, page));
});

api.MapGet("/providers/{provider}", async (ProxerClient proxer, CancellationToken ct, string provider, string sort = "clicks", int page = 1) =>
{
    if (provider is not ("proxerstream" or "aod" or "crunchyroll" or "netflix" or "amazon")
        || sort is not ("rating" or "clicks" or "title") || page < 1)
        return Results.BadRequest(new { error = "Ungültige Filter." });
    var html = await proxer.GetPageAsync($"/provider/{provider}/{sort}/all/{page}?format=raw", Minutes(15), ct);
    return Results.Ok(Parsers.EntryList(html, page));
});

api.MapGet("/season", async (ProxerClient proxer, CancellationToken ct, int? year, int? season) =>
{
    var explicitSeason = year is >= 1900 and <= 2100 && season is >= 1 and <= 4;
    var path = explicitSeason ? $"/season?y1={year}&s1={season}&format=raw" : "/season?format=raw";
    var html = await proxer.GetPageAsync(path, Minutes(30), ct);
    return Parsers.Season(html, explicitSeason ? year : null, explicitSeason ? season : null);
});

api.MapGet("/calendar", async (ProxerClient proxer, CancellationToken ct) =>
    Parsers.Calendar(await proxer.GetPageAsync("/calendar?format=raw", Minutes(10), ct)));

api.MapGet("/search", async (ProxerClient proxer, CancellationToken ct, string q) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(new EntryPage([], 1, false));
    var html = await proxer.GetPageAsync($"/search?s=search&name={Uri.EscapeDataString(q.Trim())}&format=raw", Minutes(10), ct);
    return Results.Ok(Parsers.EntryList(html, 1));
});

api.MapGet("/entries/{id:int}", async (ProxerClient proxer, CancellationToken ct, int id, bool refresh = false) =>
    Parsers.Details(await proxer.GetPageAsync($"/info/{id}/details?format=raw", Minutes(60), ct, refresh), id));

api.MapGet("/entries/{id:int}/episodes", async (ProxerClient proxer, CancellationToken ct, int id, int page = 1, bool refresh = false) =>
{
    page = Math.Max(page, 1);
    // Ongoing series get new episodes, so their list is kept only briefly; finished ones barely change.
    var details = Parsers.Details(await proxer.GetPageAsync($"/info/{id}/details?format=raw", Minutes(60), ct), id);
    var ttl = details.Ongoing ? Minutes(2) : Minutes(360);
    return Parsers.Episodes(await proxer.GetPageAsync($"/info/{id}/list/{page}?format=raw", ttl, ct, refresh), id, page);
});

api.MapGet("/watch/{id:int}/{episode:int}/{language}", async (ProxerClient proxer, CancellationToken ct, int id, int episode, string language) =>
{
    if (!Slug(language)) return Results.BadRequest(new { error = "Ungültige Sprache." });
    var html = await proxer.GetPageAsync($"/watch/{id}/{episode}/{language}?format=raw", Minutes(2), ct);
    return Results.Ok(Parsers.Watch(html, id, episode, language));
});

api.MapGet("/me/progress", async (ProxerClient proxer, CancellationToken ct) =>
    Parsers.Progress(await proxer.GetPageAsync("/ucp?format=json&type=mylistentries", Minutes(1), ct)));

api.MapGet("/me/list/{category}", async (ProxerClient proxer, CancellationToken ct, string category) =>
{
    if (category is not ("anime" or "manga")) return Results.BadRequest(new { error = "Ungültige Kategorie." });
    var items = Parsers.MyList(await proxer.GetPageAsync($"/ucp?s={category}&format=raw", Minutes(2), ct));
    // The list page does not carry the watch state; it comes from the progress endpoint.
    var states = Parsers.Progress(await proxer.GetPageAsync("/ucp?format=json&type=mylistentries", Minutes(1), ct))
        .GroupBy(p => p.EntryId).ToDictionary(g => g.Key, g => g.First().State);
    return Results.Ok(items.Select(i => i with { State = states.GetValueOrDefault(i.Id, -1) }));
});

app.Run();

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
    return Directory.GetCurrentDirectory();
}

record LoginRequest(string Username, string Password, string? SecretKey);

record LogoutRequest;
