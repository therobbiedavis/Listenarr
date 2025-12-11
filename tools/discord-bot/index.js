/*
  Simple Discord bot to integrate with Listenarr API.
  - Reads Listenarr settings from GET /api/configuration/settings
  - When enabled, logs in and registers a slash command in the application (or guild) like:
    /<group> <subcommand> title:<string>
  - Presents a select menu of search results, then an embed with a quality select and confirm button

  Usage: (from repo root)
    cd tools/discord-bot
    npm install
    LISTENARR_URL=http://localhost:5000 node index.js

  Notes:
  - This is a minimal reference implementation. In production you should handle secrets securely,
    persist sessions, and add retry/backoff and better error handling.
*/

const { Client, GatewayIntentBits, REST, Routes, SlashCommandBuilder, ActionRowBuilder, StringSelectMenuBuilder, ButtonBuilder, ButtonStyle, EmbedBuilder, InteractionType } = require('discord.js')
// node-fetch + fetch-cookie + tough-cookie to persist cookies across requests
let fetch = require('node-fetch')
try {
  const fetchCookie = require('fetch-cookie')
  const tough = require('tough-cookie')
  const jar = new tough.CookieJar()
  fetch = fetchCookie(fetch, jar)
  console.log('Initialized cookie-aware fetch for antiforgery support')
} catch (err) {
  console.warn('fetch-cookie/tough-cookie not available, antiforgery token requests may fail. Install fetch-cookie and tough-cookie to fix.', err)
}

// Read server-provided API key (if any) so we can authenticate programmatic requests
const LISTENARR_API_KEY = process.env.LISTENARR_API_KEY || null
if (LISTENARR_API_KEY) {
  try {
    // Wrap the fetch implementation to automatically add X-Api-Key header when not present
    const rawFetch = fetch
    fetch = async (url, opts) => {
      opts = opts || {}
      opts.headers = opts.headers || {}
      // Do not overwrite an explicit X-Api-Key header set by the caller
      if (!opts.headers['X-Api-Key'] && !opts.headers['x-api-key']) {
        opts.headers['X-Api-Key'] = LISTENARR_API_KEY
      }
      return rawFetch(url, opts)
    }
    console.log('Configured bot to use LISTENARR_API_KEY for backend requests')
  } catch (e) {
    console.warn('Failed to wrap fetch with API key header', e)
  }
}
const crypto = require('crypto')
const fs = require('fs')
const signalR = require('@microsoft/signalr')

// Compatibility shim: translate deprecated `flags: 64` option to `flags: 64`
// This avoids the runtime deprecation warning while keeping existing call sites unchanged.
try {
  const { Interaction } = require('discord.js')
  if (Interaction && Interaction.prototype) {
    const EPHEMERAL_FLAG = 64
    ;['reply', 'editReply', 'deferReply'].forEach(fn => {
      const orig = Interaction.prototype[fn]
      if (typeof orig === 'function') {
        Interaction.prototype[fn] = function (options) {
          try {
            // When called with a string or without options, forward through
            if (!options || typeof options !== 'object') return orig.call(this, options)
            if (options.ephemeral) {
              const { ephemeral, ...rest } = options
              // If flags already present, keep them; otherwise set ephemeral flag.
              if (rest.flags) return orig.call(this, rest)
              return orig.call(this, Object.assign({}, rest, { flags: EPHEMERAL_FLAG }))
            }
          } catch (e) {
            // ignore shim errors and call original
          }
          return orig.call(this, options)
        }
      }
    })
  }
} catch (e) {
  // If we can't patch (older/newer discord.js shapes), ignore and let warnings surface
}

const POLL_INTERVAL_MS = 15_000
const SESSION_TIMEOUT_MS = 1000 * 60 * 10 // 10 minutes

// Determine Listenarr base URL with several fallbacks:
// 1) process.env.LISTENARR_URL
// 2) tools/discord-bot/.env (key LISTENARR_URL)
// 3) prompt the user (interactive) and persist to .env
// 4) fallback to http://localhost:5000
function readLocalEnvFile(envPath) {
  try {
    if (!fs.existsSync(envPath)) return null
    const txt = fs.readFileSync(envPath, 'utf8')
    const lines = txt.split(/\r?\n/)
    for (const line of lines) {
      const m = line.match(/^\s*LISTENARR_URL\s*=\s*(.+)\s*$/)
      if (m) return m[1].trim().replace(/^"|"$/g, '')
    }
  } catch (e) {
    // ignore
  }
  return null
}

function writeLocalEnvFile(envPath, url) {
  try {
    const content = `LISTENARR_URL=${url}\n`
    fs.writeFileSync(envPath, content, { encoding: 'utf8', flag: 'w' })
    console.log(`Saved LISTENARR_URL to ${envPath}`)
  } catch (e) {
    console.warn('Failed to write .env file for listenarr url:', e && e.message ? e.message : e)
  }
}

function promptForListenarrUrl(defaultUrl) {
  try {
    if (!process.stdin.isTTY) return defaultUrl
    const readline = require('readline')
    const rl = readline.createInterface({ input: process.stdin, output: process.stdout })
    return new Promise(resolve => {
      rl.question(`Enter Listenarr URL [${defaultUrl}]: `, answer => {
        rl.close()
        const val = (answer && answer.trim()) || defaultUrl
        resolve(val)
      })
    })
  } catch (e) {
    return Promise.resolve(defaultUrl)
  }
}

async function resolveListenarrUrl() {
  // env var takes precedence
  if (process.env.LISTENARR_URL && process.env.LISTENARR_URL.trim()) return process.env.LISTENARR_URL.trim()
  const envPath = require('path').join(__dirname, '.env')
  const local = readLocalEnvFile(envPath)
  if (local) return local
  // prompt interactively and persist
  const defaultUrl = 'http://localhost:5000'
  const chosen = await promptForListenarrUrl(defaultUrl)
  if (chosen && chosen.trim()) {
    writeLocalEnvFile(envPath, chosen.trim())
    return chosen.trim()
  }
  return defaultUrl
}

// listenarrUrl will be resolved at startup (may prompt once and write tools/discord-bot/.env)
let listenarrUrl = 'http://localhost:5000'
resolveListenarrUrl().then(u => { listenarrUrl = (u || listenarrUrl).replace(/\/$/, '') }).catch(() => {})

let currentSettings = null
let client = null
let rest = null
let loggedInToken = null
let signalRConnection = null
// Whether the bot has Manage Messages permission in the configured channel. Set at runtime in ensureClient().
let canManageMessages = false

// In-memory session store for interactions: customId -> { metadata, timestamp }
const sessions = new Map()

