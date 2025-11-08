<p align="center">
  <img src=".github/logo-full.png" alt="Listenarr Logo" width="600">
</p>

<p align="center">
  <strong>Automated Audiobook Collection Management</strong>
</p>

<p align="center">
  <a href="https://github.com/therobbiedavis/Listenarr/releases"><img alt="Release" src="https://img.shields.io/github/v/release/therobbiedavis/Listenarr?style=flat-square"></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-blue?style=flat-square"></a>
  <a href="https://github.com/therobbiedavis/Listenarr/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/therobbiedavis/Listenarr/total?style=flat-square"></a>
  <a href="https://hub.docker.com/r/therobbiedavis/listenarr"><img alt="Docker Pulls" src="https://img.shields.io/docker/pulls/therobbiedavis/listenarr?style=flat-square"></a>
</p>

---

Listenarr is a fast, feature-rich, cross-platform audiobook management server. Built with a focus on being a complete solution for all your audiobook downloading needs. Set up your own server and get ready to streamline your audiobook listening!

## What Listenarr Provides

- **Serve up Audiobooks** from multiple sources (torrents, NZBs) with support for various formats (MP3, M4A, M4B, FLAC, AAC, OGG, OPUS)
- **First-class responsive web interface** that works great on any device (phone, tablet, desktop)
- **Rich metadata support** with automatic enrichment from Audible and Amazon
- **External API integration** for searching across multiple torrent and NZB indexers simultaneously
- **Ways to organize your library**: Collections, Reading Lists, Want to Read
- **Download management** with support for popular clients (qBittorrent, Transmission, SABnzbd, NZBGet)
- **Flexible configuration** with role-based access control (coming soon)
- **Real-time monitoring** of download progress and status
- **Intelligent file organization** with customizable naming patterns
- **Full localization support** (coming soon)

## Support

