using System.Net;
using System.Text.Json;
using Xunit;

public sealed class ReleaseIntegrationTests
{
    [Fact]
    public async Task PackagedInstallerCanBeDownloadedVerifiedAndHandedToLauncher()
    {
        var releaseDirectory = Environment.GetEnvironmentVariable("TEAMSIO_RELEASE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(releaseDirectory)) return;
        var manifestText = await File.ReadAllTextAsync(Path.Combine(releaseDirectory, "update.json"));
        using var manifest = JsonDocument.Parse(manifestText);
        var version = manifest.RootElement.GetProperty("version").GetString()!;
        var name = manifest.RootElement.GetProperty("installerFileName").GetString()!;
        var downloadBase = $"https://github.com/NVZLAB/TeamsIO/releases/download/v{version}/";
        var releaseJson = JsonSerializer.Serialize(new { tag_name = "v" + version, draft = false, prerelease = false,
            body = "Release package test", assets = new[] {
                new { name = "update.json", browser_download_url = downloadBase + "update.json" },
                new { name, browser_download_url = downloadBase + name } } });
        var destination = Path.Combine(Path.GetTempPath(), "TeamsIO-release-test-" + Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.Host == "api.github.com") return new(HttpStatusCode.OK) { Content = new StringContent(releaseJson) };
            var assetName = Uri.UnescapeDataString(request.RequestUri.Segments[^1]);
            return new(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(Path.Combine(releaseDirectory, assetName))) };
        }));
        using var checker = new UpdateChecker(client, destination);
        try
        {
            var result = await checker.CheckAsync("0.0.0");
            Assert.True(result.UpdateAvailable);
            var path = await checker.DownloadInstallerAsync(result);
            var launched = false;
            await checker.StartInstallerAsync(path, result, launch: _ => launched = true);
            Assert.True(launched);
        }
        finally
        {
            if (Directory.Exists(destination)) { foreach (var path in Directory.GetFiles(destination)) File.Delete(path); Directory.Delete(destination); }
        }
    }

    [Fact]
    public async Task LivePublicGitHubCheck()
    {
        var output = Environment.GetEnvironmentVariable("TEAMSIO_LIVE_CHECK_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        using var checker = new UpdateChecker();
        var result = await checker.CheckAsync("0.0.0");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
}