// Helper: delete previous ack/select messages posted by this bot for the same user
async function deletePreviousMessagesForUser(channel, userId) {
  // If we don't have Manage Messages permission, skip deletion attempts early.
  if (!canManageMessages) return
  if (!channel || !userId) return
  try {
    for (const [k, s] of sessions.entries()) {
      if (s && s.requestingUserId === userId) {
        try {
          if (s.ackMessageId && channel && typeof channel.messages.fetch === 'function') {
            try {
              const m = await channel.messages.fetch(s.ackMessageId).catch(() => null)
              if (m && m.author && m.author.id === (client && client.user && client.user.id)) {
                await m.delete().catch(() => {})
              }
            } catch {}
          }
        } catch {}
        try {
          if (s.messageId && channel && typeof channel.messages.fetch === 'function') {
            try {
              const m2 = await channel.messages.fetch(s.messageId).catch(() => null)
              if (m2 && m2.author && m2.author.id === (client && client.user && client.user.id)) {
                await m2.delete().catch(() => {})
              }
            } catch {}
          }
        } catch {}
        // remove message id references so we don't try again later
        try { delete s.ackMessageId } catch {}
        try { delete s.messageId } catch {}
        sessions.set(k, s)
      }
    }
  } catch (err) {
    console.warn('Failed during cleanup of previous messages for user', userId, err)
  }
}

// Antiforgery token cache for API requests that require X-XSRF-TOKEN
let cachedXsrfToken = null
let cachedXsrfTokenExpires = 0

async function fetchAntiforgeryTokenForBot() {
  // Return cached token if still valid (5 minutes)
  if (cachedXsrfToken && Date.now() < cachedXsrfTokenExpires) return cachedXsrfToken
  try {
    const resp = await fetch(`${listenarrUrl.replace(/\/$/, '')}/api/antiforgery/token`, { method: 'GET' })
    if (!resp.ok) {
      console.warn('Failed to fetch antiforgery token for bot:', resp.status)
      return null
    }
    const json = await resp.json()
    const token = json?.token || null
    if (token) {
      cachedXsrfToken = token
      cachedXsrfTokenExpires = Date.now() + (1000 * 60 * 5)
    }
    // Log for debugging
    logSessionEvent(`Fetched antiforgery token for bot (len=${token?token.length:0})`)
    return token
  } catch (err) {
    console.warn('Error fetching antiforgery token for bot', err)
    return null
  }
}

// Simple file-backed session event logger to aid debugging when console output is missing
function logSessionEvent(msg) {
  try {
    fs.appendFileSync('bot-session.log', `${new Date().toISOString()} ${msg}\n`)
  } catch (err) {
    // If file logging fails, still print to console so we don't lose the event
    console.error('Failed to write session log:', err)
  }
}
function stripHtml(html) {
  if (!html) return ''
  // Remove HTML tags
  return html.replace(/<[^>]*>/g, '').trim()
}

function extractYear(dateString) {
  if (!dateString) return null
  try {
    // Handle ISO date strings like "2020-01-01T00:00:00.000Z"
    const date = new Date(dateString)
    if (!isNaN(date.getTime())) {
      return date.getFullYear().toString()
    }
    // Handle year-only strings or extract from date-like strings
    const yearMatch = dateString.match(/\b(19|20)\d{2}\b/)
    return yearMatch ? yearMatch[0] : null
  } catch {
    return null
  }
}

function makeSessionId() {
  return crypto.randomBytes(8).toString('hex')
}

function extractSeriesInfo(md) {
  if (!md) return null

  // Common shapes:
  // md.series -> string OR { name, position|number|index }
  // md.seriesName, md.seriesTitle -> string
  // md.seriesNumber, md.seriesPosition, md.seriesIndex -> number/string
  try {
    // direct string
    if (typeof md.series === 'string' && md.series.trim()) return md.series.trim()

    // series object
    if (md.series && typeof md.series === 'object') {
      const name = md.series.name || md.series.seriesName || md.series.title || md.series.series || null
      const pos = md.series.position || md.series.number || md.series.index || md.series.seriesPosition || null
      if (name) {
        if (pos !== null && pos !== undefined && String(pos).trim() !== '') return `${name} (Book ${pos})`
        return name
      }
    }

    // alternate top-level fields
    const altName = md.seriesName || md.seriesTitle || md.series_title || md.series_name
    const altPos = md.seriesNumber || md.seriesPosition || md.seriesIndex || md.series_number
    if (altName) {
      if (altPos !== null && altPos !== undefined && String(altPos).trim() !== '') return `${altName} (Book ${altPos})`
      return altName
    }

    return null
  } catch (err) {
    console.warn('extractSeriesInfo failed', err)
    return null
  }
}

