<template>
  <div class="tab-content">
    <div class="general-settings-tab">
      <div class="section-header">
        <h3>General Settings</h3>
      </div>

      <div v-if="validationErrors.length > 0" class="error-summary" role="alert">
        <strong>Please fix the following:</strong>
        <ul>
          <li v-for="(e, idx) in validationErrors" :key="idx">{{ e }}</li>
        </ul>
      </div>

      <div v-if="props.settings" class="settings-form">
        <div class="form-section">
          <h4><PhFolder /> File Management</h4>

          <div class="form-group">
            <label>File Naming Pattern</label>
            <input
              v-model="localSettings.fileNamingPattern"
              type="text"
              placeholder="{Author}/{Series}/{Title}"
            />
            <span class="form-help">
              Pattern for organizing audiobook files. Available variables:<br />
              <code>{Author}</code> - Author/narrator name<br />
              <code>{Series}</code> - Series name<br />
              <code>{Title}</code> - Book title<br />
              <code>{SeriesNumber}</code> - Position in series<br />
              <code>{DiskNumber}</code> or <code>{DiskNumber:00}</code> - Disk/part number (00 =
              zero-padded)<br />
              <code>{ChapterNumber}</code> or <code>{ChapterNumber:00}</code> - Chapter number (00 =
              zero-padded)<br />
              <code>{Year}</code> - Publication year<br />
              <code>{Quality}</code> - Audio quality
            </span>
          </div>

          <div class="form-group">
            <label>Completed File Action</label>
            <select v-model="localSettings.completedFileAction">
              <option value="Move">Move (default)</option>
              <option value="Copy">Copy</option>
            </select>
            <span class="form-help"
              >Choose whether completed downloads should be moved into the library output path or
              copied and left in the client's folder.</span
            >
          </div>
        </div>

        <div class="form-section">
          <h4><PhDownload /> Download Settings</h4>

          <div class="form-group">
            <label>Max Concurrent Downloads</label>
            <input
              v-model.number="localSettings.maxConcurrentDownloads"
              type="number"
              min="1"
              max="10"
            />
            <span class="form-help">Maximum number of simultaneous downloads (1-10)</span>
          </div>

          <div class="form-group">
            <label>Polling Interval (seconds)</label>
            <input
              v-model.number="localSettings.pollingIntervalSeconds"
              type="number"
              min="10"
              max="300"
            />
            <span class="form-help">How often to check download status (10-300 seconds)</span>
          </div>

          <div class="form-group">
            <label>Download Completion Stability (seconds)</label>
            <input
              v-model.number="localSettings.downloadCompletionStabilitySeconds"
              type="number"
              min="1"
              max="600"
            />
            <span class="form-help"
              >How long (seconds) a download must be seen as complete on the client before
              finalization begins. Increase for clients that post-process/extract after
              completion.</span
            >
          </div>

          <div class="form-group">
            <label>Missing-source Retry Initial Delay (seconds)</label>
            <input
              v-model.number="localSettings.missingSourceRetryInitialDelaySeconds"
              type="number"
              min="1"
              max="600"
            />
            <span class="form-help"
              >Initial retry delay (seconds) used when files are not yet available at finalization
              time.</span
            >
          </div>

          <div class="form-group">
            <label>Missing-source Max Retries</label>
            <input
              v-model.number="localSettings.missingSourceMaxRetries"
              type="number"
              min="0"
              max="20"
            />
            <span class="form-help"
              >Maximum number of retries to attempt if the finalized download's source files are
              missing.</span
            >
          </div>
        </div>

        <div class="form-section">
          <h4><PhToggleLeft /> Features</h4>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableMetadataProcessing" type="checkbox" />
              <span>
                <strong>Enable Metadata Processing</strong>
                <small>Automatically fetch and embed audiobook metadata</small>
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableCoverArtDownload" type="checkbox" />
              <span>
                <strong>Enable Cover Art Download</strong>
                <small>Download and embed cover art for audiobooks</small>
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableNotifications" type="checkbox" />
              <span>
                <strong>Enable Notifications</strong>
                <small>Receive notifications for downloads and events</small>
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.showCompletedExternalDownloads" type="checkbox" />
              <span>
                <strong>Show completed external downloads in Activity</strong>
                <small
                  >When enabled, completed torrents/NZBs from external clients will remain visible
                  in the Activity view. When disabled, completed external items will be hidden to
                  reduce clutter.</small
                >
              </span>
            </label>
          </div>
        </div>

        <div class="form-section">
          <h4><PhMagnifyingGlass /> Search Settings</h4>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableAmazonSearch" type="checkbox" />
              <span>
                <strong>Enable Amazon Searching</strong>
                <small
                  >Include Amazon-based search providers when performing intelligent
                  searches.</small
                >
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableAudibleSearch" type="checkbox" />
              <span>
                <strong>Enable Audible Searching</strong>
                <small
                  >Include Audible provider lookups when performing intelligent searches.</small
                >
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.enableOpenLibrarySearch" type="checkbox" />
              <span>
                <strong>Enable OpenLibrary Searching</strong>
                <small
                  >Include OpenLibrary title augmentation and lookups when performing intelligent
                  searches.</small
                >
              </span>
            </label>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>Candidate Cap (max candidates)</label>
              <input
                v-model.number="localSettings.searchCandidateCap"
                type="number"
                min="1"
                max="200"
              />
              <span class="form-help"
                >Maximum number of candidate ASINs/entries to consider when searching
                (candidateLimit).</span
              >
            </div>

            <div class="form-group">
              <label>Result Cap (max results)</label>
              <input
                v-model.number="localSettings.searchResultCap"
                type="number"
                min="1"
                max="200"
              />
              <span class="form-help"
                >Maximum number of results returned to the UI (returnLimit).</span
              >
            </div>
          </div>

          <div class="form-group">
            <label>Fuzzy Threshold</label>
            <input
              v-model.number="localSettings.searchFuzzyThreshold"
              type="number"
              step="0.01"
              min="0"
              max="1"
            />
            <span class="form-help"
              >Fuzzy matching threshold used when comparing titles/authors (0.0-1.0). Higher values
              require closer matches.</span
            >
          </div>
        </div>

        <div class="form-section">
          <h4><PhUserCircle /> Authentication</h4>

          <div class="form-group">
            <label>Login Screen</label>
            <div class="auth-row">
              <input type="checkbox" id="authToggle" v-model="authEnabled" />
              <label for="authToggle">Enable login screen</label>
            </div>
            <span class="form-help"
              >Toggle to enable the login screen. This setting reflects the server's
              <code>AuthenticationRequired</code> value from <code>config.json</code>. Changes here
              are local and will not modify server files — edit <code>config/config.json</code> on
              the host to persist.</span
            >
          </div>

          <div v-if="authEnabled" class="form-group">
            <label>Admin Account Management</label>
            <div class="admin-credentials">
              <input
                v-model="localSettings.adminUsername"
                type="text"
                placeholder="Admin username"
                class="admin-input"
              />
              <div class="password-field">
                <input
                  :type="showPassword ? 'text' : 'password'"
                  v-model="localSettings.adminPassword"
                  placeholder="New admin password"
                  class="admin-input password-input"
                />
                <button
                  type="button"
                  class="password-toggle"
                  @click.prevent="showPassword = !showPassword"
                  :aria-pressed="!!showPassword"
                  :title="showPassword ? 'Hide password' : 'Show password'"
                >
                  <template v-if="showPassword">
                    <PhEyeSlash />
                  </template>
                  <template v-else>
                    <PhEye />
                  </template>
                </button>
              </div>
            </div>
            <span class="form-help"
              >Manage the admin account. Enter a new password to update the admin user's password
              when you save settings. The username field shows the current admin username. This
              section is only available when authentication is enabled.</span
            >
          </div>

          <div class="form-group">
            <label>API Key (Server)</label>
            <div class="input-group">
              <ApiKeyControl :apiKey="props.startupConfig?.apiKey" :disabled="false" @update:apiKey="(newKey) => emit('update:startupConfig', { ...(props.startupConfig || {}), apiKey: newKey })" />

            </div>
            <span class="form-help"
              >API key for authenticating external applications. Generate a new key if needed. Copy
              it to use with API clients.</span
            >
          </div>
        </div>

        <div class="form-section">
          <h4>
            <PhGlobe /> External Requests / US Proxy
            <button
              type="button"
              class="info-inline"
              @click.prevent="openProxySecurityModal"
              title="Security recommendations"
            >
              <PhInfo />
            </button>
          </h4>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.preferUsDomain" type="checkbox" />
              <span>
                <strong>Prefer US (.com) domain for Audible/Amazon</strong>
                <small
                  >When enabled, the server will attempt a retry using the US (.com) domain if a
                  localized or redirect page is detected.</small
                >
              </span>
            </label>
          </div>

          <div class="form-group checkbox-group">
            <label>
              <input v-model="localSettings.useUsProxy" type="checkbox" />
              <span>
                <strong>Use HTTP proxy for US requests</strong>
                <small
                  >When enabled, Audible/Amazon retries to the US domain will be routed through the
                  proxy configured below.</small
                >
              </span>
            </label>
          </div>

          <div class="form-group">
            <label>US Proxy Host</label>
            <input
              v-model="localSettings.usProxyHost"
              type="text"
              placeholder="proxy.example.com"
              :disabled="!localSettings.useUsProxy"
              data-cy="us-proxy-host"
            />
            <div
              v-if="
                localSettings.useUsProxy &&
                (!localSettings.usProxyHost || String(localSettings.usProxyHost).trim() === '')
              "
              class="form-error"
            >
              Proxy host is required when using a proxy.
            </div>
          </div>

          <div class="form-group">
            <label>US Proxy Port</label>
            <input
              v-model.number="localSettings.usProxyPort"
              type="number"
              min="1"
              max="65535"
              :disabled="!localSettings.useUsProxy"
              data-cy="us-proxy-port"
            />
            <div
              v-if="
                localSettings.useUsProxy &&
                (!localSettings.usProxyPort || Number(localSettings.usProxyPort) <= 0)
              "
              class="form-error"
            >
              Proxy port must be between 1 and 65535.
            </div>
          </div>

          <div class="form-group">
            <label>US Proxy Username (optional)</label>
            <input
              v-model="localSettings.usProxyUsername"
              type="text"
              placeholder="username"
              :disabled="!localSettings.useUsProxy"
            />
          </div>

          <div class="form-group">
            <label>US Proxy Password (optional)</label>
            <div class="password-field">
              <input
                :type="showPassword ? 'text' : 'password'"
                v-model="localSettings.usProxyPassword"
                placeholder="Proxy password"
                class="admin-input password-input"
                :disabled="!localSettings.useUsProxy"
              />
              <button
                type="button"
                class="password-toggle"
                @click.prevent="toggleShowPassword"
                :aria-pressed="!!showPassword"
                :title="showPassword ? 'Hide password' : 'Show password'"
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
              >Store proxy credentials here for convenience. For production, consider using a
              secrets manager instead of storing passwords in the application database.</span
            >
          </div>
        </div>
      </div>

      <!-- Proxy Security Modal -->
      <div v-if="showProxySecurityModal" class="modal-overlay" @click="closeProxySecurityModal()">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>
              <PhShieldCheck />
              Proxy security recommendations
            </h3>
            <button @click="closeProxySecurityModal()" class="modal-close"><PhX /></button>
          </div>
          <div class="modal-body">
            <p>
              Storing proxy credentials in the application database is convenient but has security
              implications. Consider the following:
            </p>
            <ul>
              <li>
                Use an OS-level secrets manager (Vault, Azure Key Vault, AWS Secrets Manager) when
                possible.
              </li>
              <li>Restrict access to the application database and backups.</li>
              <li>
                Rotate credentials periodically and prefer short-lived credentials where supported.
              </li>
              <li>
                If you must store secrets in the DB, ensure the server is deployed on trusted
                infrastructure and consider application-level encryption.
              </li>
            </ul>
            <p>
              This modal only provides guidance; the current implementation persists the proxy
              password in ApplicationSettings. For production use, consider integrating a secrets
              store and referencing credentials instead of storing plaintext.
            </p>
          </div>
          <div class="modal-actions">
            <button @click="closeProxySecurityModal()" class="save-button">Close</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import { logger } from '@/utils/logger'
