<template>
  <div class="tab-content">
    <div class="discord-bot-tab">
      <div class="section-header">
        <h3>Discord Bot</h3>
      </div>

      <div class="form-section">
        <div class="form-group checkbox-group">
          <label>
            <input v-model="settings.discordBotEnabled" type="checkbox" />
            <span>
              <strong>Enable Discord Bot Integration</strong>
              <small
                >Allow an external Discord bot to read these settings and register slash
                commands.</small
              >
            </span>
          </label>
        </div>

        <div class="form-group">
          <label>Discord Application ID</label>
          <input
            v-model="settings.discordApplicationId"
            type="text"
            placeholder="Discord Application ID (client id)"
          />
          <span class="form-help"
            >Used to register application commands. For per-guild testing, set a Guild ID
            below.</span
          >
        </div>

        <div class="form-group">
          <label>Discord Guild ID (optional)</label>
          <input
            v-model="settings.discordGuildId"
            type="text"
            placeholder="Optional guild id for testing"
          />
          <span class="form-help"
            >If provided, commands will be registered to this guild for faster updates (useful for
            development).</span
          >
        </div>

        <div class="form-group">
          <label>Discord Channel ID (optional)</label>
          <input
            v-model="settings.discordChannelId"
            type="text"
            placeholder="Optional channel id to restrict commands"
          />
          <span class="form-help"
            >If provided, the bot will only accept request commands from this channel. You can also
            set this via the bot using the <code>/request-config set-channel</code> command.</span
          >
        </div>

        <!-- Invite / Register Controls -->
        <div v-if="settings.discordApplicationId" class="form-group invite-row">
          <label>Invite Bot to Server</label>
          <div class="invite-controls">
            <button @click="openInviteLink" class="invite-button">Open Invite</button>
            <button @click="copyInviteLink" class="icon-button">Copy Invite Link</button>
            <button @click="checkDiscordStatus" class="icon-button" :disabled="checkingDiscord">
              Check Install
            </button>
            <button
              @click="registerCommands"
              class="save-button"
              :disabled="registeringCommands || !settings.discordBotToken"
            >
              Register commands now
            </button>
          </div>
          <div class="form-help">
            Use this to invite the bot with the minimal permissions needed for requests. Make sure
            <strong>Discord Application ID</strong> is filled in above. Optionally set a Guild ID to
            preselect a server.
          </div>
          <div v-if="inviteLinkPreview" class="invite-link-preview">
            <small
              >Preview:
              <a :href="inviteLinkPreview" target="_blank" rel="noopener noreferrer">{{
                inviteLinkPreview
              }}</a></small
            >
          </div>

          <div v-if="discordStatus" class="discord-status">
            <template v-if="discordStatus.installed === true">
              <span class="status-pill installed"
                >Installed in guild {{ discordStatus.guildId || '' }}</span
              >
            </template>
            <template v-else-if="discordStatus.installed === false">
              <span class="status-pill not-installed">Not installed in configured guild</span>
            </template>
            <template v-else>
              <span class="status-pill unknown">Token validated</span>
            </template>
          </div>
        </div>

        <div class="form-group">
          <label>Bot Token</label>
          <div class="password-field">
            <input
              :type="showPassword ? 'text' : 'password'"
              v-model="settings.discordBotToken"
              placeholder="Bot token (keep secret)"
              class="admin-input password-input"
            />
            <button
              type="button"
              class="password-toggle"
              @click.prevent="toggleShowPassword"
              :aria-pressed="!!showPassword"
              :title="showPassword ? 'Hide token' : 'Show token'"
            >
              <template v-if="showPassword">
                <PhEyeSlash />
              </template>
              <template v-else>
                <PhEye />
              </template>
            </button>
          </div>
          <span class="form-help"
            >The bot process will use this token to login. Be careful with this value.</span
          >
        </div>

        <div class="form-group">
          <label>Command Group Name</label>
          <input v-model="settings.discordCommandGroupName" type="text" placeholder="request" />
          <span class="form-help">Primary command group (e.g. <code>request</code>)</span>
        </div>

        <div class="form-group">
          <label>Subcommand Name</label>
          <input
            v-model="settings.discordCommandSubcommandName"
            type="text"
            placeholder="audiobook"
          />
          <span class="form-help"
            >Subcommand for audiobooks (e.g. <code>audiobook</code>) — results in
            <code>/request audiobook &lt;title&gt;</code></span
          >
        </div>

        <div class="form-group">
          <label>Bot Username (optional)</label>
          <input
            v-model="settings.discordBotUsername"
            type="text"
            placeholder="Custom bot username"
          />
          <span class="form-help"
            >Optional custom username for the bot. Leave empty to use the default username from
            Discord.</span
          >
        </div>

        <div class="form-group">
          <label>Bot Avatar URL (optional)</label>
          <input
            v-model="settings.discordBotAvatar"
            type="url"
            placeholder="https://example.com/avatar.png"
          />
          <span class="form-help"
            >Optional avatar image URL for the bot. Leave empty to use the default avatar from
            Discord.</span
          >
        </div>
      </div>

      <!-- Discord Bot Process Controls -->
      <div class="form-section">
        <h4><PhRobot /> Discord Bot Process Control</h4>

        <div class="bot-status-section">
          <div class="bot-status-display">
            <div class="status-indicator" :class="botStatusClass">
              <PhCircle v-if="botStatus === 'unknown'" />
              <PhSpinner v-else-if="botStatus === 'checking'" class="ph-spin" />
              <PhCheckCircle v-else-if="botStatus === 'running'" />
              <PhXCircle v-else-if="botStatus === 'stopped'" />
              <PhWarning v-else />
            </div>
            <div class="status-text"><strong>Bot Status:</strong> {{ botStatusText }}</div>
          </div>

          <div class="bot-controls">
            <button @click="checkBotStatus" class="status-button" :disabled="checkingBotStatus">
              <template v-if="checkingBotStatus">
                <PhSpinner class="ph-spin" />
              </template>
              <template v-else>
                <PhArrowClockwise />
              </template>
              Refresh Status
            </button>

            <button
              @click="startBot"
              class="start-button"
              :disabled="startingBot || botStatus === 'running'"
            >
              <template v-if="startingBot">
                <PhSpinner class="ph-spin" />
              </template>
              <template v-else>
                <PhPlay />
              </template>
              Start Bot
            </button>

            <button
              @click="stopBot"
              class="stop-button"
              :disabled="stoppingBot || botStatus === 'stopped'"
            >
              <template v-if="stoppingBot">
                <PhSpinner class="ph-spin" />
              </template>
              <template v-else>
                <PhStop />
              </template>
              Stop Bot
            </button>
          </div>
        </div>

        <div class="form-help">
          Control the Discord bot process directly from here. The bot will use the token configured
          above to connect to Discord and register slash commands.
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRefs } from 'vue'
import { apiService } from '@/services/api'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import type { ApplicationSettings } from '@/types'
import {
  PhRobot,
  PhEye,
  PhEyeSlash,
  PhSpinner,
  PhCircle,
  PhCheckCircle,
  PhXCircle,
  PhWarning,
  PhArrowClockwise,
  PhPlay,
  PhStop,
} from '@phosphor-icons/vue'

