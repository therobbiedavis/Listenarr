<template>
  <div class="tab-content">
    <div class="download-clients-tab">
      <div class="section-header">
        <h3>Download Clients</h3>
      </div>

      <div v-if="configStore.downloadClientConfigurations.length === 0" class="empty-state">
        <PhDownloadSimple />
        <p>
          No download clients configured. Add qBittorrent, Transmission, SABnzbd, or NZBGet to
          download audiobooks.
        </p>
      </div>

      <div v-else class="indexers-grid">
        <div
          v-for="client in configStore.downloadClientConfigurations"
          :key="client.id"
          class="indexer-card"
          :class="{ disabled: !client.isEnabled }"
        >
          <div class="indexer-header">
            <div class="indexer-info">
              <h4>{{ client.name }}</h4>
              <span class="indexer-type" :class="getClientTypeClass(client.type)">
                {{ client.type }}
              </span>
            </div>
            <div class="indexer-actions">
              <button
                @click="toggleDownloadClientFunc(client)"
                class="icon-button"
                :title="client.isEnabled ? 'Disable' : 'Enable'"
              >
                <template v-if="client.isEnabled">
                  <PhToggleRight />
                </template>
                <template v-else>
                  <PhToggleLeft />
                </template>
              </button>
              <button @click="editClientConfig(client)" class="icon-button" title="Edit">
                <PhPencil />
              </button>
              <button
                @click="testClient(client)"
                class="icon-button"
                title="Test"
                :disabled="testingClient === client.id"
              >
                <template v-if="testingClient === client.id">
                  <PhSpinner class="ph-spin" />
                </template>
                <template v-else>
                  <PhCheckCircle />
                </template>
              </button>
              <button
                @click="confirmDeleteClient(client)"
                class="icon-button danger"
                title="Delete"
              >
                <PhTrash />
              </button>
            </div>
          </div>

          <div class="indexer-details">
            <div class="detail-row">
              <PhLink />
              <span class="detail-label">Host:</span>
              <span class="detail-value">{{ client.host }}:{{ client.port }}</span>
            </div>
            <div class="detail-row">
              <PhShieldCheck />
              <span class="detail-label">Security:</span>
              <div class="feature-badges">
                <span class="badge" v-if="client.useSSL"> <PhLock /> SSL </span>
                <span class="badge" v-else> <PhLockOpen /> No SSL </span>
              </div>
            </div>
            <div class="detail-row">
              <PhFolder />
              <span class="detail-label">Download Path:</span>
              <span class="detail-value">{{ client.downloadPath || '(client local)' }}</span>
            </div>
            <div class="detail-row">
              <PhLinkSimple />
              <span class="detail-label">Mappings:</span>
              <div class="feature-badges">
                <span v-for="m in getMappingsForClient(client)" :key="m.id" class="badge">
                  <PhLink />
                  {{ m.name || m.remotePath }}
                </span>
                <span v-if="getMappingsForClient(client).length === 0" class="detail-value"
                  >(none)</span
                >
              </div>
            </div>
            <div class="detail-row">
              <PhCheckCircle />
              <span class="detail-label">Status:</span>
              <span
                class="detail-value"
                :class="{ success: client.isEnabled, error: !client.isEnabled }"
              >
                {{ client.isEnabled ? 'Enabled' : 'Disabled' }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Remote Path Mappings Section -->
      <div class="section-header" style="margin-top: 2rem">
        <h3>Remote Path Mappings</h3>
      </div>

      <div v-if="remotePathMappings.length === 0" class="empty-state">
        <PhLinkSimple />
        <p>
          No remote path mappings configured. Add a mapping to translate client remote paths to
          local paths the server can access.
        </p>
      </div>

      <div v-else class="config-list">
        <div v-for="mapping in remotePathMappings" :key="mapping.id" class="config-card">
          <div class="config-info">
            <h4>{{ mapping.name || mapping.remotePath }}</h4>
            <div class="detail-row">
              <PhBrowser />
              <span class="detail-label">Remote Path:</span>
              <span class="detail-value">{{ mapping.remotePath }}</span>
            </div>
            <div class="detail-row">
              <PhFolder />
              <span class="detail-label">Local Path:</span>
              <span class="detail-value">{{ mapping.localPath }}</span>
            </div>
          </div>
          <div class="config-actions">
            <button @click="editMapping(mapping)" class="edit-button" title="Edit">
              <PhPencil />
            </button>
            <button @click="confirmDeleteMapping(mapping)" class="delete-button" title="Delete">
              <PhTrash />
            </button>
          </div>
        </div>
      </div>

      <!-- Download Client Form Modal -->
      <DownloadClientFormModal
        :visible="showClientForm"
        :editing-client="editingClient"
        @close="showClientForm = false; editingClient = null"
        @saved="configStore.loadDownloadClientConfigurations()"
        @delete="executeDeleteClient"
      />

      <!-- Delete Client Confirmation Modal -->
      <div v-if="clientToDelete" class="modal-overlay" @click="clientToDelete = null">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>
              <PhWarningCircle />
              Delete Download Client
            </h3>
            <button @click="clientToDelete = null" class="modal-close">
              <PhX />
            </button>
          </div>
          <div class="modal-body">
            <p>
              Are you sure you want to delete the download client
              <strong>{{ clientToDelete.name }}</strong
              >?
            </p>
            <p>This action cannot be undone.</p>
          </div>
          <div class="modal-actions">
            <button @click="clientToDelete = null" class="cancel-button">Cancel</button>
            <button @click="executeDeleteClient()" class="delete-button">
              <PhTrash />
              Delete
            </button>
          </div>
        </div>
      </div>

      <!-- Remote Path Mapping Modal -->
      <div v-if="showMappingForm" class="modal-overlay" @click="closeMappingForm()">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>{{ mappingToEdit ? 'Edit' : 'Add' }} Remote Path Mapping</h3>
            <button @click="closeMappingForm()" class="modal-close"><PhX /></button>
          </div>
          <div class="modal-body">
            <div class="form-group">
              <label>Mapping Name (optional)</label>
              <input
                v-model="mappingToEditData.name"
                type="text"
                placeholder="Friendly name for this mapping"
              />
            </div>
            <div class="form-group">
              <label>Download Client</label>
              <select v-model="mappingToEditData.downloadClientId">
                <option
                  v-for="c in configStore.downloadClientConfigurations"
                  :key="c.id"
                  :value="c.id"
                >
                  {{ c.name }} ({{ c.type }})
                </option>
              </select>
            </div>
            <div class="form-group">
              <label>Remote Path (from client)</label>
              <input
                v-model="mappingToEditData.remotePath"
                type="text"
                placeholder="/path/to/complete/downloads"
              />
            </div>
            <div class="form-group">
              <label>Local Path (server)</label>
              <FolderBrowser
                v-model="mappingToEditData.localPath"
                placeholder="Select a local path..."
              />
            </div>
          </div>
          <div class="modal-actions">
            <button @click="closeMappingForm()" class="cancel-button">Cancel</button>
            <button @click="saveMapping()" class="save-button"><PhCheck /> Save</button>
          </div>
        </div>
      </div>

      <!-- Delete Remote Path Mapping Confirmation Modal -->
      <div v-if="mappingToDelete" class="modal-overlay" @click="mappingToDelete = null">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>
              <PhWarningCircle />
              Delete Remote Path Mapping
            </h3>
            <button @click="mappingToDelete = null" class="modal-close">
              <PhX />
            </button>
          </div>
          <div class="modal-body">
            <p>
              Are you sure you want to delete the remote path mapping
              <strong>{{ mappingToDelete.name || mappingToDelete.remotePath }}</strong
              >?
            </p>
            <p>This action cannot be undone.</p>
          </div>
          <div class="modal-actions">
            <button @click="mappingToDelete = null" class="cancel-button">Cancel</button>
            <button @click="executeDeleteMapping()" class="delete-button">
              <PhTrash />
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useConfigurationStore } from '@/stores/configuration'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import type { DownloadClientConfiguration, RemotePathMapping } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'
import {
  PhDownloadSimple,
  PhToggleRight,
  PhToggleLeft,
  PhSpinner,
  PhCheckCircle,
  PhPencil,
  PhTrash,
  PhLink,
  PhShieldCheck,
  PhLock,
  PhLockOpen,
  PhFolder,
  PhLinkSimple,
  PhBrowser,
  PhWarningCircle,
  PhX,
  PhCheck,
} from '@phosphor-icons/vue'
import {
  getRemotePathMappings,
  createRemotePathMapping,
  updateRemotePathMapping,
  deleteRemotePathMapping,
  testDownloadClient as apiTestDownloadClient,
} from '@/services/api'