import { apiService } from '@/services/api'
import { showConfirm } from '@/composables/useConfirm'
import type { ApplicationSettings, StartupConfig } from '@/types'
import {
  PhFolder,
  PhDownload,
  PhToggleLeft,
  PhMagnifyingGlass,
  PhUserCircle,
  PhGlobe,
  PhInfo,
  PhEye,
  PhEyeSlash,
  PhCheck,
  PhFiles,
  PhSpinner,
  PhArrowCounterClockwise,
  PhPlus,
  PhShieldCheck,
  PhX,
} from '@phosphor-icons/vue'

import ApiKeyControl from '@/components/ApiKeyControl.vue'

interface Props {
  settings: ApplicationSettings | null
  startupConfig: StartupConfig | null | undefined
  authEnabled: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  'update:authEnabled': [value: boolean]
  'update:startupConfig': [value: StartupConfig]
  'update:settings': [value: ApplicationSettings | null]
}>()

const toast = useToast()
const showPassword = ref(false)
const showProxySecurityModal = ref(false)

// Local reactive copy of settings to avoid mutating incoming prop directly
import { reactive, watch, nextTick } from 'vue'
const localSettings = reactive<ApplicationSettings>({} as ApplicationSettings)

// Prevent recursive update loops: when syncing from parent props we set this flag to
// avoid emitting update:settings during the sync process.
let isSyncing = false

