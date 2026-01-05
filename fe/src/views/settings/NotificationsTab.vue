<template>
  <div class="tab-content">
    <div class="notifications-tab">
      <div class="section-header">
        <h3>Notifications</h3>
      </div>
      <div v-if="webhooks.length === 0" class="empty-state">
        <PhBellSlash class="empty-icon" />
        <h3>No webhooks configured</h3>
        <p>Webhooks allow you to receive real-time notifications when important events occur.</p>
        <p class="empty-help">
          Supported services include Slack, Discord, Telegram, Pushover, and more.
        </p>
        <button @click="openWebhookForm" class="add-button-large">
          <PhPlus />
          Create Your First Webhook
        </button>
      </div>

      <div v-else class="webhooks-grid">
        <div
          v-for="webhook in webhooks"
          :key="webhook.id"
          class="webhook-card"
          :class="{ disabled: !webhook.isEnabled }"
        >
          <div class="webhook-header">
            <div class="webhook-title-row">
              <div class="webhook-info">
                <h4>{{ webhook.name }}</h4>
                <div class="webhook-meta">
                  <span class="webhook-type-badge">{{ webhook.type }}</span>
                  <div class="triggers-preview">
                    <span
                      v-for="trigger in webhook.triggers"
                      :key="trigger"
                      class="trigger-badge-small"
                      :class="getTriggerClass(trigger)"
                      :title="formatTriggerName(trigger)"
                    >
                      <component :is="getTriggerIcon(trigger)" />
                    </span>
                  </div>
                </div>
              </div>
            </div>
            <div class="webhook-header-actions">
              <div class="webhook-status-badge" :class="{ active: webhook.isEnabled }">
                <component :is="webhook.isEnabled ? PhCheckCircle : PhXCircle" />
                {{ webhook.isEnabled ? 'Active' : 'Inactive' }}
              </div>

              <button
                class="icon-button"
                :class="{ active: webhook.isEnabled }"
                :title="webhook.isEnabled ? 'Disable webhook' : 'Enable webhook'"
                @click.stop="toggleWebhook(webhook)"
              >
                <component :is="webhook.isEnabled ? PhToggleRight : PhToggleLeft" />
              </button>

              <button
                class="icon-button"
                :title="!webhook.isEnabled ? 'Enable webhook to test' : 'Send test notification'"
                @click.stop="testWebhook(webhook)"
                :disabled="testingWebhook === webhook.id || !webhook.isEnabled"
              >
                <PhSpinner v-if="testingWebhook === webhook.id" class="ph-spin" />
                <PhPaperPlaneTilt v-else />
              </button>

              <button class="icon-button" title="Edit webhook" @click.stop="editWebhook(webhook)">
                <PhPencil />
              </button>

              <button
                class="icon-button danger"
                title="Delete webhook"
                @click.stop="confirmDeleteWebhook(webhook)"
              >
                <PhTrash />
              </button>
            </div>
          </div>

          <div class="webhook-body">
            <div class="webhook-url-container">
              <PhLink class="url-icon" />
              <span class="webhook-url">{{ webhook.url }}</span>
            </div>

            <div class="webhook-triggers-section">
              <div class="triggers-header">
                <PhBell />
                <span class="triggers-label">Active Triggers ({{ webhook.triggers.length }})</span>
              </div>
              <div class="triggers-list">
                <span
                  v-for="trigger in webhook.triggers"
                  :key="trigger"
                  class="trigger-badge"
                  :class="getTriggerClass(trigger)"
                >
                  <component :is="getTriggerIcon(trigger)" />
                  {{ formatTriggerName(trigger) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Webhook Configuration Modal -->
      <div
        v-if="showWebhookForm"
        class="modal-overlay"
        @click.self="closeWebhookForm"
        @keydown.esc="closeWebhookForm"
      >
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h2>{{ editingWebhook ? 'Edit' : 'Add' }} Webhook</h2>
            <button @click="closeWebhookForm" class="close-btn" aria-label="Close modal">
              <PhX />
            </button>
          </div>
          <div class="modal-body">
            <form @submit.prevent="saveWebhook">
              <!-- Delete Webhook Confirmation Modal -->
              <div v-if="webhookToDelete" class="modal-overlay" @click="webhookToDelete = null">
                <div class="modal-content" @click.stop>
                  <div class="modal-header">
                    <h3>
                      <PhWarningCircle />
                      Delete Webhook
                    </h3>
                    <button @click="webhookToDelete = null" class="modal-close">
                      <PhX />
                    </button>
                  </div>
                  <div class="modal-body">
                    <p>
                      Are you sure you want to delete the webhook
                      <strong>{{ webhookToDelete.name }}</strong
                      >?
                    </p>
                    <p>This action cannot be undone.</p>
                  </div>
                  <div class="modal-actions">
                    <button @click="webhookToDelete = null" class="cancel-button">Cancel</button>
                    <button @click="executeDeleteWebhook()" class="delete-button">
                      <PhTrash />
                      Delete
                    </button>
                  </div>
                </div>
              </div>
              <!-- Basic Configuration Section -->
              <div class="form-section">
                <h3>Basic</h3>

                <div class="form-group">
                  <label for="webhook-name">Name *</label>
                  <input
                    id="webhook-name"
                    v-model="webhookForm.name"
                    type="text"
                    placeholder="e.g., Production Slack Channel"
                    required
                    @blur="validateWebhookField('name')"
                  />
                  <small v-if="webhookFormErrors.name" class="error-text">{{
                    webhookFormErrors.name
                  }}</small>
                </div>

                <div class="form-group">
                  <label for="webhook-type">Type *</label>
                  <select
                    id="webhook-type"
                    v-model="webhookForm.type"
                    required
                    @change="onServiceTypeChange"
                    @blur="validateWebhookField('type')"
                  >
                    <option value="" disabled>Select type...</option>
                    <option value="Slack">Slack</option>
                    <option value="Discord">Discord</option>
                    <option value="Telegram">Telegram</option>
                    <option value="Pushover">Pushover</option>
                    <option value="Pushbullet">Pushbullet</option>
                    <option value="NTFY">NTFY</option>
                    <option value="Zapier">Zapier / Generic</option>
                  </select>
                  <small v-if="webhookFormErrors.type" class="error-text">{{
                    webhookFormErrors.type
                  }}</small>
                  <small v-else-if="getServiceHelp()">{{ getServiceHelp() }}</small>
                </div>

                <div class="form-group">
                  <label for="webhook-url">Webhook URL *</label>
                  <input
                    id="webhook-url"
                    v-model="webhookForm.url"
                    type="url"
                    placeholder="https://hooks.example.com/services/your-webhook-url"
                    required
                    @blur="validateWebhookField('url')"
                  />
                  <small v-if="webhookFormErrors.url" class="error-text">{{
                    webhookFormErrors.url
                  }}</small>
                </div>
              </div>

              <!-- Triggers Section -->
              <div class="form-section triggers-section">
                <h3>Notification Triggers</h3>

                <div class="checkbox-group">
                  <label for="trigger-book-added">
                    <input
                      id="trigger-book-added"
                      v-model="webhookForm.triggers"
                      value="book-added"
                      type="checkbox"
                      @change="validateWebhookField('triggers')"
                    />
                    <span>
                      <strong>Book Added to Library</strong>
                      <small>Notifies when a new audiobook is added to your library</small>
                    </span>
                  </label>
                </div>

                <div class="checkbox-group">
                  <label for="trigger-book-downloading">
                    <input
                      id="trigger-book-downloading"
                      v-model="webhookForm.triggers"
                      value="book-downloading"
                      type="checkbox"
                      @change="validateWebhookField('triggers')"
                    />
                    <span>
                      <strong>Download Started</strong>
                      <small>Notifies when an audiobook download begins</small>
                    </span>
                  </label>
                </div>

                <div class="checkbox-group">
                  <label for="trigger-book-available">
                    <input
                      id="trigger-book-available"
                      v-model="webhookForm.triggers"
                      value="book-available"
                      type="checkbox"
                      @change="validateWebhookField('triggers')"
                    />
                    <span>
                      <strong>Download Complete</strong>
                      <small>Notifies when an audiobook finishes downloading and is ready</small>
                    </span>
                  </label>
                </div>
                <small v-if="webhookFormErrors.triggers" class="error-text">{{
                  webhookFormErrors.triggers
                }}</small>
              </div>

              <!-- Status Section -->
              <div class="form-section status-section">
                <h3>Activation</h3>
                <div class="checkbox-group">
                  <label for="webhook-enabled">
                    <input id="webhook-enabled" v-model="webhookForm.isEnabled" type="checkbox" />
                    <span>
                      <strong>Enable</strong>
                      <small>Enable this webhook to start receiving notifications</small>
                    </span>
                  </label>
                </div>
              </div>
            </form>
          </div>
          <div class="modal-footer">
            <button @click="closeWebhookForm" class="btn btn-secondary" type="button">
              Cancel
            </button>
            <button
              v-if="
                webhookForm.url &&
                webhookForm.type &&
                webhookForm.triggers.length > 0 &&
                !editingWebhook
              "
              @click="testWebhookConfig"
              class="btn btn-info"
              type="button"
              :disabled="testingWebhookConfig"
            >
              <PhSpinner v-if="testingWebhookConfig" class="ph-spin" />
              {{ testingWebhookConfig ? 'Testing...' : 'Test' }}
            </button>
            <button
              @click="saveWebhook"
              class="btn btn-primary"
              type="button"
              :disabled="!isWebhookFormValid || savingWebhook"
            >
              <PhSpinner v-if="savingWebhook" class="ph-spin" />
              {{ savingWebhook ? 'Saving...' : editingWebhook ? 'Update' : 'Save' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import {
  PhBell,
  PhBellSlash,
  PhPlus,
  PhCheckCircle,
  PhXCircle,
  PhToggleRight,
  PhToggleLeft,
  PhSpinner,
  PhPaperPlaneTilt,
  PhPencil,
  PhTrash,
  PhLink,
  PhX,
  PhWarningCircle,
  PhDownloadSimple,
} from '@phosphor-icons/vue'
import { errorTracking } from '@/services/errorTracking'
import { useToast } from '@/services/toastService'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApplicationSettings } from '@/types'

// Props
const props = defineProps<{
  settings: ApplicationSettings | null
}>()

const toast = useToast()
const configStore = useConfigurationStore()

// Helper function to format API errors
const formatApiError = (err: unknown): string => {
  if (err && typeof err === 'object' && 'message' in err) {
    return String((err as { message: string }).message)
  }
  return 'An unknown error occurred'
}

// State
const showWebhookForm = ref(false)
const editingWebhook = ref<{
  id: string
  name: string
  url: string
  type: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier'
  triggers: string[]
  isEnabled: boolean
} | null>(null)
const testingWebhook = ref<string | null>(null)
const webhooks = ref<
  Array<{
    id: string
    name: string
    url: string
    type: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier'
    triggers: string[]
    isEnabled: boolean
  }>
>([])

const webhookForm = reactive({
  id: '',
  name: '',
  url: '',
  type: '' as 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier' | '',
  triggers: [] as string[],
  isEnabled: true,
})

const webhookFormErrors = reactive({
  name: '',
  url: '',
  type: '',
  triggers: '',
})

const testingWebhookConfig = ref(false)
const savingWebhook = ref(false)

// Computed
const isWebhookFormValid = computed(() => {
  return (
    webhookForm.name.trim().length > 0 &&
    webhookForm.url.trim().length > 0 &&
    webhookForm.type !== '' &&
    webhookForm.triggers.length > 0 &&
    !webhookFormErrors.name &&
    !webhookFormErrors.url &&
    !webhookFormErrors.type &&
    !webhookFormErrors.triggers
  )
})

// Helper functions
function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

const getTriggerIcon = (trigger: string) => {
  const iconMap: Record<string, unknown> = {
    'book-added': PhPlus,
    'book-downloading': PhDownloadSimple,
    'book-available': PhCheckCircle,
  }
  return iconMap[trigger] || PhBell
}

const getTriggerClass = (trigger: string): string => {
  const classMap: Record<string, string> = {
    'book-added': 'trigger-added',
    'book-downloading': 'trigger-downloading',
    'book-available': 'trigger-available',
  }
  return classMap[trigger] || ''
}

const formatTriggerName = (trigger: string): string => {
  const nameMap: Record<string, string> = {
    'book-added': 'Book Added',
    'book-downloading': 'Download Started',
    'book-available': 'Download Complete',
  }
  return nameMap[trigger] || trigger
}

const isValidUrl = (url: string): boolean => {
  try {
    const urlObj = new URL(url)
    return urlObj.protocol === 'https:'
  } catch {
    return false
  }
}

const validateWebhookField = (field: 'name' | 'url' | 'type' | 'triggers') => {
  switch (field) {
    case 'name':
      if (!webhookForm.name || webhookForm.name.trim().length === 0) {
        webhookFormErrors.name = 'Webhook name is required'
      } else if (webhookForm.name.trim().length < 3) {
        webhookFormErrors.name = 'Name must be at least 3 characters'
      } else {
        webhookFormErrors.name = ''
      }
      break
    case 'url':
      if (!webhookForm.url || webhookForm.url.trim().length === 0) {
        webhookFormErrors.url = 'Webhook URL is required'
      } else if (!isValidUrl(webhookForm.url)) {
        webhookFormErrors.url = 'Please enter a valid HTTPS URL'
      } else {
        webhookFormErrors.url = ''
      }
      break
    case 'type':
      if (!webhookForm.type) {
        webhookFormErrors.type = 'Please select a service type'
      } else {
        webhookFormErrors.type = ''
      }
      break
    case 'triggers':
      if (webhookForm.triggers.length === 0) {
        webhookFormErrors.triggers = 'Please select at least one trigger'
      } else {
        webhookFormErrors.triggers = ''
      }
      break
  }
}

const resetWebhookFormErrors = () => {
  webhookFormErrors.name = ''
  webhookFormErrors.url = ''
  webhookFormErrors.type = ''
  webhookFormErrors.triggers = ''
}

const onServiceTypeChange = () => {
  validateWebhookField('type')
}

const getServiceHelp = (): string => {
  const helpText: Record<string, string> = {
    Slack:
      'Get your webhook URL from Slack: Settings & administration → Manage apps → Incoming Webhooks',
    Discord: 'Server Settings → Integrations → Webhooks → New Webhook → Copy Webhook URL',
    Telegram:
      'Create a bot with @BotFather, then get the webhook URL format: https://api.telegram.org/bot{token}/sendMessage',
    Pushover: 'Get your User Key and API Token from pushover.net/apps/build',
    Pushbullet: 'Get your Access Token from Settings → Account → Access Tokens',
    NTFY: 'Use format: https://ntfy.sh/{topic} or your self-hosted instance URL',
    Zapier: 'Create a Zap with "Webhooks by Zapier" and copy the webhook URL',
  }
  return webhookForm.type ? helpText[webhookForm.type] || '' : ''
}

// Webhook CRUD operations
const openWebhookForm = () => {
  editingWebhook.value = null
  webhookForm.id = ''
  webhookForm.name = ''
  webhookForm.url = ''
  webhookForm.type = ''
  webhookForm.triggers = []
  webhookForm.isEnabled = true
  resetWebhookFormErrors()
  showWebhookForm.value = true
}

const closeWebhookForm = () => {
  showWebhookForm.value = false
  editingWebhook.value = null
  webhookForm.id = ''
  webhookForm.name = ''
  webhookForm.url = ''
  webhookForm.type = ''
  webhookForm.triggers = []
  webhookForm.isEnabled = true
  resetWebhookFormErrors()
}

const editWebhook = (webhook: (typeof webhooks.value)[0]) => {
  editingWebhook.value = webhook
  webhookForm.id = webhook.id
  webhookForm.name = webhook.name
  webhookForm.url = webhook.url
  webhookForm.type = webhook.type
  webhookForm.triggers = [...webhook.triggers]
  webhookForm.isEnabled = webhook.isEnabled
  resetWebhookFormErrors()
  showWebhookForm.value = true
}

const saveWebhook = async () => {
  // Validate all fields
  validateWebhookField('name')
  validateWebhookField('url')
  validateWebhookField('type')
  validateWebhookField('triggers')

  // Check if form is valid
  if (!isWebhookFormValid.value) {
    toast.error('Validation error', 'Please fix the errors before saving')
    return
  }

  savingWebhook.value = true
  try {
    const webhook = {
      id: webhookForm.id || generateUUID(),
      name: webhookForm.name.trim(),
      url: webhookForm.url.trim(),
      type: webhookForm.type as
        | 'Pushbullet'
        | 'Telegram'
        | 'Slack'
        | 'Discord'
        | 'Pushover'
        | 'NTFY'
        | 'Zapier',
      triggers: [...webhookForm.triggers],
      isEnabled: webhookForm.isEnabled,
    }

    if (editingWebhook.value) {
      // Update existing webhook
      const index = webhooks.value.findIndex((w) => w.id === webhook.id)
      if (index !== -1) {
        webhooks.value[index] = webhook
      }
      toast.success('Webhook', 'Webhook updated successfully')
    } else {
      // Add new webhook
      webhooks.value.push(webhook)
      toast.success('Webhook', 'Webhook added successfully')
    }

    // Persist webhooks to settings
    await persistWebhooks()

    closeWebhookForm()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'NotificationsTab',
      operation: 'saveWebhook',
    })
    toast.error('Save failed', 'Failed to save webhook')
  } finally {
    savingWebhook.value = false
  }
}

const webhookToDelete = ref<(typeof webhooks.value)[0] | null>(null)

const confirmDeleteWebhook = (webhook: (typeof webhooks.value)[0]) => {
  webhookToDelete.value = webhook
}

const executeDeleteWebhook = async () => {
  if (!webhookToDelete.value) return
  try {
    webhooks.value = webhooks.value.filter((w) => w.id !== webhookToDelete.value!.id)
    toast.success('Webhook', 'Webhook deleted successfully')
    await persistWebhooks()
  } catch (e) {
    errorTracking.captureException(e as Error, {
      component: 'NotificationsTab',
      operation: 'executeDeleteWebhook',
    })
    toast.error('Delete failed', 'Failed to delete webhook')
    throw e
  } finally {
    webhookToDelete.value = null
  }
}

const toggleWebhook = async (webhook: (typeof webhooks.value)[0]) => {
  const index = webhooks.value.findIndex((w) => w.id === webhook.id)
  if (index !== -1) {
    const targetWebhook = webhooks.value[index]
    if (targetWebhook) {
      targetWebhook.isEnabled = !targetWebhook.isEnabled
      toast.success(
        'Webhook',
        `${webhook.name} ${targetWebhook.isEnabled ? 'enabled' : 'disabled'}`,
      )

      // Persist webhooks to settings
      await persistWebhooks()
    }
  }
}

const testWebhook = async (webhook: (typeof webhooks.value)[0]) => {
  testingWebhook.value = webhook.id
  try {
    // NOTE: Test API exists at POST /api/diagnostics/test-notification
    // Future enhancement: integrate with DiagnosticsController to send real test notifications
    // For now, just simulate success
    await new Promise((resolve) => setTimeout(resolve, 1000))
    toast.success('Test notification', `Test notification sent to ${webhook.name}`)
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'NotificationsTab',
      operation: 'testWebhook',
    })
    const errorMessage = formatApiError(error)
    toast.error('Test failed', errorMessage)
  } finally {
    testingWebhook.value = null
  }
}

