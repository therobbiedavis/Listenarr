<p align="center">
  <img src=".github/logo-full.png" alt="Listenarr Logo" width="600">
</p>

<h1 align="center">Listenarr</h1>

<p align="center">
  <strong>Automated Audiobook Download and Management System</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#configuration">Configuration</a> •
  <a href="#api-endpoints">API</a> •
  <a href="#contributing">Contributing</a>
</p>

---

Listenarr is a comprehensive solution for searching, downloading, and processing audio media (audiobooks, music, podcasts) from various sources. It features a modern web interface built with Vue.js and a robust C# backend API.

## Features

- 🔍 **Multi-API Search**: Search across multiple torrent and NZB APIs simultaneously
- 📥 **Download Management**: Support for popular download clients (qBittorrent, Transmission, SABnzbd, NZBGet)
- 🎵 **Metadata Processing**: Automatic metadata enrichment using Audnex.us API
- 📁 **File Organization**: Intelligent file naming and folder organization
- 🖥️ **Modern Web UI**: Responsive Vue.js interface with TypeScript
- ⚙️ **Flexible Configuration**: Easy setup for APIs, download clients, and settings
- 📊 **Real-time Monitoring**: Track download progress and status

## Technology Stack

### Backend (C# .NET Core)
- ASP.NET Core Web API
- Entity Framework Core (planned)
- JSON file-based configuration
- HTTP client for external API integration

### Frontend (Vue.js)
- Vue 3 with Composition API
- TypeScript for type safety
- Pinia for state management
- Vue Router for navigation
- Vite for fast development and building

## Project Structure

```
Listenarr/
├── ListenArr.Api/                 # C# Backend API
│   ├── Controllers/               # API Controllers
│   ├── Models/                    # Data Models
│   ├── Services/                  # Business Logic Services
│   └── Program.cs                 # Application Entry Point
├── fe/                 # Vue.js Frontend
│   ├── src/
│   │   ├── components/            # Vue Components
│   │   ├── views/                 # Page Views
│   │   ├── stores/                # Pinia Stores
│   │   ├── services/              # API Services
│   │   └── types/                 # TypeScript Types
│   └── package.json
└── README.md
```

## Quick Start

### Prerequisites

- .NET 7.0 SDK or later
- Node.js 20.x or later
- npm or yarn

### Simple Start (Recommended)

Run both frontend and backend with a single command:

#### Windows (PowerShell):
```powershell
.\start-dev.ps1
```

#### Windows (Command Prompt):
```cmd
start-dev.bat
```

#### Linux/macOS:
```bash
./start-dev.sh
```

#### Or use npm directly:
```bash
npm run dev
```

**Services will start on:**
- Backend API: http://localhost:5146
- Frontend Web: http://localhost:5173

The startup scripts will automatically:
- Check for required prerequisites (Node.js, .NET SDK)
- Install dependencies if needed
- Start both services with colored console output
- Display URLs when ready

### Manual Setup

#### Backend Setup

1. Navigate to the API directory:
   ```bash
   cd ListenArr.Api
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run the API:
   ```bash
   dotnet run
   ```

   The API will be available at `http://localhost:5146`

#### Frontend Setup

1. Navigate to the web directory:
   ```bash
   cd fe
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm run dev
   ```

   The web interface will be available at `http://localhost:5173` (or next available port)

### Available Commands

#### Root Directory Commands
```bash
npm run dev          # Start both API and Web (recommended)
npm start            # Alias for dev
npm run dev:api      # Start only backend API
npm run dev:web      # Start only frontend web
npm run build        # Build both for production
npm run build:api    # Build only API
npm run build:web    # Build only Web
npm run install:all  # Install all dependencies
npm test             # Run frontend tests
```

#### Frontend Directory Commands (fe/)
```bash
npm run dev          # Start frontend dev server
npm run dev:full     # Start both frontend and backend (alternative)
npm run build        # Build for production
npm run preview      # Preview production build
npm run test:unit    # Run unit tests
npm run test:e2e     # Run E2E tests
```

## Configuration

### API Sources

Configure your search APIs in the Settings page:

- **Name**: Friendly name for the API
- **Base URL**: API endpoint URL
- **API Key**: Authentication key (if required)
- **Type**: torrent or nzb
- **Priority**: Search order priority

### Download Clients

Configure your download clients:

- **qBittorrent**: Web UI enabled torrent client
- **Transmission**: Popular cross-platform torrent client
- **SABnzbd**: Usenet downloader
- **NZBGet**: Efficient usenet downloader

### Application Settings

- **Output Path**: Where processed files are stored
- **File Naming Pattern**: Customize file and folder naming
- **Metadata Processing**: Enable/disable automatic metadata enhancement
- **Audnexus Integration**: Configure metadata source API

## API Endpoints

### Search
- `GET /api/search?query={query}` - Search all configured APIs
- `GET /api/search/api/{apiId}?query={query}` - Search specific API
- `POST /api/search/test/{apiId}` - Test API connection

### Downloads
- `GET /api/downloads` - Get all downloads
- `GET /api/downloads/{id}` - Get specific download
- `POST /api/downloads` - Start new download
- `DELETE /api/downloads/{id}` - Cancel download

### Configuration
- `GET /api/configuration/apis` - Get API configurations
- `POST /api/configuration/apis` - Save API configuration
- `GET /api/configuration/download-clients` - Get download client configurations
- `POST /api/configuration/download-clients` - Save download client configuration
- `GET /api/configuration/settings` - Get application settings
- `POST /api/configuration/settings` - Save application settings

## Development

### Running Tests

Backend:
```bash
cd ListenArr.Api
dotnet test
```

Frontend:
```bash
cd fe
npm run test:unit  # Unit tests
npm run test:e2e   # End-to-end tests
```

### Building for Production

Backend:
```bash
cd ListenArr.Api
dotnet publish -c Release
```

Frontend:
```bash
cd fe
npm run build
```

### Code Quality

Frontend includes ESLint and Prettier for code quality:
```bash
npm run lint    # Check for linting issues
npm run format  # Format code with Prettier
```

## Roadmap

- [ ] Database integration (Entity Framework Core)
- [ ] User authentication and authorization
- [ ] Real-time WebSocket updates
- [ ] Download client auto-detection
- [ ] Advanced search filters
- [ ] Notification system (email, webhooks)
- [ ] Docker containerization
- [ ] Mobile-responsive improvements
- [ ] API rate limiting and caching
- [ ] Backup and restore functionality

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Audnexus](https://audnex.us/) for audiobook metadata
- Vue.js and .NET Core communities
- All the open-source libraries that make this project possible

## Support

If you encounter any issues or have questions:

1. Check the [Issues](https://github.com/yourusername/listenarr/issues) page
2. Create a new issue with detailed information
3. Join our community discussions

---

**Note**: This project is for educational and personal use. Ensure you comply with all applicable laws and terms of service when using download clients and API sources.