interface Props {
  settings: ApplicationSettings
}

const props = defineProps<Props>()
const { settings } = toRefs(props)
const toast = useToast()

// Password visibility toggle
const showPassword = ref(false)
const toggleShowPassword = () => {
  showPassword.value = !showPassword.value
}

// Invite link helpers
const PERMISSIONS_MINIMAL = 19456 | 8192 // Manage Messages included
const inviteLinkPreview = computed(() => {
  const appId = settings.value?.discordApplicationId?.trim()
  if (!appId) return ''
  const scopes = encodeURIComponent('bot applications.commands')
  const guildPart = settings.value?.discordGuildId?.trim()
    ? `&guild_id=${encodeURIComponent(settings.value.discordGuildId)}`
    : ''
  return `https://discord.com/oauth2/authorize?client_id=${encodeURIComponent(appId)}&permissions=${PERMISSIONS_MINIMAL}&scope=${scopes}${guildPart}`
})

const openInviteLink = () => {
  if (!inviteLinkPreview.value) {
    toast.error('Missing Application ID', 'Please enter the Discord Application ID first.')
    return
  }
  window.open(inviteLinkPreview.value, '_blank', 'noopener')
}

const copyInviteLink = async () => {
  if (!inviteLinkPreview.value) {
    toast.error('Missing Application ID', 'Please enter the Discord Application ID first.')
    return
  }
  try {
    await navigator.clipboard.writeText(inviteLinkPreview.value)
    toast.success('Copied', 'Invite link copied to clipboard.')
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'copyInviteLink',
    })
    toast.error('Copy failed', 'Unable to copy invite link to clipboard.')
  }
}