// State
const configStore = useConfigurationStore()
const toast = useToast()
const showClientForm = ref(false)
const editingClient = ref<DownloadClientConfiguration | null>(null)
const clientToDelete = ref<DownloadClientConfiguration | null>(null)
const testingClient = ref<string | null>(null)
const remotePathMappings = ref<RemotePathMapping[]>([])
const showMappingForm = ref(false)
const mappingToEdit = ref<RemotePathMapping | null>(null)
const mappingToDelete = ref<RemotePathMapping | null>(null)
const mappingToEditData = ref<{
  downloadClientId: string
  remotePath: string
  localPath: string
  name?: string
}>({
  downloadClientId: '',
  remotePath: '',
  localPath: '',
  name: '',
})

// Functions
const formatApiError = (error: unknown): string => {
  const e = error as { response?: { data?: unknown }; message?: unknown } | undefined
  const data = e?.response?.data
  if (data) {
    if (typeof data === 'string') return data
    if (typeof data === 'object' && data !== null) {
      const obj = data as Record<string, unknown>
      if (typeof obj.message === 'string') return obj.message
      if (typeof obj.error === 'string') return obj.error
      if (typeof obj.title === 'string') return obj.title
    }
  }
  if (typeof e?.message === 'string') return e!.message as string
  return 'An unknown error occurred'
}