watch(
  () => props.settings,
  (val) => {
    if (val) {
      isSyncing = true
      // Replace properties rather than reassigning the reactive object
      for (const key of Object.keys(localSettings) as Array<keyof ApplicationSettings>) {
        delete (localSettings as unknown as Record<string, unknown>)[key as string]
      }
      Object.assign(localSettings, val)
      // Release syncing flag after the microtask so subsequent user-driven changes emit
      nextTick(() => {
        isSyncing = false
      })
    } else {
      isSyncing = true
      for (const key of Object.keys(localSettings) as Array<keyof ApplicationSettings>) {
        delete (localSettings as unknown as Record<string, unknown>)[key as string]
      }
      nextTick(() => {
        isSyncing = false
      })
    }
  },
  { immediate: true, deep: true },
)

// Also watch the proxy toggle specifically so tests that mutate the parent object in-place
// reliably propagate to the child without waiting for a full object replacement.
watch(
  () => props.settings?.useUsProxy,
  (val) => {
    if (typeof val !== 'undefined') {
      localSettings.useUsProxy = val as boolean
    }
  },
  { immediate: true },
)

// Emit updates upstream whenever the user changes a field
watch(
  localSettings,
  (val) => {
    if (isSyncing) return
    emit('update:settings', { ...val })
  },
  { deep: true },
)

