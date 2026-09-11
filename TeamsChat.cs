internal static class TeamsChat
{
    internal static Uri CreateUri(string? address, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("This person has no email address or user principal name available.");
        return new Uri("https://teams.microsoft.com/l/chat/0/0?tenantId=" +
            Uri.EscapeDataString(tenantId) + "&users=" + Uri.EscapeDataString(address));
    }
}
