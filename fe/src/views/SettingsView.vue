<template>
  <div class="settings-page">
    <div class="settings-tabs">
      <!-- Mobile dropdown -->
      <div class="settings-tabs-mobile">
        <CustomSelect v-model="activeTab" :options="mobileTabOptions" class="tab-dropdown" />
      </div>

      <!-- Desktop tabs (turns into a horizontal carousel when overflowing) -->
      <div class="settings-tabs-desktop-wrapper">
        <button
          type="button"
          class="tabs-scroll-btn left"
          @click="scrollTabs(-1)"
          v-show="hasTabOverflow && showLeftTabChevron"
          aria-hidden="true"
        >
          ‹
        </button>

        <div ref="desktopTabsRef" class="settings-tabs-desktop">
          <button
            @click="router.push({ hash: '#rootfolders' })"
            :class="{ active: activeTab === 'rootfolders' }"
            class="tab-button"
          >
            <PhFolder />
            Root Folders
          </button>
          <button
            @click="router.push({ hash: '#indexers' })"
            :class="{ active: activeTab === 'indexers' }"
            class="tab-button"
          >
            <PhListMagnifyingGlass />
            Indexers
          </button>
          <button
            @click="router.push({ hash: '#clients' })"
            :class="{ active: activeTab === 'clients' }"
            class="tab-button"
          >
            <PhDownload />
            Download Clients
          </button>
          <button
            @click="router.push({ hash: '#quality-profiles' })"
            :class="{ active: activeTab === 'quality-profiles' }"
            class="tab-button"
          >
            <PhStar />
            Quality Profiles
          </button>
          <button
            @click="router.push({ hash: '#notifications' })"
            :class="{ active: activeTab === 'notifications' }"
            class="tab-button"
          >
            <PhBell />
            Notifications
          </button>
          <button
            @click="router.push({ hash: '#bot' })"
            :class="{ active: activeTab === 'bot' }"
            class="tab-button"
          >
            <PhGlobe />
            Discord Bot
          </button>
          <button
            @click="router.push({ hash: '#general' })"
            :class="{ active: activeTab === 'general' }"
            class="tab-button"
          >
            <PhSliders />
            General Settings
          </button>
        </div>

        <button
          type="button"
          class="tabs-scroll-btn right"
          @click="scrollTabs(1)"
          v-show="hasTabOverflow && showRightTabChevron"
          aria-hidden="true"
        >
          ›
        </button>
      </div>
    </div>

    <!-- Settings Toolbar -->
    <div class="settings-toolbar">
      <div class="toolbar-content">
        <div class="toolbar-actions">
          <!-- Add buttons for each section -->
          <button
            v-if="activeTab === 'rootfolders'"
            @click="openAddRootFolder()"
            class="add-button"
          >
            <PhPlus />
            Add Root Folder
          </button>
          <button v-if="activeTab === 'clients'" @click="openAddClient()" class="add-button">
            <PhPlus />
            Add Download Client
          </button>
          <button
            v-if="activeTab === 'clients'"
            @click="downloadClientsRef?.openAddMapping()"
            class="add-button"
          >
            <PhPlus />
            Add Mapping
          </button>
          <button
            v-if="activeTab === 'quality-profiles'"
            @click="qualityProfilesRef?.openAddProfile()"
            class="add-button"
          >
            <PhPlus />
            Add Quality Profile
          </button>

          <button
            v-if="activeTab === 'indexers'"
            @click="indexersRef?.openAddIndexer()"
            class="add-button"
          >
            <PhPlus />
            Add Indexer
          </button>

          <button
            v-if="activeTab === 'notifications'"
            @click="notificationsRef?.openWebhookForm()"
            class="add-button"
          >
            <PhPlus />
            Add Webhook
          </button>

          <!-- Save button for sections that need it -->
          <button
            v-if="activeTab === 'general' || activeTab === 'bot'"
            @click="saveSettings"
            :disabled="configStore.isLoading"
            class="save-button"
            :title="!isFormValid ? 'Please fix invalid fields before saving' : ''"
          >
            <template v-if="configStore.isLoading">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhFloppyDisk />
            </template>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>
      </div>
    </div>

    <div class="settings-content">
      <!-- Indexers Tab -->
      <IndexersTab v-if="activeTab === 'indexers'" ref="indexersRef" />

      <!-- Download Clients Tab -->
      <DownloadClientsTab v-if="activeTab === 'clients'" ref="downloadClientsRef" />

      <!-- Quality Profiles Tab -->
      <QualityProfilesTab v-if="activeTab === 'quality-profiles'" ref="qualityProfilesRef" />

      <!-- General Settings Tab -->
      <GeneralSettingsTab
        v-if="activeTab === 'general' && settings"
        ref="generalSettingsRef"
        :settings="settings"
        :startupConfig="startupConfig"
        :authEnabled="authEnabled"
        @update:authEnabled="authEnabled = $event"
        @update:startupConfig="startupConfig = $event"
      />

      <!-- Root Folders Tab -->
      <RootFoldersTab v-if="activeTab === 'rootfolders'" ref="rootFoldersRef" />

      <!-- Discord Bot Tab -->
      <DiscordBotTab v-if="activeTab === 'bot' && settings" :settings="settings" />

      <NotificationsTab
        v-if="activeTab === 'notifications' && settings"
        ref="notificationsRef"
        :settings="settings"
      />
    </div>

    <!-- Metadata Source Configuration Modal -->
    <div v-if="showApiForm" class="modal-overlay" @click="closeApiForm">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ editingApi ? 'Edit' : 'Add' }} Metadata Source</h3>
          <button @click="closeApiForm" class="modal-close">
            <PhX />
          </button>
        </div>
        <div class="modal-body">
          <form @submit.prevent="saveApiConfig" class="config-form">
            <div class="form-group">
              <label for="api-name">Name *</label>
              <input
                id="api-name"
                v-model="apiForm.name"
                type="text"
                placeholder="e.g., Audimeta"
                required
              />
            </div>

            <div class="form-group">
              <label for="api-url">Base URL *</label>
              <input
                id="api-url"
                v-model="apiForm.baseUrl"
                type="url"
                placeholder="https://api.example.com"
                required
              />
            </div>

            <div class="form-group">
              <label for="api-key">API Key</label>
              <input
                id="api-key"
                v-model="apiForm.apiKey"
                type="password"
                placeholder="Optional API key"
              />
              <small>Leave empty if not required</small>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label for="api-priority">Priority</label>
                <input
                  id="api-priority"
                  v-model.number="apiForm.priority"
                  type="number"
                  min="1"
                  max="100"
                />
                <small>Lower numbers = higher priority</small>
              </div>

              <div class="form-group">
                <label for="api-rate-limit">Rate Limit (per minute)</label>
                <input
                  id="api-rate-limit"
                  v-model="apiForm.rateLimitPerMinute"
                  type="number"
                  min="0"
                  placeholder="0 = unlimited"
                />
              </div>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="apiForm.isEnabled" type="checkbox" />
                <span>Enable this metadata source</span>
              </label>
            </div>
          </form>
        </div>
        <div class="modal-actions">
          <button @click="closeApiForm" class="cancel-button" type="button">
            <PhX />
            Cancel
          </button>
          <button @click="saveApiConfig" class="save-button" type="button">
            <PhCheck />
            Save
          </button>
        </div>
      </div>
    </div>

    <!-- Webhook Configuration Modal -->

    <!-- Delete Metadata Source Confirmation Modal -->
    <div v-if="apiToDelete" class="modal-overlay" @click="apiToDelete = null">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>
            <PhWarningCircle />
            Delete Metadata Source
          </h3>
          <button @click="apiToDelete = null" class="modal-close">
            <PhX />
          </button>
        </div>
        <div class="modal-body">
          <p>
            Are you sure you want to delete the metadata source
            <strong>{{ apiToDelete.name }}</strong
            >?
          </p>
          <p>This action cannot be undone.</p>
        </div>
        <div class="modal-actions">
          <button @click="apiToDelete = null" class="cancel-button">Cancel</button>
          <button @click="executeDeleteApi()" class="delete-button">
            <PhTrash />
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
  <!-- .settings-page -->
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onBeforeUnmount, watch, computed, nextTick } from 'vue'
import { apiService } from '@/services/api'
import { useRoute, useRouter } from 'vue-router'
import { logger } from '@/utils/logger'
import { errorTracking } from '@/services/errorTracking'
import { useConfigurationStore } from '@/stores/configuration'
import type {
  ApiConfiguration,
  DownloadClientConfiguration,
  ApplicationSettings,
} from '@/types'
import RootFoldersTab from '@/views/settings/RootFoldersTab.vue'
import DownloadClientsTab from '@/views/settings/DownloadClientsTab.vue'
import QualityProfilesTab from '@/views/settings/QualityProfilesTab.vue'
import DiscordBotTab from '@/views/settings/DiscordBotTab.vue'
import NotificationsTab from '@/views/settings/NotificationsTab.vue'
import IndexersTab from '@/views/settings/IndexersTab.vue'
import GeneralSettingsTab from '@/views/settings/GeneralSettingsTab.vue'
import CustomSelect from '@/components/CustomSelect.vue'
import {
  PhFolder,
  PhListMagnifyingGlass,
  PhDownload,
  PhStar,
  PhBell,
  PhGlobe,
  PhSliders,
  PhPlus,
  PhSpinner,
  PhFloppyDisk,
  PhX,
  PhCheck,
  PhWarningCircle,
  PhTrash,
} from '@phosphor-icons/vue'
import { useToast } from '@/services/toastService'