// Local computed for two-way binding with parent
const authEnabled = computed({
  get: () => props.authEnabled,
  set: (value) => emit('update:authEnabled', value),
})

const isProxyConfigValid = computed(() => {
  if (!localSettings) return true
  if (!localSettings.useUsProxy) return true
  const host = (localSettings.usProxyHost || '').toString().trim()
  const port = Number(localSettings.usProxyPort || 0)
  return host.length > 0 && port > 0 && port <= 65535
})

const validationErrors = computed(() => {
  const errs: string[] = []
  if (!localSettings) return errs
  if (localSettings.useUsProxy) {
    const host = (localSettings.usProxyHost || '').toString().trim()
    const port = Number(localSettings.usProxyPort || 0)
    if (!host) errs.push('US proxy host is required when proxy is enabled')
    if (!port || port <= 0 || port > 65535) errs.push('US proxy port must be between 1 and 65535')
  }
  return errs
})

const toggleShowPassword = () => {
  showPassword.value = !showPassword.value
}

const openProxySecurityModal = () => {
  showProxySecurityModal.value = true
}

const closeProxySecurityModal = () => {
  showProxySecurityModal.value = false
}



// Expose method for parent component
defineExpose({
  isProxyConfigValid,
  toggleShowPassword,
})
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

.general-settings-tab {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 2rem;
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

.error-summary {
  background: rgba(244, 67, 54, 0.1);
  border: 1px solid rgba(244, 67, 54, 0.3);
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 1.5rem;
  color: #f44336;
}

.error-summary strong {
  display: block;
  margin-bottom: 0.5rem;
}

.error-summary ul {
  margin: 0;
  padding-left: 1.5rem;
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 1.5rem;
  transition: all 0.2s ease;
}

.form-section:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.12);
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

