# Listenarr Project Instructions

This is a complete C# .NET Core Web API backend with Vue.js frontend for automated audiobook downloading and processing.

## Project Overview
- **Backend**: ASP.NET Core Web API (.NET 7.0+) with modular service architecture
- **Frontend**: Vue.js 3 + TypeScript + Pinia + Vue Router + Vite
- **Purpose**: Search multiple APIs for audiobook torrents/NZBs, manage downloads via clients (qBittorrent, Transmission, SABnzbd, NZBGet), and process files with metadata using Audnexus API
- **Database**: SQLite with Entity Framework Core (ListenArrDbContext)

## Project Structure
```
Listenarr/
├── listenarr.api/                 # Backend API (Note: lowercase directory name!)
│   ├── Controllers/               # API endpoints
│   ├── Models/                    # Data models (Audiobook, SearchResult, etc.)
│   ├── Services/                  # Business logic (Search, Metadata, etc.)
│   ├── wwwroot/cache/            # Image cache directory (gitignored)
│   ├── Program.cs                # Application entry point
│   └── listenarr.db              # SQLite database (gitignored)
├── fe/                           # Frontend Vue application
│   ├── src/
│   │   ├── components/           # Vue components (AudiobookModal, FolderBrowser, etc.)
│   │   ├── views/                # Pages (Dashboard, Search, Downloads, Settings)
│   │   ├── stores/               # Pinia stores for state management
│   │   ├── services/             # API client services
│   │   ├── types/                # TypeScript type definitions
│   │   └── router/               # Vue Router configuration
│   └── public/                   # Static assets (icon.png, logo.png)
│   └── package.json
├── .github/                      # GitHub configuration and assets
│   ├── copilot-instructions.md  # This file
│   ├── BRANDING.md              # Logo and branding guidelines
│   ├── logo-icon.png            # Brand icon (square format)
│   └── logo-full.png            # Full logo with text (horizontal)
├── start-dev.bat                 # Windows startup script
├── start-dev.ps1                 # PowerShell startup script
├── start-dev.sh                  # Linux/macOS startup script
├── package.json                  # Root package with concurrently scripts
└── docker-compose.yml            # Docker orchestration
```

## Branding
The Listenarr logo combines headphones and a book to represent audiobook listening:
- **Primary Color**: `#2196F3` (Blue)
- **Icon**: `icon.png` - Square format for favicons and app icons
- **Full Logo**: `logo.png` - Horizontal format with text for headers
- **Format**: PNG with transparency for universal compatibility
- See `.github/BRANDING.md` for complete guidelines

## Key Features Implemented
- 🔍 **Multi-API Search**: Search across multiple torrent/NZB APIs simultaneously
- 📥 **Download Management**: Support for qBittorrent, Transmission, SABnzbd, NZBGet
- 🎵 **Metadata Integration**: Audible metadata via AudibleMetadataService and Audnexus API
- 🖼️ **Image Caching**: Automatic image caching with cleanup service
- 📁 **File Browser**: FolderBrowser component for path selection
- 📚 **Library Management**: AudiobookRepository with SQLite persistence
- ⚙️ **Configuration Management**: APIs, download clients, and settings via JSON
- 🖥️ **Modern Dashboard**: Statistics and quick actions
- 📱 **Responsive Design**: Mobile and desktop support

## Architecture Details

### Backend Services
- **SearchService**: Multi-API search coordination
- **AudibleMetadataService**: Fetch metadata from Audible/Audnexus
- **AmazonAsinService**: ASIN extraction from Amazon URLs
- **ImageCacheService**: Download and cache book cover images
- **ConfigurationService**: JSON-based settings management
- **AudiobookRepository**: Database operations (EF Core)

### Frontend State Management
- **Pinia Stores**: search, downloads, configuration, library
- **API Communication**: Type-safe HTTP client with Axios-style error handling
- **Reactive Updates**: Automatic refresh for active downloads

### Database
- **SQLite** via Entity Framework Core
- **Models**: Audiobook, SearchResult, Download, Configuration
- **Context**: ListenArrDbContext with automatic migrations

## How to Run This Project

