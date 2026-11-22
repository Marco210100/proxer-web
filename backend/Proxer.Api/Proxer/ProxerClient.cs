using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Proxer.Api.Proxer;

public class ProxerException(string message, int status = 502) : Exception(message)
{
    public int Status { get; } = status;
}

public record ProxerOptions(string Username, string Password, string SessionFile);

/// <summary>
/// Single-user client for proxer.me: holds the session, throttles upstream traffic and caches pages.
/// </summary>
public partial class ProxerClient
{
    private const string BaseUrl = "https://proxer.me";
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan SessionCheckInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MinRefreshAge = TimeSpan.FromSeconds(30);

    private readonly ProxerOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProxerClient> _log;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _fetchedAt = new();
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private DateTime _lastRequest = DateTime.MinValue;
    private DateTime _sessionCheckedAt = DateTime.MinValue;
    private DateTime _lastLoginAttempt = DateTime.MinValue;
    private string? _username;

    public ProxerClient(ProxerOptions options, IMemoryCache cache, ILogger<ProxerClient> log)
    {
        _options = options;
        _cache = cache;
        _log = log;
        _http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All,
        })
        { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE,de;q=0.9");
        LoadCookies();
    }

    public async Task<SessionInfo> GetSessionAsync(CancellationToken ct)
    {
        await EnsureSessionAsync(ct);
        return new SessionInfo(_username is not null, _username);
    }

    /// <summary>Fetches a page (content fragment when the path carries format=raw), cached for <paramref name="ttl"/>.</summary>
    /// <param name="refresh">Skips the cache, unless the cached copy is younger than <see cref="MinRefreshAge"/>.</param>
    public async Task<string> GetPageAsync(string path, TimeSpan ttl, CancellationToken ct, bool refresh = false)
    {
        var bypass = refresh && (!_fetchedAt.TryGetValue(path, out var fetchedAt) || DateTime.UtcNow - fetchedAt > MinRefreshAge);
        if (!bypass && ttl > TimeSpan.Zero && _cache.TryGetValue(path, out string? cached) && cached is not null)
            return cached;

        await EnsureSessionAsync(ct);
        var (body, finalPath) = await SendAsync(HttpMethod.Get, path, null, ct);

        // Proxer answers with its 404 page when the session is gone; verify once and retry.
        if (finalPath.StartsWith("/404") && DateTime.UtcNow - _sessionCheckedAt > TimeSpan.FromSeconds(30))
        {
            await EnsureSessionAsync(ct, force: true);
            (body, finalPath) = await SendAsync(HttpMethod.Get, path, null, ct);
        }
        if (finalPath.StartsWith("/404"))
            throw new ProxerException("Seite auf Proxer nicht gefunden.", 404);

        if (ttl > TimeSpan.Zero)
        {
            _cache.Set(path, body, ttl);
            _fetchedAt[path] = DateTime.UtcNow;
        }
        return body;
    }

    private async Task EnsureSessionAsync(CancellationToken ct, bool force = false)
    {
        if (!force && DateTime.UtcNow - _sessionCheckedAt < SessionCheckInterval) return;
        await _sessionGate.WaitAsync(ct);
        try
        {
            if (!force && DateTime.UtcNow - _sessionCheckedAt < SessionCheckInterval) return;

            var (home, _) = await SendAsync(HttpMethod.Get, "/", null, ct);
            if (!IsLoggedIn(home))
            {
                // Never hammer the login form: at most one attempt per minute.
                if (DateTime.UtcNow - _lastLoginAttempt < TimeSpan.FromMinutes(1))
                    throw new ProxerException("Login bei Proxer fehlgeschlagen. Zugangsdaten in .env.local prüfen.", 401);
                _lastLoginAttempt = DateTime.UtcNow;
                home = await LoginAsync(home, ct);
            }
            _username = WelcomeRegex().Match(home) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : _options.Username;
            _sessionCheckedAt = DateTime.UtcNow;
            SaveCookies();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private async Task<string> LoginAsync(string anyPage, CancellationToken ct)
    {
        var token = LoginTokenRegex().Match(anyPage);
        if (!token.Success) throw new ProxerException("Login-Formular von Proxer nicht erkannt.");

        _log.LogInformation("Logging in to Proxer as configured user");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _options.Username,
            ["password"] = _options.Password,
            ["remember"] = "1",
            ["submit"] = "Login",
            ["redirect"] = "",
        });
        await SendAsync(HttpMethod.Post, $"/login?{token.Groups[1].Value}=1", form, ct);
        var (home, _) = await SendAsync(HttpMethod.Get, "/", null, ct);
        if (!IsLoggedIn(home))
            throw new ProxerException("Login bei Proxer fehlgeschlagen. Zugangsdaten in .env.local prüfen.", 401);
        return home;
    }

    private static bool IsLoggedIn(string fullPage) => fullPage.Contains("mod_logout_submit");

    private async Task<(string Body, string FinalPath)> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = MinInterval - (DateTime.UtcNow - _lastRequest);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

            using var request = new HttpRequestMessage(method, BaseUrl + path) { Content = content };
            request.Headers.Referrer = new Uri(BaseUrl + "/");
            using var response = await _http.SendAsync(request, ct);
            _lastRequest = DateTime.UtcNow;
            _log.LogInformation("Proxer {Method} {Path} -> {Status}", method, path.Split('?')[0], (int)response.StatusCode);

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.Forbidden)
                throw new ProxerException($"Proxer antwortet mit {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync(ct);
            return (body, response.RequestMessage?.RequestUri?.AbsolutePath ?? path);
        }
        catch (HttpRequestException e)
        {
            throw new ProxerException("Proxer ist nicht erreichbar: " + e.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private record StoredCookie(string Name, string Value, string Domain, string Path, DateTime Expires, bool Secure, bool HttpOnly);

    private void LoadCookies()
    {
        try
        {
            if (!File.Exists(_options.SessionFile)) return;
            var stored = JsonSerializer.Deserialize<StoredCookie[]>(File.ReadAllText(_options.SessionFile)) ?? [];
            foreach (var c in stored)
                _cookies.Add(new Cookie(c.Name, c.Value, c.Path, c.Domain) { Expires = c.Expires, Secure = c.Secure, HttpOnly = c.HttpOnly });
        }
        catch (Exception e)
        {
            _log.LogWarning("Stored session could not be read: {Message}", e.Message);
        }
    }

    private void SaveCookies()
    {
        try
        {
            var stored = _cookies.GetAllCookies().Select(c => new StoredCookie(c.Name, c.Value, c.Domain, c.Path, c.Expires, c.Secure, c.HttpOnly));
            File.WriteAllText(_options.SessionFile, JsonSerializer.Serialize(stored));
        }
        catch (Exception e)
        {
            _log.LogWarning("Session could not be stored: {Message}", e.Message);
        }
    }

    [GeneratedRegex(@"action=""/login\?([0-9a-f]{32})=1""")]
    private static partial Regex LoginTokenRegex();

    [GeneratedRegex(@"class=""start_welcome"">Willkommen zur(?:ü|&uuml;)ck, (.+?)!</div>")]
    private static partial Regex WelcomeRegex();
}
