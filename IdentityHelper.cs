using Azure.Identity;

internal static class IdentityHelper
{
    public static readonly string[] GraphScopes =
    [
        "GroupMember.Read.All",
        "Presence.Read.All",
        "User.ReadBasic.All"

    ];

    public static InteractiveBrowserCredential CreateCredential(
        string tenantId,
        string clientId)
    {
        return new InteractiveBrowserCredential(
            new InteractiveBrowserCredentialOptions
            {
                TenantId = tenantId,
                ClientId = clientId,
                RedirectUri = new Uri("http://localhost"),
                TokenCachePersistenceOptions =
                    new TokenCachePersistenceOptions { Name = "TeamsIO.Identity" }
            });
    }
}
