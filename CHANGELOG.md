# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.29] - 2025-11-09

### Changed

- Key changes for 0.2.29 (URL resolution & Docker-aware fallbacks)
  - Added `IHttpContextAccessor` usage/injection to support constructing the Listenarr public URL from the current HTTP request when available (useful behind reverse proxies)
  - Improved URL resolution priority (used when the app needs to provide an absolute Listenarr URL to helper processes or external systems):
    1. `LISTENARR_PUBLIC_URL` environment variable (highest priority)
    2. Current HTTP request (uses `IHttpContextAccessor` and honors X-Forwarded-* headers)
    3. Startup config (configured via the existing startup JSON/config service)
    4. Fallback: `host.docker.internal` (when running in Docker) or `localhost` (non-Docker fallback)
  - Docker-aware fallback: when `DOCKER_ENV` environment variable is present/true the runtime will prefer `host.docker.internal` instead of `localhost` for local host fallbacks
  - Additional per-step logging added to help diagnose URL resolution issues (logs which source was selected and any header-based values used)

### Recommendations

- Update your runtime Dockerfile to explicitly set the `DOCKER_ENV` environment variable so Docker-aware fallbacks are enabled. Example (in `listenarr.api/Dockerfile.runtime`):

  ENV DOCKER_ENV=true

- Prefer setting `LISTENARR_PUBLIC_URL` in production environments (recommended) so the runtime does not need to infer the value from request headers.

### Notes

- These changes make URL resolution more robust behind reverse proxies and in Docker-based deployments, and provide better logging to debug any cases where the helper bot (or external integrations) cannot reach the Listenarr API.

## [0.2.28] - 2025-11-09

### Changed

 - Publish and deployment reliability improvements
  - Ensure `tools/**` is included in publish output and runtime images by updating `listenarr.api/Listenarr.Api.csproj` and adding an explicit MSBuild `CopyToolsToPublish` target as a fallback.
  - The runtime Dockerfile (`listenarr.api/Dockerfile.runtime`) now accepts a build-arg `PUBLISH_DIR` and copies from that path; CI workflows were updated to pass the appropriate publish directory so images are built from the exact CI publish output (for example `listenarr.api/publish/linux-x64`).
  - CI and Canary workflows: added publish sanity checks, artifact upload (for debug), a fail-fast check in CI, and a copy-then-verify safety step in Canary so builds abort or repair when `tools` is missing from publish output.

### Fixed

 - Verified that local `dotnet publish` now includes `listenarr.api/publish/tools/discord-bot` and adjusted workflows and Docker build logic to make image builds deterministic and reproducible locally and in CI.
## [0.2.27] - 2025-11-09

### Fixed

  - **CI: fail-fast & publish verification**: Added quick-fail checks and publish-folder verification to CI and Canary workflows so builds abort if the `tools` folder is missing from publish output
    - Canary workflow now lists the publish folder, uploads the publish artifact for inspection, and contains a copy-then-verify step that will copy `tools` into the publish folder if CIS publish missed them
    - Main CI workflow now performs a fail-fast check after `dotnet publish` to avoid building/pushing images that don't include the discord helper files
    - These steps reduce the risk of releasing runtime images that cannot start the Discord helper bot

## [0.2.26] - 2025-11-09

### Fixed

 - **Publish: include tools folder**: Ensured `tools/**` is copied to the publish output by updating `Listenarr.Api.csproj` (added <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory> for `tools\**\*.*`)
   - Fixes missing `/app/tools/discord-bot` inside runtime containers when publishing + copying publish output into images
   - After this change, run `dotnet publish` and rebuild your image so the tooling directory is included in the container

## [0.2.25] - 2025-11-09

### Added

- **Docker as Primary Production Method**: Promoted Docker as the recommended production deployment method in README.md
  - Docker section moved to first position with clear benefits highlighted
  - Added Docker Compose example for easier production deployments
  - Emphasized Docker's advantages: isolation, updates, consistency, security, and included Node.js
