namespace Proxer.Api.Proxer;

public record SessionInfo(bool LoggedIn, string? Username);

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

public record EntryDetails(int Id, string Title, string Type, string Cover, double? Rating, int? Votes,
    Genre[] Genres, string Description, Fact[] Facts, string[] Tabs, bool Ongoing);

public record EpisodeLanguage(string Language, string[] Hosters);

public record Episode(int Number, string? Title, EpisodeLanguage[] Languages);

public record EpisodePage(string Category, int Start, int End, string[] Languages, Episode[] Episodes,
    int Page, int PageCount);

public record StreamInfo(string Id, string Name, string Type, string HosterType, string? EmbedUrl,
    string? Uploader, string? TranslatorGroup);

public record WatchInfo(int EntryId, string Title, int Episode, int? EpisodeCount, string Language,
    StreamInfo[] Streams);

public record ListProgress(int EntryId, int State, int Episode);

public record MyListItem(int Id, string Name, string Medium, int Episodes, int Episode, int State,
    int Rating, string Cover);
