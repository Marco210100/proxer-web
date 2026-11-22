using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Proxer.Api.Proxer;

/// <summary>Turns Proxer's HTML fragments into typed data. All knowledge about their markup lives here.</summary>
public static partial class Parsers
{
    private static readonly HtmlParser Html = new();

    public static string Cover(int id) => $"https://cdn.proxer.me/cover/{id}.jpg";

    public static NewsItem[] News(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("div.news").Select(n =>
        {
            var link = n.QuerySelector(".news_title a");
            var info = n.QuerySelector(".news_info");
            var infoText = Clean(info?.TextContent);
            var image = UrlInStyleRegex().Match(n.QuerySelector(".news_image")?.GetAttribute("style") ?? "");
            return new NewsItem(
                Id: link?.GetAttribute("href")?.Split('/').LastOrDefault() ?? "",
                Title: Clean(link?.TextContent),
                Text: Clean(n.QuerySelector(".news_text")?.TextContent),
                Image: image.Success ? AbsoluteUrl(image.Groups[1].Value) : null,
                Date: DateTimeRegex().Match(infoText).Value,
                Author: Clean(info?.QuerySelectorAll("a").LastOrDefault()?.TextContent),
                Category: Clean(info?.QuerySelector("a")?.TextContent));
        }).Where(n => n.Title.Length > 0).ToArray();
    }

    public static EntryPage EntryList(string html, int page)
    {
        var doc = Html.ParseDocument(html);
        var entries = new List<EntrySummary>();
        foreach (var row in doc.QuerySelectorAll("tr[class^=entry]"))
        {
            var cells = row.QuerySelectorAll("td");
            var link = row.QuerySelector("a[href^='/info/']");
            if (cells.Length < 6 || link is null) continue;
            var idMatch = InfoIdRegex().Match(link.GetAttribute("href") ?? "");
            if (!idMatch.Success) continue;
            var id = int.Parse(idMatch.Groups[1].Value);
            var rating = RatingRegex().Match(cells[5].TextContent);
            entries.Add(new EntrySummary(
                Id: id,
                Title: Clean(link.TextContent),
                Genres: SplitGenres(cells[2].TextContent),
                Type: Clean(cells[3].TextContent),
                Status: cells[0].QuerySelector("img")?.GetAttribute("title") ?? "",
                Languages: cells[4].QuerySelectorAll("img").Select(i => i.GetAttribute("alt") ?? "").Where(a => a.Length > 0).ToArray(),
                Rating: rating.Success ? ParseDouble(rating.Groups[1].Value) : null,
                Votes: rating.Success ? int.Parse(rating.Groups[2].Value) : null,
                Cover: Cover(id)));
        }
        var hasNext = doc.QuerySelectorAll("a.menu").Any(a => a.TextContent.Trim() == ">");
        return new EntryPage(entries.ToArray(), page, hasNext);
    }

    /// <summary>Season overview: cover cells grouped under one heading per medium.</summary>
    public static SeasonPage Season(string html, int? year, int? season)
    {
        var doc = Html.ParseDocument(html);
        if (year is null || season is null)
        {
            // Without explicit selection Proxer shows the current season; its link carries the numbers.
            var current = doc.QuerySelectorAll("#season a.menu")
                .Select(a => (Text: a.TextContent, Match: SeasonLinkRegex().Match(a.GetAttribute("href") ?? "")))
                .FirstOrDefault(a => a.Text.Contains("Aktuelle") && a.Match.Success);
            year = current.Match is { Success: true } ? int.Parse(current.Match.Groups[1].Value) : DateTime.Now.Year;
            season = current.Match is { Success: true } ? int.Parse(current.Match.Groups[2].Value) : (DateTime.Now.Month - 1) / 3 + 1;
        }

        var groups = new List<SeasonGroup>();
        foreach (var heading in doc.QuerySelectorAll("#seasonEntries h1"))
        {
            var entries = new List<EntrySummary>();
            foreach (var cell in heading.NextElementSibling?.QuerySelectorAll(".infocell") ?? Enumerable.Empty<IElement>())
            {
                var idMatch = InfoIdRegex().Match(cell.QuerySelector("a[href^='/info/']")?.GetAttribute("href") ?? "");
                if (!idMatch.Success) continue;
                var id = int.Parse(idMatch.Groups[1].Value);
                var blocks = cell.Children.Where(c => c.LocalName == "div").ToArray();
                var rating = RatingRegex().Match(blocks.ElementAtOrDefault(2)?.TextContent ?? "");
                entries.Add(new EntrySummary(id,
                    Title: Clean(blocks.ElementAtOrDefault(0)?.QuerySelector("a")?.TextContent),
                    Genres: SplitGenres(blocks.ElementAtOrDefault(3)?.TextContent ?? ""),
                    Type: Clean(blocks.ElementAtOrDefault(1)?.TextContent),
                    Status: blocks.ElementAtOrDefault(1)?.QuerySelector("img")?.GetAttribute("title") ?? "",
                    Languages: [],
                    Rating: rating.Success ? ParseDouble(rating.Groups[1].Value) : null,
                    Votes: rating.Success ? int.Parse(rating.Groups[2].Value) : null,
                    Cover: Cover(id)));
            }
            if (entries.Count > 0) groups.Add(new SeasonGroup(Clean(heading.TextContent), entries.ToArray()));
        }
        return new SeasonPage(year.Value, season.Value, groups.ToArray());
    }

