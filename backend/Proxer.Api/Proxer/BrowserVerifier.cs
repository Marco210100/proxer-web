using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proxer.Api.Proxer;

/// <summary>
/// Opens a visible browser window that uses the app's own Proxer session, so the person can pass Proxer's
/// bot check for manga chapters themselves. Proxer remembers a passed check per session on its server,
/// so the app can read chapters afterwards. Nothing here solves or skips the check.
/// </summary>
public sealed class BrowserVerifier(ProxerClient proxer, ILogger<BrowserVerifier> log)
{
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task OpenAsync(string url, CancellationToken ct)
    {
        var browser = FindBrowser()
            ?? throw new ProxerException("Kein Chrome, Chromium oder Edge gefunden. Den Pfad zum Browser über die Umgebungsvariable PROXER_BROWSER angeben.", 501);
        var port = FreePort();
        var profile = Path.Combine(Path.GetTempPath(), "proxer-check-" + Guid.NewGuid().ToString("N"));

        var start = new ProcessStartInfo(browser) { UseShellExecute = false };
        foreach (var argument in new[] { $"--remote-debugging-port={port}", $"--user-data-dir={profile}", "--no-first-run",
                     "--no-default-browser-check", "--window-size=1100,850", "about:blank" })
            start.ArgumentList.Add(argument);
        var process = Process.Start(start) ?? throw new ProxerException("Der Browser ließ sich nicht starten.");
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            try { Directory.Delete(profile, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        };

        try
        {
            var socketUrl = await WaitForPageAsync(port, ct);
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(socketUrl), ct);
            var id = 0;

            async Task Send(string method, object parameters)
            {
                var messageId = ++id;
                await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { id = messageId, method, @params = parameters }, Json),
                    WebSocketMessageType.Text, true, ct);
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(buffer, ct);
                        message.Write(buffer, 0, received.Count);
                    } while (!received.EndOfMessage);
                    using var answer = JsonDocument.Parse(message.ToArray());
                    if (!answer.RootElement.TryGetProperty("id", out var answerId) || answerId.GetInt32() != messageId) continue;
                    if (answer.RootElement.TryGetProperty("error", out var error))
                        throw new ProxerException("Der Browser hat einen Befehl abgelehnt: " + error.GetRawText());
                    return;
                }
            }

            var cookies = proxer.ExportCookies().Select(c => new
            {
                name = c.Name,
                value = c.Value,
                url = "https://proxer.me/",
                path = string.IsNullOrEmpty(c.Path) ? "/" : c.Path,
                secure = c.Secure,
                httpOnly = c.HttpOnly,
                expires = c.Expires == DateTime.MinValue ? (long?)null : new DateTimeOffset(c.Expires.ToUniversalTime()).ToUnixTimeSeconds(),
            }).ToArray();
            await Send("Network.setCookies", new { cookies });
            await Send("Page.navigate", new { url });
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
            log.LogInformation("Opened a browser window for Proxer's chapter check");
        }
        catch
        {
            try { process.Kill(); } catch (InvalidOperationException) { }
            throw;
        }
    }

    private static async Task<string> WaitForPageAsync(int port, CancellationToken ct)
    {
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
        var lastError = "";
        for (var attempt = 0; attempt < 80; attempt++)
        {
            await Task.Delay(250, ct);
            try
            {
                using var targets = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json", ct));
                var page = targets.RootElement.EnumerateArray()
                    .FirstOrDefault(t => t.TryGetProperty("type", out var type) && type.GetString() == "page");
                if (page.ValueKind == JsonValueKind.Object && page.TryGetProperty("webSocketDebuggerUrl", out var socketUrl))
                    return socketUrl.GetString()!;
            }
            catch (HttpRequestException e) { lastError = e.Message; }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { lastError = "Zeitüberschreitung"; }
        }
        throw new ProxerException("Der Browser hat nicht rechtzeitig reagiert" + (lastError.Length > 0 ? $" ({lastError})." : "."));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>An installed Chrome, Chromium or Edge; Playwright's bundled Chromium as the last resort.</summary>
    private static string? FindBrowser()
    {
        if (Environment.GetEnvironmentVariable("PROXER_BROWSER") is { Length: > 0 } configured && File.Exists(configured)) return configured;

        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                var folder = Environment.GetFolderPath(root);
                candidates.Add(Path.Combine(folder, "Google", "Chrome", "Application", "chrome.exe"));
                candidates.Add(Path.Combine(folder, "Microsoft", "Edge", "Application", "msedge.exe"));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.AddRange(["/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"]);
        }
        else
        {
            var path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.StartsWith("/mnt/"));
            foreach (var name in new[] { "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge" })
                candidates.AddRange(path.Select(directory => Path.Combine(directory, name)));
        }

        // Playwright's folder numbers and install dates say nothing about the Chromium version. Current builds use the
        // "chrome-linux64" layout; very old ones ("chrome-linux") may not even open a window, so they come last.
        var playwright = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "ms-playwright");
        if (Directory.Exists(playwright))
        {
            var builds = Directory.GetDirectories(playwright, "chromium-*");
            candidates.AddRange(builds.Select(d => Path.Combine(d, "chrome-linux64", "chrome")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc));
            candidates.AddRange(builds.Select(d => Path.Combine(d, "chrome-linux", "chrome")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