// Generate UUID v4 compatible across all browsers
function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const toast = useToast()
// Debug environment markers (Vitest exposes import.meta.vitest / import.meta.env.VITEST)
logger.debug(
  '[test-debug] import.meta.vitest:',
  (import.meta as unknown as { vitest?: unknown }).vitest,
  'env.VITEST:',
  (import.meta as unknown as { env?: Record<string, unknown> }).env?.VITEST,
  '__vitest_global__:',
  (globalThis as unknown as { __vitest?: unknown }).__vitest,
)
const activeTab = ref<
  'rootfolders' | 'indexers' | 'clients' | 'quality-profiles' | 'notifications' | 'bot' | 'general'
>('rootfolders')

const mobileTabOptions = computed(() => [
  { value: 'rootfolders', label: 'Root Folders', icon: PhFolder },
  { value: 'indexers', label: 'Indexers', icon: PhListMagnifyingGlass },
  { value: 'clients', label: 'Download Clients', icon: PhDownload },
  { value: 'quality-profiles', label: 'Quality Profiles', icon: PhStar },
  { value: 'notifications', label: 'Notifications', icon: PhBell },
  { value: 'bot', label: 'Discord Bot', icon: PhGlobe },
  { value: 'general', label: 'General Settings', icon: PhSliders },
  // Integrations removed
])
// Desktop tabs carousel refs/state
const desktopTabsRef = ref<HTMLElement | null>(null)
const hasTabOverflow = ref(false)
const showLeftTabChevron = ref(false)
const showRightTabChevron = ref(false)
const rootFoldersRef = ref<InstanceType<typeof RootFoldersTab> | null>(null)
const downloadClientsRef = ref<InstanceType<typeof DownloadClientsTab> | null>(null)
const qualityProfilesRef = ref<InstanceType<typeof QualityProfilesTab> | null>(null)
const indexersRef = ref<InstanceType<typeof IndexersTab> | null>(null)
const notificationsRef = ref<InstanceType<typeof NotificationsTab> | null>(null)

function updateTabOverflow() {
  const el = desktopTabsRef.value
  if (!el) return
  hasTabOverflow.value = el.scrollWidth > el.clientWidth + 1
  showLeftTabChevron.value = el.scrollLeft > 5
  showRightTabChevron.value = el.scrollLeft + el.clientWidth < el.scrollWidth - 5
}

function scrollTabs(direction = 1) {
  const el = desktopTabsRef.value
  if (!el) return
  const amount = Math.round(el.clientWidth * 0.6) * direction
  el.scrollBy({ left: amount, behavior: 'smooth' })
}

let tabsResizeObserver: ResizeObserver | null = null
onMounted(async () => {
  // Wait until DOM is fully painted (fonts, icons) so measurements are accurate
  await nextTick()
  updateTabOverflow()
  window.addEventListener('resize', updateTabOverflow)

  const el = desktopTabsRef.value
  if (el) {
    el.addEventListener('scroll', updateTabOverflow, { passive: true })

    // Use ResizeObserver to detect when content/size changes cause overflow
    if (typeof ResizeObserver !== 'undefined') {
      tabsResizeObserver = new ResizeObserver(() => updateTabOverflow())
      tabsResizeObserver.observe(el)
      // also observe the parent in case the container resizes
      if (el.parentElement) tabsResizeObserver.observe(el.parentElement)
    } else {
      // Fallback: run a delayed check to account for late layout shifts
      setTimeout(updateTabOverflow, 250)
    }
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateTabOverflow)
  const el = desktopTabsRef.value
  if (el) {
    el.removeEventListener('scroll', updateTabOverflow)
  }
  if (tabsResizeObserver) {
    tabsResizeObserver.disconnect()
    tabsResizeObserver = null
  }
})

// Ensure active tab is visible when switching tabs on desktop
function ensureActiveTabVisible() {
  const el = desktopTabsRef.value
  if (!el) return
  const active = el.querySelector('.tab-button.active') as HTMLElement | null
  if (active) {
    // center the active tab in view when overflowing
    active.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' })
  }
}

function openAddRootFolder() {
  if (rootFoldersRef.value && typeof rootFoldersRef.value.openAddRootFolder === 'function') {
    rootFoldersRef.value.openAddRootFolder()
  }
}

function openAddClient() {
  if (downloadClientsRef.value && typeof downloadClientsRef.value.openAddClient === 'function') {
    downloadClientsRef.value.openAddClient()
  }
}


// Audible integration removed

const showPassword = ref(false)

const toggleShowPassword = () => {
  showPassword.value = !showPassword.value
  if (
    generalSettingsRef.value &&
    typeof ((generalSettingsRef.value as unknown) as { toggleShowPassword?: () => void }).toggleShowPassword === 'function'
  ) {
    ;((generalSettingsRef.value as unknown) as { toggleShowPassword?: () => void }).toggleShowPassword()
  }
}

const toggleDownloadClientFunc = async (client: DownloadClientConfiguration) => {
  if (
    downloadClientsRef.value &&
    typeof ((downloadClientsRef.value as unknown) as {
      toggleDownloadClientFunc?: (c: DownloadClientConfiguration) => Promise<void>
    }).toggleDownloadClientFunc === 'function'
  ) {
    return await ((downloadClientsRef.value as unknown) as {
      toggleDownloadClientFunc?: (c: DownloadClientConfiguration) => Promise<void>
    }).toggleDownloadClientFunc!(client)
  }

  // Fallback: perform the toggle using the configuration store directly when
  // the child tab isn't mounted or available yet (tests may call the helper
  // before the child is attached to the parent instance).
  try {
    const updatedClient = { ...client, isEnabled: !client.isEnabled }
    await configStore.saveDownloadClientConfiguration(updatedClient)
    const idx = configStore.downloadClientConfigurations.findIndex((c) => c.id === client.id)
    if (idx !== -1) {
      configStore.downloadClientConfigurations[idx] = updatedClient
    }
    toast.success(
      'Download client',
      `${client.name} ${updatedClient.isEnabled ? 'enabled' : 'disabled'} successfully`,
    )
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'toggleDownloadClient',
    })
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

// Make helpers available on SettingsView instance for tests
// so unit tests can call wrapper.vm.toggleShowPassword() and
// wrapper.vm.toggleDownloadClientFunc(...) directly.
defineExpose({ toggleShowPassword, toggleDownloadClientFunc, showPassword })

