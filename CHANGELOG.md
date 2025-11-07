# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Professional Webhook Test Menu**: Enhanced notification testing UI
  - Bell icon dropdown menu in AudiobookDetailView with 3 trigger options
  - Only appears when webhooks are configured and enabled
  - Automatic webhook selector modal for multiple webhook configurations
  - Targeted testing: Send test notifications to specific webhooks
  - Backend support: DiagnosticsController now accepts optional webhookId parameter
  - Improved UX: Shows webhook name in success toast notifications

### Fixed
- **Development-Only UI Elements**: Hidden test notification buttons in production
  - AudiobookDetailView: Wrapped 3 test notification buttons in `v-if="isDevelopment"` check
  - Buttons only visible in development mode, preventing confusion in production deployments

### Changed

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

[0.2.20]: https://github.com/therobbiedavis/Listenarr/compare/v0.2.19...v0.2.20
[0.2.19]: https://github.com/therobbiedavis/Listenarr/compare/v0.2.18...v0.2.19
[0.2.18]: https://github.com/therobbiedavis/Listenarr/releases/tag/v0.2.18
