using Azure.Identity;
using Azure.Core;
using TeamsIO.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PresenceRequestBody = Microsoft.Graph.Communications.GetPresencesByUserId.GetPresencesByUserIdPostRequestBody;

internal sealed class TeamsIoService
{
    private readonly GraphServiceClient _appClient;
    private readonly TokenCredential _credential;
    public EntraUserIdentity SignedInUser { get; }

    public string SignedInAs => string.IsNullOrWhiteSpace(SignedInUser.DisplayName)
        ? "Signed in"
        : $"Signed in as {SignedInUser.DisplayName}";

    private TeamsIoService(
        GraphServiceClient appClient,
        TokenCredential credential,
        EntraUserIdentity user)
    {
        _appClient = appClient;
        _credential = credential;
        SignedInUser = user;
    }

    public static async Task<TeamsIoService> CreateAsync(AppConfig config)
    {
        var credential = IdentityHelper.CreateCredential(
            config.TenantId,
            config.ClientId);

        var graphClient = new GraphServiceClient(
            credential,
            IdentityHelper.GraphScopes);

        var profile = await graphClient.Me.GetAsync(request =>
        {
            request.QueryParameters.Select =
                new[] { "id", "displayName", "userPrincipalName" };
        });

        if (profile is null)
            throw new InvalidOperationException("Microsoft Graph returned no user profile.");

        var user = new EntraUserIdentity
        {
            DisplayName = profile.DisplayName ?? "Unknown",
            UserPrincipalName = profile.UserPrincipalName ?? "",
            ObjectId = profile.Id ?? "",
            TenantId = config.TenantId
        };

        return new TeamsIoService(graphClient, credential, user);
    }

    public async Task<Dictionary<User, Presence?>> GetBoardAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        var users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (var groupId in config.GroupIds)
        {
            var page = await _appClient.Groups[groupId].Members.GetAsync(r =>
            {
                r.QueryParameters.Select = new[]
                {
                    "id", "displayName", "mail", "userPrincipalName"
                };
                r.QueryParameters.Top = 999;
            }, cancellationToken);

            while (page is not null)
            {
                foreach (var member in page.Value ?? [])
                {
                    if (member is User user && user.Id is not null)
                        users.TryAdd(user.Id, user);
                }

                if (string.IsNullOrWhiteSpace(page.OdataNextLink))
                    break;

                page = await _appClient
                    .Groups[groupId]
                    .Members
                    .WithUrl(page.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }
        }

        var includedUsers = users.Values
            .Where(user => !config.BlockedUsers.Contains(user.DisplayName ?? ""))
            .OrderBy(user => user.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var presenceById = new Dictionary<string, Presence>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var batch in includedUsers.Chunk(650))
        {
            var ids = batch
                .Select(user => user.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToList();

            if (ids.Count == 0)
                continue;

            var response = await _appClient
                .Communications
                .GetPresencesByUserId
                .PostAsGetPresencesByUserIdPostResponseAsync(
                    new PresenceRequestBody { Ids = ids },
                    cancellationToken: cancellationToken);

            foreach (var presence in response?.Value ?? [])
            {
                if (presence.Id is not null)
                    presenceById[presence.Id] = presence;
            }
        }

        var result = new Dictionary<User, Presence?>();
        foreach (var user in includedUsers)
        {
            result[user] = user.Id is not null &&
                presenceById.TryGetValue(user.Id, out var presence)
                    ? presence
                    : null;
        }

        AppLog.Info(
            $"Loaded {users.Count} unique group members and " +
            $"{presenceById.Count} presence records.");
        return result;
    }
}