.info-inline {
  background: none;
  border: none;
  color: #4dabf7;
  cursor: pointer;
  padding: 0.25rem;
  display: inline-flex;
  align-items: center;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1rem;
}

.info-inline:hover {
  background: rgba(33, 150, 243, 0.1);
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
}

.form-group input[type='text'],
.form-group input[type='number'],
.form-group input[type='url'],
.form-group input[type='password'],
.form-group select {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  background: #1a1a1a;
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

.form-group input:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.form-help {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: #adb5bd;
  line-height: 1.5;
}

.form-help code {
  background: rgba(255, 255, 255, 0.05);
  padding: 0.2rem 0.4rem;
  border-radius: 3px;
  font-family: 'Courier New', monospace;
  font-size: 0.9em;
}

.form-error {
  color: #f44336;
  font-size: 0.85rem;
  margin-top: 0.5rem;
}

.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
  padding: 0.75rem;
  border-radius: 6px;
  transition: background 0.2s;
}

.checkbox-group label:hover {
  background: rgba(77, 171, 247, 0.08);
}

.checkbox-group input[type='checkbox'] {
  margin-top: 0.2rem;
  width: 18px;
  height: 18px;
  cursor: pointer;
  flex-shrink: 0;
}

.checkbox-group span {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkbox-group strong {
  color: #fff;
  font-weight: 500;
}

.checkbox-group small {
  color: #adb5bd;
  font-size: 0.85rem;
  line-height: 1.4;
}

.auth-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
}

.auth-row label {
  margin: 0;
  cursor: pointer;
  font-weight: normal;
  color: #fff;
}

.admin-credentials {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.admin-input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  background: #1a1a1a;
  color: #fff;
  font-size: 0.95rem;
}

.password-field {
  position: relative;
  display: flex;
  align-items: center;
}

.password-input {
  flex: 1;
  padding-right: 3rem;
}

.password-toggle {
  position: absolute;
  right: 0.75rem;
  background: none;
  border: none;
  color: #adb5bd;
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
  background: rgba(77, 171, 247, 0.1);
  color: #4dabf7;
}

.input-group {
  display: flex;
  gap: 0;
}

.input-group-input {
  flex: 1;
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
}

.input-group-append {
  display: flex;
}

.input-group-btn {
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
  border-left: none;
  padding: 0.75rem 1rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.08);
  color: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s;
  font-size: 0.95rem;
  white-space: nowrap;
}

.input-group-btn:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.15);
  border-color: #4dabf7;
  color: #4dabf7;
}

.input-group-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.icon-button {
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
  border-right: none;
}

.icon-button.copied {
  color: #4caf50;
}

.regenerate-button {
  border-top-left-radius: 6px;
  border-bottom-left-radius: 6px;
}

.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Modal Styles (canonical) */
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

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #444;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.25rem;
}

.modal-close {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
}

.modal-close:hover {
  background: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

/* Ensure modal context delete buttons are full-size */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button {
  padding: 0.75rem 1.25rem;
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.18s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.6rem;
  font-weight: 700;
  font-size: 1rem;
  min-width: 120px;
  height: auto;
  box-shadow: 0 6px 16px rgba(231, 76, 60, 0.12);
}

.modal-overlay .modal-content .modal-actions .delete-button:hover,
.modal-content .modal-actions .delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background: rgba(255, 255, 255, 0.05);
  color: #fff;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.cancel-button:hover {
  background: rgba(77, 171, 247, 0.1);
  border-color: #4dabf7;
}

.modal-body ul {
  padding-left: 1.5rem;
  margin: 1rem 0;
}

.modal-body li {
  margin-bottom: 0.5rem;
  line-height: 1.6;
}

.modal-actions {
  display: flex;
  gap: 0.75rem;
  padding: 1.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  justify-content: flex-end;
}

.save-button {
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.save-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.save-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}
</style>