const getClientTypeClass = (type: string): string => {
  const typeMap: Record<string, string> = {
    qbittorrent: 'torrent',
    transmission: 'torrent',
    sabnzbd: 'usenet',
    nzbget: 'usenet',
  }
  return typeMap[type.toLowerCase()] || 'torrent'
}

const getMappingsForClient = (client: DownloadClientConfiguration): RemotePathMapping[] => {
  const assignedIds = new Set<number>()
  try {
    const s = (client as unknown as Record<string, unknown>)?.settings as
      | Record<string, unknown>
      | undefined
    const raw = s?.remotePathMappingIds ?? s?.RemotePathMappingIds
    if (Array.isArray(raw)) {
      for (const v of raw) {
        const n = Number(v)
        if (!Number.isNaN(n)) assignedIds.add(n)
      }
    }
  } catch {
    // ignore malformed settings
  }

  return remotePathMappings.value.filter((m) => assignedIds.has(m.id))
}

const toggleDownloadClientFunc = async (client: DownloadClientConfiguration) => {
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
      component: 'DownloadClientsTab',
      operation: 'toggleDownloadClient',
    })
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

const testClient = async (client: DownloadClientConfiguration) => {
  testingClient.value = client.id
  try {
    const result = await apiTestDownloadClient(client)
    if (result.success) {
      toast.success(
        'Download client test',
        `Download client tested successfully: ${result.message}`,
      )
      const index = configStore.downloadClientConfigurations.findIndex((c) => c.id === client.id)
      if (index !== -1 && result.client) {
        configStore.downloadClientConfigurations[index] = result.client
      }
    } else {
      const errorMessage = formatApiError({ response: { data: result.message } })
      toast.error('Download client test failed', errorMessage)
      const index = configStore.downloadClientConfigurations.findIndex((c) => c.id === client.id)
      if (index !== -1 && result.client) {
        configStore.downloadClientConfigurations[index] = result.client
      }
    }
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'DownloadClientsTab',
      operation: 'testDownloadClient',
    })
    const errorMessage = formatApiError(error)
    toast.error('Download client test failed', errorMessage)
  } finally {
    testingClient.value = null
  }
}