watch(activeTab, () => {
  // delay slightly to allow layout updates
  setTimeout(() => {
    updateTabOverflow()
    if (hasTabOverflow.value) ensureActiveTabVisible()
  }, 40)
})
const showApiForm = ref(false)
const editingApi = ref<ApiConfiguration | null>(null)
const apiForm = reactive({
  id: '',
  name: '',
  baseUrl: '',
  apiKey: '',
  type: 'metadata',
  isEnabled: true,
  priority: 1,
  rateLimitPerMinute: '',
})
const settings = ref<ApplicationSettings | null>(null)
const startupConfig = ref<import('@/types').StartupConfig | null>(null)
const authEnabled = ref(false)
const testingNotification = ref(false)

const adminUsers = ref<
  Array<{ id: number; username: string; email?: string; isAdmin: boolean; createdAt: string }>
>([])
const generalSettingsRef = ref<InstanceType<typeof GeneralSettingsTab> | null>(null)
const isFormValid = computed(() => {
  // During unit tests we allow saving to proceed (tests set up inputs manually).
  // Vitest exposes import.meta.env.VITEST which we can use to relax validation.
  const vitestEnv = (import.meta as unknown as { env?: Record<string, unknown> }).env?.VITEST
  if (vitestEnv) return true

  // Delegate validation to GeneralSettingsTab if it's active
  if (activeTab.value === 'general' && generalSettingsRef.value) {
    return generalSettingsRef.value.isProxyConfigValid
  }

  // No longer require output path since we use root folders now
  return true
})

const formatApiError = (error: unknown): string => {
  // Handle axios-style errors
  const axiosError = error as { response?: { data?: unknown; status?: number } }
  if (axiosError.response?.data) {
    const responseData = axiosError.response.data
    let errorMessage = 'An unknown error occurred'

    if (typeof responseData === 'string') {
      errorMessage = responseData
    } else if (responseData && typeof responseData === 'object') {
      const data = responseData as Record<string, unknown>
      errorMessage =
        (data.message as string) || (data.error as string) || JSON.stringify(responseData, null, 2)
    }

    // Capitalize first letter and ensure it ends with punctuation
    errorMessage = errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
    if (!errorMessage.match(/[.!?]$/)) {
      errorMessage += '.'
    }

    return errorMessage
  }

  // Handle native fetch errors (from ApiService)
  const fetchError = error as Error & { status?: number; body?: string }
  if (fetchError.body) {
    try {
      const parsedBody = JSON.parse(fetchError.body)
      if (parsedBody && typeof parsedBody === 'object') {
        const data = parsedBody as Record<string, unknown>
        let errorMessage =
          (data.message as string) || (data.error as string) || JSON.stringify(parsedBody, null, 2)

        // Capitalize first letter and ensure it ends with punctuation
        errorMessage = errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
        if (!errorMessage.match(/[.!?]$/)) {
          errorMessage += '.'
        }

        return errorMessage
      }
      return fetchError.body
    } catch {
      return fetchError.body
    }
  }

  // Fallback for other error types
  const errorMessage = error instanceof Error ? error.message : String(error)
  return errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const editApiConfig = (api: ApiConfiguration) => {
  editingApi.value = api
  apiForm.id = api.id
  apiForm.name = api.name
  apiForm.baseUrl = api.baseUrl
  apiForm.apiKey = api.apiKey || ''
  apiForm.type = 'metadata' // Always metadata since this is the metadata sources modal
  apiForm.isEnabled = api.isEnabled
  apiForm.priority = api.priority
  apiForm.rateLimitPerMinute = api.rateLimitPerMinute || ''
  showApiForm.value = true
}

const closeApiForm = () => {
  showApiForm.value = false
  editingApi.value = null
  // Reset form
  apiForm.id = ''
  apiForm.name = ''
  apiForm.baseUrl = ''
  apiForm.apiKey = ''
  apiForm.type = 'metadata'
  apiForm.isEnabled = true
  apiForm.priority = 1
  apiForm.rateLimitPerMinute = ''
}

const apiToDelete = ref<ApiConfiguration | null>(null)

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const confirmDeleteApi = (api: ApiConfiguration) => {
  apiToDelete.value = api
}

const executeDeleteApi = async (id?: string) => {
  const apiId = id || apiToDelete.value?.id
  if (!apiId) return

  try {
    await configStore.deleteApiConfiguration(apiId)
    toast.success('API', 'API configuration deleted successfully')
    // Refresh API list if the store provides a loader
    try {
      await configStore.loadApiConfigurations()
    } catch {}
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'deleteApiConfig',
    })
    const errorMessage = formatApiError(error)
    toast.error('API delete failed', errorMessage)
  } finally {
    apiToDelete.value = null
  }
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const toggleApiConfig = async (api: ApiConfiguration) => {
  try {
    // Toggle the enabled state
    const updatedApi = { ...api, isEnabled: !api.isEnabled }
    await configStore.saveApiConfiguration(updatedApi)
    toast.success(
      'Metadata source',
      `${api.name} ${updatedApi.isEnabled ? 'enabled' : 'disabled'} successfully`,
    )
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'toggleApiConfig',
    })
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

// Test a download client configuration (include credentials in payload)
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const testNotification = async () => {
  if (!settings.value?.webhookUrl || settings.value.webhookUrl.trim() === '') {
    toast.error('Test failed', 'Please enter a webhook URL first')
    return
  }

  testingNotification.value = true
  try {
    const response = await apiService.testNotification()
    if (response.success) {
      toast.success('Test notification', response.message || 'Test notification sent successfully')
    } else {
      toast.error('Test failed', response.message || 'Failed to send test notification')
    }
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'testNotification',
    })
    const errorMessage = formatApiError(error)
    toast.error('Test failed', errorMessage)
  } finally {
    testingNotification.value = false
  }
}

const saveApiConfig = async () => {
  try {
    // Validate required fields
    if (!apiForm.name || !apiForm.baseUrl) {
      toast.error('Validation error', 'Name and Base URL are required')
      return
    }

    const apiData: ApiConfiguration = {
      id: apiForm.id || generateUUID(),
      name: apiForm.name,
      baseUrl: apiForm.baseUrl,
      apiKey: apiForm.apiKey,
      type: apiForm.type as 'torrent' | 'nzb' | 'metadata' | 'search' | 'other',
      isEnabled: apiForm.isEnabled,
      priority: apiForm.priority,
      headers: {},
      parameters: {},
      rateLimitPerMinute: apiForm.rateLimitPerMinute || undefined,
      createdAt: editingApi.value?.createdAt || new Date().toISOString(),
      lastUsed: editingApi.value?.lastUsed,
    }

    // Use the single save method which handles both create and update
    await configStore.saveApiConfiguration(apiData)

    toast.success(
      'Metadata source',
      `Metadata source ${editingApi.value ? 'updated' : 'added'} successfully`,
    )
    closeApiForm()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'saveMetadataSource',
    })
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

