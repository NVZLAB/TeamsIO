# Set up TeamsIO for your organization

TeamsIO uses your organization's Microsoft Entra app registration to read group members and Teams presence when you sign in. An administrator completes registration and consent once; users then enter the supplied IDs. Use a Microsoft 365 work or school account in the same tenant. This build connects to Microsoft's public cloud.

## 1. Create the app registration

Open https://entra.microsoft.com and switch to the directory containing your users and groups. Go to Entra ID > App registrations > New registration. Name it TeamsIO. Choose the single-tenant account option (Accounts in this organizational directory only / Single tenant only), then select Register. If you cannot register apps, ask your Entra administrator to do this.

On the app's Overview page, copy Directory (tenant) ID and Application (client) ID into the matching TeamsIO fields. The app's Object ID is a different value; do not use it as the client ID.

## 2. Configure desktop sign-in

In the registration, open Authentication > Add a platform > Mobile and desktop applications. Add the custom redirect URI http://localhost and save. Use http, not https. TeamsIO signs in through your default browser using a loopback redirect; you do not need to run a web server yourself.

No client secret or certificate is needed. Do not configure this redirect as a Web or SPA platform. The separate Allow public client flows toggle is for flows such as device code or password authentication; TeamsIO uses interactive browser authentication with the desktop redirect.

## 3. Add permissions and grant consent

Open API permissions > Add a permission > Microsoft Graph > Delegated permissions. Add these three permissions:

- GroupMember.Read.All — read the selected groups' members.
- User.ReadBasic.All — read basic names and addresses used by the board.
- Presence.Read.All — read availability, activity and presence information.

Choose Add permissions. Have an authorized administrator select Grant admin consent for your organization, then confirm that the permissions show Granted. The default User.Read permission can remain. TeamsIO does not require application permissions, chat-send permissions, Power BI permissions or file access. Group selection filters the board; it does not narrow the tenant-wide permission grants.

## 4. Find the group IDs

Go to Entra ID > Groups > All groups. Open each group you want to display and copy its Object ID from Overview. Paste one group ID per line in TeamsIO; commas and semicolons also work. All selected groups must belong to the configured tenant.

Use groups with direct user members. Nested groups are not expanded, and devices or other non-user members are not displayed. Users appearing in multiple selected groups appear once.

## 5. Save and sign in

Select Save and continue in first-run setup, then complete browser sign-in. For an existing configuration, Save and restart applies the changes and signs in against that connection. Follow your organization's normal MFA and Conditional Access requirements.

To change IDs or reopen this guide later, right-click the board and choose Tenant, Client & Group IDs. Connection settings stay on this PC in %LOCALAPPDATA%\TeamsIO\connection.json. No tenant values are bundled with the public app.

## Troubleshooting

- Redirect mismatch / AADSTS50011: verify http://localhost on the Mobile and desktop applications platform for the same client ID.
- Application not found / wrong directory: verify both IDs from the app's Overview and sign in to that tenant.
- Need admin approval / access denied / 403: confirm the three delegated permissions have admin consent. Ask your administrator to check sign-in logs, Conditional Access and any enterprise-app user assignment requirements.
- Empty board: verify group Object IDs and direct user membership. A group's display name, email address and an app's Object ID will not work as a group ID.
- Presence unavailable: confirm the person uses Teams in your tenant. Guest or external users may not have presence available to this account.
- Browser sign-in fails: check the connection and local firewall/proxy policy. Do not disable organizational security controls; ask your administrator to review the error.

## Microsoft references

Registration: https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app

Desktop authentication: https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration

Permission definitions: https://learn.microsoft.com/en-us/graph/permissions-reference

Instructions checked September 11, 2026. Portal wording may vary.