- **Pre-built Executables for Production**: Promoted executable downloads as secondary production deployment method
  - Clear instructions for downloading from GitHub Releases
  - Emphasized self-contained executables with no .NET Runtime requirement
  - Added Node.js and LISTENARR_PUBLIC_URL prerequisites for Discord bot functionality
  - Reorganized deployment options to prioritize Docker over executables for production
- **Docker Environment Configuration**: Added `LISTENARR_PUBLIC_URL` environment variable to `docker-compose.yml`
  - Required for Discord bot functionality in Docker production deployments
  - Enables proper URL configuration for bot interactions with the Listenarr API
  - Users must replace `https://your-domain.com` with their actual domain or IP address
- **Non-Docker Production Deployment Guide**: Added comprehensive instructions for production deployments without Docker
  - Publishing instructions for Windows, Linux, and macOS platforms
  - Environment variable configuration for Discord bot functionality
  - IIS deployment guidance for Windows servers
  - Node.js installation requirements for Discord bot support

### Fixed

- **Docker Runtime**: Added Node.js 20 installation to final runtime image for Discord bot support
  - Resolves "Failed to start bot" errors in Docker production deployments
  - Ensures Node.js runtime is available for Discord bot process execution

## [0.2.24] - 2025-11-08

### Fixed

- **Database migration: Discord settings**: recreated migration with new timestamp `20251109043000_AddDiscordSettingsToApplicationSettings` to ensure it runs on all databases, including those with broken migration history
  - Migration adds `DiscordApplicationId`, `DiscordBotAvatar`, `DiscordBotEnabled`, `DiscordBotToken`, `DiscordBotUsername`, `DiscordChannelId`, `DiscordCommandGroupName`, `DiscordCommandSubcommandName`, and `DiscordGuildId`
  - **Automatic fix for existing users**: Renamed migration ensures it executes regardless of previous broken migration state, fixing databases without manual intervention
  - Verified migration applies correctly and resolves 'no such column' errors
- **Discord bot service**: fixed production deployment issues and URL configuration
  - **Working directory**: Changed from hardcoded relative path to use `IHostEnvironment.ContentRootPath` for correct path resolution in published applications
  - **Directory inclusion**: Added tools directory to project publish to ensure discord-bot files are available in production
  - **URL configuration**: Added support for `LISTENARR_PUBLIC_URL` environment variable for production deployments, with fallback to startup config
  - **Error handling**: Added validation for bot directory and index.js existence with detailed error logging
  - **Dependencies**: Injected `IStartupConfigService` and `IHostEnvironment` for proper configuration access

## [0.2.23] - 2025-11-08

### Fixed

- **Database migration: Discord settings**: recreated migration with new timestamp `20251109043000_AddDiscordSettingsToApplicationSettings` to ensure it runs on all databases, including those with broken migration history
  - Migration adds `DiscordApplicationId`, `DiscordBotAvatar`, `DiscordBotEnabled`, `DiscordBotToken`, `DiscordBotUsername`, `DiscordChannelId`, `DiscordCommandGroupName`, `DiscordCommandSubcommandName`, and `DiscordGuildId`

## [0.2.22] - 2025-11-08

### Fixed

- **Backend: warning cleanup**: silence CS1998 compiler warnings in `DiscordBotService` by returning completed tasks for synchronous methods (StopBotAsync, IsBotRunningAsync)

## [0.2.21] - 2025-11-08

### Added

- **Professional Webhook Test Menu**: Enhanced notification testing UI
  - Bell icon dropdown menu in AudiobookDetailView with 3 trigger options
  - Only appears in development builds when webhooks are configured and at least one is enabled
  - Automatic webhook selector modal for multiple webhook configurations
  - Targeted testing: Send test notifications to specific webhooks
  - Backend support: DiagnosticsController now accepts optional webhookId parameter
  - Improved UX: Shows webhook name in success toast notifications