### Prerequisites
- **.NET 7.0 SDK or later** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 20.x or later** - [Download](https://nodejs.org/)
- **npm** (comes with Node.js)

### Recommended: Single Command Start

Use the provided startup scripts that handle everything automatically:

**Windows (Command Prompt):**
```bash
start-dev.bat
```

**Windows (PowerShell):**
```bash
.\start-dev.ps1
```

**Linux/macOS:**
```bash
chmod +x start-dev.sh
./start-dev.sh
```

**Cross-platform (npm):**
```bash
npm install          # First time only: installs concurrently
npm run dev          # Starts both API and Web with colored output
```

The scripts will:
1. ✅ Check prerequisites (Node.js, .NET SDK)
2. ✅ Install frontend dependencies if needed
3. ✅ Restore .NET dependencies
4. ✅ Start both servers with concurrently
5. ✅ Display colored console output (blue=API, green=WEB)

**URLs:**
- **Backend API**: http://localhost:5000
- **Frontend Web**: http://localhost:5173

### Manual Setup (Alternative)

If you prefer to start services separately:

**Terminal 1 - Backend:**
```bash
cd listenarr.api
dotnet restore       # First time only
dotnet run --urls http://localhost:5000
```

**Terminal 2 - Frontend:**
```bash
cd fe
npm install          # First time only
npm run dev
```

### Available npm Scripts (Root Directory)
```bash
npm run dev          # Start both API and Web (uses concurrently)
npm start            # Alias for 'npm run dev'
npm run dev:api      # Start only backend API
npm run dev:web      # Start only frontend web
npm run build        # Build both for production
npm run build:api    # Build only API (Release configuration)
npm run build:web    # Build only Web (production bundle)
npm run install:all  # Install frontend dependencies
npm test             # Run frontend unit tests
```

### Docker Deployment
```bash
docker-compose up --build
```

## Important Directory Names
⚠️ **Note**: The backend directory is **lowercase** `listenarr.api`, not `ListenArr.Api`
- Backend: `listenarr.api/`
- Frontend: `fe/`
- Solution file references: Uses proper casing in `listenarr.sln`

## API Endpoints

### Search
- `GET /api/search?query={query}` - Search configured APIs
- `POST /api/search/audible?query={query}` - Search Audible specifically

### Library
- `GET /api/library` - Get all audiobooks
- `GET /api/library/{id}` - Get specific audiobook
- `POST /api/library` - Add audiobook
- `PUT /api/library/{id}` - Update audiobook
- `DELETE /api/library/{id}` - Remove audiobook

### Configuration
- `GET /api/configuration` - Get all settings
- `POST /api/configuration` - Save settings

### Metadata
- `GET /api/audible/metadata?asin={asin}` - Get Audible metadata
- `POST /api/amazon/extract-asin` - Extract ASIN from URL

### File System
- `GET /api/filesystem/browse?path={path}` - Browse directories
- `GET /api/filesystem/drives` - Get available drives (Windows)

### Images
- `GET /api/images/{filename}` - Get cached cover image

## Current Status
- ✅ **FULLY OPERATIONAL** - Both frontend and backend running successfully
- ✅ **Backend API**: Running on `http://localhost:5000` with all endpoints functional
- ✅ **Frontend Web**: Running on `http://localhost:5173` with complete UI
- ✅ **Database**: SQLite with Entity Framework Core integrated
- ✅ **Integration**: API communication configured and working
- ✅ **Docker**: Ready for containerized deployment
- ✅ **Startup Scripts**: Automated development environment setup

## Development Workflow
1. Use `npm run dev` or startup scripts to run both services
2. Backend auto-restarts on C# file changes (with `dotnet watch`)
3. Frontend hot-reloads on Vue/TS file changes (Vite HMR)
4. Database migrations apply automatically on startup
5. Image cache stored in `wwwroot/cache/images/` (gitignored)

## Future Enhancements
- [ ] WebSocket for real-time download progress updates
- [ ] Enhanced error handling and validation
- [ ] User authentication system
- [ ] Advanced search filters and sorting
- [ ] Notification system integration (email, webhooks)
- [ ] Download queue management
- [ ] Automatic metadata tagging of downloaded files