const editClientConfig = (client: DownloadClientConfiguration) => {
  editingClient.value = client
  showClientForm.value = true
}

const confirmDeleteClient = (client: DownloadClientConfiguration) => {
  clientToDelete.value = client
}

const executeDeleteClient = async (id?: string) => {
  const clientId = id || clientToDelete.value?.id
  if (!clientId) return

  try {
    await configStore.deleteDownloadClientConfiguration(clientId)
    toast.success('Download client', 'Download client deleted successfully')
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'DownloadClientsTab',
      operation: 'deleteDownloadClient',
    })
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    clientToDelete.value = null
  }
}

// Remote Path Mappings functions
const loadRemotePathMappings = async () => {
  try {
    remotePathMappings.value = await getRemotePathMappings()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'DownloadClientsTab',
      operation: 'loadRemotePathMappings',
    })
  }
}

const openMappingForm = (mapping?: RemotePathMapping) => {
  mappingToEdit.value = mapping || null
  if (mapping) {
    mappingToEditData.value = { ...mapping }
  } else {
    mappingToEditData.value = {
      downloadClientId: configStore.downloadClientConfigurations[0]?.id || '',
      remotePath: '',
      localPath: '',
      name: '',
    }
  }
  showMappingForm.value = true
}

const closeMappingForm = () => {
  showMappingForm.value = false
  mappingToEdit.value = null
  mappingToEditData.value = { downloadClientId: '', remotePath: '', localPath: '', name: '' }
}

const saveMapping = async () => {
  try {
    const payload: Omit<RemotePathMapping, 'id' | 'createdAt' | 'updatedAt'> = {
      downloadClientId: mappingToEditData.value.downloadClientId || '',
      remotePath: mappingToEditData.value.remotePath || '',
      localPath: mappingToEditData.value.localPath || '',
      name: mappingToEditData.value.name || '',
    }

    if (mappingToEdit.value && mappingToEdit.value.id) {
      const updated = await updateRemotePathMapping(mappingToEdit.value.id, payload)
      const idx = remotePathMappings.value.findIndex((m) => m.id === updated.id)
      if (idx !== -1) remotePathMappings.value[idx] = updated
      toast.success('Remote path mapping', 'Remote path mapping updated')
    } else {
      const created = await createRemotePathMapping(payload)
      remotePathMappings.value.push(created)

      // Automatically assign the new mapping to the selected download client
      if (payload.downloadClientId) {
        const selectedClient = configStore.downloadClientConfigurations.find(
          (c) => c.id === payload.downloadClientId,
        )
        if (selectedClient) {
          const updatedClient = { ...selectedClient }
          if (!updatedClient.settings.remotePathMappingIds) {
            updatedClient.settings.remotePathMappingIds = []
          }
          if (!updatedClient.settings.remotePathMappingIds.includes(created.id)) {
            updatedClient.settings.remotePathMappingIds.push(created.id)
            const clientIndex = configStore.downloadClientConfigurations.findIndex(
              (c) => c.id === payload.downloadClientId,
            )
            if (clientIndex !== -1) {
              configStore.downloadClientConfigurations[clientIndex] = updatedClient
            }
            configStore.saveDownloadClientConfiguration(updatedClient).catch((err) => {
              errorTracking.captureException(err as Error, {
                component: 'DownloadClientsTab',
                operation: 'saveClientConfig',
              })
              configStore.loadDownloadClientConfigurations()
            })
          }
        }
      }

      toast.success('Remote path mapping', 'Remote path mapping created')
    }

    closeMappingForm()
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DownloadClientsTab',
      operation: 'saveMapping',
    })
    toast.error('Save failed', 'Failed to save mapping')
  }
}

const editMapping = (mapping: RemotePathMapping) => openMappingForm(mapping)

const confirmDeleteMapping = (mapping: RemotePathMapping) => {
  mappingToDelete.value = mapping
}