    /// <summary>Release calendar: date headings followed by the entries airing that day.</summary>
    public static CalendarDay[] Calendar(string html)
    {
        var doc = Html.ParseDocument(html);
        var days = new List<(string Date, List<CalendarEntry> Entries)>();
        foreach (var element in doc.QuerySelectorAll(".headDate, .calendarEntry"))
        {
            if (element.ClassList.Contains("headDate"))
            {
                days.Add((Clean(element.TextContent), []));
                continue;
            }
            if (days.Count == 0) continue;
            var idMatch = InfoIdRegex().Match(element.QuerySelector("a[href^='/info/']")?.GetAttribute("href") ?? "");
            if (!idMatch.Success) continue;
            var id = int.Parse(idMatch.Groups[1].Value);
            var watch = WatchLinkRegex().Match(element.QuerySelector("a[href^='/watch/']")?.GetAttribute("href") ?? "");
            var genres = element.QuerySelectorAll("div").Select(d => Clean(d.TextContent))
                .FirstOrDefault(t => t.StartsWith("Genres:"))?["Genres:".Length..] ?? "";
            days[^1].Entries.Add(new CalendarEntry(id,
                Title: Clean(element.QuerySelector("a[data-ajax][href^='/info/']")?.TextContent),
                Cover: Cover(id),
                Episode: watch.Success ? int.Parse(watch.Groups[1].Value) : null,
                Language: watch.Success ? watch.Groups[2].Value : null,
                AirTime: long.TryParse(element.QuerySelector("[id^=airTime]")?.TextContent, out var air) ? air : null,
                UploadTime: long.TryParse(element.QuerySelector("[id^=upTime]")?.TextContent, out var up) ? up : null,
                Genres: SplitGenres(genres)));
        }
        return days.Where(d => d.Entries.Count > 0).Select(d => new CalendarDay(d.Date, d.Entries.ToArray())).ToArray();
    }