async function handleSetChannelCommand(interaction) {
  // Only allow in guild context
  if (!interaction.guildId) {
    await interaction.reply({ content: 'This command must be used in a guild.', flags: 64 })
    return
  }

  // Use provided channel option or fallback to the channel where the command was invoked
  const channelOption = interaction.options.getChannel('channel')
  const channelId = (channelOption && channelOption.id) || interaction.channelId

  // Fetch current settings from Listenarr
  await interaction.deferReply({ flags: 64 })
  try {
    const resp = await fetch(`${listenarrUrl}/api/configuration/settings`)
    if (!resp.ok) {
      await interaction.editReply({ content: `Failed to fetch Listenarr settings: ${resp.status}` })
      return
    }
    const appSettings = await resp.json()

    // Set guild and channel
    appSettings.discordGuildId = interaction.guildId
    appSettings.discordChannelId = channelId

    // POST back to save settings
    const saveResp = await fetch(`${listenarrUrl}/api/configuration/settings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(appSettings)
    })
    if (!saveResp.ok) {
      const txt = await saveResp.text()
      await interaction.editReply({ content: `Failed to save settings: ${saveResp.status} ${txt}` })
      return
    }

    await interaction.editReply({ content: `Configured bot to respond in <#${channelId}> for guild ${interaction.guildId}.` })
  } catch (err) {
    console.error('Failed to set channel', err)
    try { await interaction.editReply({ content: 'Failed to set channel due to an internal error.' }) } catch {}
  }
}

async function fetchSettings() {
  try {
    const resp = await fetch(`${listenarrUrl}/api/configuration/settings`)
    if (!resp.ok) {
      console.error('Failed to fetch settings:', resp.status, resp.statusText)
      return null
    }
    return await resp.json()
  } catch (err) {
    console.error('Error fetching settings:', err)
    return null
  }
}

async function createSignalRConnection() {
  if (signalRConnection) {
    try { await signalRConnection.stop() } catch {}
  }

  const hubUrl = `${listenarrUrl.replace(/\/$/, '')}/hubs/settings`
  console.log(`Connecting to SignalR hub: ${hubUrl}`)

  // Include API key as access token for SignalR negotiate when provided so the
  // hub negotiate endpoint accepts the connection in auth-required environments.
  const hubBuilder = new signalR.HubConnectionBuilder()
  if (LISTENARR_API_KEY) {
    hubBuilder.withUrl(hubUrl, { accessTokenFactory: () => LISTENARR_API_KEY })
  } else {
    hubBuilder.withUrl(hubUrl)
  }
  signalRConnection = hubBuilder
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build()

  signalRConnection.on('SettingsUpdated', async (settings) => {
    console.log('Received settings update via SignalR')
    if (JSON.stringify(settings) !== JSON.stringify(currentSettings)) {
      currentSettings = settings
      await ensureClient(settings)
    }
  })

  signalRConnection.onclose(() => {
    console.log('SignalR connection closed')
  })

  signalRConnection.onreconnected(() => {
    console.log('SignalR connection reconnected')
  })

  try {
    await signalRConnection.start()
    console.log('SignalR connection established')
  } catch (err) {
    console.error('Failed to start SignalR connection:', err)
  }
}

async function updateBotAppearance(client, settings) {
  if (!client.user) return

  try {
    const updates = {}

    // Update username if configured
    if (settings.discordBotUsername && settings.discordBotUsername.trim()) {
      const newUsername = settings.discordBotUsername.trim()
      if (client.user.username !== newUsername) {
        updates.username = newUsername
        console.log(`Updating bot username to: ${newUsername}`)
      }
    }

    // Update avatar if configured
    if (settings.discordBotAvatar && settings.discordBotAvatar.trim()) {
      updates.avatar = settings.discordBotAvatar.trim()
      console.log(`Updating bot avatar to: ${settings.discordBotAvatar}`)
    }

    // Apply updates if any changes needed
    if (Object.keys(updates).length > 0) {
      await client.user.setUsername(updates.username)
      if (updates.avatar) {
        // For avatar, we need to fetch the image and convert to buffer
        const avatarResponse = await fetch(updates.avatar)
        if (avatarResponse.ok) {
          const avatarBuffer = await avatarResponse.buffer()
          await client.user.setAvatar(avatarBuffer)
          console.log('Bot avatar updated successfully')
        } else {
          console.warn(`Failed to fetch avatar from URL: ${updates.avatar}`)
        }
      }
      console.log('Bot appearance updated')
    }
  } catch (err) {
    console.error('Failed to update bot appearance:', err)
  }
}

async function ensureClient(settings) {
  if (!settings?.discordBotEnabled) return
  if (!settings?.discordBotToken) {
    console.warn('Discord bot enabled in settings but no token provided')
    return
  }

  if (client && loggedInToken === settings.discordBotToken) return // already logged in

  // If there's a client logged in with a different token, destroy it first
  if (client) {
    try { await client.destroy() } catch {}
    client = null
    rest = null
    // Reset runtime permission flag when client changes
    canManageMessages = false
  }

  loggedInToken = settings.discordBotToken
  client = new Client({ intents: [GatewayIntentBits.Guilds] })
  rest = new REST({ version: '10' }).setToken(settings.discordBotToken)

  client.on('interactionCreate', async (interaction) => {
    try {
      if (interaction.type === InteractionType.ApplicationCommand) {
        // Slash command
        const group = settings.discordCommandGroupName || 'request'
        const sub = settings.discordCommandSubcommandName || 'audiobook'
        if (interaction.commandName === group && interaction.options.getSubcommand(false) === sub) {
          const title = interaction.options.getString('title') || ''
          await handleSearchCommand(interaction, title)
        }
        // admin config command: set-channel
        // NOTE: temporarily disabled — uncomment to re-enable
        // if (interaction.commandName === 'request-config' && interaction.options.getSubcommand(false) === 'set-channel') {
        //   await handleSetChannelCommand(interaction)
        // }
        // debug command: request-debug perms
        // NOTE: temporarily disabled — uncomment to re-enable
        // if (interaction.commandName === 'request-debug' && interaction.options.getSubcommand(false) === 'perms') {
        //   await handleDebugCommand(interaction)
        // }
      } else if (interaction.isStringSelectMenu()) {
        if (interaction.customId.startsWith('listenarr_results_')) {
          await handleSelectMenuInteraction(interaction)
        } else if (interaction.customId.startsWith('quality_')) {
          await handleQualitySelectInteraction(interaction)
        }
      } else if (interaction.isButton()) {
        await handleButtonInteraction(interaction)
      }
    } catch (err) {
      console.error('interaction handler failed', err)
      try { await interaction.reply({ content: 'An error occurred handling your interaction.', flags: 64 }) } catch {}
    }
  })

  client.once('clientReady', () => {
    console.log('Discord client ready as', client.user?.tag)
  })

  await client.login(settings.discordBotToken)

  // Update bot appearance if configured
  await updateBotAppearance(client, settings)

  // Permission check: warn if the bot is configured to post in a channel but lacks Manage Messages
  try {
    if (settings.discordChannelId) {
      const channel = await client.channels.fetch(settings.discordChannelId).catch(() => null)
      if (channel) {
        const perms = channel.permissionsFor(client.user)
        const { PermissionsBitField } = require('discord.js')
        // Set runtime flag based on actual permissions
        canManageMessages = !!(perms && perms.has(PermissionsBitField.Flags.ManageMessages))
        if (!canManageMessages) {
            console.warn(`Bot does not have Manage Messages in configured channel ${settings.discordChannelId}. Embed deletion may fail.`)
            logSessionEvent(`Warning: missing ManageMessages permission in channel ${settings.discordChannelId}`)
            try {
              // If we have the application id, show an OAuth invite URL that includes the Manage Messages permission
              const appId = settings.discordApplicationId || process.env.DISCORD_APPLICATION_ID || process.env.DISCORD_APP_ID
              const manageBit = Number(PermissionsBitField.Flags?.ManageMessages) || 8192
              if (appId) {
                const inviteUrl = `https://discord.com/oauth2/authorize?client_id=${appId}&permissions=${manageBit}&scope=bot%20applications.commands`
                console.warn(`Invite link to grant Manage Messages: ${inviteUrl}`)
                logSessionEvent(`Invite link for ManageMessages: ${inviteUrl}`)
              }
            } catch (e) {
              // fail silently - this logging is best-effort
            }
          } else {
            console.log(`Bot has Manage Messages permission in configured channel ${settings.discordChannelId}`)
            logSessionEvent(`Bot has ManageMessages permission in channel ${settings.discordChannelId}`)
          }
      }
    }
  } catch (err) {
    console.warn('Failed to verify channel permissions for bot', err)
  }

  // Register slash command
  await registerCommands(settings)
}

async function registerCommands(settings) {
  const appId = settings.discordApplicationId
  if (!appId) {
    console.warn('discordApplicationId not configured - skipping command registration')
    return
  }

  const group = (settings.discordCommandGroupName || 'request').toLowerCase()
  const sub = (settings.discordCommandSubcommandName || 'audiobook').toLowerCase()

  // Build a command with a subcommand 'audiobook' and a required "title" option
  const command = new SlashCommandBuilder()
    .setName(group)
    .setDescription('Request items')
    .addSubcommand(sc => sc.setName(sub).setDescription('Request an audiobook').addStringOption(o => o.setName('title').setDescription('Title to search for').setRequired(true)))

  // Small admin command to set the configured channel in Listenarr (guild-scoped)
  const configCommand = new SlashCommandBuilder()
    .setName('request-config')
    .setDescription('Configure Listenarr Discord integration')
    // Temporarily disabled: comment out the admin 'set-channel' subcommand so it is not registered
    // .addSubcommand(sc => sc.setName('set-channel').setDescription('Set the channel the bot should respond in').addChannelOption(o => o.setName('channel').setDescription('Target channel').setRequired(false)));

  // Debug command to inspect permissions and sessions
  const debugCommand = new SlashCommandBuilder()
    .setName('request-debug')
    .setDescription('Debug request flow and permissions')
    .addSubcommand(sc => sc.setName('perms').setDescription('Show permission and session debug info'))

  try {
    if (settings.discordGuildId) {
      console.log(`Registering commands in guild ${settings.discordGuildId}`)
      // Commented out admin/debug commands for now: only register the main request command
      await rest.put(Routes.applicationGuildCommands(appId, settings.discordGuildId), { body: [command.toJSON()] })
    } else {
      console.log('Registering global commands')
      // Commented out admin/debug commands for now: only register the main request command
      await rest.put(Routes.applicationCommands(appId), { body: [command.toJSON()] })
    }
    console.log('Commands registered')
  } catch (err) {
    console.error('Failed to register commands', err)
  }
}

async function handleDebugCommand(interaction) {
  try {
    // Provide ephemeral diagnostic info about permissions and sessions
    const channelId = currentSettings?.discordChannelId || 'unset'
    let channelPermsInfo = 'Not configured or channel not found'
    let missing = []
    let permsSummary = ''
    try {
      if (channelId && client) {
        const channel = await client.channels.fetch(channelId).catch(() => null)
        if (channel) {
          const { PermissionsBitField } = require('discord.js')
          const perms = channel.permissionsFor(client.user)
          const needed = [PermissionsBitField.Flags.ViewChannel, PermissionsBitField.Flags.SendMessages, PermissionsBitField.Flags.ManageMessages]
          const has = (p) => !!(perms && perms.has && perms.has(p))
          missing = needed.filter(p => !has(p))
          permsSummary = `ViewChannel=${has(needed[0])} SendMessages=${has(needed[1])} ManageMessages=${has(needed[2])}`
          channelPermsInfo = `Channel ${channelId} permissions: ${permsSummary}`
        }
      }
    } catch (e) {
      channelPermsInfo = `Failed to fetch channel permissions: ${e && e.message ? e.message : e}`
    }

    const totalSessions = sessions.size
    const sessionsWithEphemeral = Array.from(sessions.values()).filter(s => s && s.ephemeralReplyToken).length
    const userSessions = Array.from(sessions.entries()).filter(([, s]) => s && s.requestingUserId === interaction.user?.id).map(([k, s]) => ({ id: k, hasEphemeral: !!s.ephemeralReplyToken }))

    const lines = []
    lines.push(`canManageMessages=${canManageMessages}`)
    lines.push(`configuredChannel=${channelId}`)
    lines.push(channelPermsInfo)
    lines.push(`missingPermIds=[${missing.join(',')}]`)
    lines.push(`sessions_total=${totalSessions} sessions_with_ephemeral=${sessionsWithEphemeral}`)
    lines.push(`your_sessions=${JSON.stringify(userSessions)}`)

    try {
      await interaction.reply({ content: lines.join('\n'), flags: 64 })
    } catch (e) {
      try { await interaction.editReply({ content: lines.join('\n') }) } catch {}
    }
  } catch (err) {
    console.error('handleDebugCommand failed', err)
    try { await interaction.reply({ content: 'Debug failed. See server logs.', flags: 64 }) } catch {}
  }
}

async function handleSearchCommand(interaction, title) {
  // If a channel is configured in Listenarr settings, only allow commands from that channel
  if (currentSettings?.discordChannelId && interaction.channelId !== currentSettings.discordChannelId) {
    try { await interaction.reply({ content: `This bot is configured to only respond in <#${currentSettings.discordChannelId}>.`, flags: 64 }) } catch {}
    return
  }

  await interaction.deferReply({ flags: 64 })

  // Call Listenarr search by title
  try {
    const resp = await fetch(`${listenarrUrl}/api/search/title?query=${encodeURIComponent(title)}&limit=10`)
    if (!resp.ok) {
      await interaction.editReply({ content: `Search failed: ${resp.status}` })
      return
    }

    // Read body as text first to allow better error logging on invalid JSON
    const bodyText = await resp.text()
    let results = null
    try {
      results = JSON.parse(bodyText)
    } catch (parseErr) {
      // Log the bad response to a local file for debugging and return a friendly message
      const snippet = bodyText ? bodyText.slice(0, 2000) : '<empty>'
      const msg = `Failed to parse search response for query=${title}: ${parseErr.message}\nResponse snippet:\n${snippet}\n`
      try { fs.appendFileSync('bot-search-errors.log', `${new Date().toISOString()} ${msg}\n`) } catch (fileErr) { console.error('Failed to append to log', fileErr) }
      console.error(msg)
      await interaction.editReply({ content: 'Search failed while parsing server response. See server logs.' })
      return
    }

    if (!results || results.length === 0) {
      await interaction.editReply({ content: 'No results found.' })
      return
    }

    // Build select menu options defensively so a single malformed result doesn't break everything
    const options = []
    const sliced = results.slice(0, 10)
    for (let idx = 0; idx < sliced.length; idx++) {
      const r = sliced[idx]
      try {
        const md = r.metadata || r || {}
        const titleLabel = (md.title || r.title || 'Unknown')
        // Normalize to string
        const titleStr = (typeof titleLabel === 'string') ? titleLabel : JSON.stringify(titleLabel)

        // Extract authors/narrators safely
        let authors = ''
        if (Array.isArray(md.authors)) {
          const first = md.authors[0]
          if (first && typeof first === 'object' && first.name) authors = md.authors.map(a => a.name).join(', ')
          else authors = md.authors.map(a => String(a)).join(', ')
        } else if (Array.isArray(md.narrators)) {
          const first = md.narrators[0]
          if (first && typeof first === 'object' && first.name) authors = md.narrators.map(n => n.name).join(', ')
          else authors = md.narrators.map(n => String(n)).join(', ')
        }

  const year = md.publishYear || (md.releaseDate ? extractYear(md.releaseDate) : '') || ''
  const seriesInfo = extractSeriesInfo(md) || ''
  // Include series in the label so users can see it when choosing
  const label = `${titleStr}${seriesInfo ? ` — ${seriesInfo}` : ''}${authors ? ` by ${authors}` : ''}${year ? ` (${year})` : ''}`.slice(0, 100)
        const value = md.asin || r.asin || `idx:${idx}`
        if (label && value) options.push({ label, value })
      } catch (optErr) {
        const msg = `Failed to build select option for result index ${idx} (query=${title}): ${optErr.stack || optErr}`
        try { fs.appendFileSync('bot-search-errors.log', `${new Date().toISOString()} ${msg}\n`) } catch (fileErr) { console.error('Failed to append to log', fileErr) }
        console.error(msg)
        // skip this option
      }
    }

    if (!options.length) {
      await interaction.editReply({ content: 'No valid results found to select.' })
      return
    }
    // If there is only one result, skip the select menu and show the confirm embed directly
    if (results.length === 1) {
      const md = results[0].metadata || results[0]

  // Save to a new session id for confirm flow
  const confirmSessionId = `confirm_${makeSessionId()}`
  // Store the interaction token so we can delete the ephemeral reply later (if needed)
  const appIdForEphemeral = (currentSettings && currentSettings.discordApplicationId) || process.env.DISCORD_APPLICATION_ID || process.env.DISCORD_APP_ID || (client && client.application && client.application.id)
  sessions.set(confirmSessionId, { metadata: md, timestamp: Date.now(), requestingUserId: interaction.user?.id, ephemeralReplyToken: interaction.token, ephemeralAppId: appIdForEphemeral })
      console.log(`Created session: ${confirmSessionId} for book: ${md.title}`)
  logSessionEvent(`Created confirm session ${confirmSessionId} title="${(md.title||'').replace(/\n/g,' ')}" (single-result)`)

      // Fetch quality profiles
      let profiles = []
      try {
        const qp = await fetch(`${listenarrUrl}/api/qualityprofile`)
        if (qp.ok) profiles = await qp.json()
      } catch (err) {
        console.warn('Failed to fetch quality profiles', err)
      }

      const embed = new EmbedBuilder()
        .setTitle(md.title || 'Unknown')
        .setDescription(stripHtml(md.description || '').slice(0, 2048))
      if (md.imageUrl) embed.setThumbnail(md.imageUrl)
      if (md.authors) {
        const authors = md.authors || []
        const authorNames = authors.map(a => typeof a === 'object' ? a.name : a).filter(name => name).slice(0, 3)
        embed.addFields({ name: 'Author(s)', value: authorNames.join(', ') || 'Unknown' })
      }
      if (md.narrators) {
        const narrators = md.narrators || []
        const narratorNames = narrators.map(n => typeof n === 'object' ? n.name : n).filter(name => name).slice(0, 3)
        embed.addFields({ name: 'Narrator(s)', value: narratorNames.join(', ') || 'Unknown' })
      }
      if (md.publisher) {
        embed.addFields({ name: 'Publisher', value: (md.publisher || 'Unknown').toString() })
      }
      const publishYear = md.publishYear || extractYear(md.releaseDate)
      if (publishYear) embed.addFields({ name: 'Published Year', value: publishYear })
      const seriesInfo = extractSeriesInfo(md)
      if (seriesInfo) embed.addFields({ name: 'Series', value: seriesInfo })

      const qpOptions = (profiles || []).map(p => ({ label: p.name, value: String(p.id) }))
      const qpSelect = new StringSelectMenuBuilder()
        .setCustomId(`quality_${confirmSessionId}`)
        .setPlaceholder('Choose quality profile (optional)')
        .addOptions(qpOptions.length ? qpOptions : [{ label: 'Default', value: '0' }])

      // Check whether this audiobook already exists in the library
      let existingBook = null
      try {
        const asin = md.asin || md.Asin || md.ASIN
        if (asin) {
          const lb = await fetch(`${listenarrUrl}/api/library/by-asin/${encodeURIComponent(asin)}`)
          if (lb.ok) existingBook = await lb.json()
        }
      } catch (err) {
        console.warn('Failed to query library for existing audiobook', err)
      }

      // Build the confirm button; if the book exists, show green 'Already Added!' or 'Available!'
      let confirmButton = new ButtonBuilder()
        .setCustomId(confirmSessionId)
        .setLabel('Request')
        .setStyle(ButtonStyle.Primary)

      if (existingBook) {
        const hasFiles = Array.isArray(existingBook.files) && existingBook.files.length > 0
        if (hasFiles) {
          confirmButton = new ButtonBuilder()
            .setCustomId(confirmSessionId)
            .setLabel('Available!')
            .setStyle(ButtonStyle.Success)
            .setDisabled(true)
        } else {
          confirmButton = new ButtonBuilder()
            .setCustomId(confirmSessionId)
            .setLabel('Already Added!')
            .setStyle(ButtonStyle.Success)
            .setDisabled(true)
        }
      }

      const row1 = new ActionRowBuilder().addComponents(qpSelect)
      const row2 = new ActionRowBuilder().addComponents(confirmButton)

      // Post the confirm embed as a regular channel message (non-ephemeral) so it can be deleted later
      try {
        const channel = interaction.channel || (await interaction.guild.channels.fetch(currentSettings?.discordChannelId).catch(() => null))
        // Check whether we can actually post to the configured channel (avoid Missing Access errors)
        let canPostInChannel = false
        let missingPerms = []
        if (channel) {
          try {
            const { PermissionsBitField } = require('discord.js')
            const perms = channel.permissionsFor(client.user)
            const needed = [PermissionsBitField.Flags.ViewChannel, PermissionsBitField.Flags.SendMessages]
            canPostInChannel = !!(perms && perms.has && perms.has(needed))
            if (!canPostInChannel) {
              // compute missing perms for logging
              for (const p of needed) {
                if (!perms || !perms.has || !perms.has(p)) missingPerms.push(p)
              }
            }
          } catch (e) {
            canPostInChannel = false
          }
        }
        // Show the confirm embed ephemerally to the user (do not post non-ephemeral messages)
        try {
          await interaction.editReply({ content: 'See details below. Optionally choose a quality profile, then press Request.', embeds: [embed], components: [row1, row2] })
        } catch (err) {
          // If editing the deferred ephemeral reply fails, fall back to replying ephemerally
          try { await interaction.reply({ content: 'See details below. Optionally choose a quality profile, then press Request.', embeds: [embed], components: [row1, row2], flags: 64 }) } catch {}
        }
      } catch (err) {
        console.warn('Failed to post confirm embed to channel, falling back to ephemeral reply', err)
        try { await interaction.editReply({ content: 'See details below. Optionally choose a quality profile, then press Request.', embeds: [embed], components: [row1, row2] }) } catch {}
      }

      cleanupSessions()
      return
    }

    const select = new StringSelectMenuBuilder()
      .setCustomId(`listenarr_results_${makeSessionId()}`)
      .setPlaceholder('Select a book')
      .addOptions(options)

    // store mapping from session id -> results list for later resolution
  const id = select.data?.custom_id || select.data?.customId || select.customId
  sessions.set(id, { results, timestamp: Date.now(), requestingUserId: interaction.user?.id })
    logSessionEvent(`Created results session ${id} (results=${results.length})`)

    const row = new ActionRowBuilder().addComponents(select)
    // Post the select menu as a regular channel message so the resulting confirm can be deleted
      try {
        const channel = interaction.channel || (await interaction.guild.channels.fetch(currentSettings?.discordChannelId).catch(() => null))
        // Check send+view permission to avoid Missing Access
        let canPostInChannel2 = false
        let missingPerms2 = []
        if (channel) {
          try {
            const { PermissionsBitField } = require('discord.js')
            const perms2 = channel.permissionsFor(client.user)
            const needed2 = [PermissionsBitField.Flags.ViewChannel, PermissionsBitField.Flags.SendMessages]
            canPostInChannel2 = !!(perms2 && perms2.has && perms2.has(needed2))
            if (!canPostInChannel2) {
              for (const p of needed2) {
                if (!perms2 || !perms2.has || !perms2.has(p)) missingPerms2.push(p)
              }
            }
          } catch (e) {
            canPostInChannel2 = false
          }
        }
        // Post the select menu ephemerally to the interacting user (do not post to channel)
        try {
          const s = sessions.get(id) || {}
          s.requestingUserId = interaction.user?.id
          s.timestamp = Date.now()
          s.results = results
          sessions.set(id, s)
          await interaction.editReply({ content: 'Select a result from the list below:', components: [row] })
        } catch (err) {
          logSessionEvent(`Failed to send ephemeral select for session ${id}: ${err && err.message ? err.message : err}`)
          try { await interaction.editReply({ content: 'Select a result from the list below:' }) } catch {}
        }
    } catch (err) {
      console.warn('Failed to post select menu to channel, falling back to ephemeral reply', err)
      try { await interaction.editReply({ content: 'Select a result from the list below:', components: [row] }) } catch {}
    }

    // Cleanup stale sessions periodically
    cleanupSessions()
  } catch (err) {
    // Log full error to disk for investigation and show friendly message
    const logMsg = `Search handler error for query=${title}: ${err.stack || err}\n`
    try { fs.appendFileSync('bot-search-errors.log', `${new Date().toISOString()} ${logMsg}\n`) } catch (fileErr) { console.error('Failed to append to log', fileErr) }
    console.error('Search failed', err)
    await interaction.editReply({ content: 'Search failed. See server logs.' })
  }
}

async function handleSelectMenuInteraction(interaction) {
  const customId = interaction.customId
  const session = sessions.get(customId)
  if (!session) {
    await interaction.reply({ content: 'Session expired or invalid. Please run the command again.', flags: 64 })
    return
  }

  const selected = interaction.values && interaction.values[0]
  if (!selected) {
    await interaction.reply({ content: 'No selection made.', flags: 64 })
    return
  }

  // Resolve metadata by asin or index
  let metadata = null
  if (selected.startsWith('idx:')) {
    const idx = parseInt(selected.split(':')[1], 10)
    metadata = session.results[idx]?.metadata || session.results[idx]
  } else {
    // fetch metadata endpoint
    try {
      const asin = selected
      // Deprecated route replaced by /api/metadata/{asin}; keep compatibility header-wise
      const resp = await fetch(`${listenarrUrl}/api/metadata/${encodeURIComponent(asin)}`)
      if (resp.ok) {
        const data = await resp.json()
        metadata = data.metadata || data
      } else {
        // Fall back: try audimeta lookup
        // Deprecated route replaced by /api/metadata/audimeta/{asin}; keep compatibility header-wise
        const fallback = await fetch(`${listenarrUrl}/api/metadata/audimeta/${encodeURIComponent(asin)}`)
        if (fallback.ok) {
          metadata = await fallback.json()
        }
      }
    } catch (err) {
      console.warn('Metadata fetch failed', err)
    }

    // If API metadata fetch failed, fall back to search result metadata
    if (!metadata) {
      // Find the original search result by ASIN
      const originalResult = session.results.find(r => (r.metadata?.asin || r.asin) === selected)
      if (originalResult) {
        metadata = originalResult.metadata || originalResult
        console.log('Using fallback metadata from search results for ASIN:', selected)
      }
    }
  }

  if (!metadata) {
    await interaction.reply({ content: 'Failed to load metadata for the selected item.', flags: 64 })
    return
  }

  // Save to a new session id for confirm flow; carry over ack/message IDs from the results session so we can clean them up later
  const confirmSessionId = `confirm_${makeSessionId()}`
  // Store the interaction token so we can delete the ephemeral reply later (if needed)
  const appIdForEphemeral2 = (currentSettings && currentSettings.discordApplicationId) || process.env.DISCORD_APPLICATION_ID || process.env.DISCORD_APP_ID || (client && client.application && client.application.id)
  sessions.set(confirmSessionId, { metadata, timestamp: Date.now(), ackMessageId: session.ackMessageId, messageId: session.messageId, ephemeralReplyToken: interaction.token, ephemeralAppId: appIdForEphemeral2 })
  console.log(`Created session: ${confirmSessionId} for book: ${metadata.title}`)
  logSessionEvent(`Created confirm session ${confirmSessionId} title="${(metadata.title||'').replace(/\n/g,' ')}"`)

  // Fetch quality profiles
  let profiles = []
  try {
    const qp = await fetch(`${listenarrUrl}/api/qualityprofile`)
    if (qp.ok) profiles = await qp.json()
  } catch (err) {
    console.warn('Failed to fetch quality profiles', err)
  }

  const embed = new EmbedBuilder()
    .setTitle(metadata.title || 'Unknown')
    .setDescription(stripHtml(metadata.description || '').slice(0, 2048))
  if (metadata.imageUrl) embed.setThumbnail(metadata.imageUrl)
  if (metadata.authors) {
    const authors = metadata.authors || []
    const authorNames = authors.map(a => typeof a === 'object' ? a.name : a).filter(name => name).slice(0, 3)
    embed.addFields({ name: 'Author(s)', value: authorNames.join(', ') || 'Unknown' })
  }
  if (metadata.narrators) {
    const narrators = metadata.narrators || []
    const narratorNames = narrators.map(n => typeof n === 'object' ? n.name : n).filter(name => name).slice(0, 3)
    embed.addFields({ name: 'Narrator(s)', value: narratorNames.join(', ') || 'Unknown' })
  }
  if (metadata.publisher) {
    embed.addFields({ name: 'Publisher', value: (metadata.publisher || 'Unknown').toString() })
  }
  // Try publishYear first, then extract from releaseDate
  const publishYear = metadata.publishYear || extractYear(metadata.releaseDate)
  if (publishYear) {
    embed.addFields({ name: 'Published Year', value: publishYear })
  }

  // Series information (if available)
  const seriesInfo = extractSeriesInfo(metadata)
  if (seriesInfo) {
    embed.addFields({ name: 'Series', value: seriesInfo })
  }

  // Quality select
  const qpOptions = (profiles || []).map(p => ({ label: p.name, value: String(p.id) }))
  const qpSelect = new StringSelectMenuBuilder()
    .setCustomId(`quality_${confirmSessionId}`)
    .setPlaceholder('Choose quality profile (optional)')
    .addOptions(qpOptions.length ? qpOptions : [{ label: 'Default', value: '0' }])

  // Check whether this audiobook already exists in the library
  let existingBook = null
  try {
    const asin = metadata.asin || metadata.Asin || metadata.ASIN
    if (asin) {
      const lb = await fetch(`${listenarrUrl}/api/library/by-asin/${encodeURIComponent(asin)}`)
      if (lb.ok) existingBook = await lb.json()
    }
  } catch (err) {
    console.warn('Failed to query library for existing audiobook', err)
  }

  // Button should use the same session key we stored (confirmSessionId).
  let confirmButton = new ButtonBuilder()
    .setCustomId(confirmSessionId)
    .setLabel('Request')
    .setStyle(ButtonStyle.Primary)

  // If the audiobook exists, show a green button and mark appropriately
  if (existingBook) {
    const hasFiles = Array.isArray(existingBook.files) && existingBook.files.length > 0
    if (hasFiles) {
      confirmButton = new ButtonBuilder()
        .setCustomId(confirmSessionId)
        .setLabel('Available!')
        .setStyle(ButtonStyle.Success)
        .setDisabled(true)
    } else {
      confirmButton = new ButtonBuilder()
        .setCustomId(confirmSessionId)
        .setLabel('Already Added!')
        .setStyle(ButtonStyle.Success)
        .setDisabled(true)
    }
  }

  const row1 = new ActionRowBuilder().addComponents(qpSelect)
  const row2 = new ActionRowBuilder().addComponents(confirmButton)

  // Show the confirm embed ephemerally to the user who selected the result
  try {
    await interaction.update({ content: 'See details below. Optionally choose a quality profile, then press Request.', embeds: [embed], components: [row1, row2] })
  } catch (err) {
    // Fallback to replying ephemerally if update fails
    try { await interaction.reply({ content: 'See details below. Optionally choose a quality profile, then press Request.', embeds: [embed], components: [row1, row2], flags: 64 }) } catch (e) {}
  }
}

async function handleQualitySelectInteraction(interaction) {
  // Clean up old sessions first
  cleanupSessions()

  // Extract the confirm session ID from the quality select customId
  // customId is 'quality_confirm_${sessionId}', so we need to remove 'quality_' prefix
  const confirmSessionId = interaction.customId.replace('quality_', '')
  console.log(`Quality select: looking for session ${confirmSessionId}`)
  const session = sessions.get(confirmSessionId)
  if (!session) {
    console.log(`Session not found. Available sessions:`, Array.from(sessions.keys()))
    logSessionEvent(`Quality select: session not found ${confirmSessionId} available=[${Array.from(sessions.keys()).join(',')}]`)
    await interaction.reply({ content: 'Session expired or invalid. Please run the command again.', flags: 64 })
    return
  }

  // Store the selected quality profile in the session
  const selectedQualityId = interaction.values && interaction.values[0]
  session.selectedQualityId = selectedQualityId ? Number(selectedQualityId) : 0
  console.log(`Quality selected: ${session.selectedQualityId}`)
  logSessionEvent(`Quality select: session=${confirmSessionId} selected=${session.selectedQualityId}`)

  // Update the session timestamp
  session.timestamp = Date.now()

  // Acknowledge the interaction (required for select menus)
  await interaction.deferUpdate()
}

async function handleButtonInteraction(interaction) {
  // Clean up old sessions first
  cleanupSessions()

  const customId = interaction.customId
  console.log(`Button interaction: customId=${customId}`)
  if (!customId.startsWith('confirm_')) {
    await interaction.reply({ content: 'Unknown confirmation action', flags: 64 })
    return
  }

  // The customId is already 'confirm_${sessionId}', which matches how we stored it
  const session = sessions.get(customId)
  console.log(`Looking for session: ${customId}, found: ${!!session}`)
  if (!session) {
    console.log(`Session not found. Available sessions:`, Array.from(sessions.keys()))
    logSessionEvent(`Button click: session not found ${customId} available=[${Array.from(sessions.keys()).join(',')}]`)
    await interaction.reply({ content: 'Session expired. Please run the command again.', flags: 64 })
    return
  }

  console.log(`Session found, metadata title: ${session.metadata?.title || 'unknown'}`)
  logSessionEvent(`Button click: session found ${customId} title="${(session.metadata?.title||'').replace(/\n/g,' ')}" selectedQuality=${session.selectedQualityId ?? 'unset'}`)

  // Update session timestamp to prevent cleanup
  session.timestamp = Date.now()

  // Prevent double-processing: if a request is already being handled for this session, inform the user
  if (session.processing) {
    try {
      await interaction.reply({ content: 'Request is already being processed. Please wait a moment.', flags: 64 })
    } catch {}
    return
  }
  // Mark as processing so subsequent clicks are rejected
  session.processing = true

  // Acknowledge the interaction to give us time to perform API calls and update the original message.
  // We will update the message later via editReply().
  let didDefer = false
  try {
    // Immediately disable components on the original message to prevent double-clicks
    try {
      if (interaction.message && interaction.message.edit) {
        const disabledComponents = (interaction.message.components || []).map(row => ({
          type: row.type,
          components: (row.components || []).map(c => {
            // c may already be a plain object or a MessageComponent; copy and set disabled
            const data = (typeof c.toJSON === 'function') ? c.toJSON() : Object.assign({}, c)
            data.disabled = true
            return data
          })
        }))
        // best-effort edit; ignore errors (may fail if message is ephemeral or permissions missing)
        await interaction.message.edit({ components: disabledComponents }).catch(() => {})
      }
    } catch (e) {
      // ignore
    }

    await interaction.deferUpdate()
    didDefer = true
  } catch (e) {
    // If defer fails, we'll fall back to replying directly later
    didDefer = false
  }

  // Use the selected quality profile from the session (defaults to 0 if not selected)
  const qualityProfileId = session.selectedQualityId !== undefined ? session.selectedQualityId : 0
  console.log(`Using quality profile ID: ${qualityProfileId}`)

  // Make API call to add to library
  try {
  // Deep-clone the metadata so we don't mutate the session object
    const clonedMeta = JSON.parse(JSON.stringify(session.metadata || {}))

    // Helper to normalize array entries to strings (authors, narrators, genres, tags)
    function normalizeStringArray(arr) {
      if (!arr) return undefined
      if (!Array.isArray(arr)) return undefined
      const out = arr.map(a => {
        if (a === null || a === undefined) return null
        if (typeof a === 'string') return a
        if (typeof a === 'object') {
          // Common shapes: { name: 'Foo' } or { author: 'Foo' }
          if (a.name) return String(a.name)
          if (a.author) return String(a.author)
          if (a.authorName) return String(a.authorName)
          if (a.narrator) return String(a.narrator)
          // Fallback to JSON string
          try { return JSON.stringify(a) } catch { return String(a) }
        }
        return String(a)
      }).filter(x => x !== null && x !== undefined)
      return out.length ? out : undefined
    }

    clonedMeta.authors = normalizeStringArray(clonedMeta.authors) || normalizeStringArray(clonedMeta.Authors) || clonedMeta.authors
    clonedMeta.narrators = normalizeStringArray(clonedMeta.narrators) || normalizeStringArray(clonedMeta.Narrators) || clonedMeta.narrators
    clonedMeta.genres = normalizeStringArray(clonedMeta.genres) || normalizeStringArray(clonedMeta.Genres) || clonedMeta.genres
    clonedMeta.tags = normalizeStringArray(clonedMeta.tags) || normalizeStringArray(clonedMeta.Tags) || clonedMeta.tags

    // Normalize series: server expects a string (AudibleBookMetadata.Series)
    function normalizeSeriesField(m) {
      if (!m) return undefined
      const s = m.series || m.Series || m.seriesName || m.seriesTitle || m.SeriesName || m.SeriesTitle
      if (!s) return undefined
      if (typeof s === 'string') return s
      if (typeof s === 'object') {
        // Common object shapes: { name: 'Series Name', position: 1 }
        if (s.name) return String(s.name)
        if (s.title) return String(s.title)
        if (s.series) return String(s.series)
        // Fallback: try to stringify
        try { return JSON.stringify(s) } catch { return String(s) }
      }
      return String(s)
    }

    clonedMeta.series = normalizeSeriesField(clonedMeta)
    // Normalize series number if present
    const seriesNum = clonedMeta.seriesNumber || clonedMeta.series_position || clonedMeta.seriesPosition || clonedMeta.seriesIndex || clonedMeta.SeriesNumber
    if (seriesNum !== undefined && seriesNum !== null) clonedMeta.seriesNumber = String(seriesNum)

    // Ensure publish year is a string when present
    if (clonedMeta.publishYear === undefined && clonedMeta.PublishYear) clonedMeta.publishYear = String(clonedMeta.PublishYear)
    if (clonedMeta.publishYear && typeof clonedMeta.publishYear !== 'string') clonedMeta.publishYear = String(clonedMeta.publishYear)

    const body = {
      metadata: clonedMeta,
      monitored: true,
      qualityProfileId: qualityProfileId || undefined,
      autoSearch: true
    }
    // Generate an idempotency key for this request so the server can deduplicate retries
    const idempotencyKey = crypto.randomBytes(12).toString('hex')
    console.log(`Making API call to add book: ${session.metadata.title}`)
    const resp = await fetch(`${listenarrUrl}/api/library/add`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify(body)
    })
    // If server rejects due to missing CSRF, try fetching antiforgery token and retry once
    if (resp.status === 400) {
      const txt = await resp.text().catch(() => '')
      if (txt && txt.includes('CSRF')) {
        console.log('Library add failed due to CSRF; attempting token fetch and retry')
        const xsrf = await fetchAntiforgeryTokenForBot()
        if (xsrf) {
          const retryResp = await fetch(`${listenarrUrl}/api/library/add`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': xsrf, 'Idempotency-Key': idempotencyKey },
            body: JSON.stringify(body)
          })
          if (!retryResp.ok) {
            const txt2 = await retryResp.text().catch(() => '')
            if (didDefer) await interaction.editReply({ content: `Failed to request book: ${retryResp.status} ${txt2}` })
            else await interaction.reply({ content: `Failed to request book: ${retryResp.status} ${txt2}`, flags: 64 })
            return
          }
          const json2 = await retryResp.json()
          // Friendly response: show title and first author instead of internal id
          const addedTitle2 = session.metadata?.title || json2.audiobook?.title || 'Unknown Title'
          let addedAuthor2 = 'Unknown Author'
          const sourceAuthors2 = session.metadata?.authors || session.metadata?.Authors || json2.audiobook?.authors || json2.audiobook?.Authors
          if (Array.isArray(sourceAuthors2) && sourceAuthors2.length) {
            addedAuthor2 = typeof sourceAuthors2[0] === 'object' ? (sourceAuthors2[0].name || String(sourceAuthors2[0])) : String(sourceAuthors2[0])
          } else if (typeof sourceAuthors2 === 'string') addedAuthor2 = sourceAuthors2
          // Update the message with success text and change button to green "Added"
          const successButton2 = new ButtonBuilder()
            .setCustomId('added') // dummy, since disabled
            .setLabel('Added')
            .setStyle(ButtonStyle.Success)
            .setDisabled(true)
          const successRow2 = new ActionRowBuilder().addComponents(successButton2)
          if (didDefer) await interaction.editReply({ content: `Request submitted! Added ${addedTitle2} by ${addedAuthor2}.`, components: [successRow2] })
          else await interaction.reply({ content: `Request submitted! Added ${addedTitle2} by ${addedAuthor2}.`, components: [successRow2], flags: 64 })
          return
        }
      }
    }
    if (!resp.ok) {
      const txt = await resp.text()
      console.error(`API call failed: ${resp.status} ${txt}`)
      if (didDefer) await interaction.editReply({ content: `Failed to request book: ${resp.status} ${txt}` })
      else await interaction.reply({ content: `Failed to request book: ${resp.status} ${txt}`, flags: 64 })
      return
    }
    const json = await resp.json()
    console.log(`API call successful, response:`, json)
    const addedTitle = session.metadata?.title || json.audiobook?.title || 'Unknown Title'
    let addedAuthor = 'Unknown Author'
    const sourceAuthors = session.metadata?.authors || session.metadata?.Authors || json.audiobook?.authors || json.audiobook?.Authors
    if (Array.isArray(sourceAuthors) && sourceAuthors.length) {
      addedAuthor = typeof sourceAuthors[0] === 'object' ? (sourceAuthors[0].name || String(sourceAuthors[0])) : String(sourceAuthors[0])
    } else if (typeof sourceAuthors === 'string') addedAuthor = sourceAuthors
    // Update the message with success text and change button to green "Added"
    const successButton = new ButtonBuilder()
      .setCustomId('added') // dummy, since disabled
      .setLabel('Added')
      .setStyle(ButtonStyle.Success)
      .setDisabled(true)
    const successRow = new ActionRowBuilder().addComponents(successButton)
    if (didDefer) await interaction.editReply({ content: `Request submitted! Added ${addedTitle} by ${addedAuthor}.`, components: [successRow] })
    else await interaction.reply({ content: `Request submitted! Added ${addedTitle} by ${addedAuthor}.`, components: [successRow], flags: 64 })
  } catch (err) {
    console.error('Failed to submit request', err)
    try {
      if (didDefer) await interaction.editReply({ content: 'Failed to submit request. See server logs.' })
      else await interaction.reply({ content: 'Failed to submit request. See server logs.', flags: 64 })
    } catch {}
  } finally {
    // Clear processing flag so the session can be used again if needed
    try { delete session.processing } catch {}
  }
}

