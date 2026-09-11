using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class UpdateSecurityTests
{
    private const string BaseUrl = "https://github.com/NVZLAB/TeamsIO/releases/download/v1.1.0/";
    private const string Installer = "TeamsIO_Setup_1.1.0.exe";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("test installer payload; never executed");
    private static string Hash => Convert.ToHexString(SHA256.HashData(Payload));

    [Fact]
    public async Task FullUpdateFollowsGitHubCdnAndVerifiesBeforeLaunch()
    {
        var directory = NewDirectory();
        var requests = new List<Uri>();
        using var checker = Create(request =>
        {
            var uri = request.RequestUri!;
            requests.Add(uri);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.False(request.Headers.Contains("Proxy-Authorization"));
            Assert.NotEmpty(request.Headers.UserAgent);
            if (uri.Host == "api.github.com") return Json(Release());
            if (uri.Host == "github.com") return Redirect("https://release-assets.githubusercontent.com/test/" + uri.Segments[^1] + "?signature=test");
            return uri.AbsolutePath.EndsWith("update.json") ? Json(Manifest()) : Bytes(Payload);
        }, directory);
        try
        {
            var update = await checker.CheckAsync("1.0.0");
            var file = await checker.DownloadInstallerAsync(update);
            Assert.Equal(Payload, await File.ReadAllBytesAsync(file));
            Assert.Contains("ZoneId=3", await File.ReadAllTextAsync(file + ":Zone.Identifier"));
            var launched = false;
            await checker.StartInstallerAsync(file, update, launch: path =>
            {
                Assert.Throws<IOException>(() => File.WriteAllText(path, "replacement"));
                launched = true;
            });
            Assert.True(launched);
            Assert.Equal(5, requests.Count);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task TamperingAfterDownloadPreventsLaunch()
    {
        var directory = NewDirectory();
        using var checker = Create(_ => Bytes(Payload), directory);
        try
        {
            var update = Result();
            var file = await checker.DownloadInstallerAsync(update);
            await File.WriteAllTextAsync(file, "modified");
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.StartInstallerAsync(file, update,
                launch: _ => Assert.Fail("Must not execute altered installer")));
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData("https://evil.example/download.exe")]
    [InlineData("http://release-assets.githubusercontent.com/file")]
    [InlineData("https://release-assets.githubusercontent.com.evil.example/file")]
    [InlineData("https://user@release-assets.githubusercontent.com/file")]
    [InlineData("https://release-assets.githubusercontent.com:444/file")]
    public async Task UnsafeRedirectIsRejectedBeforeContact(string destination)
    {
        var count = 0;
        var directory = NewDirectory();
        using var checker = Create(_ => { count++; return Redirect(destination); }, directory);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadInstallerAsync(Result()));
            Assert.Equal(1, count);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task RedirectLoopIsBounded()
    {
        var count = 0;
        var directory = NewDirectory();
        using var checker = Create(_ => { count++; return Redirect(BaseUrl + Installer); }, directory);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadInstallerAsync(Result()));
            Assert.Equal(6, count);
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData("{bad-json")]
    [InlineData("null")]
    public async Task BadManifestIsRejected(string manifest)
    {
        using var checker = Create(request => request.RequestUri!.Host == "api.github.com" ? Json(Release()) : Json(manifest));
        await Assert.ThrowsAnyAsync<Exception>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task WrongReleaseAssetIsRejected()
    {
        using var checker = Create(_ => Json(Release().Replace("/v1.1.0/update.json", "/v9.0.0/update.json")));
        await Assert.ThrowsAsync<InvalidDataException>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task MissingInstallerIsRejected()
    {
        using var checker = Create(request => request.RequestUri!.Host == "api.github.com" ? Json(Release(omitInstaller: true)) : Json(Manifest()));
        await Assert.ThrowsAsync<InvalidDataException>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task DuplicateManifestIsRejected()
    {
        using var checker = Create(_ => Json(Release(duplicateManifest: true)));
        await Assert.ThrowsAsync<InvalidDataException>(() => checker.CheckAsync("1.0.0"));
    }

    [Theory]
    [InlineData("../TeamsIO_Setup_1.1.0.exe")]
    [InlineData("TeamsIO_Setup_1.1.0.exe:extra.exe")]
    [InlineData("TeamsIO_Setup_2.0.0.exe")]
    public async Task UnsafeOrWrongInstallerNameIsRejected(string name)
    {
        using var checker = Create(request => request.RequestUri!.Host == "api.github.com" ? Json(Release()) : Json(Manifest(name)));
        await Assert.ThrowsAsync<InvalidDataException>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task OversizedManifestIsRejected()
    {
        using var checker = Create(request => request.RequestUri!.Host == "api.github.com" ? Json(Release()) : Json(new string(' ', 65537)));
        await Assert.ThrowsAsync<InvalidDataException>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task OversizedInstallerIsRejectedBeforeWriting()
    {
        var directory = NewDirectory();
        using var checker = Create(_ => { var response = Bytes(Payload); response.Content.Headers.ContentLength = 251L * 1024 * 1024; return response; }, directory);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadInstallerAsync(Result()));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task HttpErrorsDoNotReportUpToDate(HttpStatusCode status)
    {
        using var checker = Create(_ => new(status));
        await Assert.ThrowsAsync<HttpRequestException>(() => checker.CheckAsync("1.0.0"));
    }

    [Fact]
    public async Task CanceledDownloadCleansTemporaryFile()
    {
        var directory = NewDirectory();
        using var checker = Create(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new CancellableStream()) }, directory);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.DownloadInstallerAsync(Result(), cancellation.Token));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData("1.1")]
    [InlineData("1.1.0-beta")]
    [InlineData("vv1.1.0")]
    [InlineData("1.1.0.1")]
    public void InvalidReleaseVersionIsRejected(string version) => Assert.False(UpdateChecker.TryParseVersion(version, out _));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DraftAndPrereleaseAreIgnored(bool draft, bool prerelease)
    {
        using var checker = Create(_ => Json(JsonSerializer.Serialize(new { draft, prerelease })));
        Assert.False((await checker.CheckAsync("1.0.0")).UpdateAvailable);
    }

    private static UpdateChecker Create(Func<HttpRequestMessage, HttpResponseMessage> respond, string? directory = null) =>
        new(new HttpClient(new Handler(respond)), directory);
    private static UpdateCheckResult Result() => new(true, "1.1.0", "notes", Installer, Hash, BaseUrl + Installer, "available");
    private static string NewDirectory() { var path = Path.Combine(Path.GetTempPath(), "TeamsIO-update-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void DeleteDirectory(string directory) { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private static HttpResponseMessage Json(string json) => Bytes(Encoding.UTF8.GetBytes(json));
    private static HttpResponseMessage Redirect(string url) { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri(url); return response; }
    private static string Manifest(string name = Installer) => JsonSerializer.Serialize(new { version = "1.1.0", installerFileName = name, sha256 = Hash });
    private static string Release(bool omitInstaller = false, bool duplicateManifest = false)
    {
        var assets = new List<object> { new { name = "update.json", browser_download_url = BaseUrl + "update.json" } };
        if (duplicateManifest) assets.Add(assets[0]);
        if (!omitInstaller) assets.Add(new { name = Installer, browser_download_url = BaseUrl + Installer });
        return JsonSerializer.Serialize(new { tag_name = "v1.1.0", draft = false, prerelease = false, body = "notes", assets });
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
    private sealed class CancellableStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