- **Discord helper bot (tools/discord-bot)**: reference Node.js bot to register a slash command and forward requests to the Listenarr API for development and troubleshooting
  - Ephemeral interactive flow: search → select → quality → confirm → request
  - Automatic Listenarr URL persistence: prompts once and saves `tools/discord-bot/.env` (or reads `LISTENARR_URL` env)
  - README documentation for bot setup and troubleshooting

### Fixed

- **Development-Only UI Elements**: Hidden test notification buttons in production
  - AudiobookDetailView: Wrapped 3 test notification buttons in `v-if="isDevelopment"` check
  - Buttons only visible in development mode, preventing confusion in production deployments
- **Discord bot session & CSRF flows**: improved reliability when users interact with the ephemeral select/confirm flow
  - Preserve interaction tokens so ephemeral replies can be removed when a request completes
  - Fetch antiforgery token from `/api/antiforgery/token` and retry POST /api/library/add with `X-XSRF-TOKEN` when the server returns CSRF errors
  - Implement cookie-aware fetch where possible (optional `fetch-cookie` + `tough-cookie` packages)
- **Metadata validation**: normalize metadata shapes before POSTing to `/api/library/add` so authors, narrators, tags and genres are always string arrays and series fields are stringified
- **Idempotency**: add `Idempotency-Key` header to library add requests to enable safe retries and deduplication
- **Message lifecycle & UX**: make the interactive flow ephemeral-only to avoid duplicate channel posts and update the original message instead of replying
  - On success the confirm button is updated to a disabled green “Added” button
  - Components are disabled immediately after Request to prevent double-processing

### Changed

- **Webhook Test Menu**: Gate Test menu to development builds and require at least one enabled webhook for visibility

## [0.2.20] - 2025-11-05

### Added

