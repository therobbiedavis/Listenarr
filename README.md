<p align="center">
  <img src=".github/logo-full.png" alt="Listenarr Logo" width="600">
</p>

<h1 align="center">Listenarr</h1>

<p align="center">
  <strong>Automated Audiobook Management and Downloading</strong>
</p>

<p align="center">
  <a href="https://github.com/therobbiedavis/Listenarr/releases"><img alt="Release" src="https://img.shields.io/github/v/release/therobbiedavis/Listenarr?style=flat-square"></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/github/license/therobbiedavis/Listenarr?style=flat-square"></a>
  <a href="https://github.com/therobbiedavis/Listenarr/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/therobbiedavis/Listenarr/total?style=flat-square"></a>
  <a href="https://hub.docker.com/r/therobbiedavis/listenarr"><img alt="Docker Pulls" src="https://img.shields.io/docker/pulls/therobbiedavis/listenarr?style=flat-square"></a>
</p>

---

Listenarr is a fast, feature-rich, cross-platform audiobook management server. Built with a focus on being a complete solution for all your audiobook needs. Set up your own server and share your audiobook collection with your friends and family!

## What Listenarr Provides

- **Serve up Audiobooks** from multiple sources (torrents, NZBs) with support for various formats (MP3, M4A, M4B, FLAC, AAC, OGG, OPUS)
- **First-class responsive web interface** that works great on any device (phone, tablet, desktop)
- **Rich metadata support** with automatic enrichment from Audible via Audnexus API
- **External API integration** for searching across multiple torrent and NZB indexers simultaneously
- **Ways to organize your library**: Collections, Reading Lists, Want to Read
- **Download management** with support for popular clients (qBittorrent, Transmission, SABnzbd, NZBGet)
- **Flexible configuration** with role-based access control (coming soon)
- **Real-time monitoring** of download progress and status
- **Intelligent file organization** with customizable naming patterns
- **Full localization support** (coming soon)

## Support

[![Discord](https://img.shields.io/badge/Discord-Join-7289DA?style=flat-square&logo=discord)](https://discord.gg/your-invite) [![GitHub Issues](https://img.shields.io/badge/GitHub-Issues-red?style=flat-square&logo=github)](https://github.com/therobbiedavis/Listenarr/issues)

## Setup

The easiest way to get started is to use one of our startup scripts or Docker:

### Quick Start Scripts

**Windows (PowerShell):**
```powershell
.\start-dev.ps1
```

**Windows (Command Prompt):**
```cmd
start-dev.bat
```

**Linux/macOS:**
```bash
chmod +x start-dev.sh
./start-dev.sh
```

**Cross-platform (npm):**
```bash
npm install
npm run dev
```

### Docker

```bash
docker-compose up --build
```

**Services will be available at:**
- Backend API: http://localhost:5146
- Frontend Web: http://localhost:5173

For detailed installation instructions, see our [Wiki](https://github.com/therobbiedavis/Listenarr/wiki) (coming soon).

## Feature Requests

Got a great idea? Throw it up on [Discussions](https://github.com/therobbiedavis/Listenarr/discussions) or vote on another idea. Many great features in Listenarr are driven by our community.

## Notice

⚠️ Listenarr is being actively developed and should be considered **beta software**. The platform may be subject to changes as it is being built out. You may experience data loss and need to restart. The Listenarr team strives to avoid any data loss, but please maintain backups of important data.

## Technology Stack

### Backend
- **ASP.NET Core** - Web API framework
- **Entity Framework Core** - Database ORM with SQLite
- **C# 12** with .NET 7.0+

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
│   ├── Controllers/            # API Endpoints
│   ├── Models/                 # Data Models
│   ├── Services/               # Business Logic
│   └── Program.cs              # Entry Point
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

- .NET 7.0 SDK or later
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

## Roadmap

- [ ] User authentication and authorization
- [ ] Real-time WebSocket updates for downloads
- [ ] Advanced search filters and smart collections
- [ ] Notification system (email, webhooks, Discord)
- [ ] Mobile apps (iOS/Android)
- [ ] Audiobook playback integration
- [ ] Multi-language support
- [ ] Backup and restore functionality
- [ ] Plugin system for extensibility

## Contributors

This project exists thanks to all the people who contribute. [Contribute](CONTRIBUTING.md).

<a href="https://github.com/therobbiedavis/Listenarr/graphs/contributors"><img src="https://opencollective.com/Listenarr/contributors.svg?width=890&button=false" /></a>

## License

- [MIT License](LICENSE)
- Copyright 2024-2025

## Acknowledgments

- [Audnexus](https://audnex.us/) - Audiobook metadata API
- [Sonarr](https://sonarr.tv/) / [Radarr](https://radarr.video/) - Inspiration for the *arr naming and architecture
- Vue.js and .NET communities
- All the open-source libraries that make this project possible

---

<p align="center">
  <strong>Disclaimer:</strong> This project is for educational and personal use. Ensure you comply with all applicable laws and terms of service when using download clients and API sources.
</p>