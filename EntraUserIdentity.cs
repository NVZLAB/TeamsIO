namespace TeamsIO.Identity;

internal sealed class EntraUserIdentity
{
    public string DisplayName { get; init; } = "";
    public string UserPrincipalName { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public string TenantId { get; init; } = "";
}