[![Discord](https://img.shields.io/badge/Discord-Join-7289DA?style=flat-square&logo=discord)](https://discord.gg/CwZ2Sqp9NF) [![GitHub Issues](https://img.shields.io/badge/GitHub-Issues-red?style=flat-square&logo=github)](https://github.com/therobbiedavis/Listenarr/issues)

Join our community on Discord for help, announcements, and discussion: https://discord.gg/CwZ2Sqp9NF

## Setup

The easiest way to get started is to use Docker/Executables or npm scripts:

### Executables
While Listenarr provides executables for Windows, Linux, and MacOS, only Windows is tested due to hardware access limitations.

- Download and extract the release that matches your system

 #### Windows
 ```cmd
cd .\publish\win-x64
.\Listenarr.Api.exe
```

```terminal
cd ./publish/osx-x64  # or osx-arm64 for Apple Silicon
chmod +x Listenarr.Api
./Listenarr.Api
```

#### Linux
```terminal
cd ./publish/linux-x64
chmod +x Listenarr.Api
./Listenarr.Api
```

**Service will be available at:**
- Web App: http://localhost:5000

**Note**:
If you need to override the port, use `--urls "http://localhost:5656"` when running the executable

### Docker

```bash
```

**Service will be available at:**
- Web App: http://localhost:5000

The Docker image includes both the backend API and frontend in a single container. For production, use the latest stable image from Docker Hub.

**Available Tags:**
- `latest` / `stable` - Latest stable release
- `canary` - Latest canary build (pre-release)
- `canary-X.Y.Z` - Specific canary version
- `nightly-X.Y.Z` - Specific nightly version
- `X.Y.Z` - Specific release version
LISTENARR_URL=http://localhost:5000 node index.js
### Manual Setup

If you prefer to run the services separately:

**Prerequisites:**
- .NET 8.0 SDK or later
- Node.js 20.x or later

**Install dependencies:**
```bash
npm run install:all  # Install frontend dependencies
```

**Start development servers:**
```bash
npm run dev          # Start both API and Web (recommended)
# OR run separately:
npm run dev:api      # Start only backend API
npm run dev:web      # Start only frontend web
```

**Services will be available at:**
- Backend API: http://localhost:5000
- Frontend Web: http://localhost:5173

## CI/CD

Listenarr uses GitHub Actions for automated building and deployment:

- **Canary Builds** (on `canary` pushes): Builds self-contained executables (Linux x64, Windows x64) and Docker images. Creates GitHub pre-releases with downloadable zips and pushes Docker images tagged as `canary` and `canary-X.Y.Z`.
- **Nightly Builds** (on `develop` pushes): Builds self-contained executables (Linux x64, Windows x64) and Docker images. Creates GitHub pre-releases with downloadable zips and pushes Docker images tagged as `nightly` and `nightly-X.Y.Z`.
- **Release Builds** (on version tags): Builds executables for Linux x64, Windows x64, and macOS x64. Creates GitHub releases with artifacts and pushes Docker images tagged as `stable`, `latest`, and `X.Y.Z`.

Version numbers are automatically incremented:
- Canary: Patch version +1
- Nightly: Minor version +1, patch reset to 0
- Release: Major version +1, minor and patch reset to 0

All builds are CI-first: `dotnet publish` automatically builds the frontend and includes it in the API's `wwwroot`.

## Feature Requests

Got a great idea? Throw it up on [Discussions](https://github.com/therobbiedavis/Listenarr/discussions) or vote on another idea. Many great features in Listenarr are driven by our community.

## Notice

⚠️ Listenarr is being actively developed and should be considered **beta software**. The platform may be subject to changes as it is being built out. You may experience data loss and need to restart. The Listenarr team strives to avoid any data loss, but please maintain backups of important data.

## Technology Stack

### Backend
- **ASP.NET Core** - Web API framework
- **Entity Framework Core** - Database ORM with SQLite
- **C# 12** with .NET 8.0+

### Frontend
- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe development
- **Pinia** - State management
- **Vite** - Lightning-fast build tool
- **Phosphor Icons** - Beautiful icon library

## Project Structure

```
Listenarr/
├── listenarr.api/              # C# Backend API
│   ├── config/                 # User configuration and data
│   │   ├── appsettings/        # Application configuration files
│   │   ├── cache/              # Image cache storage
│   │   ├── database/           # SQLite database files
│   │   ├── logs/               # Application log files
│   │   └── temp/               # Temporary download storage
│   ├── Controllers/            # API Endpoints
│   ├── Models/                 # Data Models
│   ├── Services/               # Business Logic
│   ├── Dockerfile.runtime      # Runtime Docker image for combined API + frontend
│   ├── Program.cs              # Entry Point
│   └── ...
├── fe/                         # Vue.js Frontend
│   ├── src/
│   │   ├── components/         # Vue Components
│   │   ├── views/              # Pages
│   │   ├── stores/             # Pinia Stores
│   │   └── services/           # API Services
│   └── public/                 # Static Assets
├── docker-compose.yml          # Docker Configuration
└── README.md
```

## Configuration

### API Sources

Configure your search APIs in the Settings page:

- Multiple torrent/NZB indexer support
- API key management
- Priority-based search ordering
- Connection testing

### Download Clients

Supported download clients:

- **qBittorrent** - Popular torrent client with web UI
- **Transmission** - Cross-platform torrent client
- **SABnzbd** - Usenet downloader
- **NZBGet** - Efficient usenet client

### Application Settings

- Custom output paths and file organization
- Configurable naming patterns
- Automatic metadata fetching
- Library management options

## API Endpoints

### Search
- `GET /api/search?query={query}` - Search all configured APIs
- `POST /api/search/audible?query={query}` - Search Audible metadata

### Library
- `GET /api/library` - Get all audiobooks
- `GET /api/library/{id}` - Get specific audiobook
- `POST /api/library` - Add audiobook
- `PUT /api/library/{id}` - Update audiobook
- `DELETE /api/library/{id}` - Remove audiobook

### Configuration
- `GET /api/configuration` - Get all settings
- `POST /api/configuration` - Save settings

For complete API documentation, see our [API Reference](https://github.com/therobbiedavis/Listenarr/wiki/API) (coming soon).

## Development

### Prerequisites

- .NET 8.0 SDK or later
- Node.js 20.x or later
- npm (comes with Node.js)

### Available Commands

```bash
npm run dev          # Start both API and Web
npm run dev:api      # Start only backend API
npm run dev:web      # Start only frontend web
npm run build        # Build both for production
npm run test         # Run frontend tests
```

### Building for Production

**Backend:**
```bash
cd listenarr.api
dotnet publish -c Release
```

**Frontend:**
```bash
cd fe
npm run build
```

### CI-first monorepo build

The repository is configured so the API publish will build the frontend and copy the `fe/dist` output into the API `wwwroot`. This produces a single publish artifact that serves both backend and frontend.

To build locally (requires Node + npm):

```bash
# from repo root
dotnet publish listenarr.api/Listenarr.Api.csproj -c Release -o ./publish/local
```

If you want to skip the frontend build (no Node on host):

```bash
dotnet publish listenarr.api/Listenarr.Api.csproj -c Release -o ./publish/local /p:SkipFrontendBuild=true
```

To build a runtime Docker image from the publish output (CI-first):

```bash
# context: listenarr.api/publish/<rid>
docker build -f listenarr.api/Dockerfile.runtime -t <your-image> listenarr.api/publish/linux-x64
```

### Version Management

Application versions are managed in `listenarr.api/Listenarr.Api.csproj` with a `<Version>` element. CI automatically bumps versions on builds:
- Nightly: Increments patch (e.g., 1.2.3 → 1.2.4)
- Release: Increments minor and resets patch (e.g., 1.2.3 → 1.3.0)

Bumped versions are persisted via PR to maintain branch protection.


## Roadmap

- [x] User authentication and authorization
- [x] Real-time WebSocket updates for downloads
- [ ] Advanced search filters and smart collections
- [ ] Notification system (email, webhooks, Discord)
- [ ] Mobile apps (iOS/Android)
- [ ] Audiobookshelf integration
- [ ] Multi-language support
- [ ] Backup and restore functionality
- [ ] Plugin system for extensibility

## Contributors

This project exists thanks to all the people who contribute. [Contribute](CONTRIBUTING.md).

<a href="https://github.com/therobbiedavis/Listenarr/graphs/contributors"><img src="https://opencollective.com/Listenarr/contributors.svg?width=890&button=false" /></a>

## License

- [GNU Affero General Public License v3.0](LICENSE)
- Copyright 2024-2025 Robbie Davis

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

The AGPL-3.0 license ensures that any modifications made to Listenarr, including when hosted as a network service, must be shared with users. This protects the open-source nature of the project and prevents proprietary forks.

## Acknowledgments

- [Audnexus](https://audnex.us/) - Audiobook metadata API
- [Sonarr](https://sonarr.tv/) / [Radarr](https://radarr.video/) - Inspiration for the *arr naming and architecture
- Vue.js and .NET communities
- All the open-source libraries that make this project possible

---

<p align="center">
  <strong>Disclaimer:</strong> This project is for educational and personal use. Ensure you comply with all applicable laws and terms of service when using download clients and API sources.
</p>