// Discord status checking
const discordStatus = ref<{
  success?: boolean
  installed?: boolean | null
  guildId?: string
  botInfo?: unknown
  message?: string
} | null>(null)
const checkingDiscord = ref(false)
const registeringCommands = ref(false)

const formatApiError = (err: unknown): string => {
  if (err && typeof err === 'object' && 'message' in err) {
    return String((err as { message: string }).message)
  }
  return 'An unknown error occurred'
}

const checkDiscordStatus = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to check install status.')
    return
  }
  checkingDiscord.value = true
  try {
    const resp = await apiService.getDiscordStatus()
    discordStatus.value = resp
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'checkDiscordStatus',
    })
    const errorMessage = formatApiError(err)
    toast.error('Status failed', errorMessage)
  } finally {
    checkingDiscord.value = false
  }
}

const registerCommands = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to register commands.')
    return
  }
  registeringCommands.value = true
  try {
    const resp = await apiService.registerDiscordCommands()
    if (resp?.success) {
      toast.success('Registered', resp.message || 'Commands registered')
      await checkDiscordStatus()
    } else {
      toast.error('Register failed', JSON.stringify(resp?.body || resp?.message || resp))
    }
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'registerCommands',
    })
    const errorMessage = formatApiError(err)
    toast.error('Register failed', errorMessage)
  } finally {
    registeringCommands.value = false
  }
}

// Bot process control
const botStatus = ref<'unknown' | 'checking' | 'running' | 'stopped' | 'error'>('unknown')
const checkingBotStatus = ref(false)
const startingBot = ref(false)
const stoppingBot = ref(false)

const botStatusClass = computed(() => {
  switch (botStatus.value) {
    case 'running':
      return 'status-running'
    case 'stopped':
      return 'status-stopped'
    case 'checking':
      return 'status-checking'
    case 'error':
      return 'status-error'
    default:
      return 'status-unknown'
  }
})

const botStatusText = computed(() => {
  switch (botStatus.value) {
    case 'running':
      return 'Running'
    case 'stopped':
      return 'Stopped'
    case 'checking':
      return 'Checking...'
    case 'error':
      return 'Error'
    default:
      return 'Unknown'
  }
})

const checkBotStatus = async () => {
  checkingBotStatus.value = true
  botStatus.value = 'checking'
  try {
    const resp = await apiService.getDiscordBotStatus()
    if (resp.success) {
      botStatus.value = resp.isRunning ? 'running' : 'stopped'
      toast.info('Bot Status', resp.status)
    } else {
      botStatus.value = 'error'
      toast.error('Status check failed', resp.status || 'Failed to check bot status')
    }
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'checkBotStatus',
    })
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Status check failed', errorMessage)
  } finally {
    checkingBotStatus.value = false
  }
}

const startBot = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to start the bot.')
    return
  }
  startingBot.value = true
  try {
    const resp = await apiService.startDiscordBot()
    if (resp.success) {
      botStatus.value = 'running'
      toast.success('Bot Started', resp.message || 'Discord bot started successfully')
      setTimeout(() => checkBotStatus(), 2000)
    } else {
      botStatus.value = 'error'
      toast.error('Start failed', resp.message || 'Failed to start Discord bot')
    }
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'startBot',
    })
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Start failed', errorMessage)
  } finally {
    startingBot.value = false
  }
}

const stopBot = async () => {
  stoppingBot.value = true
  try {
    const resp = await apiService.stopDiscordBot()
    if (resp.success) {
      botStatus.value = 'stopped'
      toast.success('Bot Stopped', resp.message || 'Discord bot stopped successfully')
      setTimeout(() => checkBotStatus(), 2000)
    } else {
      botStatus.value = 'error'
      toast.error('Stop failed', resp.message || 'Failed to stop Discord bot')
    }
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DiscordBotTab',
      operation: 'stopBot',
    })
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Stop failed', errorMessage)
  } finally {
    stoppingBot.value = false
  }
}
</script>