- **Production Logger Utility**: Environment-aware logging system (`fe/src/utils/logger.ts`)
- Automatically disabled in production (except errors) for performance
- Supports debug, info, warn, and error levels
- Integrated across entire Vue.js frontend
- **CHANGELOG.md**: Comprehensive changelog following Keep a Changelog format
- **SECURITY.md**: Complete security policy with vulnerability reporting process, best practices, and audit trail
- **Audiobook Status Indicators**: Visual border colors on audiobook cards
- Red border: No files (missing)
- Blue pulsing border: Currently downloading
- Yellow border: Quality mismatch (has files but doesn't meet cutoff)
- Green border: Quality match (meets requirements)

### Fixed

- **CRITICAL: qBittorrent Incremental Sync Cache**: Fixed torrents disappearing from queue UI on incremental updates
- The `/api/v2/sync/maindata` endpoint only returns changed torrents, not the full list
- Implemented `_qbittorrentTorrentCache` dictionary to maintain complete torrent state across polls
- Incremental updates now merge into cache instead of replacing it
- Handles `torrents_removed` to properly clean up deleted torrents
- Full updates clear cache and rebuild from scratch
- Queue UI now shows all torrents consistently, regardless of which ones changed
- **Production URL Configuration**: Fixed hardcoded localhost in `loadInitialLogs` function
- Now uses environment-aware URL: localhost in dev, configured base URL in production
- Ensures system logs load correctly in all deployment scenarios
- **XML Documentation**: Fixed incorrect HTML entity decode example in NotificationService
- Changed from confusing double-encoded example to accurate single-decode: "&amp;amp;" -> "&amp;"
- **Critical Test Failures**: Fixed 6 failing unit tests achieving 100% pass rate (50/50 tests passing)
  - Fixed 4 DownloadService constructor tests by adding IHttpClientFactory, IMemoryCache, and IDbContextFactory dependencies
  - Fixed 2 SearchController tests by properly mocking AudimetaService with required constructor parameters
  - Test files: `DownloadProcessingTests.cs`, `DownloadProcessing_NoDoubleMoveTests.cs`, `DownloadNaming_AudiobookMetadataTests.cs`, `SearchControllerTests.cs`
- **Production Logging Cleanup**: Removed/replaced 35+ console.log statements for production readiness
  - App.vue: 19 console statements replaced with logger calls
  - SettingsView.vue: 5 debug statements removed from webhook migration code
  - WantedView.vue: 5 statements replaced with logger.debug/error
  - SystemView.vue: 2 statements replaced with logger.debug/error
  - AudiobookDetailView.vue: 4 console statements replaced with logger.debug/error
- **Resource Management**: Fixed memory leaks by properly disposing HttpContent objects
  - DownloadService: Added `using var` to 8 FormUrlEncodedContent instances
  - DownloadService: Added `using var` to 1 StringContent instance (NZBGet ping)
  - DownloadMonitorService: Added `using var` to 1 FormUrlEncodedContent instance
  - NotificationService: Added `using var` to 1 StringContent instance
- **Cross-Browser Compatibility**: Replaced `crypto.randomUUID()` with polyfill for Safari <15.4 and older browsers
  - SettingsView: Implemented `generateUUID()` function using `Math.random()` with RFC 4122 v4 format
- **Virtual Scrolling**: Fixed ROW_HEIGHT constant in WantedView (140 → 165) for accurate scroll positioning
- **Performance Optimization**: Replaced inefficient `ContainsKey` + indexer pattern with `TryGetValue`
  - DownloadService: 30+ instances optimized in qBittorrent queue parsing
  - SearchService: Changed ASIN deduplication to use `TryAdd`
- **Code Quality**: Fixed useless assignment in SystemService log reading

### Changed

- **Code Documentation**: Replaced vague TODO comments with detailed NOTE explanations
- DownloadService: Documented 4 minimal method implementations (GetActiveDownloadsAsync, GetDownloadAsync, CancelDownloadAsync, UpdateDownloadStatusAsync)
- AudiobooksView: Explained downloading status requires Download-to-Audiobook linking
- SettingsView: Documented webhook test API integration path for future enhancement
- **Code Formatting**: Moved inline comments to separate lines for better readability
- DownloadService: Fixed 3 inline comments in dictionary declarations following C# conventions
- **Logger Integration**: Systematic replacement of console statements with environment-aware logging
  - Development: Full debug logging enabled
  - Production: Only error logging for performance and log pollution prevention
- **API Documentation**: Verified Swagger/OpenAPI configuration with XML comments enabled
- **Release Readiness**: Comprehensive polish for stable production deployment

### Documentation

- Added security best practices for deployment, configuration, and known security considerations
- Documented supported versions and security update process
- Created complete release documentation structure
- Added GitHub repository links for version comparison
- Fixed XML comment HTML entity encoding in NotificationService

### Technical Debt

- Download-to-Audiobook linking system not yet implemented (documented in AudiobooksView.vue)
  - Currently downloads tracked separately in DownloadsView until completion
  - Future enhancement: Link Download records to Audiobook IDs for real-time status
- DownloadService methods remain minimal as downloads managed by external clients
  - Architecture decision: Queue fetched directly from qBittorrent, Transmission, SABnzbd, NZBGet
  - SignalR broadcasts handle real-time updates without polling
- **Generic Exception Catches**: Program.cs uses generic catches for startup resilience (intentional design)
  - Proxy configuration (line 331): Non-critical, logs warning and continues
  - Swagger XML comments (line 378): Non-critical, logs warning and continues
  - Database migration (line 465): Has detailed fallback strategy for test compatibility
  - EnsureCreated fallback (line 493): Explicitly designed for test harness flexibility
  - All catches log appropriately and allow app to start despite configuration failures

## [0.2.19] - Previous Release

### Added

- Initial beta release with core audiobook management features
- Multi-API search across torrent and NZB providers
- Download client integration (qBittorrent, Transmission, SABnzbd, NZBGet)
- Audible metadata integration via Audnexus API
- SQLite database with Entity Framework Core
- Vue.js 3 frontend with TypeScript and Pinia state management
- Real-time download status via SignalR
- Image caching service with automatic cleanup
- File browser for path selection
- Modern responsive dashboard

---

## Version History Legend

- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Vulnerability fixes
