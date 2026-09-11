# Security

TeamsIO is a per-user Windows presence board. It uses interactive Microsoft Entra sign-in and delegated Graph permissions. No app secret, tenant configuration, signing key, or GitHub token is distributed with the application. Azure Identity manages the protected authentication cache. Names, presence and status messages are displayed as text.

Required Graph scopes are GroupMember.Read.All, User.ReadBasic.All and Presence.Read.All. Selected groups filter the display; the permission grants themselves are tenant-wide. The app reads data and opens Teams chat links; it does not send chat messages.

Connection settings and diagnostic logs stay under the user's local application data directory. Logs can contain account/error details; review them before sharing. Do not commit deployment configuration, credentials, or log files.

Dependencies are locked, audited during restore, and monitored by Dependabot. The release workflow scans publication candidates for secrets. Keep the .NET SDK/runtime and Inno Setup current, and rerun these checks for every release.

Update trust, signing limitations and the release validation procedure are documented in docs/RELEASING.md. No automated scan is a guarantee against all vulnerabilities.

For a vulnerability report, use GitHub's private reporting option on this repository if enabled. Otherwise contact a repository maintainer privately before sharing sensitive details. Do not post credentials or private tenant data in public issues.
