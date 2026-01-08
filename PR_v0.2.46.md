# Release v0.2.46

Major feature update with advanced search, production logging, and critical download fixes.

## What's New

### Advanced Search & Collections

Added a new advanced search modal with prefix support (ASIN, author, title, series) for more precise queries. Built out a complete collection view for managing audiobooks by author and series grouping. Backend now resolves author ASINs via Audimeta and caches author images.

The search UI now includes Prowlarr-style composite scoring with normalized display and sorting by Grabs and Language. Added ability to cancel ongoing searches with proper abort signal support throughout the backend.

### Library Management

Complete root folder system - you can now create named folders, select them when adding/editing audiobooks, and handle move/rename operations with confirmation dialogs. Added sidebar sub-navigation to group audiobooks by books, authors, or series.

Series information is now displayed more prominently with full series lists as badges with tooltips. Normalized series data from various sources for consistency.

Implemented bulk update API endpoint with frontend integration for efficient mass operations.

### Download Processing

Added archive extraction support with new `ExtractArchives` application setting (new migration included). Implemented proper import item resolution across all download client adapters following Sonarr's approach.

Download clients now have test connection functionality in settings. You can enable/disable clients directly from the settings view. Added visual indicators in wanted view showing active downloads with icons, status badges, and animations.

### UI Improvements

Quality profiles now support minimum score thresholds (similar to Sonarr's MinFormatScore) to auto-reject low-quality releases. Toast messages now also appear as persistent notifications via SignalR.

Added inspect modal for viewing and downloading cached torrent files and announce URLs. Implemented consistent image fallback system with placeholder handling. All images now use native browser lazy loading.

## Architecture Changes

### Production Logging

Cleaned up console logging across the entire application:
- Removed 10 debug console.log statements
- Migrated 82 console.error calls to errorTracking service (18 files)
- Migrated 16 console.warn calls to logger.warn service (10 files)
- SignalR logs now gated behind DEV mode checks

All application code now uses centralized logging. Infrastructure components still use console for low-level diagnostics. The errorTracking service is ready for external monitoring tools like Sentry or LogRocket.

### SearchService Refactoring

Refactored SearchService to use provider pattern for better maintainability. Created IIndexerSearchProvider interface with dynamic provider selection. Extracted three indexer-specific providers:
- TorznabNewznabSearchProvider
- MyAnonamouseSearchProvider
- InternetArchiveSearchProvider

This follows the same adapter pattern we use for download clients and makes adding new indexer types much easier.

### SettingsView Componentization

Broke down the massive SettingsView component into focused tab components:
- Reduced main file from 4,722 to 3,540 lines (25% reduction)
- Extracted 7 tab components: ApiSettingsTab, DownloadClientSettingsTab, QualityProfilesTab, NotificationSettingsTab, ImportSettingsTab, UiSettingsTab, GeneralSettingsTab
- All use composition API with proper props/emits pattern

### Search Pipeline Refactoring

Modularized the search pipeline with dedicated handlers:
- AsinEnricher for ASIN enrichment
- FallbackScraper for fallback scraping
- AsinSearchHandler for direct ASIN searches
- SearchResultScorer for result scoring

Added new api/metadata controller. The old api/search/metadata and api/search/audimeta endpoints are deprecated.

## Bug Fixes

### Download Processing (Critical)

Fixed several blocking issues with downloads:

**Transmission Import** - Downloads weren't importing automatically due to authentication failures. Implemented proper 409/session-id retry pattern in PollTransmissionAsync to match TransmissionAdapter's CSRF protection.

**Stuck Job Queue** - Jobs were getting stuck in "Processing" state and blocking all imports. Added ResetStuckJobsAsync() to reset these jobs on startup in DownloadProcessingBackgroundService.

**Status Lifecycle** - Status wasn't being set correctly after import. Ensured Status = DownloadStatus.Moved is set in all 8 code paths within CompletedDownloadProcessor.

**Import Notifications** - History entries and notifications were only being created if downloads remained in the client. Moved this logic to execute BEFORE cleanup operations so they're always created.

**Transmission Cleanup** - Torrents weren't being removed after import. Fixed by extracting torrent hash using torrentInfo.HashString instead of download.ExternalId.

### UI & Data Accuracy

**Wanted Status** - Wanted view was showing incorrect status when files were deleted from disk. Added physical file existence checks (File.Exists) in three places: LibraryController.GetAllAudiobooks, LibraryController.GetAudiobook, and ScanBackgroundService.BroadcastLibraryUpdate.

**Download Filtering** - Active downloads now properly exclude terminal states (Moved, Completed, Failed, Cancelled) for cleaner UI state.

**TypeScript** - Removed non-existent contentPath property reference from downloads store that was causing compilation errors.

### MyAnonamouse

Fixed cookie handling - now properly persists mam_id values from tracker responses and includes them on direct torrent downloads. Added unit tests for cookie persistence and download caching.

Enhanced result data with seeders, leechers, grabs, files, language, and quality fields. Added advanced search options with filters, language, and enrichment toggles.

### Other Fixes

- Import now creates destination directories automatically if missing
- Added AsyncKeyedLock to prevent cache stampede issues
- Improved null handling across services and test setup
- Fixed font loading (removed unavailable WOFF format)
- Better logging for download updates and auth operations

## Performance

- Reduced qBittorrent API calls by consolidating torrent info requests
- Added per-client poll scheduling for Transmission, SABnzbd, and NZBGet
- qBittorrent now uses per-client polling intervals, batch requests, memory caching, and field limiting
- Added v-memo directive to WantedView audiobook cards for better list rendering
- Normalized all image URLs to use API endpoint with proper placeholder handling

## Database Migrations

Four new migrations included:
- `20251225220155_AddAuthorAsins` - Author ASIN support
- `20251231003000_AddExtractArchivesToApplicationSettings` - Archive extraction setting
- `20260103175654_AddRemoveCompletedDownloadsToClients` - Remove completed downloads option
- `20260103235802_AddMinimumScoreToQualityProfile` - Quality profile minimum score

## Documentation

Updated all AI assistant instruction files in .github folder with critical patterns, troubleshooting scenarios, and project-specific guidance. Enhanced copilot-instructions.md, .cursorrules, and RULES.md. Updated all provider-specific files (ANTHROPIC.md, OpenAI.md, AZURE_OPENAI.md, BARD.md, COHERE.md, BEDROCK.md, HUGGINGFACE.md) and tool-specific files (clinerules, windsurfrules, WARP.md).

## Testing

- Added E2E tests for move flow and root folders
- Unit tests for move queue, import resolution, quality profile scoring, bulk updates
- Comprehensive tests for search sorting, scoring, MyAnonamouse parsing
- Import service tests for directory creation
- Replaced Assert.True(...Any) with Assert.Contains per xUnit analyzers

## Breaking Changes

The api/search/metadata and api/search/audimeta endpoints are deprecated. Use the new api/metadata controller instead.

## Deployment

Migrations run automatically on startup. Legacy ApplicationSettings.outputPath will be migrated to RootFolder table automatically. No manual intervention needed. New features (ExtractArchives, quality profile minimum scores) are opt-in.

---

49 commits total across features, refactoring, bug fixes, testing, and documentation.