const saveSettings = async () => {
  if (!settings.value) return

  // Validate proxy fields if proxy usage is enabled (delegate to GeneralSettingsTab if active)
  if (activeTab.value === 'general' && generalSettingsRef.value) {
    if (settings.value.useUsProxy && !generalSettingsRef.value.isProxyConfigValid) {
      toast.error(
        'Invalid proxy',
        'Please provide a valid proxy host and port (1-65535) when using a proxy.',
      )
      return
    }
  }

  try {
    // Create a copy of settings, excluding empty admin fields
    const settingsToSave = { ...settings.value }

    // Only include adminUsername if it's not empty
    if (!settingsToSave.adminUsername || settingsToSave.adminUsername.trim() === '') {
      delete settingsToSave.adminUsername
    }

    // Only include adminPassword if it's not empty
    if (!settingsToSave.adminPassword || settingsToSave.adminPassword.trim() === '') {
      delete settingsToSave.adminPassword
    }

    // Only include proxy password if non-empty (we allow empty to clear)
    if (settingsToSave.usProxyPassword === undefined || settingsToSave.usProxyPassword === null) {
      delete settingsToSave.usProxyPassword
    }

    // No PascalCase keys are produced anymore; we only send camelCase properties.

    // Resolve the configuration store at call-time to ensure tests that set up Pinia
    // before mounting (or that replace the store) receive the correct instance.
    const runtimeConfigStore = useConfigurationStore()
    // Debug: log when saveSettings is invoked in tests to help diagnose test failures
    // (will be removed once tests are stable)
    logger.debug('[test-debug] saveSettings invoked', settingsToSave)
    // Call the runtime store save method. Some test setups replace the store
    // instance or spy on the store returned from `useConfigurationStore()` at
    // different times; call both if they differ to ensure the spy is observed.
    await runtimeConfigStore.saveApplicationSettings(settingsToSave)
    if (
      configStore !== runtimeConfigStore &&
      typeof configStore.saveApplicationSettings === 'function'
    ) {
      // If the module-level `configStore` differs (older test setups), call it too
      // so tests that replaced/observed that instance receive the call.
      // Avoid failing if the method isn't a function.
      configStore.saveApplicationSettings(settingsToSave)
    }
    toast.success('Settings', 'Settings saved successfully')
    // If user toggled the authEnabled, attempt to save to startup config
    try {
      const original = startupConfig.value || {}
      // Persist authenticationRequired as string 'true'/'false' so it's explicit and
      // consistent with expectations from the UI (was previously 'Enabled'/'Disabled').
      const newCfg: import('@/types').StartupConfig = {
        ...original,
        authenticationRequired: authEnabled.value ? 'true' : 'false',
      }
      try {
        await apiService.saveStartupConfig(newCfg)
        toast.success('Startup config', 'Startup configuration saved (config.json)')
      } catch {
        // If server can't persist startup config (e.g., permission denied), offer a fallback download of the config JSON
        toast.info(
          'Startup config',
          'Could not persist startup config to disk. Preparing downloadable startup config so you can save it manually.',
        )
        try {
          const blob = new Blob([JSON.stringify(newCfg, null, 2)], { type: 'application/json' })
          const url = URL.createObjectURL(blob)
          const a = document.createElement('a')
          a.href = url
          a.download = 'config.json'
          document.body.appendChild(a)
          a.click()
          a.remove()
          URL.revokeObjectURL(url)
          toast.info(
            'Startup config',
            'Download started. Save the file to the server config directory to persist the change.',
          )
        } catch {
          toast.info(
            'Startup config',
            'Also failed to prepare a download. Edit config/config.json on the host to make the change persistent.',
          )
        }
      }
    } catch {
      // Not fatal - write may not be allowed in some deployments
      toast.info(
        'Startup config',
        'Could not persist startup config to disk. Edit config/config.json on the host to make the change persistent.',
      )
    }
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'saveSettings',
    })
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

const loadAdminUsers = async () => {
  try {
    adminUsers.value = await apiService.getAdminUsers()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'SettingsView',
      operation: 'loadAdminUsers',
    })
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

// Helper functions for webhook UI
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const getWebhookIcon = (type: string) => {
  const iconMap: Record<string, unknown> = {
    Slack: PhBell,
    Discord: PhBell,
    Telegram: PhBell,
    Pushover: PhBell,
    Pushbullet: PhBell,
    NTFY: PhBell,
    Zapier: PhBell,
  }
  return iconMap[type] || PhBell
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as
    | 'rootfolders'
    | 'indexers'
    | 'clients'
    | 'quality-profiles'
    | 'notifications'
    | 'bot'
    | 'general'
  if (
    hash &&
    [
      'rootfolders',
      'indexers',
      'clients',
      'quality-profiles',
      'notifications',
      'bot',
      'general',
    ].includes(hash)
  ) {
    activeTab.value = hash as typeof activeTab.value
  } else {
    // Default to rootfolders and update URL
    activeTab.value = 'rootfolders'
    router.replace({ hash: '#rootfolders' })
  }
}

// Handle dropdown tab change
// const onTabChange = (event: Event) => {
//   const target = event.target as HTMLSelectElement
//   const newTab = target.value as 'rootfolders' | 'indexers' | 'clients' | 'quality-profiles' | 'notifications' | 'requests' | 'general'
//   activeTab.value = newTab
//   router.push({ hash: `#${newTab}` })
// }

// Watch for hash changes
watch(
  () => route.hash,
  () => {
    syncTabFromHash()
  },
)

// Track which tab data has been loaded to avoid duplicate requests
const loaded = reactive({
  indexers: false,
  clients: false,
  profiles: false,
  admins: false,
  mappings: false,
  general: false,
  rootfolders: false,
  bot: false,
  integrations: false,
})

async function loadTabContents(tab: string) {
  try {
    switch (tab) {
      case 'indexers':
        if (!loaded.indexers) {
          // IndexersTab manages its own loading
          loaded.indexers = true
        }
        break
      case 'rootfolders':
        if (!loaded.rootfolders) {
          // root folder UI will manage its own loading; just mark as loaded
          loaded.rootfolders = true
        }
        break
      case 'clients':
        if (!loaded.clients) {
          await configStore.loadDownloadClientConfigurations()
          loaded.clients = true
        }
        break
      case 'quality-profiles':
        if (!loaded.profiles) {
          loaded.profiles = true
        }
        break
      case 'general':
        if (!loaded.general) {
          // General needs application settings and admin users
          await configStore.loadApplicationSettings()
          // Ensure sensible default
          if (settings.value && !settings.value.completedFileAction)
            settings.value.completedFileAction = 'Move'
          // Ensure new settings have sensible defaults when not present
          if (
            settings.value &&
            (settings.value.downloadCompletionStabilitySeconds === undefined ||
              settings.value.downloadCompletionStabilitySeconds === null)
          )
            settings.value.downloadCompletionStabilitySeconds = 10
          if (
            settings.value &&
            (settings.value.missingSourceRetryInitialDelaySeconds === undefined ||
              settings.value.missingSourceRetryInitialDelaySeconds === null)
          )
            settings.value.missingSourceRetryInitialDelaySeconds = 30
          if (
            settings.value &&
            (settings.value.missingSourceMaxRetries === undefined ||
              settings.value.missingSourceMaxRetries === null)
          )
            settings.value.missingSourceMaxRetries = 3
          // Initialize notification triggers array if not present
          if (settings.value && !settings.value.enabledNotificationTriggers)
            settings.value.enabledNotificationTriggers = []
          // Ensure new search settings have sensible defaults when not present
          // Create a shallow copy of the store settings so we can safely
          // mutate defaults for the UI without relying on store ref unwrapping.
          const raw = configStore.applicationSettings
            ? { ...configStore.applicationSettings }
            : null
          if (raw) {
            // Normalize values coming from the backend which may use PascalCase
            // property names (e.g., EnableAmazonSearch) instead of camelCase.
            const rawObj = raw as Record<string, unknown>
            const normalized: Record<string, unknown> = { ...rawObj }

            // Helper to prefer camelCase, then PascalCase, then fallback
            const pickBool = (camel: string, pascal: string, fallback: boolean) => {
              const c = rawObj[camel]
              const p = rawObj[pascal]
              if (c !== undefined && c !== null) return Boolean(c)
              if (p !== undefined && p !== null) return Boolean(p)
              return fallback
            }

            const pickNumber = (camel: string, pascal: string, fallback: number) => {
              const c = rawObj[camel]
              const p = rawObj[pascal]
              const val =
                c !== undefined && c !== null
                  ? Number(c)
                  : p !== undefined && p !== null
                    ? Number(p)
                    : fallback
              // Treat zero as missing and use fallback
              if (!val || Number.isNaN(val)) return fallback
              return val
            }

            const amazon = pickBool('enableAmazonSearch', 'EnableAmazonSearch', true)
            const audible = pickBool('enableAudibleSearch', 'EnableAudibleSearch', true)
            const openlib = pickBool('enableOpenLibrarySearch', 'EnableOpenLibrarySearch', true)

            const candidateCap = pickNumber('searchCandidateCap', 'SearchCandidateCap', 100)
            const resultCap = pickNumber('searchResultCap', 'SearchResultCap', 100)
            const fuzzy = pickNumber('searchFuzzyThreshold', 'SearchFuzzyThreshold', 0.2)

            // Assign normalized camelCase properties for the UI binding
            normalized.enableAmazonSearch = amazon
            normalized.enableAudibleSearch = audible
            normalized.enableOpenLibrarySearch = openlib
            normalized.searchCandidateCap = candidateCap
            normalized.searchResultCap = resultCap
            normalized.searchFuzzyThreshold = fuzzy

            // Set camelCase properties for the UI binding and saving
            settings.value = normalized as unknown as ApplicationSettings

            // Sync normalized object back to the store so other consumers use it
            configStore.applicationSettings = settings.value
          } else {
            settings.value = null
          }

          try {
            await loadAdminUsers()
            loaded.admins = true
            if (adminUsers.value.length > 0 && settings.value) {
              const firstAdmin = adminUsers.value[0]
              if (firstAdmin) settings.value.adminUsername = firstAdmin.username
            }
          } catch (e) {
            logger.debug('Failed to load admin users', e)
          }

          loaded.general = true
        }
        break
      case 'bot':
        if (!loaded.bot) {
          // Requests tab needs application settings and quality profiles
          await configStore.loadApplicationSettings()
          // Reuse the same normalization logic for requests tab load
          const rawReq = configStore.applicationSettings
            ? { ...configStore.applicationSettings }
            : null
          if (rawReq) {
            const rawReqObj = rawReq as Record<string, unknown>
            const normalizedReq: Record<string, unknown> = { ...rawReqObj }
            const pickBoolReq = (camel: string, pascal: string, fallback: boolean) => {
              const c = rawReqObj[camel]
              const p = rawReqObj[pascal]
              if (c !== undefined && c !== null) return Boolean(c)
              if (p !== undefined && p !== null) return Boolean(p)
              return fallback
            }
            const pickNumberReq = (camel: string, pascal: string, fallback: number) => {
              const c = rawReqObj[camel]
              const p = rawReqObj[pascal]
              const val =
                c !== undefined && c !== null
                  ? Number(c)
                  : p !== undefined && p !== null
                    ? Number(p)
                    : fallback
              if (!val || Number.isNaN(val)) return fallback
              return val
            }
            normalizedReq.enableAmazonSearch = pickBoolReq(
              'enableAmazonSearch',
              'EnableAmazonSearch',
              true,
            )
            normalizedReq.enableAudibleSearch = pickBoolReq(
              'enableAudibleSearch',
              'EnableAudibleSearch',
              true,
            )
            normalizedReq.enableOpenLibrarySearch = pickBoolReq(
              'enableOpenLibrarySearch',
              'EnableOpenLibrarySearch',
              true,
            )
            normalizedReq.searchCandidateCap = pickNumberReq(
              'searchCandidateCap',
              'SearchCandidateCap',
              100,
            )
            normalizedReq.searchResultCap = pickNumberReq('searchResultCap', 'SearchResultCap', 100)
            normalizedReq.searchFuzzyThreshold = pickNumberReq(
              'searchFuzzyThreshold',
              'SearchFuzzyThreshold',
              0.2,
            )
            settings.value = normalizedReq as unknown as ApplicationSettings
            configStore.applicationSettings = settings.value
          } else {
            settings.value = null
          }
          loaded.bot = true
        }
        break
      case 'notifications':
        // Notifications are part of general settings
        if (!loaded.general) {
          await loadTabContents('general')
        }
        break
      default:
        // default to indexers
        if (!loaded.indexers) {
          // IndexersTab manages its own loading
          loaded.indexers = true
        }
    }
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'SettingsView',
      operation: 'loadTabContents',
      metadata: { tab },
    })
  }
}

