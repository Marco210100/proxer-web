using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Proxer.Api.Proxer;

/// <summary>Parsers for the smaller tabs of an entry, the extended search and the overview lists.</summary>
public static partial class MoreParsers
{
    private static readonly HtmlParser Html = new();

    /// <summary>Upload log of an entry: who added which episode in which language.</summary>
    public static EpisodeUpdate[] EntryUpdates(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("table#box-table-a tr").Select(row =>
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 5) return null;
            var user = cells[0].QuerySelector("a[href^='/user/']");
            var userId = UserIdRegex().Match(user?.GetAttribute("href") ?? "");
            var watch = WatchLinkRegex().Match(cells[4].QuerySelector("a")?.GetAttribute("href") ?? "");
            return new EpisodeUpdate(Clean(cells[0].TextContent), userId.Success ? int.Parse(userId.Groups[1].Value) : null,
                Clean(cells[1].TextContent), Clean(cells[2].TextContent), Clean(cells[3].TextContent),
                watch.Success ? int.Parse(watch.Groups[1].Value) : null, watch.Success ? watch.Groups[2].Value : null);
        }).OfType<EpisodeUpdate>().ToArray();
    }

    /// <summary>Forum topics and news that are attached to an entry.</summary>
    public static Discussion[] EntryDiscussions(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("table.details tr").Select(row =>
        {
            var cells = row.QuerySelectorAll("td");
            var topic = cells.Length < 4 ? null : cells[1].QuerySelector("a[href^='/forum/']");
            var ids = TopicLinkRegex().Match(topic?.GetAttribute("href") ?? "");
            if (topic is null || !ids.Success) return null;
            return new Discussion(int.Parse(ids.Groups[1].Value), int.Parse(ids.Groups[2].Value), Clean(topic.TextContent),
                int.TryParse(Clean(cells[0].TextContent), out var replies) ? replies : 0,
                Clean(cells[2].TextContent),
                Clean(cells[1].QuerySelector("a[href^='/user/']")?.TextContent),
                Clean(cells[1].QuerySelectorAll("a[href^='/forum/']").Skip(1).FirstOrDefault()?.TextContent),
                Clean(cells[3].TextContent));
        }).OfType<Discussion>().ToArray();
    }

    public static GalleryPage Gallery(string html, int page)
    {
        var doc = Html.ParseDocument(html);
        var images = doc.QuerySelectorAll(".galgrid").Select(cell =>
        {
            var image = UrlInStyleRegex().Match(cell.GetAttribute("style") ?? "");
            var link = cell.QuerySelector("a.gallink");
            var id = (cell.Id ?? "").Replace("entry", "");
            return !image.Success || id.Length == 0 ? null
                : new GalleryImage(id, Clean(link?.GetAttribute("title")), Absolute(image.Groups[1].Value.Trim('\'', '"')));
        }).OfType<GalleryImage>().ToArray();
        return new GalleryPage(images, page, doc.QuerySelectorAll("a.menu").Any(a => a.TextContent.Trim() == ">"));
    }

    /// <summary>The user's own watch history for one entry.</summary>
    public static HistoryItem[] EntryHistory(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("table#box-table-a tr").Select(row =>
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 6) return null;
            var watch = WatchLinkRegex().Match(cells[5].QuerySelector("a")?.GetAttribute("href") ?? "");
            return new HistoryItem(Clean(cells[1].TextContent), Clean(cells[2].TextContent), Clean(cells[4].TextContent),
                watch.Success ? int.Parse(watch.Groups[1].Value) : null, watch.Success ? watch.Groups[2].Value : null);
        }).OfType<HistoryItem>().ToArray();
    }

    /// <summary>Merchandise and releases listed for an entry, grouped by kind.</summary>
    public static ArticleSection[] EntryArticles(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll(".articlesection").Select(section =>
        {
            var items = section.QuerySelectorAll(".article").Select(article =>
            {
                var link = article.QuerySelector("a[href^='/article?id=']");
                var id = QueryIdRegex().Match(link?.GetAttribute("href") ?? "");
                var image = UrlInStyleRegex().Match(article.QuerySelector(".articleimg")?.GetAttribute("style") ?? "");
                return link is null || !id.Success ? null
                    : new ArticleItem(id.Groups[1].Value, Clean(link.GetAttribute("title")), image.Success ? Absolute(image.Groups[1].Value.Trim('\'', '"')) : null);
            }).OfType<ArticleItem>().ToArray();
            return new ArticleSection(Clean(section.QuerySelector(".articlehead")?.TextContent), items);
        }).Where(s => s.Items.Length > 0).ToArray();
    }

    /// <summary>The genres offered by Proxer's extended search, with the ids its search expects.</summary>
    public static SearchGenre[] SearchGenres(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("input.genre_g").Select(input =>
        {
            var label = input.ParentElement?.QuerySelector("span");
            return !int.TryParse(input.GetAttribute("value"), out var id) || label is null ? null
                : new SearchGenre(id, Clean(label.TextContent), label.GetAttribute("title") is { Length: > 0 } title ? Clean(title) : null);
        }).OfType<SearchGenre>().ToArray();
    }

    /// <summary>Overview of characters, persons, companies or groups, either the newest ones or one letter.</summary>
    public static OverviewItem[] Overview(string html, string kind)
    {
        var doc = Html.ParseDocument(html);
        var prefix = $"/{kind}?id=";
        var items = new List<OverviewItem>();

        // Lettered pages use portrait cells with the works listed underneath.
        foreach (var cell in doc.QuerySelectorAll(".pGridCell"))
        {
            var link = cell.QuerySelector($".pGridTextBig a[href^='{prefix}']") ?? cell.QuerySelector($"a[href^='{prefix}']");
            var id = QueryIdRegex().Match(link?.GetAttribute("href") ?? "");
            if (link is null || !id.Success) continue;
            var image = UrlInStyleRegex().Match(cell.QuerySelector(".pGridImageLink")?.GetAttribute("style") ?? "");
            var name = Clean(link.TextContent);
            if (name.Length == 0) name = Clean(cell.QuerySelector(".pGridTextBig")?.TextContent);
            items.Add(new OverviewItem(int.Parse(id.Groups[1].Value), name,
                image.Success ? Absolute(image.Groups[1].Value.Trim('\'', '"')) : null,
                cell.QuerySelectorAll(".pGridTextSmall p").Select(p => Clean(p.TextContent)).Where(t => t.Length > 0).Take(4).ToArray()));
        }
        if (items.Count > 0) return items.DistinctBy(i => i.Id).ToArray();

        // The start page of each overview only shows thumbnails of the newest entries.
        foreach (var link in doc.QuerySelectorAll($"a[href^='{prefix}']"))
        {
            var id = QueryIdRegex().Match(link.GetAttribute("href") ?? "");
            if (!id.Success) continue;
            var image = link.QuerySelector("img")?.GetAttribute("src");
            items.Add(new OverviewItem(int.Parse(id.Groups[1].Value), Clean(link.TextContent),
                string.IsNullOrEmpty(image) ? null : Absolute(image), []));
        }
        return items.DistinctBy(i => i.Id).ToArray();
    }

    private static string Absolute(string url) =>
        url.StartsWith("//") ? "https:" + url : url.StartsWith('/') ? "https://proxer.me" + url : url;

    private static string Clean(string? text) => text is null ? "" : WhitespaceRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"[?&]id=(\d+)")] private static partial Regex QueryIdRegex();
    [GeneratedRegex(@"/user/(\d+)")] private static partial Regex UserIdRegex();
    [GeneratedRegex(@"url\(([^)]+)\)")] private static partial Regex UrlInStyleRegex();
    [GeneratedRegex(@"/(?:watch|chapter)/\d+/(\d+)/([a-z]+)")] private static partial Regex WatchLinkRegex();
    [GeneratedRegex(@"^/forum/(\d+)[^/]*/(\d+)")] private static partial Regex TopicLinkRegex();
}
