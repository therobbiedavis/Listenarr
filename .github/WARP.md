# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

Listenarr is an automated audiobook collection management system built as a full-stack application with a C# .NET 8 backend API and Vue.js 3 frontend. The project follows a monorepo structure with integrated build processes.

**Core Purpose**: Search multiple APIs for audiobook torrents/NZBs, manage downloads via popular clients (qBittorrent, Transmission, SABnzbd, NZBGet), and process files with rich metadata integration.

## Development Commands

### Prerequisites
- .NET 8.0 SDK or later
- Node.js 20.x or later (specified in `fe/package.json` engines)

### Common Development Tasks

```powershell
# Start both API and Web servers (recommended)
npm run dev

# Start only backend API (runs on http://localhost:5000)
npm run dev:api

# Start only frontend web (runs on http://localhost:5173)  
npm run dev:web

# Install frontend dependencies
npm run install:all

# Build for production
npm run build

# Run frontend tests
npm test

# Run a single test file
cd fe
npm run test:unit -- src/__tests__/specific-test.spec.ts

# Lint and format frontend code
cd fe
npm run lint
npm run format
```

### Backend-Specific Commands

```powershell
# Run API with custom URL/port
cd listenarr.api
dotnet run --urls "http://localhost:5656"

# Build API in Release mode
cd listenarr.api
dotnet build -c Release

# Publish with integrated frontend build
dotnet publish listenarr.api/Listenarr.Api.csproj -c Release -o ./publish/local

# Publish API only (skip frontend build, useful when Node.js unavailable)
dotnet publish listenarr.api/Listenarr.Api.csproj -c Release -o ./publish/local /p:SkipFrontendBuild=true

# Database migrations
cd listenarr.api
dotnet ef migrations add NewMigrationName
dotnet ef database update
```

### Testing and Quality

```powershell
# Run frontend unit tests with Vitest
cd fe
npm run test:unit

# Run E2E tests with Cypress
cd fe  
npm run test:e2e

# Open Cypress interactively for debugging
cd fe
npm run test:e2e:dev
```

## Architecture Overview

### Monorepo Structure with Integrated Build

The project uses a **CI-first monorepo build approach** where the backend publish automatically builds and includes the frontend:

- **Root**: Contains orchestration scripts and Docker configuration
- **`listenarr.api/`**: C# backend with integrated MSBuild frontend targets
- **`fe/`**: Vue.js frontend that gets built into `listenarr.api/wwwroot/` during API publish

The API's `.csproj` contains MSBuild targets that automatically:
1. Run `npm ci` in the frontend directory
2. Run `npm run build` to create `fe/dist/`
3. Copy the built frontend to API's `wwwroot/` folder

This produces a single deployment artifact containing both backend and frontend.

### Backend Architecture (.NET 8 Web API)

**Service-Oriented Architecture** with dependency injection:

- **Controllers/**: API endpoints organized by feature (Search, Library, Configuration, etc.)
- **Services/**: Business logic with interface-based abstractions
  - `SearchService`: Multi-API search coordination across torrent/NZB indexers
  - `AudibleMetadataService`: Metadata fetching from Audible/Audnexus API
  - `ImageCacheService`: Automatic image downloading and caching with cleanup
  - `DownloadService`: Integration with download clients (qBittorrent, Transmission, etc.)
  - `AudioFileService`: Audio metadata extraction using FFprobe
- **Models/**: Entity Framework Core data models with SQLite
- **Hubs/**: SignalR hubs for real-time updates (download progress, etc.)
- **Background Services**: Multiple hosted services for async processing:
  - `DownloadMonitorService`: Real-time download status monitoring
  - `AutomaticSearchService`: Scheduled audiobook searching
  - `ScanBackgroundService`: File system scanning and metadata extraction
  - `ImageCacheCleanupService`: Daily cleanup of cached images

**Database**: SQLite with Entity Framework Core using `ListenArrDbContext`. Complex properties (lists, JSON) are converted to pipe-separated strings or JSON for storage.

### Frontend Architecture (Vue.js 3 + TypeScript)

**Modern Vue.js SPA** with composition API:

- **views/**: Page components (Dashboard, Search, Library, Settings, etc.)
- **components/**: Reusable UI components (modals, forms, browsers)
- **stores/**: Pinia state management (search, downloads, library, configuration)
- **services/**: API communication layer with type-safe HTTP client
- **types/**: TypeScript interfaces shared between frontend and API responses

**Key Frontend Patterns**:
- Composition API with `<script setup>` syntax
- Pinia stores for reactive state management
- SignalR integration for real-time updates
- Phosphor Icons for consistent iconography

### Data Flow and Integration

1. **Search Flow**: Multi-API search via `SearchService` → Results cached → Frontend displays with metadata enrichment
2. **Download Flow**: User initiates → `DownloadService` → External client API → Background monitoring → SignalR updates → Frontend updates
3. **Library Flow**: File scanning → Metadata extraction → Database persistence → Cache management
4. **Real-time Updates**: SignalR hubs push updates to connected frontend clients

## Key Design Patterns

### Configuration Management
- JSON-based configuration stored in `config/` directory
- `ConfigurationService` manages API endpoints, download clients, application settings
- Settings are hot-reloadable and persisted to SQLite database

### Background Processing
- Multiple `IHostedService` implementations for async work
- Queue-based processing for file scanning and metadata extraction
- Automatic retry logic and error handling

### API Integration
- Typed HttpClient services with automatic decompression
- Rate limiting and caching for external API calls
- Playwright integration for JavaScript-rendered pages when needed

### Error Handling and Resilience  
- Global exception handling in API controllers
- Frontend error boundaries and user notifications
- Download client connection testing and fallback mechanisms

### Security
- Cookie-based authentication with CSRF protection
- API key middleware for external integrations
- CORS configuration for development and production

## Development Workflow

### Local Development
1. Use `npm run dev` to start both services simultaneously with colored console output
2. Backend auto-restarts on C# changes (via `dotnet watch`)
3. Frontend hot-reloads on Vue/TS changes (via Vite HMR)
4. Database migrations apply automatically on startup
5. Images cached in `listenarr.api/wwwroot/cache/` (gitignored)

### Production Deployment
- Docker images available at `therobbiedavis/listenarr` (latest, stable, nightly tags)
- Self-contained executables for Windows, Linux, macOS
- Single-container deployment includes both API and frontend
- Persistent volume at `/app/config` for database and configuration

### Version Management
- Version controlled in `listenarr.api/Listenarr.Api.csproj`
- CI automatically increments versions:
  - Nightly builds: patch increment (1.2.3 → 1.2.4)
  - Release builds: minor increment + patch reset (1.2.3 → 1.3.0)

## Important Development Notes

### Directory Naming
- Backend directory is **lowercase**: `listenarr.api/` (not `ListenArr.Api/`)
- Frontend directory: `fe/` 
- Docker contexts and paths are case-sensitive

### Port Configuration
- Backend API: `http://localhost:5000` (development)
- Frontend Web: `http://localhost:5173` (development)  
- Production: `http://localhost:5000` (single container serves both) 
- macOS users: Port 5000 conflicts with Airplay, use `--urls` parameter to override

### Database Location
- Development: `listenarr.api/config/database/listenarr.db`
- Production: `/app/config/database/listenarr.db`
- Automatic migrations on startup

### External Dependencies
- FFprobe binary required for audio metadata extraction (auto-installed by `FfmpegInstallerService`)
- Playwright browsers for web scraping (downloaded on first use)

### CI/CD Integration  
- GitHub Actions for automated builds and deployment
- Multi-platform Docker images (AMD64/ARM64)
- Automated version bumping with branch protection persistence