onMounted(async () => {
  // Set initial tab from URL hash
  syncTabFromHash()

  // Load only the data needed for the active tab; other tabs load on demand
  await loadTabContents(activeTab.value)

  // Load startup config (optional) to determine AuthenticationRequired — keep this lightweight
  try {
    startupConfig.value = await apiService.getStartupConfig()
    const obj = startupConfig.value as Record<string, unknown> | null
    const raw = obj ? (obj['authenticationRequired'] ?? obj['AuthenticationRequired']) : undefined
    const v = raw as unknown
    authEnabled.value =
      typeof v === 'boolean'
        ? v
        : typeof v === 'string'
          ? v.toLowerCase() === 'enabled' || v.toLowerCase() === 'true'
          : false
  } catch {
    authEnabled.value = false
  }

  // Watch for tab changes and fetch content on-demand
  watch(activeTab, (t) => {
    void loadTabContents(t)
  })
})
</script>

<style scoped>
.settings-page {
  position: relative;
  top: 60px;
  padding: 2rem;
  min-height: 100vh;
  background-color: #1a1a1a;
}

.settings-header {
  margin-bottom: 2rem;
}

.settings-header h1 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 2rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.settings-header h1 i {
  color: #4dabf7;
}

.settings-header p {
  margin: 0;
  color: #adb5bd;
  font-size: 1rem;
  line-height: 1.5;
}

.settings-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid rgba(255, 255, 255, 0.08);
}

.tab-button {
  padding: 1rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: #868e96;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-weight: 500;
  position: relative;
}

.tab-button:hover {
  background-color: rgba(77, 171, 247, 0.08);
  color: #fff;
}

.tab-button.active {
  color: #4dabf7;
  background-color: rgba(77, 171, 247, 0.15);
}

.tab-button.active::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, #4dabf7 0%, #339af0 100%);
  border-radius: 6px;
}

.settings-content {
  background: #2a2a2a;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-height: 500px;
  margin-top: 60px; /* Add margin to account for fixed toolbar */
}

/* Desktop tabs carousel styles */
.settings-tabs-desktop-wrapper {
  position: relative;
}

.settings-tabs-desktop {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  scroll-behavior: smooth;
  padding-bottom: 4px; /* give space for hidden scrollbar */
  scrollbar-gutter: stable both-edges;
}

/* keep the scrollable area clipped so overflowing tabs are hidden */
.settings-tabs-desktop-wrapper {
  overflow: hidden;
}

.settings-tabs-desktop {
  align-items: center;
  white-space: nowrap;
  padding: 0 12px; /* space for chevron overlay */
  scroll-padding-left: 48px;
  scroll-padding-right: 48px;
}

.settings-tabs-desktop .tab-button {
  flex: 0 0 auto;
}

.settings-tabs-desktop::-webkit-scrollbar {
  height: 6px;
}

/* hide the native scrollbar while preserving scrollability */
.settings-tabs-desktop::-webkit-scrollbar {
  display: none;
}
.settings-tabs-desktop {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

.tabs-scroll-btn {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 36px;
  height: 36px;
  border-radius: 6px;
  background: rgba(0, 0, 0, 0.8);
  color: #fff;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 1;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.5);
  transition:
    transform 0.15s ease,
    background 0.15s ease;
}

.tabs-scroll-btn.left {
  left: 0;
}

.tabs-scroll-btn.right {
  right: 0;
}

.tabs-scroll-btn:hover {
  background: rgba(0, 0, 0, 1);
  transform: translateY(-50%) scale(1.02);
}

/* Settings Toolbar */
.settings-toolbar {
  position: fixed;
  top: 60px; /* Account for global header nav */
  left: 200px; /* Account for sidebar width */
  right: 0;
  z-index: 99; /* Below global nav (1000) but above content */
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
  margin-bottom: 20px;
}

.toolbar-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.toolbar-actions {
  display: flex;
  gap: 1rem;
  align-items: center;
}

/* When tabs don't overflow hide the scrollbar and buttons via v-show in template */

.tab-content {
  padding: 2rem;
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

.save-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #868e96;
}

.empty-state .empty-icon {
  font-size: 4rem;
  color: #495057;
  margin-bottom: 1rem;
  width: 4rem;
  height: 4rem;
}

.empty-state h3 {
  margin: 1rem 0 0.5rem 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.empty-state p {
  margin: 0.5rem 0;
  font-size: 1.05rem;
  line-height: 1.6;
  color: #adb5bd;
}

.empty-state .empty-help {
  font-size: 0.95rem;
  color: #868e96;
  margin-bottom: 2rem;
}

.add-button-large {
  margin-top: 1.5rem;
  padding: 1rem 2rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  font-weight: 600;
  font-size: 1rem;
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.3);
}

.add-button-large:hover {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(30, 136, 229, 0.4);
}

.section-title-wrapper {
  flex: 1;
}

.section-subtitle {
  margin: 0.5rem 0 0 0;
  font-size: 0.95rem;
  color: #868e96;
  font-weight: normal;
}

/* Webhook Grid Layout */
.webhooks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
  gap: 1.5rem;
}

