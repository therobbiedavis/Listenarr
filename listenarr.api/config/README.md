# Listenarr API Configuration

This document describes the configuration folder structure for the Listenarr API.

## Config Folder Structure

```
config/
├── appsettings/          # Application configuration files
│   ├── appsettings.json
│   └── appsettings.Development.json
├── cache/                # Image cache storage
│   └── images/
│       ├── temp/         # Temporary image cache
│       └── library/      # Permanent image cache
├── database/             # SQLite database files
````markdown
# Listenarr API Configuration

This document describes the configuration folder structure for the Listenarr API.

## Config Folder Structure

```
config/
├── appsettings/          # Application configuration files
│   ├── appsettings.json
│   └── appsettings.Development.json
├── cache/                # Image cache storage
│   └── images/
│       ├── temp/         # Temporary image cache
│       └── library/      # Permanent image cache
├── database/             # SQLite database files
│   └── listenarr.db
├── logs/                 # Application log files
│   └── listenarr-YYYY-MM-DD.log
└── temp/                 # Temporary download storage
        └── downloads/        # DDL download staging area
```

## Directory Purposes

### appsettings/
Contains application configuration files:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development-specific settings

### cache/images/
Stores cached book cover images:
- `temp/` - Temporary cache for newly downloaded images
- `library/` - Permanent storage for processed images

### database/
Contains the SQLite database file:
- `listenarr.db` - Main application database

### logs/
Contains application log files:
- Daily log files named `listenarr-YYYY-MM-DD.log`
- Automatically cleaned up by the application

### temp/downloads/
Temporary storage for DDL (Direct Download Link) files:
- Files download here first, then get processed and moved to final locations
- Automatically cleaned up after successful processing or after 24 hours

## Important Notes

- The `config/` folder contains all user-specific data and should be backed up
- Database files are SQLite-based and contain all application data
- Temp and cache directories are automatically managed by background services
- Log files are rotated daily and older logs may be cleaned up automatically

## ExternalRequests configuration (US domain / optional proxy)

The application supports an `ExternalRequests` configuration section that controls retry behavior for external scrapes (Amazon/Audible) and an optional named `us` HttpClient that can be routed through an HTTP proxy.

Example settings (see `appsettings.Development.json`):

```json
{
    "ExternalRequests": {
        "PreferUsDomain": true,
        "UseUsProxy": false,
        "UsProxyHost": "us-proxy.example.com",
        "UsProxyPort": 8080,
        "UsProxyUsername": "",
        "UsProxyPassword": ""
    }
}
```

Notes:
- `PreferUsDomain`: when true the services will attempt a retry using a `.com` (US) domain if a localized/redirect/noise page is detected.
- `UseUsProxy`: when true, the named `us` HttpClient is configured to use the proxy host/port and optional credentials (read from `UsProxyUsername`/`UsProxyPassword`).
- Credentials should be provided securely via environment variables or a secrets store in production. The development sample above leaves them blank to avoid committing secrets.

If you enable proxying, set the values appropriately in `appsettings.Development.json` (for local testing) or in your environment/secret store for production.
````