    public static EntryDetails Details(string html, int id)
    {
        var doc = Html.ParseDocument(html);
        var title = Clean(doc.QuerySelector(".hreview-aggregate .fn")?.TextContent);
        if (title.Length == 0) throw new ProxerException("Detailseite konnte nicht gelesen werden.");

        var header = Clean(doc.QuerySelector(".hreview-aggregate > span")?.TextContent);
        var type = HeaderTypeRegex().Match(header) is { Success: true } t ? t.Groups[1].Value : "";

        var genres = new List<Genre>();
        var facts = new List<(string Label, List<string> Values)>();
        var description = "";
        foreach (var row in doc.QuerySelector("table.details")?.QuerySelectorAll("tr") ?? Enumerable.Empty<IElement>())
        {
            var cells = row.Children.Where(c => c.LocalName == "td").ToArray();
            var label = Clean(cells.FirstOrDefault()?.QuerySelector("b")?.TextContent).TrimEnd(':');
            if (cells.Length == 1 && label == "Beschreibung")
            {
                cells[0].QuerySelector("b")?.Remove();
                description = Regex.Replace(string.Join("\n", TextLines(cells[0], keepEmpty: true)), @"\n{3,}", "\n\n").Trim();
            }
            else if (cells.Length == 2 && label == "Genre")
            {
                genres.AddRange(cells[1].QuerySelectorAll("a.genreTag")
                    .Select(a => new Genre(Clean(a.TextContent), a.GetAttribute("data-description"))));
            }
            else if (cells.Length == 2 && label.Length > 0 && label != "Tags")
            {
                var values = TextLines(cells[1]);
                if (label == "PSK")
                    values = cells[1].QuerySelectorAll("[title]").Select(e => Clean(e.GetAttribute("title"))).ToList();
                values = values.Select(v => v.Replace(" Warum?", ""))
                    .Where(v => v != "Weitere anzeigen" && !v.StartsWith("Proxer.Me bietet keinerlei Downloads")).ToList();
                if (values.Count == 0) continue;
                var existing = facts.FirstOrDefault(f => f.Label == label);
                if (existing.Values is not null) existing.Values.AddRange(values);
                else facts.Add((label, values));
            }
        }

        var tabs = doc.QuerySelectorAll("#simple-navi a[href]")
            .Select(a => (a.GetAttribute("href") ?? "").Split('#')[0].Split('/').Last())
            .Where(s => s.Length > 0 && !char.IsDigit(s[0])).Distinct().ToArray();

        return new EntryDetails(id, title, type, Cover(id),
            ParseDouble(doc.QuerySelector(".rating .average")?.TextContent),
            int.TryParse(doc.QuerySelector(".votes")?.TextContent, out var votes) ? votes : null,
            genres.ToArray(), description,
            facts.Select(f => new Fact(f.Label, f.Values.ToArray())).ToArray(), tabs,
            Ongoing: !IsFinished(facts.FirstOrDefault(f => f.Label == "Status").Values?.FirstOrDefault()));
    }

