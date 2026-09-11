using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed class UpdateChecker : IDisposable
{
    private const string Repository = "NVZLAB/TeamsIO";
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _downloadDirectory;
    public UpdateChecker(HttpClient? client = null, string? downloadDirectory = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false
        }) { Timeout = TimeSpan.FromMinutes(10) };
        _downloadDirectory = downloadDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamsIO", "Updates");
    }

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        cancellationToken = timeout.Token;
        using var response = await SendAsync(new Uri($"https://api.github.com/repos/{Repository}/releases/latest"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return UpdateCheckResult.NoUpdate("No published update is accessible. The repository may be private or have no published stable release.");
        response.EnsureSuccessStatusCode();
        using var release = JsonDocument.Parse(await ReadLimitedAsync(response, 1024 * 1024, cancellationToken));
        var root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            return UpdateCheckResult.NoUpdate("No stable release is available.");
        if (!TryParseVersion(currentVersion, out var installed) ||
            !TryParseVersion(root.GetProperty("tag_name").GetString(), out var available))
            throw new InvalidDataException("The installed or release version is invalid.");
        if (available <= installed) return UpdateCheckResult.NoUpdate("TeamsIO is up to date.");
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var tag = root.GetProperty("tag_name").GetString()!;
        var manifestAsset = FindAsset(assets, "update.json");
        using var manifestResponse = await SendAsync(AssetUri(manifestAsset, tag, "update.json"), cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(
            await ReadLimitedAsync(manifestResponse, 65536, cancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty update manifest.");
        if (!TryParseVersion(manifest.Version, out var manifestVersion) || manifestVersion != available)
            throw new InvalidDataException("The manifest version does not match the GitHub release.");
        ValidateFileName(manifest.InstallerFileName);
        if (manifest.Sha256 is not { Length: 64 } || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid installer SHA-256 checksum.");
        if (manifest.InstallerFileName != $"TeamsIO_Setup_{available}.exe")
            throw new InvalidDataException("The installer filename does not match the release version.");
        var installer = FindAsset(assets, manifest.InstallerFileName);
        return new(true, available.ToString(), root.TryGetProperty("body", out var notes) ? notes.GetString() : null,
            manifest.InstallerFileName, manifest.Sha256, AssetUri(installer, tag, manifest.InstallerFileName).AbsoluteUri, "A newer version is available.");
    }

    public async Task<string> DownloadInstallerAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        if (!update.UpdateAvailable || update.InstallerFileName is null || update.ExpectedSha256 is null || update.DownloadUrl is null)
            throw new InvalidOperationException("No downloadable update.");
        ValidateFileName(update.InstallerFileName);
        var uri = ValidateAssetUri(update.DownloadUrl);
        if (Uri.UnescapeDataString(uri.Segments[^1]) != update.InstallerFileName)
            throw new InvalidDataException("The installer URL and filename do not match.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        cancellationToken = timeout.Token;
        var directory = _downloadDirectory;
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, update.InstallerFileName);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var response = await SendAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 250L * 1024 * 1024)
                throw new InvalidDataException("Update file exceeds the size limit.");
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                await CopyLimitedAsync(source, target, 250L * 1024 * 1024, cancellationToken);
            if (!await FileMatchesSha256Async(temporary, update.ExpectedSha256, cancellationToken))
                throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");
            // Retain Windows Internet-zone handling for downloaded executables.
            await File.WriteAllTextAsync(temporary + ":Zone.Identifier",
                "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://github.com/NVZLAB/TeamsIO\r\n", cancellationToken);
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        return Version.TryParse(normalized, out version!) && version.Build >= 0 && version.Revision < 0;
    }

    internal static Uri ValidateAssetUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.Query.Length != 0 ||
            uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length != 6 ||
            !uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("The update asset must belong to the TeamsIO GitHub releases.");
        return uri;
    }
    private static Uri AssetUri(JsonElement asset, string tag, string name)
    {
        var uri = ValidateAssetUri(asset.GetProperty("browser_download_url").GetString() ?? "");
        var expected = $"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}";
        if (uri.AbsoluteUri != expected) throw new InvalidDataException("Asset URL does not match its release tag and filename.");
        return uri;
    }
    private static JsonElement FindAsset(JsonElement[] assets, string name)
    {
        var found = assets.Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (found.Length != 1) throw new InvalidDataException($"Release must contain exactly one '{name}' asset.");
        return found[0];
    }
    private static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.StartsWith("TeamsIO_Setup_", StringComparison.Ordinal) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsafe installer filename.");
    }
    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, long limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit)
            throw new InvalidDataException("Update metadata exceeds the size limit.");
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var target = new MemoryStream();
        await CopyLimitedAsync(source, target, limit, token);
        return target.ToArray();
    }
    private static async Task CopyLimitedAsync(Stream source, Stream target, long limit, CancellationToken token)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, token)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Update file exceeds the size limit.");
            await target.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken token)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("TeamsIO/1.0");
            if (uri.Host == "api.github.com")
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            }
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (uri.Host == "api.github.com" || location is null || redirects == 5)
                throw new InvalidDataException("Unexpected GitHub redirect. Check the configured repository.");
            var next = new Uri(uri, location);
            if (next.Scheme != "https" || !next.IsDefaultPort || next.UserInfo.Length != 0 || next.Fragment.Length != 0)
                throw new InvalidDataException("Unsafe update redirect.");
            if (next.Host == "github.com") ValidateAssetUri(next.AbsoluteUri);
            else if (next.Host is not ("release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new InvalidDataException("Update redirected outside GitHub's release asset hosts.");
            uri = next;
        }
        throw new InvalidDataException("Too many update redirects.");
    }

    internal async Task StartInstallerAsync(string path, UpdateCheckResult update,
        CancellationToken token = default, Action<string>? launch = null)
    {
        if (!update.UpdateAvailable || update.InstallerFileName is null || update.ExpectedSha256 is null)
            throw new InvalidOperationException("No verified update to install.");
        ValidateFileName(update.InstallerFileName);
        var expectedPath = Path.GetFullPath(Path.Combine(_downloadDirectory, update.InstallerFileName));
        if (!Path.GetFullPath(path).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Installer is outside the update directory.");
        // Prevent replacement between the last verification and process creation.
        await using var stream = new FileStream(expectedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, token);
        if (!Convert.ToHexString(hash).Equals(update.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installer changed after download. Download it again.");
        token.ThrowIfCancellationRequested();
        if (launch is not null) launch(expectedPath);
        else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(expectedPath)
        {
            UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(expectedPath)
        });
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    internal static async Task<bool> FileMatchesSha256Async(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
internal sealed class UpdateManifest
{
    public string Version { get; init; } = "";
    public string InstallerFileName { get; init; } = "";
    public string Sha256 { get; init; } = "";
}
internal sealed record UpdateCheckResult(bool UpdateAvailable, string? AvailableVersion, string? ReleaseNotes,
    string? InstallerFileName, string? ExpectedSha256, string? DownloadUrl, string Message)
{
    public static UpdateCheckResult NoUpdate(string message) => new(false, null, null, null, null, null, message);
}