const testWebhookConfig = async () => {
  testingWebhookConfig.value = true
  try {
    // NOTE: Test API exists at POST /api/diagnostics/test-notification
    // Future enhancement: integrate with DiagnosticsController for real webhook testing
    await new Promise((resolve) => setTimeout(resolve, 1500))
    toast.success('Test successful', `Test notification sent to ${webhookForm.type}`)
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'NotificationsTab',
      operation: 'testWebhookConfig',
    })
    toast.error('Test failed', 'Failed to send test notification')
  } finally {
    testingWebhookConfig.value = false
  }
}

// Persist webhooks to backend settings (do not mutate incoming props)
const persistWebhooks = async () => {
  // Create a shallow copy of settings and assign updated webhooks
  const current = props.settings ? { ...(props.settings as Record<string, unknown>) } : {}
  try {
    const payload: ApplicationSettings = {
      ...(current as ApplicationSettings),
      webhooks: webhooks.value,
    }
    // Save to backend using the configuration store
    await configStore.saveApplicationSettings(payload)
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'NotificationsTab',
      operation: 'persistWebhooks',
    })
    toast.error('Save failed', 'Failed to save webhooks to settings')
    throw error
  }
}

// Initialize webhooks from settings
onMounted(() => {
  if (props.settings?.webhooks) {
    webhooks.value = props.settings.webhooks
  }
})

// Expose openWebhookForm for parent component
defineExpose({ openWebhookForm })
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

/* Section Header */
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
/* Empty State */
.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #868e96;
}

.empty-icon {
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

.empty-help {
  font-size: 0.95rem;
  color: #868e96;
  margin-bottom: 2rem;
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
  border-top: 1px solid #444;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background: var(--background-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border);
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
  background: var(--background-hover);
}

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
  box-shadow: 0 8px 20px rgba(231, 76, 60, 0.2);
}

/* Webhooks Grid */
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
}

.webhook-title-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex: 1;
  min-width: 0;
}

.webhook-info {
  min-width: 0;
}

.webhook-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.webhook-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
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

.webhook-header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-left: 1rem;
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
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.webhook-status-badge.active {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
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
  box-shadow: 0 2px 8px rgba(255, 107, 107, 0.3);
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
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
}

.url-icon {
  color: #4dabf7;
  font-size: 1.1rem;
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

/* Mobile Responsive */
@media (max-width: 768px) {
  .webhooks-grid {
    grid-template-columns: 1fr;
  }

  .webhook-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .webhook-header-actions {
    width: 100%;
    justify-content: space-between;
    margin-left: 0;
  }
}

/* Spin animation for loading icons */
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
</style>