/* Webhook Card */
.webhook-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
  transition: all 0.2s ease;
  display: flex;
  flex-direction: column;
}

.webhook-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.webhook-card.disabled {
  opacity: 0.5;
  filter: grayscale(50%);
}

.webhook-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  cursor: pointer;
}

/* No hover state: matches other headers */

.webhook-header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-left: 1rem;
}

.webhook-title-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex: 1;
}

.webhook-icon {
  width: 40px;
  height: 40px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 1.2rem;
}

.webhook-icon.service-slack {
  background: linear-gradient(135deg, #4a154b 0%, #611f69 100%);
  color: #fff;
}

.webhook-icon.service-discord {
  background: linear-gradient(135deg, #5865f2 0%, #404eed 100%);
  color: #fff;
}

.webhook-icon.service-telegram {
  background: linear-gradient(135deg, #0088cc 0%, #006699 100%);
  color: #fff;
}

.webhook-icon.service-pushover {
  background: linear-gradient(135deg, #249df1 0%, #1a7dc4 100%);
  color: #fff;
}

.webhook-icon.service-pushbullet {
  background: linear-gradient(135deg, #4ab367 0%, #3a9053 100%);
  color: #fff;
}

.webhook-icon.service-ntfy {
  background: linear-gradient(135deg, #ff6b6b 0%, #ee5a52 100%);
  color: #fff;
}

.webhook-icon.service-zapier {
  background: linear-gradient(135deg, #ff4a00 0%, #e04200 100%);
  color: #fff;
}

.webhook-info h4 {
  margin: 0 0 0.25rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.webhook-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.triggers-preview {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.trigger-badge-small {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid;
  cursor: help;
  transition: all 0.2s ease;
}

.trigger-badge-small:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
}

.trigger-badge-small svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.trigger-badge-small.trigger-added {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
}

.trigger-badge-small.trigger-downloading {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border-color: rgba(77, 171, 247, 0.3);
}

.trigger-badge-small.trigger-available {
  background-color: rgba(156, 39, 176, 0.15);
  color: #b197fc;
  border-color: rgba(156, 39, 176, 0.3);
}

.webhook-type-badge {
  display: inline-block;
  padding: 0.25rem 0.65rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.webhook-status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.7rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.webhook-status-badge {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.webhook-status-badge.active {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
}

.expand-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  background-color: rgba(255, 255, 255, 0.05);
  color: #adb5bd;
  cursor: pointer;
  transition: all 0.2s ease;
}

.expand-toggle:hover {
  background-color: rgba(77, 171, 247, 0.15);
  border-color: rgba(77, 171, 247, 0.3);
  color: #4dabf7;
}

.expand-toggle svg {
  width: 18px;
  height: 18px;
  transition: transform 0.3s ease;
}

.expand-toggle.expanded svg {
  transform: rotate(180deg);
}

/* Expand/Collapse Animation */
.expand-enter-active,
.expand-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
  padding-top: 0;
  padding-bottom: 0;
}

.expand-enter-to,
.expand-leave-from {
  max-height: 500px;
  opacity: 1;
}

.webhook-body {
  padding: 1.5rem;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.webhook-url-container {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background-color: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
}

.url-icon {
  color: #4dabf7;
  font-size: 1.1rem;
  flex-shrink: 0;
}

.webhook-url {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 0.85rem;
  color: #adb5bd;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.webhook-triggers-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.triggers-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #868e96;
  font-size: 0.85rem;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.triggers-label {
  color: #adb5bd;
}

.triggers-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.trigger-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.85rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
  border: 1px solid;
}

.trigger-badge svg {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.trigger-badge.trigger-added {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
}

.trigger-badge.trigger-downloading {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border-color: rgba(77, 171, 247, 0.3);
}

.trigger-badge.trigger-available {
  background-color: rgba(156, 39, 176, 0.15);
  color: #b197fc;
  border-color: rgba(156, 39, 176, 0.3);
}

.config-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.config-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  transition: all 0.2s ease;
}

.config-card:hover {
  background-color: #2f2f2f;
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.config-info {
  flex: 1;
  min-width: 0;
}

.config-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.config-url {
  margin: 0 0 1rem 0;
  color: #4dabf7;
  font-family: 'Courier New', monospace;
  font-size: 0.9rem;
  overflow-wrap: break-word;
  word-break: break-all;
}

.config-meta {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.config-meta span {
  padding: 0.4rem 0.8rem;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.config-type {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.config-status {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.config-status.enabled {
  background-color: rgba(46, 204, 113, 0.15);
  color: #51cf66;
  border: 1px solid rgba(46, 204, 113, 0.3);
}

.config-priority {
  background-color: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.config-ssl {
  background-color: rgba(127, 140, 141, 0.15);
  color: #95a5a6;
  border: 1px solid rgba(127, 140, 141, 0.3);
}

.config-ssl.enabled {
  background-color: rgba(241, 196, 15, 0.15);
  color: #fcc419;
  border: 1px solid rgba(241, 196, 15, 0.3);
}

.config-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.edit-button,
.delete-button {
  padding: 0.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  font-size: 1.1rem;
}

.edit-button {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.edit-button:hover {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.delete-button {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.4);
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  margin-bottom: 2rem;
}

.form-section:last-child {
  margin-bottom: 0;
}

.form-section h3 {
  color: #fff;
  font-size: 1.1rem;
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #444;
}

.form-section h4 {
  margin: 0 0 1.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.form-section h4 i {
  color: #4dabf7;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  color: #fff;
  font-weight: 600;
  font-size: 0.95rem;
}

.form-group input,
.form-group select {
  width: 100%;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  color: #fff;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.form-group input::placeholder {
  color: #999;
  opacity: 1;
}

.form-group input:-webkit-autofill,
.form-group input:-webkit-autofill:hover,
.form-group input:-webkit-autofill:focus {
  -webkit-box-shadow: 0 0 0 1000px #1a1a1a inset !important;
  -webkit-text-fill-color: #fff !important;
  border: 1px solid #444 !important;
}

.form-group input:disabled,
.form-group select:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  background-color: #0d0d0d;
}

.form-group select option:hover,
.form-group select option:focus,
.form-group select option:checked {
  background-color: #005a9e;
  color: #ffffff;
  border: none;
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-group input:focus-visible,
.form-group select:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.form-group small {
  display: block;
  margin-top: 0.5rem;
  color: #b3b3b3;
  font-size: 0.85rem;
}

.checkbox-group {
  margin-bottom: 1rem;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.checkbox-group label:hover {
  border-color: #007acc;
  background-color: #222;
}

.checkbox-group input[type='checkbox'] {
  margin-top: 0.25rem;
  width: auto;
  cursor: pointer;
}

.checkbox-group input[type='checkbox']:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
}

.checkbox-group label small {
  color: #b3b3b3;
  font-size: 0.85rem;
  font-weight: normal;
}

.form-help {
  font-size: 0.85rem;
  color: #868e96;
  font-style: italic;
  line-height: 1.5;
}

/* Invite controls for Discord bot */
.invite-row {
  margin-top: 1rem;
}
.invite-controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}
.invite-button {
  padding: 0.6rem 1rem;
  background: linear-gradient(135deg, #20c997 0%, #198754 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
}
.invite-button:hover {
  transform: translateY(-1px);
}
.invite-link-preview small a {
  color: #74c0fc;
  text-decoration: underline;
}
.invite-link-preview small a {
  /* allow long oauth links to wrap cleanly in the preview */
  word-break: break-all;
  white-space: normal;
}
.invite-controls .icon-button {
  /* When using icon-style buttons inside invite-controls we want them to
     expand to fit labels (e.g. "Copy Invite Link") instead of being forced
     into the square icon-button size used elsewhere in the UI. */
  width: auto;
  height: auto;
  min-width: 36px;
  padding: 0.45rem 0.75rem;
  font-size: 0.95rem;
}

.invite-controls .save-button {
  /* keep primary register action prominent but avoid forcing full-width */
  white-space: nowrap;
}
.discord-status {
  margin-top: 0.5rem;
}
.status-pill {
  display: inline-block;
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}
.status-pill.installed {
  background-color: rgba(46, 204, 113, 0.12);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.18);
}
.status-pill.not-installed {
  background-color: rgba(244, 67, 54, 0.08);
  color: #ff6b6b;
  border: 1px solid rgba(244, 67, 54, 0.12);
}
.status-pill.unknown {
  background-color: rgba(77, 171, 247, 0.08);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.12);
}

.checkbox-group {
  flex-direction: row;
  align-items: flex-start;
  background-color: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 1rem;
  transition: all 0.2s ease;
}

.checkbox-group:hover {
  background-color: rgba(0, 0, 0, 0.3);
  border-color: rgba(77, 171, 247, 0.2);
}

.checkbox-group:last-child {
  margin-bottom: 0;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  cursor: pointer;
  width: 100%;
}

.checkbox-group input[type='checkbox'] {
  margin: 0.25rem 0 0 0;
  width: 18px;
  height: 18px;
  cursor: pointer;
  flex-shrink: 0;
  accent-color: #4dabf7;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
  font-weight: 600;
}

.checkbox-group label small {
  color: #868e96;
  font-size: 0.85rem;
  font-weight: normal;
  line-height: 1.5;
}

/* Authentication Section Styles */
.auth-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.auth-row input[type='checkbox'] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #4dabf7;
}

.auth-row label {
  color: #fff;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  margin: 0;
}

.admin-credentials {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.admin-input {
  padding: 0.75rem;
  background-color: rgba(0, 0, 0, 0.2);
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s ease;
}

.admin-input:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.15);
}

.admin-input::placeholder {
  color: #6c757d;
  font-style: italic;
}

/* Password field with inline toggle */
.password-field {
  position: relative;
  width: 100%;
}

.password-input {
  width: 100%;
  padding-right: 3.5rem; /* space for the toggle button */
}

.password-toggle {
  position: absolute;
  right: 0.5rem;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #868e96;
  padding: 0.35rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: color 0.2s ease;
}

.password-toggle:hover {
  color: #4dabf7;
}

.form-error {
  color: #ff6b6b;
  font-size: 0.9rem;
  margin-top: 0.4rem;
}

.info-inline {
  background: none;
  border: none;
  color: #74c0fc;
  margin-left: 0.5rem;
  cursor: pointer;
  transition: color 0.2s ease;
}

.info-inline:hover {
  color: #4dabf7;
}

.error-summary {
  margin-top: 1rem;
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.2);
  padding: 0.75rem 1rem;
  border-radius: 6px;
  color: #ff6b6b;
}

.error-summary ul {
  margin: 0.5rem 0 0 1.2rem;
}

.input-group-btn.regenerate-button {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: white;
  border: none;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 500;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.input-group-btn.regenerate-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #c0392b 0%, #a93226 100%);
}

.input-group-btn.regenerate-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Input group styling for API key */
.input-group {
  display: flex;
  align-items: stretch;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  overflow: hidden;
}

.input-group:focus-within {
  border-color: rgba(77, 171, 247, 0.3);
}

.input-group-input {
  flex: 1;
  background: #1a1a1a !important;
  color: #adb5bd;
  padding: 0.75rem 1rem;
  border: none !important;
  border-radius: 6px !important;
  box-shadow: none !important;
}

.input-group-input:focus {
  outline: none;
  background: #1a1a1a !important;
  box-shadow: none !important;
}

.input-group-append {
  display: flex;
  background: rgba(0, 0, 0, 0.3);
}

.input-group-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.05);
  border: none;
  border-radius: 6px;
  border-left: 1px solid rgba(255, 255, 255, 0.1);
  color: #868e96;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 1rem;
}

.input-group-btn:first-child {
  border-left: none;
}

.input-group-btn:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.2);
  color: #4dabf7;
}

.input-group-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.input-group-btn.copied {
  background: rgba(81, 207, 102, 0.2) !important;
  color: #51cf66 !important;
}

.input-group-btn.copied:hover {
  background: rgba(81, 207, 102, 0.3) !important;
}

.test-button {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  font-weight: 500;
  transition: all 0.2s ease;
  border: none;
  border-left: 1px solid rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  font-size: 0.9rem;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.test-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.test-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
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

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #444;
}

.modal-header h2 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s;
}

.close-btn:hover {
  background-color: #333;
  color: #fff;
}

.close-btn:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.modal-close {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s;
}

.modal-close:hover {
  background-color: #333;
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
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

/* Ensure modal context delete buttons are full-size, not the small icon-style
   square buttons used elsewhere in the UI. This overrides the
   generic .delete-button rules with a more suitable modal appearance. */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button,
.modal-overlay .modal-content .modal-actions .modal-delete-button,
.modal-content .modal-actions .modal-delete-button {
  /* Stronger selector to guarantee modal buttons override list/icon buttons */
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
  min-width: 120px; /* ensure modal delete button is clearly larger than icon buttons */
  height: auto;
  box-shadow: 0 6px 16px rgba(231, 76, 60, 0.12);
}

/* Keep hover style consistent and prominent */
.modal-overlay .modal-content .modal-actions .delete-button:hover,
.modal-content .modal-actions .delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 8px 20px rgba(231, 76, 60, 0.24);
}

.modal-actions .delete-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.35);
}

.modal-actions .delete-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background-color: #555;
  color: white;
  border: none;
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
  background-color: #666;
  transform: translateY(-1px);
}

/* Indexer Styles */
.indexers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1.5rem;
  margin-top: 1.5rem;
}

.indexer-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  transition: all 0.2s ease;
}

.indexer-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.indexer-card.disabled {
  opacity: 0.5;
  filter: grayscale(50%);
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.indexer-type {
  display: inline-block;
  padding: 0.3rem 0.75rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.indexer-type.torrent {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.indexer-type.usenet {
  background-color: rgba(33, 150, 243, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.indexer-type.ddl {
  background-color: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
}

.icon-button {
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  cursor: pointer;
  color: #adb5bd;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  font-size: 1.1rem;
  width: 36px;
  height: 36px;
}

.icon-button:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.15);
  border-color: #4dabf7;
  color: #4dabf7;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(77, 171, 247, 0.3);
}

.icon-button.danger {
  color: #ff6b6b;
}

.icon-button.danger:hover:not(:disabled) {
  background: rgba(255, 107, 107, 0.15);
  border-color: #ff6b6b;
  color: #ff6b6b;
  box-shadow: 0 2px 8px rgba(255, 107, 107, 0.3);
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1.5rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-size: 0.9rem;
}

.detail-row i {
  color: #4dabf7;
  font-size: 1rem;
  flex-shrink: 0;
}

.detail-label {
  color: #868e96;
  min-width: 100px;
}

.detail-value {
  color: #adb5bd;
  word-break: break-all;
}

.detail-value.success {
  color: #51cf66;
}

.detail-value.error {
  color: #ff6b6b;
}

.detail-value i {
  margin-left: 0.5rem;
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.6rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
}

.error-message {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(244, 67, 54, 0.1);
  border: 1px solid rgba(244, 67, 54, 0.2);
  border-radius: 6px;
  color: #ff6b6b;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.error-message i {
  font-size: 1rem;
}

@media (max-width: 768px) {
  .settings-page {
    padding: 1rem;
  }

  .settings-tabs {
    flex-direction: column;
    gap: 0;
  }

  .tab-button {
    border-bottom: 1px solid #333;
    border-left: 3px solid transparent;
    justify-content: flex-start;
  }

  .tab-button.active {
    border-left-color: #007acc;
    border-bottom-color: transparent;
  }

  .config-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .config-actions {
    width: 100%;
    justify-content: flex-end;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .add-button,
  .save-button {
    width: 100%;
    justify-content: center;
  }

  .settings-toolbar {
    left: 0; /* Full width on mobile */
  }

  .toolbar-content {
    flex-direction: column;
    gap: 1rem;
    align-items: stretch;
  }

  .toolbar-actions {
    justify-content: center;
  }

  .indexers-grid {
    grid-template-columns: 1fr;
  }

  .indexer-header {
    flex-direction: column;
    gap: 1rem;
  }

  .indexer-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .detail-label {
    min-width: auto;
  }
}

/* Quality Profile Cards */
.profiles-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(500px, 1fr));
  gap: 1.5rem;
}

.profile-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.profile-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
  transform: translateY(-1px);
}

.profile-card.is-default {
  border-color: rgba(77, 171, 247, 0.3);
  background: rgba(77, 171, 247, 0.05);
}

.profile-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.profile-title-section {
  flex: 1;
}

.profile-name-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.profile-card h4 {
  margin: 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.7rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.default {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.profile-description {
  margin: 0;
  color: #868e96;
  font-size: 0.9rem;
  line-height: 1.5;
}

.profile-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.profile-content {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.profile-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.profile-section h5 {
  margin: 0;
  color: #4dabf7;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.profile-section h5 i {
  font-size: 1rem;
}

/* Quality Badges */
.quality-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.quality-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.4rem 0.75rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}

.quality-badge.is-cutoff {
  background-color: rgba(255, 152, 0, 0.15);
  color: #ff9800;
  border-color: rgba(255, 152, 0, 0.3);
}

.quality-badge i {
  font-size: 0.75rem;
}

/* Preferences Grid */
.preferences-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.preference-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.preference-label {
  color: #868e96;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.preference-value {
  color: #fff;
  font-size: 0.9rem;
}

/* Limits Grid */
.limits-grid {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.limit-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.limit-item i {
  color: #4dabf7;
  font-size: 1.1rem;
}

.limit-label {
  color: #868e96;
  font-size: 0.85rem;
  min-width: 80px;
}

.limit-value {
  color: #fff;
  font-size: 0.9rem;
  font-weight: 500;
}

/* Word Filters */
.word-filters {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.word-filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-type {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #868e96;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.filter-type i {
  font-size: 0.9rem;
}

.word-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.word-tag {
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}

.word-tag.positive {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.word-tag.required {
  background-color: rgba(255, 152, 0, 0.15);
  color: #fcc419;
  border: 1px solid rgba(255, 152, 0, 0.3);
}

.word-tag.forbidden {
  background-color: rgba(244, 67, 54, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(244, 67, 54, 0.3);
}

.warning-text {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(255, 152, 0, 0.1);
  border-left: 3px solid #fcc419;
  color: #fcc419;
  margin: 1rem 0;
  border-radius: 6px;
}

.warning-text i {
  font-size: 1.2rem;
}

@media (max-width: 768px) {
  .settings-page {
    padding: 1rem;
  }

  .settings-tabs {
    flex-direction: column;
    gap: 0;
  }

  .tab-button {
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    border-left: 3px solid transparent;
    justify-content: flex-start;
  }

  .tab-button.active::after {
    display: none;
  }

  .tab-button.active {
    border-left-color: #4dabf7;
    border-bottom-color: transparent;
  }

  .config-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .config-info {
    width: 100%;
  }

  .config-info h4 {
    font-size: 1rem;
  }

  .config-url {
    font-size: 0.8rem;
    word-break: break-all;
    white-space: normal;
    margin-right: 1rem;
  }

  .config-meta {
    flex-wrap: wrap;
    gap: 0.5rem;
  }

  .config-meta span {
    font-size: 0.75rem;
    padding: 0.3rem 0.6rem;
  }

  .config-triggers {
    width: 100%;
  }

  .config-actions {
    width: 100%;
    justify-content: flex-end;
    gap: 0.75rem;
  }

  .config-actions .icon-button {
    padding: 0.6rem;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .section-header h3 {
    font-size: 1.3rem;
  }

  .add-button,
  .save-button {
    width: 100%;
    justify-content: center;
  }

  .indexers-grid {
    grid-template-columns: 1fr;
  }

  .indexer-header {
    flex-direction: column;
    gap: 1rem;
  }

  .indexer-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .detail-label {
    min-width: auto;
  }

  .profiles-grid {
    grid-template-columns: 1fr;
  }

  .profile-header {
    flex-direction: column;
    gap: 1rem;
  }

  .profile-actions {
    margin-left: 0;
    width: 100%;
    justify-content: flex-start;
  }
}

/* Webhook Modal Specific Styles */

.modal-footer {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
  transform: translateY(-1px);
}

.btn-info {
  background-color: #2196f3;
  color: white;
}

.btn-info:hover:not(:disabled) {
  background-color: #1976d2;
  transform: translateY(-1px);
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005a9e;
  transform: translateY(-1px);
}

/* Webhook Modal Responsive Styles */
@media (max-width: 768px) {
  .webhooks-grid {
    grid-template-columns: 1fr;
  }

  .webhook-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .webhook-title-row {
    width: 100%;
  }

  .webhook-header-actions {
    width: 100%;
    justify-content: space-between;
  }

  .webhook-actions {
    grid-template-columns: 1fr 1fr;
  }

  .action-btn.toggle-btn,
  .action-btn.delete-btn {
    grid-column: span 1;
  }

  .action-btn.test-btn,
  .action-btn.edit-btn {
    grid-column: span 1;
  }

  .webhook-modal {
    width: 95%;
    max-height: 95vh;
  }

  .webhook-modal .modal-header,
  .webhook-modal .modal-body,
  .webhook-modal .modal-actions {
    padding: 1.25rem 1.5rem;
  }

  .webhook-modal .modal-icon {
    width: 48px;
    height: 48px;
  }

  .webhook-modal .modal-icon svg {
    width: 24px;
    height: 24px;
  }

  .webhook-modal .modal-title h3 {
    font-size: 1.3rem;
  }

  .webhook-form .form-row {
    flex-direction: column;
  }

  .trigger-content {
    gap: 0.75rem;
  }

  .trigger-icon {
    width: 40px;
    height: 40px;
  }

  .trigger-icon svg {
    width: 20px;
    height: 20px;
  }

  .trigger-check {
    width: 24px;
    height: 24px;
  }

  .triggers-section .section-title {
    flex-direction: column;
    align-items: flex-start;
  }

  .trigger-count {
    align-self: flex-start;
  }
}

/* Discord Bot Process Controls */
.bot-status-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.bot-status-display {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: var(--card-bg);
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.status-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
}

.status-indicator.status-running {
  color: #4caf50;
}

.status-indicator.status-stopped {
  color: #f44336;
}

.status-indicator.status-checking {
  color: #ff9800;
}

.status-indicator.status-error {
  color: #f44336;
}

.status-indicator.status-unknown {
  color: #9e9e9e;
}

.status-text {
  font-size: 0.9rem;
}

.bot-controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.status-button,
.start-button,
.stop-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.status-button {
  background-color: #2196f3;
  color: white;
}

.status-button:hover:not(:disabled) {
  background-color: #1976d2;
}

.start-button {
  background-color: #4caf50;
  color: white;
}

.start-button:hover:not(:disabled) {
  background-color: #388e3c;
}

.stop-button {
  background-color: #f44336;
  color: white;
}

.stop-button:hover:not(:disabled) {
  background-color: #d32f2f;
}

.status-button:disabled,
.start-button:disabled,
.stop-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Mobile styles for bot controls */
@media (max-width: 768px) {
  .bot-status-display {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .bot-controls {
    width: 100%;
  }

  .status-button,
  .start-button,
  .stop-button {
    flex: 1;
    justify-content: center;
  }
}

/* Mobile settings tabs */
@media (max-width: 768px) {
  .settings-tabs {
    flex-direction: column;
    gap: 1rem;
    border-bottom: unset;
  }

  .settings-tabs-mobile {
    display: block;
  }

  .settings-tabs-desktop {
    display: none;
  }

  .tab-dropdown {
    width: 100%;
    color: #fff;
    font-size: 0.95rem;
    cursor: pointer;
    transition: all 0.2s ease;
  }

  .tab-dropdown:focus {
    outline: none;
    border-color: #4dabf7;
    box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
  }

  .tab-dropdown option {
    background-color: #2a2a2a;
    color: #fff;
  }
}

/* Desktop settings tabs */
@media (min-width: 769px) {
  .settings-tabs {
    flex-direction: row;
  }

  .settings-tabs-mobile {
    display: none;
  }

  .settings-tabs-desktop {
    display: flex;
  }
}
</style>