const executeDeleteMapping = async (id?: number) => {
  const mappingId = id || mappingToDelete.value?.id
  if (!mappingId) return

  try {
    await deleteRemotePathMapping(mappingId)
    remotePathMappings.value = remotePathMappings.value.filter((m) => m.id !== mappingId)
    toast.success('Remote path mapping', 'Remote path mapping deleted')
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'DownloadClientsTab',
      operation: 'deleteMapping',
    })
    toast.error('Delete failed', 'Failed to delete mapping')
  } finally {
    mappingToDelete.value = null
  }
}

// Lifecycle
onMounted(async () => {
  await Promise.all([configStore.loadDownloadClientConfigurations(), loadRemotePathMappings()])
})

// Expose methods for parent component to open add client form and add mapping
defineExpose({
  openAddClient: () => {
    editingClient.value = null
    showClientForm.value = true
  },
  openAddMapping: () => {
    openMappingForm()
  },
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

.download-clients-tab {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
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

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  text-align: center;
  color: var(--color-text-secondary);
  background: var(--color-background-secondary);
  border-radius: 8px;
  border: 2px dashed var(--color-border);
}

.empty-state svg {
  width: 48px;
  height: 48px;
  margin-bottom: 1rem;
  opacity: 0.5;
}

.indexers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
  gap: 1rem;
}

.indexer-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
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
  font-size: 1.1rem;
  color: #fff;
  font-weight: 600;
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

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1.5rem;
}

.indexer-type.torrent {
  background: rgba(34, 197, 94, 0.1);
  color: #22c55e;
}

.indexer-type.usenet {
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
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
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
}

.detail-row svg {
  width: 16px;
  height: 16px;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.detail-label {
  font-weight: 500;
  color: var(--color-text-secondary);
  min-width: 100px;
}

.detail-value {
  color: var(--color-text);
}

.detail-value.success {
  color: #22c55e;
}

.detail-value.error {
  color: #ef4444;
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.5rem;
  background: var(--color-background-tertiary);
  border-radius: 4px;
  font-size: 0.75rem;
}

.badge svg {
  width: 12px;
  height: 12px;
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
  padding: 1rem;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  transition: all 0.2s ease;
}

.config-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.12);
}

.config-info {
  flex: 1;
}

.config-info h4 {
  margin: 0 0 0.75rem 0;
  font-size: 1rem;
  font-weight: 600;
}

.config-actions {
  display: flex;
  gap: 0.5rem;
}

.edit-button,
.delete-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  padding: 0;
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.edit-button:hover {
  background: var(--color-background-tertiary);
  border-color: var(--color-primary);
}

.delete-button:hover {
  background: rgba(239, 68, 68, 0.1);
  border-color: #ef4444;
  color: #ef4444;
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

/* Ensure modal context delete buttons are full-size */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button,
.modal-overlay .modal-content .modal-actions .modal-delete-button,
.modal-content .modal-actions .modal-delete-button {
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

.modal-close {
  background: transparent;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  color: var(--color-text-secondary);
  transition: all 0.2s;
}

.modal-close:hover {
  background: var(--color-background-tertiary);
  color: var(--color-text);
}

.modal-body {
  padding: 1.5rem;
}

.modal-body p {
  margin: 0 0 1rem 0;
  line-height: 1.6;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.form-group input,
.form-group select {
  width: 100%;
  padding: 0.5rem;
  background: var(--color-background-secondary);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  font-size: 0.875rem;
  color: var(--color-text);
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: var(--color-primary);
}

.modal-actions {
  display: flex;
  gap: 0.75rem;
  padding: 1.5rem;
  border-top: 1px solid var(--color-border);
  justify-content: flex-end;
}

.cancel-button,
.save-button,
.delete-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.cancel-button {
  background: var(--color-background-secondary);
  color: var(--color-text);
  border: 1px solid var(--color-border);
}

.cancel-button:hover {
  background: var(--color-background-tertiary);
}

.save-button {
  background: var(--color-primary);
  color: white;
}

.save-button:hover {
  background: var(--color-primary-dark);
}

.delete-button {
  background: #ef4444;
  color: white;
}

.delete-button:hover {
  background: #dc2626;
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
</style>
