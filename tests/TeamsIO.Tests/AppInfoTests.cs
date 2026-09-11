using Xunit;
public sealed class AppInfoTests
{
    [Fact] public void ApplicationVersionCanBeComparedByUpdater() => Assert.True(UpdateChecker.TryParseVersion(AppInfo.CurrentVersion, out _));
    [Fact] public void BlankConfigurationRequiresSetup() => Assert.NotNull(new AppConfig().Validate());
    [Fact] public void ValidConfigurationAcceptsMultipleGroups() => Assert.Null(new AppConfig {
        TenantId = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(),
        GroupIds = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()] }.Validate());
    [Fact] public void ChatEscapesAddress() => Assert.Contains("users=a%2Bb%40example.com", TeamsChat.CreateUri("a+b@example.com", "tenant").AbsoluteUri);
    [Fact] public void ChatRequiresAddress() => Assert.Throws<InvalidOperationException>(() => TeamsChat.CreateUri(null, "tenant"));
}
