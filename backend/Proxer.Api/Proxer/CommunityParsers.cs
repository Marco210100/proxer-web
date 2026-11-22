using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Proxer.Api.Proxer;

/// <summary>Parsers for the community side of Proxer: notifications, messages, forum and profiles.</summary>
public static partial class CommunityParsers
{
    private static readonly HtmlParser Html = new();

    /// <summary>Proxer answers the counter as <c>error#messages#messages#requests#news#other</c>.</summary>
    public static NotificationCounts Counts(string text)
    {
        var parts = text.Trim().Split('#').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        return parts.Length < 6 || parts[0] != 0
            ? new NotificationCounts(0, 0, 0, 0)
            : new NotificationCounts(parts[1] + parts[2], parts[3], parts[4], parts[5]);
    }

    public static NotificationPage Notifications(string html, int page)
    {
        var doc = Html.ParseDocument(html);
        var items = doc.QuerySelectorAll("a.notificationMainList").Select(a =>
        {
            var date = Clean(a.QuerySelector(".nMainDate")?.TextContent);
            a.QuerySelector(".nMainDate")?.Remove();
            var (link, external) = Links(a.GetAttribute("href"));
            return new Notification((a.Id ?? "").Replace("notification", ""), Clean(a.TextContent), date, link, external);
        }).Where(n => n.Id.Length > 0).ToArray();
        return new NotificationPage(items, page, doc.QuerySelectorAll("a.menu").Any(a => a.TextContent.Trim() == ">"));
    }

    public static Conversation[] Conversations(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("a.conferenceGrid").Select(a =>
        {
            var id = QueryIdRegex().Match(a.GetAttribute("href") ?? "");
            var lines = a.QuerySelectorAll("div").Select(d => Clean(d.TextContent)).ToArray();
            if (!id.Success || lines.Length < 2) return null;
            // Read conversations carry only the base class; anything extra marks new messages.
            var unread = a.ClassList.Any(c => c != "conferenceGrid");
            return new Conversation(int.Parse(id.Groups[1].Value), lines[0], lines[1], Absolute(a.QuerySelector("img")?.GetAttribute("src")), unread);
        }).OfType<Conversation>().ToArray();
    }

    public static Participant[] Participants(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("#conferenceUsers .message_item").Select(item =>
        {
            var link = item.QuerySelector("a[href^='/user/']");
            var id = UserIdRegex().Match(link?.GetAttribute("href") ?? "");
            return !id.Success ? null
                : new Participant(int.Parse(id.Groups[1].Value), Clean(link!.TextContent), Absolute(item.QuerySelector("img")?.GetAttribute("src")));
        }).OfType<Participant>().ToArray();
    }

    /// <summary>Messages come newest first, 15 per page.</summary>
    public static (Message[] Messages, bool HasMore) Messages(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (Str(root, "error") is not ("0" or "" or "false"))
            throw new ProxerException(Str(root, "msg") is { Length: > 0 } msg ? msg : "Die Nachrichten konnten nicht geladen werden.");
        var me = Str(root, "uid");
        if (!root.TryGetProperty("messages", out var list) || list.ValueKind != JsonValueKind.Array) return ([], false);
        var messages = list.EnumerateArray().Select(m => new Message(
            Str(m, "id"),
            int.TryParse(Str(m, "fromid"), out var from) ? from : 0,
            Str(m, "username"),
            Str(m, "avatar") is { Length: > 0 } avatar ? $"https://cdn.proxer.me/avatar/tn/{avatar}" : null,
            Str(m, "message"),
            long.TryParse(Str(m, "timestamp"), out var time) ? time : 0,
            System: Str(m, "action").Length > 0,
            Mine: Str(m, "fromid") == me)).ToArray();
        return (messages, messages.Length >= 15);
    }

    public static ForumCategory[] ForumCategories(string html)
    {
        var doc = Html.ParseDocument(html);
        return doc.QuerySelectorAll("select#catid option").Select(option =>
        {
            if (!int.TryParse(option.GetAttribute("value"), out var id) || id == 0) return null;
            var label = option.TextContent.Replace(' ', ' ').Trim();
            var depth = 0;
            while (label.StartsWith("- ")) { depth++; label = label[2..]; }
            return new ForumCategory(id, label.Trim(), depth);
        }).OfType<ForumCategory>().ToArray();
    }

