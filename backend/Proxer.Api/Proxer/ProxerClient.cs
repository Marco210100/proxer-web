using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Proxer.Api.Proxer;

public class ProxerException(string message, int status = 502) : Exception(message)
{
    public int Status { get; } = status;
}

public record ProxerOptions(string SessionFile);

/// <summary>
/// Single-user client for proxer.me: holds the session, throttles upstream traffic and caches pages.
/// </summary>
public partial class ProxerClient
{
    private const string BaseUrl = "https://proxer.me";
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan SessionCheckInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MinRefreshAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinLoginInterval = TimeSpan.FromSeconds(10);

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
    private string? _token;
    private int? _userId;

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
        // Present a complete, consistent Chrome-on-Windows header set so ordinary requests are not
        // flagged as an automated client. This does NOT touch Proxer's captcha itself: if one is
        // shown at login, a human still has to solve it.
        var headers = _http.DefaultRequestHeaders;
        headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        headers.AcceptLanguage.ParseAdd("de-DE,de;q=0.9,en;q=0.8");
        headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
        headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        LoadCookies();
    }

    /// <summary>Reports whether the stored session is still accepted by Proxer.</summary>
    public async Task<SessionInfo> GetSessionAsync(CancellationToken ct)
    {
        await CheckSessionAsync(ct);
        return new SessionInfo(_username is not null, _username, _userId);
    }

    /// <summary>
    /// Logs in with the given credentials. Only the resulting session cookie is kept; the password is never stored.
    /// </summary>
    public async Task<SessionInfo> LoginAsync(string username, string password, string? secretKey, CancellationToken ct)
    {
        await _sessionGate.WaitAsync(ct);
        try
        {
            // Never hammer the login form, whatever the frontend does.
            if (DateTime.UtcNow - _lastLoginAttempt < MinLoginInterval)
                throw new ProxerException("Bitte warte ein paar Sekunden vor dem nächsten Login-Versuch.", 429);
            _lastLoginAttempt = DateTime.UtcNow;

            var (loginPage, _) = await SendAsync(HttpMethod.Get, "/login", null, ct);
            var token = LoginTokenRegex().Match(loginPage);
            if (!token.Success) throw new ProxerException("Login-Formular von Proxer nicht erkannt.");

            var fields = new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
                ["remember"] = "1",
                ["submit"] = "Login",
                ["redirect"] = "",
            };
            if (!string.IsNullOrWhiteSpace(secretKey)) fields["secretkey"] = secretKey.Trim();

            _log.LogInformation("Logging in to Proxer");
            var (answer, _) = await SendAsync(HttpMethod.Post, $"/login?{token.Groups[1].Value}=1", new FormUrlEncodedContent(fields), ct);
            var (home, _) = await SendAsync(HttpMethod.Get, "/", null, ct);
            ApplySessionState(home);
            if (_username is null)
            {
                // Pass on what Proxer itself says (wrong password, 2FA needed, temporarily locked, ...).
                // After several logins in a short time Proxer asks for a captcha, which only a person can solve.
                if (answer.Contains("Captcha Eingabe"))
                {
                    _log.LogWarning("Proxer asks for a captcha before login");
                    throw new ProxerException("Proxer verlangt vor dem Login ein Captcha (meist nach vielen Anmeldungen in kurzer Zeit). "
                        + "Öffne https://proxer.me/login in einem Browser auf dem Rechner, auf dem das Backend läuft, löse dort das Captcha "
                        + "und melde dich danach hier erneut an. Läuft das Backend in WSL, muss auch der Browser aus WSL gestartet werden; "
                        + "Details stehen in der README.", 403);
                }
                var reason = LoginMessage(answer);
                _log.LogWarning("Proxer rejected the login: {Reason}", reason ?? "(no message found)");
                throw new ProxerException(reason is null
                    ? "Login fehlgeschlagen. Bitte Benutzername, Passwort und ggf. den 2FA-Code prüfen."
                    : $"Login fehlgeschlagen. Proxer meldet: {reason}", 401);
            }

            ClearCache();
            return new SessionInfo(true, _username, _userId);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    /// <summary>Ends the session on Proxer (best effort) and forgets it locally.</summary>
    public async Task LogoutAsync(CancellationToken ct)
    {
        await _sessionGate.WaitAsync(ct);
        try
        {
            try
            {
                var (home, _) = await SendAsync(HttpMethod.Get, "/", null, ct);
                var token = ProxerTokenRegex().Match(home);
                if (IsLoggedIn(home) && token.Success)
                {
                    var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["check"] = "true" });
                    await SendAsync(HttpMethod.Post, $"/register?format=json&action=logout&{(token.Groups[1].Success ? token.Groups[1] : token.Groups[2]).Value}=1", form, ct);
                }
            }
            catch (ProxerException e)
            {
                _log.LogWarning("Logout on Proxer failed, dropping the session locally: {Message}", e.Message);
            }

            foreach (Cookie cookie in _cookies.GetAllCookies()) cookie.Expired = true;
            try { File.Delete(_options.SessionFile); } catch (IOException) { }
            _username = null;
            _sessionCheckedAt = DateTime.UtcNow;
            ClearCache();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    /// <summary>Fetches a page (content fragment when the path carries format=raw), cached for <paramref name="ttl"/>.</summary>
    /// <param name="refresh">Skips the cache, unless the cached copy is younger than <see cref="MinRefreshAge"/>.</param>
    public async Task<string> GetPageAsync(string path, TimeSpan ttl, CancellationToken ct, bool refresh = false)
    {
        await RequireSessionAsync(ct);

        var bypass = refresh && (!_fetchedAt.TryGetValue(path, out var fetchedAt) || DateTime.UtcNow - fetchedAt > MinRefreshAge);
        if (!bypass && ttl > TimeSpan.Zero && _cache.TryGetValue(path, out string? cached) && cached is not null)
            return cached;

        var (body, finalPath) = await SendAsync(HttpMethod.Get, path, null, ct);

        // Proxer answers with its 404 page when the session is gone; verify once and retry.
        if (finalPath.StartsWith("/404") && DateTime.UtcNow - _sessionCheckedAt > TimeSpan.FromSeconds(30))
        {
            await RequireSessionAsync(ct, force: true);
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

    /// <summary>
    /// Performs a write action the way Proxer's own pages do: a request carrying the session's security token.
    /// Cached pages starting with one of <paramref name="invalidate"/> are dropped afterwards.
    /// </summary>
    public Task<ActionResult> SendActionAsync(HttpMethod method, string path, string? checkField, CancellationToken ct, params string[] invalidate) =>
        SendFormActionAsync(method, path, checkField is null ? null : new Dictionary<string, string> { [checkField] = "1" }, ct, invalidate);

    public async Task<ActionResult> SendFormActionAsync(HttpMethod method, string path, Dictionary<string, string>? form, CancellationToken ct, params string[] invalidate)
    {
        var body = await SendWithTokenAsync(method, path, form, ct);
        var (ok, message) = Parsers.ActionAnswer(body);
        foreach (var prefix in invalidate) Invalidate(prefix);
        return new ActionResult(ok, message);
    }

    /// <summary>Reads one of Proxer's JSON endpoints that only answer with the session's security token.</summary>
    public Task<string> GetJsonAsync(string path, CancellationToken ct) => SendWithTokenAsync(HttpMethod.Get, path, null, ct);

    private async Task<string> SendWithTokenAsync(HttpMethod method, string path, Dictionary<string, string>? form, CancellationToken ct)
    {
        await RequireSessionAsync(ct);
        for (var attempt = 0; ; attempt++)
        {
            if (_token is null) await RequireSessionAsync(ct, force: true);
            if (_token is null) throw new ProxerException("Sicherheitstoken von Proxer nicht gefunden.");

            var url = $"{path}{(path.Contains('?') ? '&' : '?')}{_token}=1";
            var (body, _) = await SendAsync(method, url, form is null ? null : new FormUrlEncodedContent(form), ct);

            // The token changes with the session; fetch a fresh one once and try again.
            if (attempt == 0 && body.Length < 300 && (body.Contains("Sicherheitstoken") || body.Contains("Sicherheitscode")))
            {
                _token = null;
                continue;
            }
            return body;
        }
    }

    private void Invalidate(string prefix)
    {
        foreach (var key in _fetchedAt.Keys.Where(k => k.StartsWith(prefix)).ToArray())
        {
            _cache.Remove(key);
            _fetchedAt.TryRemove(key, out _);
        }
    }

    private async Task RequireSessionAsync(CancellationToken ct, bool force = false)
    {
        await CheckSessionAsync(ct, force);
        if (_username is null) throw new ProxerException("Nicht bei Proxer eingeloggt.", 401);
    }

    private async Task CheckSessionAsync(CancellationToken ct, bool force = false)
    {
        if (!force && DateTime.UtcNow - _sessionCheckedAt < SessionCheckInterval) return;
        await _sessionGate.WaitAsync(ct);
        try
        {
            if (!force && DateTime.UtcNow - _sessionCheckedAt < SessionCheckInterval) return;
            var (home, _) = await SendAsync(HttpMethod.Get, "/", null, ct);
            ApplySessionState(home);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private void ApplySessionState(string home)
    {
        _username = !IsLoggedIn(home) ? null
            : WelcomeRegex().Match(home) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : "Proxer-Nutzer";
        var token = ProxerTokenRegex().Match(home);
        _token = !token.Success ? null : (token.Groups[1].Success ? token.Groups[1] : token.Groups[2]).Value;
        _userId = _username is not null && OwnProfileRegex().Match(home) is { Success: true } profile ? int.Parse(profile.Groups[1].Value) : null;
        _sessionCheckedAt = DateTime.UtcNow;
        SaveCookies();
    }

    private void ClearCache()
    {
        // Cached pages belong to the account that fetched them.
        (_cache as MemoryCache)?.Clear();
        _fetchedAt.Clear();
    }

    private static string? LoginMessage(string html)
    {
        var doc = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        var text = doc.QuerySelector("#system-message, #system-message-container, #main .error, #main .message, #main .alert")?.TextContent;
        text = text is null ? null : Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > 300 ? text[..300] : text;
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
            // Browsers attach an Origin header to same-origin form posts such as the login.
            if (content is not null) request.Headers.TryAddWithoutValidation("Origin", BaseUrl);
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

    [GeneratedRegex(@"<input[^>]*value=""([0-9a-f]{32})""[^>]*id=""proxerToken""|<input[^>]*id=""proxerToken""[^>]*value=""([0-9a-f]{32})""")]
    private static partial Regex ProxerTokenRegex();

    [GeneratedRegex(@"href=""/user/(\d+)[^""]*""[^>]*>\s*Profil anzeigen")]
    private static partial Regex OwnProfileRegex();

    [GeneratedRegex(@"class=""start_welcome"">Willkommen zur(?:ü|&uuml;)ck, (.+?)!</div>")]
    private static partial Regex WelcomeRegex();
}