    public static EpisodePage Episodes(string html, int id, int page)
    {
        var json = ExtractJsVar(html, "listEntries");
        var doc = Html.ParseDocument(html);
        var pageCount = EpisodePageRegex().Matches(html).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(1).Max();
        if (json is null) return new EpisodePage("", 0, 0, [], [], page, pageCount);

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return new EpisodePage("", 0, 0, [], [], page, pageCount);

        int start = Int(root, "start"), end = Int(root, "end");
        var category = Str(root, "kat");
        var languages = root.TryGetProperty("lang", out var lang) && lang.ValueKind == JsonValueKind.Array
            ? lang.EnumerateArray().Select(l => l.GetString() ?? "").ToArray() : [];

        var byNumber = new Dictionary<int, List<EpisodeLanguage>>();
        var titles = new Dictionary<int, string>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var no = Int(item, "no");
                if (!byNumber.TryGetValue(no, out var list)) byNumber[no] = list = [];
                var hosters = Str(item, "types").Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                list.Add(new EpisodeLanguage(Str(item, "typ"), hosters));
                if (Str(item, "title") is { Length: > 0 } title) titles[no] = title;
            }
        }

        var episodes = new List<Episode>();
        for (var no = start; no <= end; no++)
        {
            var available = byNumber.GetValueOrDefault(no);
            if (available is null) continue;
            var title = titles.GetValueOrDefault(no) ?? Clean(doc.GetElementById($"listTitle{no}")?.TextContent);
            episodes.Add(new Episode(no, title.Length > 0 ? title : null, available.ToArray()));
        }
        return new EpisodePage(category, start, end, languages, episodes.ToArray(), page, pageCount);
    }

    public static WatchInfo Watch(string html, int id, int episode, string language)
    {
        var doc = Html.ParseDocument(html);
        var json = ExtractJsVar(html, "streams")
            ?? throw new ProxerException("Für diese Episode gibt es in dieser Sprache keinen Stream.", 404);
        using var parsed = JsonDocument.Parse(json);

        var streams = parsed.RootElement.EnumerateArray().Select(s =>
        {
            var code = Str(s, "code");
            var template = Str(s, "replace");
            var hosterType = Str(s, "htype");
            var url = template.Length > 0 ? template.Replace("#", code) : code;
            var embeddable = hosterType == "iframe" && (url.StartsWith("http") || url.StartsWith("//"));
            return new StreamInfo(Str(s, "id"), Str(s, "name"), Str(s, "type"), hosterType,
                embeddable ? AbsoluteUrl(url) : null, NullIfEmpty(Str(s, "username")), NullIfEmpty(Str(s, "tname")));
        }).ToArray();

        var count = EpisodeCountRegex().Match(html);
        return new WatchInfo(id, Clean(doc.QuerySelector(".wName")?.TextContent), episode,
            count.Success ? int.Parse(count.Groups[1].Value) : null, language, streams);
    }

    public static ListProgress[] Progress(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        if (!parsed.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) return [];
        return entries.EnumerateArray()
            .Select(e => new ListProgress(Int(e, "tid"), Int(e, "state"), Int(e, "episode"))).ToArray();
    }

    public static MyListItem[] MyList(string html)
    {
        var json = ExtractJsVar(html, "ucp_list_items");
        if (json is null) return [];
        using var parsed = JsonDocument.Parse(json);
        return parsed.RootElement.EnumerateArray().Select(e =>
        {
            var id = Int(e, "id");
            return new MyListItem(id, Str(e, "name"), Str(e, "medium"), Int(e, "episodes"), Int(e, "episode"),
                Int(e, "entry_state"), Int(e, "rating"), Cover(id));
        }).ToArray();
    }

    /// <summary>Only finished or cancelled works stop getting new episodes; anything else may change any time.</summary>
    private static bool IsFinished(string? status) =>
        status is not null && (status.StartsWith("Abgeschlossen") || status.StartsWith("Abgebrochen"));

    /// <summary>Returns the JSON literal assigned to <c>var name = ...</c> inside an inline script.</summary>
    public static string? ExtractJsVar(string html, string name)
    {
        var match = Regex.Match(html, $@"var\s+{Regex.Escape(name)}\s*=\s*(?=[\[{{])");
        if (!match.Success) return null;
        int start = match.Index + match.Length, depth = 0;
        var inString = false;
        for (var i = start; i < html.Length; i++)
        {
            var c = html[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c is '[' or '{') depth++;
            else if (c is ']' or '}')
            {
                if (--depth == 0) return html[start..(i + 1)];
            }
        }
        return null;
    }

    private static List<string> TextLines(IElement element, bool keepEmpty = false)
    {
        var withBreaks = BreakRegex().Replace(element.InnerHtml, "\n");
        var text = Html.ParseDocument($"<body>{withBreaks}</body>").Body?.TextContent ?? "";
        return text.Split('\n').Select(l => WhitespaceRegex().Replace(l, " ").Trim())
            .Where(l => keepEmpty || l.Length > 0).ToList();
    }

    private static string[] SplitGenres(string text) =>
        Clean(text).Replace("Slice of Life", "Slice of Life")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(g => g.Replace(' ', ' ')).ToArray();

    private static string Clean(string? text) => text is null ? "" : WhitespaceRegex().Replace(text, " ").Trim();

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string AbsoluteUrl(string url) =>
        url.StartsWith("//") ? "https:" + url : url.StartsWith('/') ? "https://proxer.me" + url : url;

    private static double? ParseDouble(string? text) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                _ => "",
            }
            : "";

    private static int Int(JsonElement element, string name) =>
        int.TryParse(Str(element, name), out var value) ? value : 0;

    [GeneratedRegex(@"y1=(\d{4})&(?:amp;)?s1=(\d)")] private static partial Regex SeasonLinkRegex();
    [GeneratedRegex(@"/watch/\d+/(\d+)/([a-z]+)")] private static partial Regex WatchLinkRegex();
    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex BreakRegex();
    [GeneratedRegex(@"url\(([^)]+)\)")] private static partial Regex UrlInStyleRegex();
    [GeneratedRegex(@"\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}")] private static partial Regex DateTimeRegex();
    [GeneratedRegex(@"/info/(\d+)")] private static partial Regex InfoIdRegex();
    [GeneratedRegex(@"([\d.]+)\s*\((\d+)\)")] private static partial Regex RatingRegex();
    [GeneratedRegex(@"\(([^()]+)\):?\s*$")] private static partial Regex HeaderTypeRegex();
    [GeneratedRegex(@"/info/\d+/list/(\d+)")] private static partial Regex EpisodePageRegex();
    [GeneratedRegex(@"class=""wEp"">\d+</span>\s*/\s*(\d+)")] private static partial Regex EpisodeCountRegex();
}
