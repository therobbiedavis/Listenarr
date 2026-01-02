# Listenarr Discord Bot

This folder contains a small reference Discord bot that integrates with Listenarr via the API.

Features:
- Reads Listenarr settings from `GET /api/configuration/settings` to find bot token, app id and command names
- Registers a slash command of the form `/request audiobook` (configurable)
- Runs a title search against Listenarr and shows results in a select menu
- Shows metadata embed, quality profile select and a confirm button
- Submits a request to Listenarr using `POST /api/library/add`

Prerequisites
- Node.js 18+ installed
- The Listenarr API reachable (default: `http://localhost:5000`)

Quick start

1. Install dependencies

```bash
cd tools/discord-bot
npm install
```

2. Run the bot

```bash
# from project root or the tools/discord-bot folder
LISTENARR_URL=http://localhost:5000 node index.js
```

Configuration
- Configure the bot values in the Listenarr UI: go to Settings -> Requests and fill in:
  - Discord Application ID
  - Discord Guild ID (optional for per-guild registration)
  - Bot Token (the bot must be created in the Discord Developer Portal)
  - Command Group Name (default: `request`)
  - Command Subcommand Name (default: `audiobook`)

Security
- Storing a bot token in the application database is convenient for local setups but is not recommended for production.
- Consider running the bot separately and providing the token as an environment variable instead of saving it in Listenarr.

Notes
- This is an example implementation to get you started. You may want to harden interaction handling, add persistent session storage, and better error handling.
- API note: metadata lookups now live at `/api/metadata/{asin}` (and `/api/metadata/audimeta/{asin}`); the bot has been updated accordingly. Legacy `/api/search/metadata` routes are deprecated and may be removed.