function cleanupSessions() {
  const now = Date.now()
  const beforeCount = sessions.size
  console.log(`Running session cleanup, ${beforeCount} sessions before cleanup`)
  for (const [k, v] of sessions.entries()) {
    if (now - v.timestamp > SESSION_TIMEOUT_MS) {
      console.log(`Cleaning up expired session: ${k} (age: ${(now - v.timestamp)/1000}s)`)
      sessions.delete(k)
    }
  }
  const afterCount = sessions.size
  if (beforeCount !== afterCount) {
    console.log(`Cleaned up ${beforeCount - afterCount} expired sessions. ${afterCount} remaining.`)
  } else {
    console.log(`No sessions cleaned up. ${afterCount} sessions remain.`)
  }
}

async function start() {
  console.log('Starting Listenarr Discord bot (tools/discord-bot)')
  // Resolve the Listenarr URL synchronously from env/.env before making any requests.
  // This avoids a race where the module-level resolver runs async and the initial
  // fetchSettings() call happens with the default 'http://localhost:5000'.
  try {
    const resolved = await resolveListenarrUrl()
    if (resolved && resolved.trim()) listenarrUrl = resolved.trim().replace(/\/$/, '')
  } catch (e) {
    // ignore and fall back to existing value
  }

  // initial fetch (now guaranteed to use the resolved listenarrUrl)
  currentSettings = await fetchSettings()
  if (currentSettings) await ensureClient(currentSettings)

  // Establish SignalR connection for real-time settings updates
  await createSignalRConnection()

  // Graceful shutdown
  process.on('SIGINT', async () => {
    console.log('Shutting down...')
    if (signalRConnection) {
      try { await signalRConnection.stop() } catch {}
    }
    if (client) {
      try { await client.destroy() } catch {}
    }
    process.exit(0)
  })

  process.on('SIGTERM', async () => {
    console.log('Shutting down...')
    if (signalRConnection) {
      try { await signalRConnection.stop() } catch {}
    }
    if (client) {
      try { await client.destroy() } catch {}
    }
    process.exit(0)
  })
}

start().catch(err => console.error('failed to start bot', err))