<style scoped>
.tab-content {
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.form-section {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.form-section h4 {
  margin: 0 0 1.5rem 0;
  font-size: 1.1rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #fff;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #fff;
  font-size: 0.95rem;
}

.form-group input[type='text'],
.form-group input[type='url'],
.form-group input[type='password'],
.form-group select {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  background: rgba(0, 0, 0, 0.2);
  color: #fff;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: #4dabf7;
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
}

.form-help {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: #868e96;
  line-height: 1.5;
}

.form-help code {
  background: rgba(0, 0, 0, 0.3);
  padding: 0.2rem 0.4rem;
  border-radius: 3px;
  font-family: 'Courier New', monospace;
  font-size: 0.9em;
}

.invite-row {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.invite-controls {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.invite-button {
  padding: 0.75rem 1.25rem;
  border-radius: 6px;
  background: linear-gradient(135deg, #51cf66 0%, #37b24d 100%);
  color: #fff;
  border: none;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(81, 207, 102, 0.3);
}

.invite-button:hover {
  background: linear-gradient(135deg, #37b24d 0%, #2b8a3e 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(81, 207, 102, 0.4);
}

.invite-link-preview {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.invite-link-preview a {
  color: #4dabf7;
  text-decoration: none;
  word-break: break-all;
}

.discord-status {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.password-field {
  position: relative;
  display: flex;
  align-items: center;
}

.password-input {
  padding-right: 3rem;
}

.password-toggle {
  position: absolute;
  right: 0.75rem;
  background: none;
  border: none;
  color: #868e96;
  cursor: pointer;
  padding: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.2rem;
}

.password-toggle:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #fff;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.85rem;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 600;
}

.status-pill.installed {
  background: rgba(81, 207, 102, 0.15);
  color: #51cf66;
  border: 1px solid rgba(81, 207, 102, 0.3);
}

.status-pill.not-installed {
  background: rgba(255, 107, 107, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(255, 107, 107, 0.3);
}

.status-pill.unknown {
  background: rgba(173, 181, 189, 0.15);
  color: #adb5bd;
  border: 1px solid rgba(173, 181, 189, 0.3);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.add-button,
.save-button {
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.add-button:hover,
.save-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.85);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 6px;
  max-width: 700px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.bot-status-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
}

.bot-status-display {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.status-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.5rem;
  height: 2.5rem;
  border-radius: 50%;
  font-size: 1.5rem;
}

.status-indicator.status-running {
  color: #51cf66;
  background: rgba(81, 207, 102, 0.1);
}

.status-indicator.status-stopped {
  color: #adb5bd;
  background: rgba(173, 181, 189, 0.1);
}

.status-indicator.status-checking {
  color: #4dabf7;
  background: rgba(77, 171, 247, 0.1);
}

.status-indicator.status-error {
  color: #ff6b6b;
  background: rgba(255, 107, 107, 0.1);
}

.status-indicator.status-unknown {
  color: #868e96;
  background: rgba(134, 142, 150, 0.1);
}

.status-text {
  color: #fff;
  font-size: 1rem;
}

.bot-controls {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.status-button,
.start-button,
.stop-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.25rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
}

.status-button {
  background: rgba(77, 171, 247, 0.1);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.status-button:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.2);
  border-color: rgba(77, 171, 247, 0.5);
}

.start-button {
  background: linear-gradient(135deg, #51cf66 0%, #37b24d 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(81, 207, 102, 0.3);
}

.start-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #37b24d 0%, #2b8a3e 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(81, 207, 102, 0.4);
}

.stop-button {
  background: rgba(255, 107, 107, 0.1);
  color: #ff6b6b;
  border: 1px solid rgba(255, 107, 107, 0.3);
}

.stop-button:hover:not(:disabled) {
  background: rgba(255, 107, 107, 0.2);
  border-color: rgba(255, 107, 107, 0.5);
}

.status-button:disabled,
.start-button:disabled,
.stop-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.invite-row .invite-controls {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-bottom: 0.75rem;
}

.invite-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.25rem;
  background: linear-gradient(135deg, #5865f2 0%, #4752c4 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
}

.invite-button:hover {
  background: linear-gradient(135deg, #4752c4 0%, #3c45a5 100%);
  transform: translateY(-1px);
}

.invite-link-preview {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.3);
  border-radius: 4px;
  word-break: break-all;
}

.discord-status {
  margin-top: 1rem;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.875rem;
  font-weight: 500;
}

.status-pill.installed {
  background: rgba(81, 207, 102, 0.1);
  color: #51cf66;
  border: 1px solid rgba(81, 207, 102, 0.3);
}

.status-pill.not-installed {
  background: rgba(255, 107, 107, 0.1);
  color: #ff6b6b;
  border: 1px solid rgba(255, 107, 107, 0.3);
}

.status-pill.unknown {
  background: rgba(173, 181, 189, 0.1);
  color: #adb5bd;
  border: 1px solid rgba(173, 181, 189, 0.3);
}
</style>
