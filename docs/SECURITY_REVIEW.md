# Pre-publication security review

Review date: September 11, 2026. Scope: publication-candidate files, application code, dependencies, Windows publish output, installer packaging and GitHub update flow. This is a point-in-time review, not a guarantee that all vulnerabilities are absent.

Reviewed release: 1.0.1, so existing 1.0.0 installations will detect it as a newer version when published.

## Findings addressed

| Finding | Resolution |
| --- | --- |
| High-severity Kiota advisory GHSA-7j59-v9qr-6fq9 in transitive version 1.15.2 | Pinned Microsoft.Kiota.Abstractions and its HTTP implementation to patched 1.22.2; subsequent transitive dependency audit is clean. |
| Self-contained runtime behind Microsoft's current security release | Updated the build SDK to 8.0.425 and verified that the published application bundles .NET and Windows Desktop runtime 8.0.31. |
| Updates automatically followed unrestricted HTTP redirects | Disabled automatic redirects and cookies; allow only HTTPS GitHub release asset hosts, with a redirect limit. Requests do not use an Entra token or GitHub token. |
| Release metadata could name an asset under another tag | Bound manifest and installer URLs to the selected repository, release tag and exact filenames; validated the manifest version against the release. |
| Installer could change after download validation | Recheck SHA-256 while holding a read lock through the launch handoff; reject altered files and unexpected local paths. |
| Downloaded executables lacked Windows Internet-zone metadata | Apply Zone.Identifier before committing the verified download. |
| Download body could outlive the HTTP header timeout | Added whole-operation deadlines, cancellation, and metadata/installer size bounds; partial downloads are cleaned up. |
| Full user-profile scope exceeded displayed data | Changed to User.ReadBasic.All and removed unused profile fields; setup instructions updated. |
| WinForms default enabled unsafe BinaryFormatter serialization | Explicitly disabled it in the application runtime configuration. |
| Startup could force-kill the previous instance's process tree | Replaced force-kill with graceful close/wait; kept instance coordination local to the Windows session. |
| Installer packaged arbitrary leftover publish files | Restricted installer inputs to the application, DLLs, blank example configuration and setup guide. Release debug symbols are disabled. |
| Future version bumps would fail a hard-coded 1.0.0 test | Test now requires a version compatible with the updater instead. |
| Publishing tools were absent from dependency locks | Made single-file publishing explicit so test and publish restores use consistent lock files. |
| Security-scan snapshots could enter SDK source globs | Explicitly excluded artifacts and local SDK/cache directories from compilation. |

## Verification

- Publication scan: no retired organization/product references. A separate comparison against the private source configuration found no copied deployment values; those values were not written to the report.
- Gitleaks 8.30.1: zero findings in publication candidates. Scanner binary checksum verified against its official release.
- Git history: zero local commits and no remote refs at review time; no inherited history to clean.
- Published application: no retired-name matches in ASCII/UTF-16 scan.
- NuGet audit: no known vulnerable direct or transitive packages after remediation; failed audit queries and vulnerability warnings now fail restore.
- Automated tests cover normal/current/missing releases, full download with CDN redirects, invalid manifests, asset mismatch, unsafe names/URLs, HTTP failures, cancellation, size bounds, tampering and locked launch verification.
- Real anonymous GitHub check returned no accessible published update, consistent with the repository being private and having no releases.
- Release package validation feeds the actual installer and generated manifest through the updater with simulated HTTP responses. The launcher is substituted so the test does not install software.

## Remaining publication and operational steps

The GitHub repository was private and empty with no releases at review time. The public updater cannot deliver a private or draft release. Publish a reviewed stable release with its installer and update.json, then perform the older-version upgrade test on a test PC described in RELEASING.md. The real production download/install and authenticated tenant behavior remain unverified until those environments are available.

The installer is currently unsigned. Its checksum proves consistency with the manifest, not independent publisher identity. Use Authenticode signing for production distribution and protect repository maintainer accounts and release workflows. Signing must happen before manifest generation. Do not distribute an older pre-review build.

The app registration now needs User.ReadBasic.All in place of User.Read.All. Administrators who followed the earlier guide should update that delegated permission and consent. Keep GroupMember.Read.All and Presence.Read.All. Local logs can include account/error details and should be reviewed before sharing.

No repository visibility changes, pushes, tags or releases were performed during this review. The private source project was not modified.

## References

- Kiota advisory: https://github.com/advisories/GHSA-7j59-v9qr-6fq9
- Microsoft .NET 8 release metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json
- GitHub Releases API: https://docs.github.com/en/rest/releases/releases
- Graph user permissions: https://learn.microsoft.com/en-us/graph/api/user-get
- Release validation and signing: RELEASING.md
