namespace Proxer.Api.Proxer;

public record SessionInfo(bool LoggedIn, string? Username, int? UserId = null);

public record NewsItem(string Id, string Title, string Text, string? Image, string Date, string Author, string? Category);

public record EntrySummary(int Id, string Title, string[] Genres, string Type, string Status, string[] Languages,
    double? Rating, int? Votes, string Cover);

public record EntryPage(EntrySummary[] Entries, int Page, bool HasNext);

public record SeasonGroup(string Title, EntrySummary[] Entries);

public record SeasonPage(int Year, int Season, SeasonGroup[] Groups);

public record CalendarEntry(int EntryId, string Title, string Cover, int? Episode, string? Language,
    long? AirTime, long? UploadTime, string[] Genres);

public record CalendarDay(string Date, CalendarEntry[] Entries);

public record Fact(string Label, string[] Values);

public record Genre(string Name, string? Description);

public record PersonRef(int Id, string Name, string Role, string? Image);

public record EntryDetails(int Id, string Title, string Type, string Cover, double? Rating, int? Votes,
    Genre[] Genres, string Description, Fact[] Facts, string[] Tabs, bool Ongoing,
    Genre[] Tags, PersonRef[] Persons, PersonRef[] Characters, FactLink[] Links);

public record RelatedEntry(EntrySummary Entry, string Year, string Relation);

public record Recommendation(EntrySummary Entry, int Up, int Down);

public record CommentPart(bool Spoiler, string Text);

public record CommentRating(string Category, int Stars);

public record Comment(string Id, int? UserId, string Username, string? Avatar, CommentPart[] Content,
    CommentRating[] Ratings, int? Rating, string State, int Likes);

public record CommentPage(Comment[] Comments, int Page, bool HasNext);

public record Song(int Number, string Title, string Length);

public record Album(string Name, string? ReleaseDate, string? Cover, Song[] Songs);

public record EpisodeLanguage(string Language, string[] Hosters);

public record Episode(int Number, string? Title, EpisodeLanguage[] Languages);

public record EpisodePage(string Category, int Start, int End, string[] Languages, Episode[] Episodes,
    int Page, int PageCount);

public record StreamInfo(string Id, string Name, string Type, string HosterType, string? EmbedUrl,
    string? Uploader, string? TranslatorGroup);

public record WatchInfo(int EntryId, string Title, int Episode, int? EpisodeCount, string Language,
    StreamInfo[] Streams);

public record ListProgress(int EntryId, int State, int Episode, bool Subscribed);

/// <param name="Cid">Id of the list entry itself (Proxer calls it the comment id); needed to change it.</param>
/// <param name="State">0 finished, 1 in progress, 2 planned, 3 dropped.</param>
public record MyListItem(int Id, string Cid, string Name, string Medium, int Episodes, int Episode, int State,
    int Rating, string Cover);

public record Bookmark(string Id, int EntryId, string Title, string Category, int Episode, string Language,
    string Medium, bool Available, string Cover);

public record ActionResult(bool Ok, string Message);

public record NotificationCounts(int Messages, int Requests, int News, int Other);

/// <param name="Link">Route inside this app when the target exists here, otherwise null.</param>
/// <param name="External">The original address on proxer.me.</param>
public record Notification(string Id, string Text, string Date, string? Link, string? External);

public record NotificationPage(Notification[] Items, int Page, bool HasNext);

public record Conversation(int Id, string Name, string Date, string? Avatar, bool Unread);

public record Participant(int Id, string Name, string? Avatar);

public record Message(string Id, int FromId, string Username, string? Avatar, string Text, long Timestamp, bool System, bool Mine);

public record ConversationPage(int Id, Participant[] Participants, Message[] Messages, int Page, bool HasMore);

public record ForumCategory(int Id, string Name, int Depth);

public record ForumTopic(int Id, int CategoryId, string Title, string? Excerpt, int Replies, string Views, string Author,
    string LastPostBy, string LastPostAge, string? LastAvatar, bool Sticky);

public record ForumTopicPage(int CategoryId, string Title, ForumTopic[] Topics, int Page, int PageCount);

/// <param name="Html">Post body reduced to a small set of harmless tags; safe to insert as markup.</param>
public record ForumPost(string Id, int? UserId, string Username, string? Avatar, string Date, string Age, string Html);

public record ForumThread(int CategoryId, int TopicId, string Title, ForumPost[] Posts, int Page, int PageCount);

public record UserProfile(int Id, string Name, string? Avatar, string? StatusText, Fact[] Facts);

public record LinkedEntry(int Id, string Title, string Medium, string Cover);

/// <summary>A character, person, company or translator group, all of which Proxer lays out alike.</summary>
public record InfoPage(string Kind, int Id, string Title, string? Image, Fact[] Facts, string BodyHtml,
    LinkedEntry[] Entries, PersonRef[] People);

public record FactLink(string Kind, int Id, string Name, string Role);

public record UpdateItem(int EntryId, string Title, string Language, string Medium, string Uploader, string Date, string Cover);

public record EpisodeUpdate(string Uploader, int? UploaderId, string Title, string Language, string Date, int? Episode, string? LanguageCode);

public record Discussion(int CategoryId, int TopicId, string Title, int Replies, string Views, string Author, string Category, string LastPost);

public record GalleryImage(string Id, string Title, string Image);

public record GalleryPage(GalleryImage[] Images, int Page, bool HasNext);

public record HistoryItem(string Episode, string Language, string Date, int? EpisodeNumber, string? LanguageCode);

public record ArticleItem(string Id, string Title, string? Image);

public record ArticleSection(string Title, ArticleItem[] Items);

public record SearchGenre(int Id, string Name, string? Description);

public record OverviewItem(int Id, string Name, string? Image, string[] Works);
