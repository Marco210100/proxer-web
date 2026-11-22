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
            Ongoing: !IsFinished(facts.FirstOrDefault(f => f.Label == "Status").Values?.FirstOrDefault()),
            Tags: EntryTags(html),
            Persons: PersonCells(doc, "/persons?id="),
            Characters: PersonCells(doc, "/character?id="),
            Links: FactLinks(doc));
    }

    /// <summary>Tags are not in the markup; the page carries them as a script variable.</summary>
    private static Genre[] EntryTags(string html)
    {
        var json = ExtractJsVar(html, "entryTags");
        if (json is null) return [];
        using var parsed = JsonDocument.Parse(json);
        return parsed.RootElement.EnumerateArray()
            .Select(t => new Genre(Str(t, "tag"), NullIfEmpty(Str(t, "description"))))
            .Where(t => t.Name.Length > 0).ToArray();
    }

    /// <summary>Companies and translator groups named in the facts table, with their ids.</summary>
    private static FactLink[] FactLinks(IParentNode doc) =>
        (doc.QuerySelector("table.details")?.QuerySelectorAll("a[href^='/industry?id='], a[href^='/translatorgroups?id=']") ?? Enumerable.Empty<IElement>())
        .Select(a =>
        {
            var href = a.GetAttribute("href") ?? "";
            var id = QueryIdRegex().Match(href);
            if (!id.Success) return null;
            // Companies carry their role in brackets right after the link, e.g. "(Studio)".
            var role = a.NextSibling is IText text && Regex.Match(text.Data, @"^\s*\(([^)]+)\)") is { Success: true } m ? m.Groups[1].Value : "";
            return new FactLink(href.StartsWith("/industry") ? "industry" : "translatorgroups", int.Parse(id.Groups[1].Value), Clean(a.TextContent), role);
        }).OfType<FactLink>().DistinctBy(l => (l.Kind, l.Id)).ToArray();

    public static UpdateItem[] Updates(string html)
    {
        var doc = Html.ParseDocument(html);
        var items = new List<UpdateItem>();
        foreach (var row in doc.QuerySelectorAll("tr[class*=entry]"))
        {
            var link = row.QuerySelector("a[href^='/info/']");
            var id = InfoIdRegex().Match(link?.GetAttribute("href") ?? "");
            var cells = row.QuerySelectorAll("td");
            if (link is null || !id.Success || cells.Length < 6) continue;
            var entryId = int.Parse(id.Groups[1].Value);
            items.Add(new UpdateItem(entryId, Clean(link.TextContent), Clean(cells[2].TextContent).TrimStart('-', ' '),
                Clean(cells[3].TextContent), Clean(cells[4].TextContent), Clean(cells[5].TextContent), Cover(entryId)));
        }
        return items.ToArray();
    }

    /// <summary>Character, person, company and group pages share one loose layout of fact tables and free text.</summary>
    public static InfoPage InfoPage(string html, string kind, int id)
    {
        var doc = Html.ParseDocument(html);
        var root = doc.QuerySelector(".inner") ?? doc.Body!;
        var meta = Clean(doc.GetElementById("pageMetaAjax")?.TextContent);
        var title = Clean(root.QuerySelector("h1")?.TextContent);
        if (title.Length == 0) title = Regex.Match(meta, @"^[^:]+:\s*(.+?)\s*-\s*Details") is { Success: true } m ? m.Groups[1].Value : "";
        if (title.Length == 0) throw new ProxerException("Seite nicht gefunden.", 404);

        var image = root.QuerySelectorAll("img").Select(i => i.GetAttribute("src") ?? "")
            .FirstOrDefault(s => s.Contains("cdn.proxer.me/") && !s.Contains("/cover/"));

        var facts = new List<Fact>();
        var body = new System.Text.StringBuilder();
        foreach (var row in root.QuerySelectorAll("table.details tr"))
        {
            var cells = row.Children.Where(c => c.LocalName == "td").ToArray();
            // Some rows start with an image cell spanning the table; the label/value pair comes after it.
            var labelIndex = Array.FindIndex(cells, c => c.QuerySelector("b") is not null && c.QuerySelector("img") is null);
            if (labelIndex < 0) continue;
            var labelCell = cells[labelIndex];
            var label = Clean(labelCell.QuerySelector("b")?.TextContent);
            if (label.Length == 0) continue;
            if (labelIndex + 1 < cells.Length)
            {
                var value = cells[labelIndex + 1];
                var text = value.QuerySelector("img.flag") is { } flag && Clean(value.TextContent).Length == 0
                    ? (flag.GetAttribute("alt") ?? "").ToUpperInvariant() : Clean(value.TextContent);
                if (text.Length > 0 && text != "-") facts.Add(new Fact(label, [text]));
            }
            else
            {
                // A lone cell holds a long text under its label, e.g. the description.
                labelCell.QuerySelector("b")?.Remove();
                var text = CommunityParsers.Sanitize(labelCell);
                if (Regex.Replace(text, "<[^>]+>", "").Trim().Length > 0) body.Append("<h3>").Append(System.Net.WebUtility.HtmlEncode(label)).Append("</h3>").Append(text);
            }
        }

        var entries = root.QuerySelectorAll("a[href^='/info/']").Select(a =>
        {
            var entryId = InfoIdRegex().Match(a.GetAttribute("href") ?? "");
            var name = Clean(a.TextContent);
            if (!entryId.Success || name.Length == 0 || a.ParentElement?.LocalName != "td") return null;
            var eid = int.Parse(entryId.Groups[1].Value);
            return new LinkedEntry(eid, name, Clean(a.ParentElement.NextElementSibling?.TextContent), Cover(eid));
        }).OfType<LinkedEntry>().DistinctBy(e => e.Id).ToArray();

        var people = root.QuerySelectorAll("td > a[href^='/persons?id=']").Select(a =>
        {
            var personId = QueryIdRegex().Match(a.GetAttribute("href") ?? "");
            return !personId.Success ? null
                : new PersonRef(int.Parse(personId.Groups[1].Value), Clean(a.TextContent), Clean(a.ParentElement?.NextElementSibling?.TextContent), null);
        }).OfType<PersonRef>().DistinctBy(p => p.Id).ToArray();

        // Whatever is left outside the tables is free text such as a biography.
        foreach (var remove in root.QuerySelectorAll("table, h1, #pageMetaAjax").ToArray()) remove.Remove();
        foreach (var heading in root.QuerySelectorAll("h2").ToArray())
            if (Clean(heading.TextContent) is "Synchronsprecher" or "Vorkommen" or "Charakterinformationen") heading.Remove();
        var free = CommunityParsers.Sanitize(root);
        if (Regex.Replace(free, "<[^>]+>", "").Trim().Length > 0) body.Append(free);

        return new InfoPage(kind, id, title, string.IsNullOrEmpty(image) ? null : AbsoluteUrl(image), facts.ToArray(), body.ToString(), entries, people);
    }

    /// <summary>The small portrait cells used for persons and characters.</summary>
    private static PersonRef[] PersonCells(IParentNode root, string hrefPrefix) =>
        root.QuerySelectorAll(".pGridCell").Select(cell =>
        {
            var link = cell.QuerySelector($".pGridTextBig a[href^='{hrefPrefix}']");
            var idMatch = QueryIdRegex().Match(link?.GetAttribute("href") ?? "");
            if (link is null || !idMatch.Success) return null;
            var image = UrlInStyleRegex().Match(cell.QuerySelector(".pGridImageLink")?.GetAttribute("style") ?? "");
            return new PersonRef(int.Parse(idMatch.Groups[1].Value), Clean(link.TextContent),
                Clean(cell.QuerySelector(".pGridText")?.TextContent),
                image.Success ? AbsoluteUrl(image.Groups[1].Value.Trim('\'', '"')) : null);
        }).OfType<PersonRef>().ToArray();

    public static RelatedEntry[] Relations(string html, int id)
    {
        var doc = Html.ParseDocument(html);
        var summaries = EntryList(html, 1).Entries.ToDictionary(e => e.Id);
        var related = new List<RelatedEntry>();
        foreach (var row in doc.QuerySelectorAll("tr[class^=entry]"))
        {
            var idMatch = InfoIdRegex().Match(row.QuerySelector("a[href^='/info/']")?.GetAttribute("href") ?? "");
            var cells = row.QuerySelectorAll("td");
            if (!idMatch.Success || cells.Length < 8) continue;
            var entryId = int.Parse(idMatch.Groups[1].Value);
            if (entryId == id || !summaries.TryGetValue(entryId, out var summary)) continue;
            related.Add(new RelatedEntry(summary, Clean(cells[6].TextContent), Clean(cells[7].TextContent)));
        }
        return related.ToArray();
    }

    public static Recommendation[] Recommendations(string html)
    {
        var doc = Html.ParseDocument(html);
        var result = new List<Recommendation>();
        foreach (var cell in doc.QuerySelectorAll(".infocell"))
        {
            var idMatch = InfoIdRegex().Match(cell.QuerySelector("a[href^='/info/']")?.GetAttribute("href") ?? "");
            if (!idMatch.Success) continue;
            var id = int.Parse(idMatch.Groups[1].Value);
            var blocks = cell.Children.Where(c => c.LocalName == "div").ToArray();
            var rating = RatingRegex().Match(blocks.ElementAtOrDefault(2)?.TextContent ?? "");
            var summary = new EntrySummary(id,
                Title: Clean(blocks.ElementAtOrDefault(0)?.QuerySelector("a")?.TextContent),
                Genres: [],
                Type: Clean(blocks.ElementAtOrDefault(1)?.TextContent),
                Status: blocks.ElementAtOrDefault(1)?.QuerySelector("img")?.GetAttribute("title") ?? "",
                Languages: [],
                Rating: rating.Success ? ParseDouble(rating.Groups[1].Value) : null,
                Votes: rating.Success ? int.Parse(rating.Groups[2].Value) : null,
                Cover: Cover(id));
            int Count(string selector) => int.TryParse(cell.QuerySelector(selector + " .count")?.TextContent, out var n) ? n : 0;
            result.Add(new Recommendation(summary, Count(".recommend_positive"), Count(".recommend_negative")));
        }
        return result.OrderByDescending(r => r.Up - r.Down).ToArray();
    }

    public static CommentPage Comments(string html, int page)
    {
        var doc = Html.ParseDocument(html);
        var comments = new List<Comment>();
        foreach (var table in doc.QuerySelectorAll("table.details"))
        {
            var like = table.QuerySelector("a.ajaxComment[title^='recommend:']");
            var cells = table.QuerySelector("tr")?.Children.Where(c => c.LocalName == "td").ToArray();
            if (like is null || cells is null || cells.Length < 3) continue;

            var user = cells[0].QuerySelector("a[href^='/user/']");
            var userId = UserIdRegex().Match(user?.GetAttribute("href") ?? "");
            var avatar = user?.QuerySelector("img")?.GetAttribute("src");
            var ratings = cells[1].QuerySelectorAll("table.misc_rating tr").Select(row =>
            {
                var tds = row.QuerySelectorAll("td");
                return tds.Length < 2 ? null : new CommentRating(Clean(tds[0].TextContent), tds[1].QuerySelectorAll("img[src$='stern.png']").Length);
            }).OfType<CommentRating>().ToArray();

            var parts = new List<CommentPart>();
            var body = cells[1].Children.FirstOrDefault(c => c.LocalName == "div");
            if (body is not null)
            {
                var text = new System.Text.StringBuilder();
                CollectText(body, text, parts);
                FlushText(text, parts);
            }

            var stars = cells[2].QuerySelector("p")?.QuerySelectorAll("img");
            var likes = BracketNumberRegex().Match(like.TextContent);
            comments.Add(new Comment(
                Id: like.GetAttribute("title")!["recommend:".Length..],
                UserId: userId.Success ? int.Parse(userId.Groups[1].Value) : null,
                Username: Clean(user?.TextContent),
                Avatar: string.IsNullOrEmpty(avatar) ? null : AbsoluteUrl(avatar),
                Content: parts.ToArray(),
                Ratings: ratings,
                Rating: stars is { Length: > 0 } ? stars.Count(i => (i.GetAttribute("src") ?? "").EndsWith("stern.png")) : null,
                State: CommentStateRegex().Match(cells[2].InnerHtml) is { Success: true } s ? Clean(s.Groups[1].Value) : "",
                Likes: likes.Success ? int.Parse(likes.Groups[1].Value) : 0));
        }
        var hasNext = doc.QuerySelectorAll("a.menu").Any(a => a.TextContent.Trim() == ">");
        return new CommentPage(comments.ToArray(), page, hasNext);
    }

    /// <summary>Flattens comment markup to text, keeping line breaks and splitting off spoiler blocks.</summary>
    private static void CollectText(INode node, System.Text.StringBuilder text, List<CommentPart> parts)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText t) text.Append(WhitespaceRegex().Replace(t.Data, " "));
            else if (child is IElement { LocalName: "br" }) text.Append('\n');
            else if (child is IElement spoiler && spoiler.ClassList.Contains("spoiler"))
            {
                FlushText(text, parts);
                var inner = spoiler.Children.LastOrDefault(c => c.LocalName == "div");
                if (inner is null) continue;
                var hidden = new List<CommentPart>();
                var buffer = new System.Text.StringBuilder();
                CollectText(inner, buffer, hidden);
                FlushText(buffer, hidden);
                var joined = string.Join("\n", hidden.Select(p => p.Text));
                if (joined.Length > 0) parts.Add(new CommentPart(true, joined));
            }
            else if (child is IElement { LocalName: "script" or "style" }) continue;
            else CollectText(child, text, parts);
        }
    }

    private static void FlushText(System.Text.StringBuilder text, List<CommentPart> parts)
    {
        var value = string.Join("\n", text.ToString().Split('\n').Select(l => l.Trim())).Trim();
        value = Regex.Replace(value, @"\n{3,}", "\n\n");
        if (value.Length > 0) parts.Add(new CommentPart(false, value));
        text.Clear();
    }

    public static Album[] Music(string html)
    {
        var doc = Html.ParseDocument(html);
        var albums = new List<Album>();
        foreach (var row in doc.QuerySelectorAll("div[style*='display:table-row']"))
        {
            var info = row.QuerySelectorAll("table").FirstOrDefault();
            if (info is null) continue;
            string? Field(string label) => info.QuerySelectorAll("tr")
                .Select(r => r.QuerySelectorAll("td"))
                .Where(tds => tds.Length == 2 && Clean(tds[0].TextContent) == label)
                .Select(tds => Clean(tds[1].TextContent)).FirstOrDefault();
            var cover = info.QuerySelector("img")?.GetAttribute("src");
            var songs = row.QuerySelectorAll("table#box-table-a tr").Select(r => r.QuerySelectorAll("td"))
                .Where(tds => tds.Length >= 3 && int.TryParse(tds[0].TextContent.Trim(), out _))
                .Select(tds => new Song(int.Parse(tds[0].TextContent.Trim()), Clean(tds[1].TextContent), Clean(tds[2].TextContent)))
                .ToArray();
            var name = Field("Name");
            if (name is null) continue;
            albums.Add(new Album(name, Field("Release-Date"), string.IsNullOrEmpty(cover) ? null : AbsoluteUrl(cover), songs));
        }
        return albums.ToArray();
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
            .Select(e => new ListProgress(Int(e, "tid"), Int(e, "state"), Int(e, "episode"), Int(e, "subscribe") == 1)).ToArray();
    }

    public static MyListItem[] MyList(string html)
    {
        var json = ExtractJsVar(html, "ucp_list_items");
        if (json is null) return [];
        using var parsed = JsonDocument.Parse(json);
        return parsed.RootElement.EnumerateArray().Select(e =>
        {
            var id = Int(e, "id");
            return new MyListItem(id, Str(e, "cid"), Str(e, "name"), Str(e, "medium"), Int(e, "episodes"), Int(e, "episode"),
                Int(e, "comment_state"), Int(e, "rating"), Cover(id));
        }).ToArray();
    }

    public static Bookmark[] Bookmarks(string html)
    {
        var doc = Html.ParseDocument(html);
        var bookmarks = new List<Bookmark>();
        foreach (var row in doc.QuerySelectorAll("tr[id^=entry]"))
        {
            var link = row.QuerySelector("a[href^='/watch/'], a[href^='/chapter/']");
            var match = BookmarkLinkRegex().Match(link?.GetAttribute("href") ?? "");
            var cells = row.QuerySelectorAll("td");
            if (link is null || !match.Success || cells.Length < 6) continue;
            var entryId = int.Parse(match.Groups[2].Value);
            bookmarks.Add(new Bookmark(
                Id: row.Id!["entry".Length..],
                EntryId: entryId,
                Title: Clean(link.TextContent),
                Category: match.Groups[1].Value == "watch" ? "anime" : "manga",
                Episode: int.Parse(match.Groups[3].Value),
                Language: match.Groups[4].Value,
                Medium: string.Join(" ", TextLines(cells[4])),
                Available: (cells[5].QuerySelector("img")?.GetAttribute("src") ?? "").Contains("online"),
                Cover: Cover(entryId)));
        }
        return bookmarks.ToArray();
    }

    /// <summary>Reads the JSON answer Proxer gives to a write action: <c>{error, msg, title}</c>.</summary>
    public static (bool Ok, string Message) ActionAnswer(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);
            var root = parsed.RootElement;
            var message = string.Join(" ", new[] { Str(root, "title"), Str(root, "msg"), Str(root, "message") }.Where(s => s.Length > 0));
            message = Clean(Html.ParseDocument($"<body>{message}</body>").Body?.TextContent);
            return (Str(root, "error") is "0" or "", message);
        }
        catch (JsonException)
        {
            throw new ProxerException("Proxer hat auf die Aktion unerwartet geantwortet.");
        }
    }

    /// <summary>Only finished or cancelled works stop getting new episodes; anything else may change any time.</summary>
    private static bool IsFinished(string? status) =>
        status is not null && (status.StartsWith("Abgeschlossen") || status.StartsWith("Abgebrochen"));

    /// <summary>The reader page of a chapter: page images with their sizes, plus the way to the next chapter.</summary>
    public static ChapterInfo Chapter(string html, int id, int chapter, string language)
    {
        if (html.Contains("challenges.cloudflare.com/turnstile") || html.Contains("Captcha Prüfung"))
            throw new ProxerException("Proxer möchte vor dem Lesen eine Bot-Prüfung, die du einmal selbst bestehen musst.", 428);

        var json = ExtractJsVar(html, "pages") ?? throw new ProxerException("Dieses Kapitel gibt es nicht oder es ist nicht verfügbar.", 404);
        var server = Regex.Match(html, @"var serverurl = '(//[a-z0-9.-]+\.proxer\.me/[^']*)'");
        if (!server.Success) throw new ProxerException("Der Bildserver des Kapitels wurde nicht erkannt.");
        var baseUrl = "https:" + server.Groups[1].Value;

        using var parsed = JsonDocument.Parse(json);
        var pages = parsed.RootElement.EnumerateArray().Select(page =>
        {
            var values = page.EnumerateArray().ToArray();
            var name = values.ElementAtOrDefault(0).ValueKind == JsonValueKind.String ? values[0].GetString() ?? "" : "";
            if (name.Length == 0 || name.Contains('/') || name.Contains("..")) return null;
            int Size(int index) => values.Length > index && values[index].TryGetInt32(out var n) ? n : 0;
            return new ChapterPage(baseUrl + Uri.EscapeDataString(name), Size(1), Size(2));
        }).OfType<ChapterPage>().ToArray();

        var doc = Html.ParseDocument(html);
        var crumbs = doc.QuerySelectorAll("#breadcrumb a").Select(a => Clean(a.TextContent)).ToArray();
        var chapterTitle = crumbs.ElementAtOrDefault(1) ?? "";
        var count = Regex.Match(chapterTitle, @"\((\d+)/(\d+)\)$");
        var next = Regex.Match(html, @"var nextChapter = ""/chapter/\d+/(\d+)/");
        return new ChapterInfo(id, crumbs.ElementAtOrDefault(0) ?? "", chapter, language,
            count.Success ? chapterTitle[..count.Index].Trim() : chapterTitle,
            count.Success ? int.Parse(count.Groups[2].Value) : null,
            next.Success ? int.Parse(next.Groups[1].Value) : null, pages);
    }

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
    [GeneratedRegex(@"[?&]id=(\d+)")] private static partial Regex QueryIdRegex();
    [GeneratedRegex(@"/user/(\d+)")] private static partial Regex UserIdRegex();
    [GeneratedRegex(@"\[(\d+)\]")] private static partial Regex BracketNumberRegex();
    [GeneratedRegex(@"Stand:\s*</b>\s*([^<]+)")] private static partial Regex CommentStateRegex();
    [GeneratedRegex(@"^/(watch|chapter)/(\d+)/(\d+)/([a-z]+)")] private static partial Regex BookmarkLinkRegex();
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