    public static ForumTopicPage ForumTopics(string html, int categoryId, int page)
    {
        var doc = Html.ParseDocument(html);
        var topics = new List<ForumTopic>();
        foreach (var row in doc.QuerySelectorAll("tr[class*=krow]"))
        {
            var title = row.QuerySelector("a.topictitle");
            var id = TopicIdRegex().Match(title?.GetAttribute("href") ?? "");
            if (title is null || !id.Success) continue;
            var lastUser = row.QuerySelector(".ktopic-latest-post a[href^='/user/']");
            topics.Add(new ForumTopic(
                Id: int.Parse(id.Groups[1].Value),
                CategoryId: categoryId,
                Title: Clean(title.TextContent),
                Excerpt: title.GetAttribute("title") is { Length: > 0 } excerpt ? Clean(excerpt) : null,
                Replies: int.TryParse(row.QuerySelector(".kcol-ktopicreplies strong")?.TextContent.Trim(), out var replies) ? replies : 0,
                Views: Clean(row.QuerySelector(".ktopic-views-number")?.TextContent),
                Author: Clean(row.QuerySelector(".kcol-ktopictitle div.hidden-phone a[href^='/user/']")?.TextContent),
                LastPostBy: Clean(lastUser?.TextContent),
                LastPostAge: Clean(row.QuerySelector(".ktopic-date")?.TextContent),
                LastAvatar: Absolute(row.QuerySelector(".ktopic-latest-post-avatar img")?.GetAttribute("src")),
                Sticky: (row.ClassName ?? "").Contains("sticky")));
        }
        var name = (doc.Title ?? "").Split(" - Proxer.Me")[0].Trim();
        // The category's own pager is the one that starts with the "Seite:" label.
        var pager = doc.QuerySelectorAll("ul.kpagination").FirstOrDefault(p => p.QuerySelector("li.page") is not null);
        return new ForumTopicPage(categoryId, name, topics.ToArray(), page, PageCount(pager));
    }

    public static ForumThread ForumThread(string html, int categoryId, int topicId, int page)
    {
        var doc = Html.ParseDocument(html);
        var posts = new List<ForumPost>();
        foreach (var table in doc.QuerySelectorAll("table.kmsg"))
        {
            var header = table.PreviousElementSibling;
            var body = table.QuerySelector(".kmsgtext");
            if (body is null) continue;
            var user = table.QuerySelector(".kpost-username a") ?? table.QuerySelector(".kpost-username");
            var userId = UserIdRegex().Match(user?.GetAttribute("href") ?? "");
            var date = header?.QuerySelector(".kmsgdate");
            posts.Add(new ForumPost(
                Id: Clean(header?.QuerySelector("[class^=kmsg-id] a")?.TextContent).TrimStart('#'),
                UserId: userId.Success ? int.Parse(userId.Groups[1].Value) : null,
                Username: Clean(user?.TextContent),
                Avatar: Absolute(table.QuerySelector(".kpost-profile img.img-polaroid")?.GetAttribute("src")),
                Date: date?.GetAttribute("title") ?? "",
                Age: Clean(date?.TextContent),
                Html: Sanitize(body)));
        }
        var title = Clean(doc.QuerySelector(".kheader h1 span")?.TextContent);
        if (title.Length == 0) title = (doc.Title ?? "").Split(" - Proxer.Me")[0].Trim();
        return new ForumThread(categoryId, topicId, title, posts.ToArray(), page, PageCount(doc.QuerySelector("ul.kpagination")));
    }

    private static int PageCount(IElement? pager) =>
        pager?.QuerySelectorAll("a[title], span.pagenav")
            .Select(e => int.TryParse(e.GetAttribute("title") ?? e.TextContent, out var n) ? n : 1)
            .DefaultIfEmpty(1).Max() ?? 1;

    public static UserProfile Profile(string overviewHtml, string aboutHtml, int id)
    {
        var doc = Html.ParseDocument(overviewHtml);
        var name = Clean(doc.QuerySelector("#simple-navi a")?.TextContent).Replace("Profil:", "").Trim();
        if (name.Length == 0) throw new ProxerException("Profil nicht gefunden.", 404);

        var facts = new List<Fact>();
        void AddRows(IParentNode root)
        {
            foreach (var row in root.QuerySelectorAll("table.profile tr"))
            {
                var cells = row.Children.Where(c => c.LocalName == "td").ToArray();
                var label = Clean(cells.FirstOrDefault()?.QuerySelector("b")?.TextContent);
                if (cells.Length != 2 || label.Length == 0) continue;
                var value = Clean(cells[1].TextContent).Replace("[?]", "").Trim();
                if (value.Length > 0) facts.Add(new Fact(label, [value]));
            }
        }
        AddRows(doc);
        var about = Html.ParseDocument(aboutHtml);
        AddRows(about);
        // The free text sits in the row after the "About" heading.
        var aboutText = about.QuerySelectorAll("table.profile tr")
            .SkipWhile(r => Clean(r.TextContent) != "About").Skip(1).Select(r => Clean(r.TextContent)).FirstOrDefault();
        if (!string.IsNullOrEmpty(aboutText)) facts.Add(new Fact("Über mich", [aboutText]));

        return new UserProfile(id, name,
            Absolute(doc.QuerySelectorAll("img").Select(i => i.GetAttribute("src")).FirstOrDefault(s => s is not null && s.Contains("/avatar/"))),
            Clean(doc.QuerySelector("#whatrustatus b")?.TextContent) is { Length: > 0 } status ? status : null,
            facts.ToArray());
    }

