using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class UpdateCheckerTests
{
    [Fact]
    public async Task NoReleaseIsNormal()
    {
        var checker = new UpdateChecker(new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound))));
        Assert.False((await checker.CheckAsync("1.0.0")).UpdateAvailable);
    }

    [Fact]
    public async Task CurrentReleaseDoesNotRequireAssets()
    {
        var checker = CreateChecker("1.0.0", "1.0.0", new string('A', 64));
        Assert.False((await checker.CheckAsync("1.0.0")).UpdateAvailable);
    }

    [Fact]
    public async Task NewReleaseUsesMatchingManifest()
    {
        var result = await CreateChecker("1.1.0", "1.1.0", new string('A', 64)).CheckAsync("1.0.0");
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.1.0", result.AvailableVersion);
        Assert.StartsWith("https://github.com/NVZLAB/TeamsIO/releases/download/", result.DownloadUrl);
    }

    [Fact]
    public async Task MismatchedManifestIsRejected() => await Assert.ThrowsAsync<InvalidDataException>(
        () => CreateChecker("1.1.0", "1.2.0", new string('A', 64)).CheckAsync("1.0.0"));

    [Fact]
    public async Task MalformedChecksumIsRejected() => await Assert.ThrowsAsync<InvalidDataException>(
        () => CreateChecker("1.1.0", "1.1.0", "invalid").CheckAsync("1.0.0"));

    [Theory]
    [InlineData("https://evil.example/NVZLAB/TeamsIO/releases/download/v1/a.exe")]
    [InlineData("https://github.com/other/repo/releases/download/v1/a.exe")]
    [InlineData("http://github.com/NVZLAB/TeamsIO/releases/download/v1/a.exe")]
    public void ForeignAssetRejected(string uri) => Assert.Throws<InvalidDataException>(() => UpdateChecker.ValidateAssetUri(uri));

    [Fact]
    public async Task CorruptDownloadCannotBecomeInstaller()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamsIO-tests-" + Guid.NewGuid().ToString("N"));
        var checker = new UpdateChecker(new HttpClient(new Handler(_ => Json("corrupt installer"))), directory);
        var result = new UpdateCheckResult(true, "1.1.0", null, "TeamsIO_Setup_1.1.0.exe", new string('0', 64),
            "https://github.com/NVZLAB/TeamsIO/releases/download/v1.1.0/TeamsIO_Setup_1.1.0.exe", "");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadInstallerAsync(result));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory); }
    }

    private static UpdateChecker CreateChecker(string releaseVersion, string manifestVersion, string hash)
    {
        var baseUrl = $"https://github.com/NVZLAB/TeamsIO/releases/download/v{releaseVersion}/";
        var installer = $"TeamsIO_Setup_{releaseVersion}.exe";
        return new(new HttpClient(new Handler(request =>
        {
            Assert.Null(request.Headers.Authorization);
            if (request.RequestUri!.Host == "api.github.com")
                return Json(JsonSerializer.Serialize(new { tag_name = "v" + releaseVersion, draft = false, prerelease = false,
                    body = "Release notes", assets = new[] {
                        new { name = "update.json", browser_download_url = baseUrl + "update.json" },
                        new { name = installer, browser_download_url = baseUrl + installer } } }));
            return Json(JsonSerializer.Serialize(new { version = manifestVersion, installerFileName = installer, sha256 = hash }));
        })));
    }
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
}
