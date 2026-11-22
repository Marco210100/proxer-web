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
    if (category is not ("anime" or "manga" or "novel") || !Slug(medium) || sort is not ("rating" or "clicks" or "title")
        || !Regex.IsMatch(letter, "^(all|0-9|[A-Z])$") || page < 1)
        return Results.BadRequest(new { error = "Ungültige Filter." });
    var html = await proxer.GetPageAsync($"/{category}/{medium}/{sort}/{letter}/{page}?format=raw", Minutes(15), ct);
    return Results.Ok(Parsers.EntryList(html, page));
});

api.MapGet("/providers/{provider}", async (ProxerClient proxer, CancellationToken ct, string provider,
    string sort = "clicks", string letter = "all", int page = 1) =>
{
    if (provider is not ("proxerstream" or "aod" or "crunchyroll" or "netflix" or "amazon")
        || sort is not ("rating" or "clicks" or "title") || !Regex.IsMatch(letter, "^(all|0-9|[A-Z])$") || page < 1)
        return Results.BadRequest(new { error = "Ungültige Filter." });
    var html = await proxer.GetPageAsync($"/provider/{provider}/{sort}/{letter}/{page}?format=raw", Minutes(15), ct);
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

api.MapGet("/entries/{id:int}/relations", async (ProxerClient proxer, CancellationToken ct, int id) =>
    Parsers.Relations(await proxer.GetPageAsync($"/info/{id}/relation?format=raw", Minutes(60), ct), id));

api.MapGet("/entries/{id:int}/recommendations", async (ProxerClient proxer, CancellationToken ct, int id) =>
    Parsers.Recommendations(await proxer.GetPageAsync($"/info/{id}/recommendations?format=raw", Minutes(30), ct)));

api.MapGet("/entries/{id:int}/comments", async (ProxerClient proxer, CancellationToken ct, int id, int page = 1, string sort = "latest") =>
{
    if (sort is not ("latest" or "rating") || page < 1) return Results.BadRequest(new { error = "Ungültige Filter." });
    var html = await proxer.GetPageAsync($"/info/{id}/comments/{page}?sort={sort}&format=raw", Minutes(5), ct);
    return Results.Ok(Parsers.Comments(html, page));
});

api.MapGet("/entries/{id:int}/music", async (ProxerClient proxer, CancellationToken ct, int id) =>
    Parsers.Music(await proxer.GetPageAsync($"/info/{id}/music?format=raw", Minutes(60), ct)));

api.MapGet("/watch/{id:int}/{episode:int}/{language}", async (ProxerClient proxer, CancellationToken ct, int id, int episode, string language) =>
{
    if (!Slug(language)) return Results.BadRequest(new { error = "Ungültige Sprache." });
    var html = await proxer.GetPageAsync($"/watch/{id}/{episode}/{language}?format=raw", Minutes(2), ct);
    return Results.Ok(Parsers.Watch(html, id, episode, language));
});

api.MapGet("/me/progress", async (ProxerClient proxer, CancellationToken ct) =>
    Parsers.Progress(await proxer.GetPageAsync("/ucp?format=json&type=mylistentries", Minutes(1), ct)));

// ---- Write actions. All take a JSON body, so plain cross-site form posts cannot trigger them. ----

api.MapPost("/entries/{id:int}/list-action", async (ProxerClient proxer, CancellationToken ct, int id, ListActionRequest request) =>
    request.Action is not ("note" or "favor" or "finish")
        ? Results.BadRequest(new { error = "Unbekannte Aktion." })
        : Results.Ok(await proxer.SendActionAsync(HttpMethod.Post, $"/info/{id}?format=json&json={request.Action}", "checkPost", ct, "/ucp")));

api.MapPost("/entries/{id:int}/subscribe", async (ProxerClient proxer, CancellationToken ct, int id, SubscribeRequest request) =>
    await proxer.SendActionAsync(HttpMethod.Get, $"/info/{id}?format=json&json=subscribe&positive={(request.Subscribe ? "1" : "-1")}", null, ct, "/ucp"));

// Sets how far the entry has been watched or read; adds it to the list first when it is not on it yet.
api.MapPost("/entries/{id:int}/progress", async (ProxerClient proxer, CancellationToken ct, int id, ProgressRequest request) =>
{
    if (request.Category is not ("anime" or "manga" or "novel") || request.Episode < 0)
        return Results.BadRequest(new { error = "Ungültige Angaben." });
    var listPath = $"/ucp?s={request.Category}&format=raw";
    async Task<MyListItem?> Find() => Parsers.MyList(await proxer.GetPageAsync(listPath, Minutes(2), ct)).FirstOrDefault(i => i.Id == id);

    var item = await Find();
    if (item is null)
    {
        var added = await proxer.SendActionAsync(HttpMethod.Post, $"/info/{id}?format=json&json=note", "checkPost", ct, "/ucp");
        if (!added.Ok) return Results.Ok(added);
        item = await Find();
        if (item is null) return Results.Ok(new ActionResult(false, "Der Eintrag ließ sich nicht in die Liste aufnehmen."));
    }

    var result = await proxer.SendActionAsync(HttpMethod.Post, $"/ucp?format=json&type=incState&id={item.Cid}&value={request.Episode}", "check", ct, "/ucp");
    // A planned entry that now has progress is being watched.
    if (result.Ok && item.State == 2 && request.Episode > 0)
        await proxer.SendActionAsync(HttpMethod.Post, $"/comment?id={item.Cid}&format=json&json=updateState&status=1", "check", ct, "/ucp");
    return Results.Ok(result);
});

api.MapPost("/watch/{id:int}/{episode:int}/{language}/bookmark", async (ProxerClient proxer, CancellationToken ct,
    int id, int episode, string language, BookmarkRequest request) =>
{
    if (!Slug(language) || request.Target is not ("this" or "next") || request.Category is not ("anime" or "manga"))
        return Results.BadRequest(new { error = "Ungültige Angaben." });
    var page = request.Category == "anime" ? "watch" : "chapter";
    return Results.Ok(await proxer.SendActionAsync(HttpMethod.Post,
        $"/{page}/{id}/{episode}/{language}?format=json&type=reminder&title=reminder_{request.Target}", "check", ct, "/ucp"));
});

api.MapGet("/me/bookmarks", async (ProxerClient proxer, CancellationToken ct) =>
    Parsers.Bookmarks(await proxer.GetPageAsync("/ucp?s=reminder&format=raw", Minutes(2), ct)));

api.MapPost("/me/bookmarks/{bookmarkId:long}/delete", async (ProxerClient proxer, CancellationToken ct, long bookmarkId, EmptyRequest _) =>
    await proxer.SendActionAsync(HttpMethod.Post, $"/ucp?format=json&type=deleteReminder&id={bookmarkId}", "check", ct, "/ucp"));

api.MapPost("/me/list/{cid:long}/episode", async (ProxerClient proxer, CancellationToken ct, long cid, EpisodeRequest request) =>
    request.Value < 0
        ? Results.BadRequest(new { error = "Ungültiger Wert." })
        : Results.Ok(await proxer.SendActionAsync(HttpMethod.Post, $"/ucp?format=json&type=incState&id={cid}&value={request.Value}", "check", ct, "/ucp")));

api.MapPost("/me/list/{cid:long}/state", async (ProxerClient proxer, CancellationToken ct, long cid, StateRequest request) =>
    request.State is < 0 or > 3
        ? Results.BadRequest(new { error = "Ungültiger Status." })
        : Results.Ok(await proxer.SendActionAsync(HttpMethod.Post, $"/comment?id={cid}&format=json&json=updateState&status={request.State}", "check", ct, "/ucp")));

api.MapPost("/me/list/{cid:long}/delete", async (ProxerClient proxer, CancellationToken ct, long cid, EmptyRequest _) =>
    await proxer.SendActionAsync(HttpMethod.Post, $"/comment?id={cid}&format=json&json=delete", "check", ct, "/ucp"));

api.MapGet("/me/list/{category}", async (ProxerClient proxer, CancellationToken ct, string category) =>
{
    if (category is not ("anime" or "manga" or "novel")) return Results.BadRequest(new { error = "Ungültige Kategorie." });
    return Results.Ok(Parsers.MyList(await proxer.GetPageAsync($"/ucp?s={category}&format=raw", Minutes(2), ct)));
});

api.MapGet("/pages/{kind}/{id:int}", async (ProxerClient proxer, CancellationToken ct, string kind, int id) =>
{
    if (kind is not ("character" or "persons" or "industry" or "translatorgroups"))
        return Results.BadRequest(new { error = "Unbekannte Seite." });
    return Results.Ok(Parsers.InfoPage(await proxer.GetPageAsync($"/{kind}?id={id}&format=raw", Minutes(120), ct), kind, id));
});

api.MapGet("/updates/{category}", async (ProxerClient proxer, CancellationToken ct, string category) =>
    category is not ("anime" or "manga")
        ? Results.BadRequest(new { error = "Ungültige Kategorie." })
        : Results.Ok(Parsers.Updates(await proxer.GetPageAsync($"/{category}/updates?format=raw", Minutes(5), ct))));

// ---- Community: notifications, messages, forum, profiles ----

api.MapGet("/notifications/counts", async (ProxerClient proxer, CancellationToken ct) =>
    CommunityParsers.Counts(await proxer.GetPageAsync("/components/com_proxer/misc/notifications.php", Minutes(2), ct)));

api.MapGet("/notifications", async (ProxerClient proxer, CancellationToken ct, int page = 1) =>
    CommunityParsers.Notifications(await proxer.GetPageAsync($"/notifications?p={Math.Max(page, 1)}&format=raw", Minutes(2), ct), Math.Max(page, 1)));

api.MapPost("/notifications/{notificationId:long}/delete", async (ProxerClient proxer, CancellationToken ct, long notificationId, EmptyRequest _) =>
    await proxer.SendFormActionAsync(HttpMethod.Post, "/notifications?format=json&s=deleteNotification",
        new Dictionary<string, string> { ["id"] = notificationId.ToString() }, ct, "/notifications", "/components/com_proxer/misc/notifications"));

api.MapGet("/messages", async (ProxerClient proxer, CancellationToken ct) =>
    CommunityParsers.Conversations(await proxer.GetPageAsync("/messages?format=raw", Minutes(1), ct)));

// Opening a conversation marks it as read on Proxer, exactly as on the website.
api.MapGet("/messages/{conversationId:int}", async (ProxerClient proxer, CancellationToken ct, int conversationId, int page = 0) =>
{
    page = Math.Max(page, 0);
    var participants = CommunityParsers.Participants(await proxer.GetPageAsync($"/messages?id={conversationId}&format=raw", Minutes(10), ct));
    var (messages, hasMore) = CommunityParsers.Messages(await proxer.GetJsonAsync($"/messages/?format=json&json=messages&id={conversationId}&p={page}", ct));
    return new ConversationPage(conversationId, participants, messages, page, hasMore);
});

api.MapPost("/messages/{conversationId:int}/send", async (ProxerClient proxer, CancellationToken ct, int conversationId, SendMessageRequest request) =>
    string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 65000
        ? Results.BadRequest(new { error = "Die Nachricht ist leer oder zu lang." })
        : Results.Ok(await proxer.SendFormActionAsync(HttpMethod.Post, $"/messages/?format=json&json=answer&id={conversationId}",
            new Dictionary<string, string> { ["message"] = request.Text }, ct, "/messages", "/components/com_proxer/misc/notifications")));

api.MapGet("/forum", async (ProxerClient proxer, CancellationToken ct) =>
    CommunityParsers.ForumCategories(await proxer.GetPageAsync("/forum", Minutes(360), ct)));

api.MapGet("/forum/{categoryId:int}", async (ProxerClient proxer, CancellationToken ct, int categoryId, int page = 1) =>
{
    page = Math.Max(page, 1);
    var html = await proxer.GetPageAsync($"/forum/{categoryId}?start={(page - 1) * 30}", Minutes(5), ct);
    return CommunityParsers.ForumTopics(html, categoryId, page);
});

api.MapGet("/forum/{categoryId:int}/{topicId:int}", async (ProxerClient proxer, CancellationToken ct, int categoryId, int topicId, int page = 1) =>
{
    page = Math.Max(page, 1);
    var html = await proxer.GetPageAsync($"/forum/{categoryId}/{topicId}?start={(page - 1) * 15}", Minutes(3), ct);
    return CommunityParsers.ForumThread(html, categoryId, topicId, page);
});

api.MapGet("/users/{id:int}", async (ProxerClient proxer, CancellationToken ct, int id) =>
    CommunityParsers.Profile(
        await proxer.GetPageAsync($"/user/{id}/overview?format=raw", Minutes(15), ct),
        await proxer.GetPageAsync($"/user/{id}/about?format=raw", Minutes(15), ct), id));

// Development aid: shows the raw Proxer markup of a page, through the same session, throttle and limits as everything else.
if (app.Environment.IsDevelopment())
    api.MapGet("/dev/raw", async (ProxerClient proxer, CancellationToken ct, string path, bool token = false) =>
        path.StartsWith('/') && !path.StartsWith("//")
            ? Results.Text(token ? await proxer.GetJsonAsync(path, ct) : await proxer.GetPageAsync(path, TimeSpan.Zero, ct), "text/plain")
            : Results.BadRequest());

app.Run();

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
    return Directory.GetCurrentDirectory();
}

record ListActionRequest(string Action);

record SubscribeRequest(bool Subscribe);

record ProgressRequest(int Episode, string Category);

record BookmarkRequest(string Target, string Category);

record EpisodeRequest(int Value);

record StateRequest(int State);

record EmptyRequest;

record SendMessageRequest(string Text);

record LoginRequest(string Username, string Password, string? SecretKey);

record LogoutRequest;