    /// <summary>Maps a Proxer address to a route of this app where one exists.</summary>
    public static (string? Link, string? External) Links(string? href)
    {
        if (string.IsNullOrEmpty(href) || href.StartsWith("javascript")) return (null, null);
        var path = href.Split('#')[0];
        var external = path.StartsWith('/') ? "https://proxer.me" + path : path;
        string? link = null;
        if (Regex.Match(path, @"^/info/(\d+)") is { Success: true } info) link = $"/eintrag/{info.Groups[1].Value}";
        else if (Regex.Match(path, @"^/watch/(\d+)/(\d+)/([a-z]+)") is { Success: true } watch) link = watch.Value;
        else if (Regex.Match(path, @"^/forum/(\d+)[^/]*/(\d+)") is { Success: true } topic) link = $"/forum/{topic.Groups[1].Value}/{topic.Groups[2].Value}";
        else if (Regex.Match(path, @"^/forum/(\d+)[^/]*$") is { Success: true } category) link = $"/forum/{category.Groups[1].Value}";
        else if (Regex.Match(path, @"^/user/(\d+)") is { Success: true } user) link = $"/user/{user.Groups[1].Value}";
        else if (Regex.Match(path, @"^/messages\?id=(\d+)") is { Success: true } conversation) link = $"/nachrichten/{conversation.Groups[1].Value}";
        return (link, external);
    }

    private static readonly HashSet<string> Inline = ["b", "strong", "i", "em", "u", "s", "strike", "code", "pre", "blockquote", "ul", "ol", "li", "p", "sub", "sup", "h3", "h4"];
    private static readonly Dictionary<string, string> Renamed = new() { ["h1"] = "h3", ["h2"] = "h3" };
    private static readonly HashSet<string> Dropped = ["script", "style", "form", "input", "textarea", "select", "button", "iframe", "object", "embed", "video", "audio", "svg", "link", "meta"];

    /// <summary>
    /// Rebuilds user-written forum markup from a fixed whitelist. Nothing from the source is copied
    /// as markup: tags are re-emitted by name, text is encoded, and only http(s) addresses survive.
    /// </summary>
    public static string Sanitize(INode root)
    {
        var output = new StringBuilder();
        Emit(root, output);
        return Regex.Replace(output.ToString(), @"(<br>\s*){3,}", "<br><br>").Trim();
    }

    private static void Emit(INode node, StringBuilder output)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text) { output.Append(WebUtility.HtmlEncode(text.Data)); continue; }
            if (child is not IElement element) continue;
            var tag = element.LocalName;

            if (Dropped.Contains(tag)) continue;
            if (tag is "br" or "hr") { output.Append('<').Append(tag).Append('>'); continue; }
            if (element.ClassList.Contains("kspoiler"))
            {
                var title = Clean(element.QuerySelector(".kspoiler-title")?.TextContent);
                output.Append("<details><summary>").Append(WebUtility.HtmlEncode(title.Length > 0 ? title : "Spoiler")).Append("</summary>");
                if (element.QuerySelector(".kspoiler-content") is { } content) Emit(content, output);
                output.Append("</details>");
                continue;
            }
            if (element.ClassList.Contains("kmsgtext-quote")) { Wrap("blockquote"); continue; }
            if (tag == "a")
            {
                var href = SafeUrl(element.GetAttribute("href"));
                if (href is null) { Emit(element, output); continue; }
                output.Append("<a href=\"").Append(WebUtility.HtmlEncode(href)).Append("\" target=\"_blank\" rel=\"noreferrer nofollow\">");
                Emit(element, output);
                output.Append("</a>");
                continue;
            }
            if (tag == "img")
            {
                if (SafeUrl(element.GetAttribute("src")) is { } src)
                    output.Append("<img src=\"").Append(WebUtility.HtmlEncode(src)).Append("\" loading=\"lazy\" referrerpolicy=\"no-referrer\" alt=\"\">");
                continue;
            }
            if (Inline.Contains(tag)) { Wrap(tag); continue; }
            if (Renamed.TryGetValue(tag, out var renamed)) { Wrap(renamed); continue; }

            // Unknown containers contribute their content only; block-level ones end with a line break.
            Emit(element, output);
            if (tag is "div" or "table" or "tr") output.Append("<br>");

            void Wrap(string name)
            {
                output.Append('<').Append(name).Append('>');
                Emit(element, output);
                output.Append("</").Append(name).Append('>');
            }
        }
    }

    private static string? SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (url.StartsWith("//")) url = "https:" + url;
        else if (url.StartsWith('/')) url = "https://proxer.me" + url;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    }

    private static string? Absolute(string? url) => string.IsNullOrEmpty(url) ? null : SafeUrl(url);

    private static string Clean(string? text) => text is null ? "" : WhitespaceRegex().Replace(text, " ").Trim();

    private static string Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => "",
            }
            : "";

    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"[?&]id=(\d+)")] private static partial Regex QueryIdRegex();
    [GeneratedRegex(@"/user/(\d+)")] private static partial Regex UserIdRegex();
    [GeneratedRegex(@"/forum/[^/]+/(\d+)")] private static partial Regex TopicIdRegex();
}
