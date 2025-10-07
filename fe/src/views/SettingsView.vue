<template>
  <div class="settings-page">
    <div class="settings-header">
      <h1>
        <i class="ph ph-gear"></i>
        Settings
      </h1>
      <p>Configure your APIs, download clients, and application settings</p>
    </div>

    <div class="settings-tabs">
      <button 
        @click="router.push({ hash: '#indexers' })" 
        :class="{ active: activeTab === 'indexers' }"
        class="tab-button"
      >
        <i class="ph ph-list-magnifying-glass"></i>
        Indexers
      </button>
      <button 
        @click="router.push({ hash: '#apis' })" 
        :class="{ active: activeTab === 'apis' }"
        class="tab-button"
      >
        <i class="ph ph-cloud"></i>
        API Sources
      </button>
      <button 
        @click="router.push({ hash: '#clients' })" 
        :class="{ active: activeTab === 'clients' }"
        class="tab-button"
      >
        <i class="ph ph-download"></i>
        Download Clients
      </button>
      <button 
        @click="router.push({ hash: '#general' })" 
        :class="{ active: activeTab === 'general' }"
        class="tab-button"
      >
        <i class="ph ph-sliders"></i>
        General Settings
      </button>
    </div>

    <div class="settings-content">
      <!-- Indexers Tab -->
      <div v-if="activeTab === 'indexers'" class="tab-content">
        <div class="section-header">
          <h3>Indexers</h3>
          <button @click="showIndexerForm = true" class="add-button">
            <i class="ph ph-plus"></i>
            Add Indexer
          </button>
        </div>

        <div v-if="indexers.length === 0" class="empty-state">
          <i class="ph ph-list-magnifying-glass"></i>
          <p>No indexers configured. Add Newznab or Torznab indexers to search for audiobooks.</p>
        </div>

        <div v-else class="indexers-grid">
          <div 
            v-for="indexer in indexers" 
            :key="indexer.id"
            class="indexer-card"
            :class="{ disabled: !indexer.isEnabled }"
          >
            <div class="indexer-header">
              <div class="indexer-info">
                <h4>{{ indexer.name }}</h4>
                <span class="indexer-type" :class="indexer.type.toLowerCase()">
                  {{ indexer.implementation === 'InternetArchive' ? 'DDL' : indexer.type }}
                </span>
              </div>
              <div class="indexer-actions">
                <button 
                  @click="toggleIndexerFunc(indexer.id)" 
                  class="icon-button"
                  :title="indexer.isEnabled ? 'Disable' : 'Enable'"
                >
                  <i :class="indexer.isEnabled ? 'ph ph-toggle-right' : 'ph ph-toggle-left'"></i>
                </button>
                <button 
                  @click="testIndexerFunc(indexer.id)" 
                  class="icon-button"
                  title="Test"
                  :disabled="testingIndexer === indexer.id"
                >
                  <i v-if="testingIndexer === indexer.id" class="ph ph-spinner ph-spin"></i>
                  <i v-else class="ph ph-check-circle"></i>
                </button>
                <button 
                  @click="editIndexer(indexer)" 
                  class="icon-button"
                  title="Edit"
                >
                  <i class="ph ph-pencil"></i>
                </button>
                <button 
                  @click="confirmDeleteIndexer(indexer)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <i class="ph ph-trash"></i>
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <i class="ph ph-link"></i>
                <span class="detail-label">URL:</span>
                <span class="detail-value">{{ indexer.url }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-list-checks"></i>
                <span class="detail-label">Features:</span>
                <div class="feature-badges">
                  <span v-if="indexer.enableRss" class="badge">RSS</span>
                  <span v-if="indexer.enableAutomaticSearch" class="badge">Automatic Search</span>
                  <span v-if="indexer.enableInteractiveSearch" class="badge">Interactive Search</span>
                </div>
              </div>
              <div class="detail-row" v-if="indexer.lastTestedAt">
                <i class="ph ph-clock"></i>
                <span class="detail-label">Last Tested:</span>
                <span class="detail-value" :class="{ success: indexer.lastTestSuccessful, error: indexer.lastTestSuccessful === false }">
                  {{ formatDate(indexer.lastTestedAt) }}
                  <i v-if="indexer.lastTestSuccessful" class="ph ph-check-circle success"></i>
                  <i v-else-if="indexer.lastTestSuccessful === false" class="ph ph-x-circle error"></i>
                </span>
              </div>
              <div class="detail-row error-row" v-if="indexer.lastTestError">
                <i class="ph ph-warning"></i>
                <span class="detail-value error">{{ indexer.lastTestError }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- API Sources Tab -->
      <div v-if="activeTab === 'apis'" class="tab-content">
        <div class="section-header">
          <h3>API Sources</h3>
          <button @click="showApiForm = true" class="add-button">
            <i class="ph ph-plus"></i>
            Add API Source
          </button>
        </div>

        <div v-if="configStore.apiConfigurations.length === 0" class="empty-state">
          <i class="ph ph-cloud-slash"></i>
          <p>No API sources configured. Add one to start searching for media.</p>
        </div>

        <div v-else class="config-list">
          <div 
            v-for="api in configStore.apiConfigurations" 
            :key="api.id"
            class="config-card"
          >
            <div class="config-info">
              <h4>{{ api.name }}</h4>
              <p class="config-url">{{ api.baseUrl }}</p>
              <div class="config-meta">
                <span class="config-type">{{ api.type.toUpperCase() }}</span>
                <span class="config-status" :class="{ enabled: api.isEnabled }">
                  <i :class="api.isEnabled ? 'ph ph-check-circle' : 'ph ph-x-circle'"></i>
                  {{ api.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
                <span class="config-priority">
                  <i class="ph ph-arrow-up"></i>
                  Priority: {{ api.priority }}
                </span>
              </div>
            </div>
            <div class="config-actions">
              <button @click="editApiConfig(api)" class="edit-button" title="Edit">
                <i class="ph ph-pencil"></i>
              </button>
              <button @click="deleteApiConfig(api.id)" class="delete-button" title="Delete">
                <i class="ph ph-trash"></i>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Download Clients Tab -->
      <div v-if="activeTab === 'clients'" class="tab-content">
        <div class="section-header">
          <h3>Download Clients</h3>
          <button @click="showClientForm = true; editingClient = null" class="add-button">
            <i class="ph ph-plus"></i>
            Add Download Client
          </button>
        </div>

        <div v-if="configStore.downloadClientConfigurations.length === 0" class="empty-state">
          <i class="ph ph-download-simple"></i>
          <p>No download clients configured. Add qBittorrent, Transmission, SABnzbd, or NZBGet to download audiobooks.</p>
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
                  @click="editClientConfig(client)" 
                  class="icon-button"
                  title="Edit"
                >
                  <i class="ph ph-pencil"></i>
                </button>
                <button 
                  @click="confirmDeleteClient(client)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <i class="ph ph-trash"></i>
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <i class="ph ph-link"></i>
                <span class="detail-label">Host:</span>
                <span class="detail-value">{{ client.host }}:{{ client.port }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-shield-check"></i>
                <span class="detail-label">Security:</span>
                <div class="feature-badges">
                  <span class="badge" v-if="client.useSSL">
                    <i class="ph ph-lock"></i> SSL
                  </span>
                  <span class="badge" v-else>
                    <i class="ph ph-lock-open"></i> No SSL
                  </span>
                </div>
              </div>
              <div class="detail-row">
                <i class="ph ph-folder"></i>
                <span class="detail-label">Download Path:</span>
                <span class="detail-value">{{ client.downloadPath }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-check-circle"></i>
                <span class="detail-label">Status:</span>
                <span class="detail-value" :class="{ success: client.isEnabled, error: !client.isEnabled }">
                  {{ client.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- General Settings Tab -->
      <div v-if="activeTab === 'general'" class="tab-content">
        <div class="section-header">
          <h3>General Settings</h3>
          <button @click="saveSettings" :disabled="configStore.isLoading" class="save-button">
            <i v-if="configStore.isLoading" class="ph ph-spinner ph-spin"></i>
            <i v-else class="ph ph-floppy-disk"></i>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>

        <div v-if="settings" class="settings-form">
          <div class="form-section">
            <h4><i class="ph ph-folder"></i> File Management</h4>
            
            <div class="form-group">
              <label>Root Folder / Output Path</label>
              <FolderBrowser 
                v-model="settings.outputPath" 
                placeholder="Select a folder for audiobooks..."
              />
              <span class="form-help">Root folder where downloaded audiobooks will be saved. This must be set before adding audiobooks.</span>
            </div>

            <div class="form-group">
              <label>File Naming Pattern</label>
              <input v-model="settings.fileNamingPattern" type="text" placeholder="{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}">
              <span class="form-help">
                Pattern for organizing audiobook files. Available variables:<br>
                <code>{Author}</code> - Author/narrator name<br>
                <code>{Series}</code> - Series name<br>
                <code>{Title}</code> - Book title<br>
                <code>{SeriesNumber}</code> - Position in series<br>
                <code>{DiskNumber}</code> or <code>{DiskNumber:00}</code> - Disk/part number (00 = zero-padded)<br>
                <code>{ChapterNumber}</code> or <code>{ChapterNumber:00}</code> - Chapter number (00 = zero-padded)<br>
                <code>{Year}</code> - Publication year<br>
                <code>{Quality}</code> - Audio quality
              </span>
            </div>
          </div>

          <div class="form-section">
            <h4><i class="ph ph-link"></i> API Configuration</h4>
            
            <div class="form-group">
              <label>Audnexus API URL</label>
              <input v-model="settings.audnexusApiUrl" type="text" placeholder="https://api.audnex.us">
              <span class="form-help">API endpoint for audiobook metadata</span>
            </div>
          </div>

          <div class="form-section">
            <h4><i class="ph ph-download"></i> Download Settings</h4>
            
            <div class="form-group">
              <label>Max Concurrent Downloads</label>
              <input v-model.number="settings.maxConcurrentDownloads" type="number" min="1" max="10">
              <span class="form-help">Maximum number of simultaneous downloads (1-10)</span>
            </div>

            <div class="form-group">
              <label>Polling Interval (seconds)</label>
              <input v-model.number="settings.pollingIntervalSeconds" type="number" min="10" max="300">
              <span class="form-help">How often to check download status (10-300 seconds)</span>
            </div>
          </div>

          <div class="form-section">
            <h4><i class="ph ph-toggle-left"></i> Features</h4>
            
            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableMetadataProcessing" type="checkbox">
                <span>
                  <strong>Enable Metadata Processing</strong>
                  <small>Automatically fetch and embed audiobook metadata</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableCoverArtDownload" type="checkbox">
                <span>
                  <strong>Enable Cover Art Download</strong>
                  <small>Download and embed cover art for audiobooks</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableNotifications" type="checkbox">
                <span>
                  <strong>Enable Notifications</strong>
                  <small>Receive notifications for downloads and events</small>
                </span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- API Configuration Modal (placeholder) -->
    <div v-if="showApiForm" class="modal-overlay" @click="showApiForm = false">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ editingApi ? 'Edit' : 'Add' }} API Source</h3>
          <button @click="showApiForm = false" class="modal-close">
            <i class="ph ph-x"></i>
          </button>
        </div>
        <div class="modal-body">
          <p>API configuration form would go here...</p>
        </div>
        <div class="modal-actions">
          <button @click="showApiForm = false" class="cancel-button">
            <i class="ph ph-x"></i>
            Cancel
          </button>
          <button @click="saveApiConfig" class="save-button">
            <i class="ph ph-check"></i>
            Save
          </button>
        </div>
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

  <!-- Indexer Form Modal -->
  <IndexerFormModal 
    :visible="showIndexerForm" 
    :editing-indexer="editingIndexer"
    @close="showIndexerForm = false; editingIndexer = null"
    @saved="loadIndexers()"
  />

  <!-- Delete Client Confirmation Modal -->
  <div v-if="clientToDelete" class="modal-overlay" @click="clientToDelete = null">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>
          <i class="ph ph-warning-circle"></i>
          Delete Download Client
        </h3>
        <button @click="clientToDelete = null" class="modal-close">
          <i class="ph ph-x"></i>
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the download client <strong>{{ clientToDelete.name }}</strong>?</p>
        <p>This action cannot be undone.</p>
      </div>
      <div class="modal-actions">
        <button @click="clientToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button @click="executeDeleteClient()" class="delete-button">
          <i class="ph ph-trash"></i>
          Delete
        </button>
      </div>
    </div>
  </div>

  <!-- Delete Indexer Confirmation Modal -->
  <div v-if="indexerToDelete" class="modal-overlay" @click="indexerToDelete = null">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>
          <i class="ph ph-warning-circle"></i>
          Delete Indexer
        </h3>
        <button @click="indexerToDelete = null" class="modal-close">
          <i class="ph ph-x"></i>
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the indexer <strong>{{ indexerToDelete.name }}</strong>?</p>
        <p>This action cannot be undone.</p>
      </div>
      <div class="modal-actions">
        <button @click="indexerToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button @click="executeDeleteIndexer" class="delete-button">
          <i class="ph ph-trash"></i>
          Delete
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings, Indexer } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'
import IndexerFormModal from '@/components/IndexerFormModal.vue'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'
import { useNotification } from '@/composables/useNotification'
import { getIndexers, deleteIndexer, toggleIndexer as apiToggleIndexer, testIndexer as apiTestIndexer } from '@/services/api'

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const { success, error: showError, info } = useNotification()
const activeTab = ref<'indexers' | 'apis' | 'clients' | 'general'>('indexers')
const showApiForm = ref(false)
const showClientForm = ref(false)
const showIndexerForm = ref(false)
const editingApi = ref<ApiConfiguration | null>(null)
const editingClient = ref<DownloadClientConfiguration | null>(null)
const editingIndexer = ref<Indexer | null>(null)
const settings = ref<ApplicationSettings | null>(null)
const indexers = ref<Indexer[]>([])
const testingIndexer = ref<number | null>(null)
const indexerToDelete = ref<Indexer | null>(null)

const editApiConfig = (api: ApiConfiguration) => {
  editingApi.value = api
  showApiForm.value = true
}

const editClientConfig = (client: DownloadClientConfiguration) => {
  editingClient.value = client
  showClientForm.value = true
}

const deleteApiConfig = async (id: string) => {
  if (confirm('Are you sure you want to delete this API configuration?')) {
    try {
      await configStore.deleteApiConfiguration(id)
      success('API configuration deleted successfully')
    } catch (error) {
      console.error('Failed to delete API configuration:', error)
      showError('Failed to delete API configuration')
    }
  }
}

const clientToDelete = ref<DownloadClientConfiguration | null>(null)

const confirmDeleteClient = (client: DownloadClientConfiguration) => {
  clientToDelete.value = client
}

const executeDeleteClient = async (id?: string) => {
  const clientId = id || clientToDelete.value?.id
  if (!clientId) return
  
  try {
    await configStore.deleteDownloadClientConfiguration(clientId)
    success('Download client deleted successfully')
  } catch (error) {
    console.error('Failed to delete download client:', error)
    showError('Failed to delete download client')
  } finally {
    clientToDelete.value = null
  }
}

const getClientTypeClass = (type: string): string => {
  const typeMap: Record<string, string> = {
    'qbittorrent': 'torrent',
    'transmission': 'torrent',
    'sabnzbd': 'usenet',
    'nzbget': 'usenet'
  }
  return typeMap[type.toLowerCase()] || 'torrent'
}

const saveApiConfig = () => {
  // Placeholder for API config save
  info('API configuration form would be implemented here')
  showApiForm.value = false
  editingApi.value = null
}

const saveSettings = async () => {
  if (!settings.value) return
  
  try {
    await configStore.saveApplicationSettings(settings.value)
    success('Settings saved successfully')
  } catch (error) {
    console.error('Failed to save settings:', error)
    showError('Failed to save settings')
  }
}

// Indexer functions
const loadIndexers = async () => {
  try {
    indexers.value = await getIndexers()
  } catch (error) {
    console.error('Failed to load indexers:', error)
    showError('Failed to load indexers')
  }
}

const toggleIndexerFunc = async (id: number) => {
  try {
    const updatedIndexer = await apiToggleIndexer(id)
    const index = indexers.value.findIndex(i => i.id === id)
    if (index !== -1) {
      indexers.value[index] = updatedIndexer
    }
    success(`Indexer ${updatedIndexer.isEnabled ? 'enabled' : 'disabled'} successfully`)
  } catch (error) {
    console.error('Failed to toggle indexer:', error)
    showError('Failed to toggle indexer')
  }
}

const testIndexerFunc = async (id: number) => {
  testingIndexer.value = id
  try {
    const result = await apiTestIndexer(id)
    if (result.success) {
      success(`Indexer tested successfully: ${result.message}`)
      // Update the indexer with test results
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    } else {
      showError(`Indexer test failed: ${result.error || result.message}`)
      // Still update to show failed test status
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    }
  } catch (error) {
    console.error('Failed to test indexer:', error)
    showError('Failed to test indexer')
  } finally {
    testingIndexer.value = null
  }
}

const editIndexer = (indexer: Indexer) => {
  editingIndexer.value = indexer
  showIndexerForm.value = true
}

const confirmDeleteIndexer = (indexer: Indexer) => {
  indexerToDelete.value = indexer
}

const executeDeleteIndexer = async () => {
  if (!indexerToDelete.value) return
  
  try {
    await deleteIndexer(indexerToDelete.value.id)
    indexers.value = indexers.value.filter(i => i.id !== indexerToDelete.value!.id)
    success('Indexer deleted successfully')
  } catch (error) {
    console.error('Failed to delete indexer:', error)
    showError('Failed to delete indexer')
  } finally {
    indexerToDelete.value = null
  }
}

const formatDate = (dateString: string | undefined): string => {
  if (!dateString) return 'Never'
  const date = new Date(dateString)
  return date.toLocaleString()
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as 'indexers' | 'apis' | 'clients' | 'general'
  if (hash && ['indexers', 'apis', 'clients', 'general'].includes(hash)) {
    activeTab.value = hash
  } else {
    // Default to indexers and update URL
    activeTab.value = 'indexers'
    router.replace({ hash: '#indexers' })
  }
}

// Watch for hash changes
watch(() => route.hash, () => {
  syncTabFromHash()
})

onMounted(async () => {
  // Set initial tab from URL hash
  syncTabFromHash()
  
  await Promise.all([
    configStore.loadApiConfigurations(),
    configStore.loadDownloadClientConfigurations(),
    configStore.loadApplicationSettings(),
    loadIndexers()
  ])
  
  settings.value = configStore.applicationSettings
})
</script>

<style scoped>
.settings-page {
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
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.settings-header h1 i {
  color: #007acc;
}

.settings-header p {
  margin: 0;
  color: #999;
  font-size: 1rem;
}

.settings-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid #333;
}

.tab-button {
  padding: 1rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: #999;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.tab-button:hover {
  background-color: rgba(255, 255, 255, 0.05);
  color: #fff;
}

.tab-button.active {
  color: #007acc;
  border-bottom-color: #007acc;
  background-color: rgba(0, 122, 204, 0.1);
}

.settings-content {
  background: #2a2a2a;
  border-radius: 8px;
  border: 1px solid #333;
  min-height: 500px;
}

.tab-content {
  padding: 2rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #444;
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
}

.add-button,
.save-button {
  padding: 0.75rem 1.5rem;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.add-button:hover,
.save-button:hover:not(:disabled) {
  background-color: #005fa3;
  transform: translateY(-1px);
}

.save-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #999;
}

.empty-state i {
  font-size: 4rem;
  color: #555;
  margin-bottom: 1rem;
}

.empty-state p {
  margin: 0;
  font-size: 1.1rem;
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
  background-color: #333;
  border: 1px solid #444;
  border-radius: 8px;
  transition: all 0.2s;
}

.config-card:hover {
  background-color: #3a3a3a;
  border-color: #555;
  transform: translateY(-2px);
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
}

.config-url {
  margin: 0 0 1rem 0;
  color: #007acc;
  font-family: 'Courier New', monospace;
  font-size: 0.9rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.config-meta {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.config-meta span {
  padding: 0.4rem 0.8rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.config-type {
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid rgba(0, 122, 204, 0.3);
}

.config-status {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.config-status.enabled {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.3);
}

.config-priority {
  background-color: rgba(155, 89, 182, 0.2);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.config-ssl {
  background-color: rgba(127, 140, 141, 0.2);
  color: #95a5a6;
  border: 1px solid rgba(127, 140, 141, 0.3);
}

.config-ssl.enabled {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
  border: 1px solid rgba(241, 196, 15, 0.3);
}

.config-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.edit-button,
.delete-button {
  padding: 0.75rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.2rem;
}

.edit-button {
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid rgba(0, 122, 204, 0.3);
}

.edit-button:hover {
  background-color: #007acc;
  color: #fff;
  transform: translateY(-1px);
}

.delete-button {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.delete-button:hover {
  background-color: #e74c3c;
  color: #fff;
  transform: translateY(-1px);
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  background-color: #333;
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1.5rem;
}

.form-section h4 {
  margin: 0 0 1.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #444;
}

.form-section h4 i {
  color: #007acc;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  font-weight: 600;
  color: #fff;
  font-size: 0.95rem;
}

.form-group input[type="text"],
.form-group input[type="number"] {
  padding: 0.75rem;
  background-color: #2a2a2a;
  border: 1px solid #555;
  border-radius: 4px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s;
}

.form-group input[type="text"]:focus,
.form-group input[type="number"]:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-help {
  font-size: 0.85rem;
  color: #999;
  font-style: italic;
}

.checkbox-group {
  flex-direction: row;
  align-items: flex-start;
  background-color: #2a2a2a;
  border: 1px solid #444;
  border-radius: 4px;
  padding: 1rem;
  margin-bottom: 1rem;
  transition: all 0.2s;
}

.checkbox-group:hover {
  background-color: #333;
  border-color: #555;
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

.checkbox-group input[type="checkbox"] {
  margin: 0.25rem 0 0 0;
  width: 18px;
  height: 18px;
  cursor: pointer;
  flex-shrink: 0;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
}

.checkbox-group label small {
  color: #999;
  font-size: 0.85rem;
  font-weight: normal;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 8px;
  max-width: 600px;
  width: 90%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #444;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
}

.modal-close {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.modal-close:hover {
  background-color: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  color: #ccc;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background-color: #555;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
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
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1.5rem;
  transition: all 0.2s;
}

.indexer-card:hover {
  border-color: #007acc;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 122, 204, 0.2);
}

.indexer-card.disabled {
  opacity: 0.6;
  filter: grayscale(50%);
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #333;
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
}

.indexer-type {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.indexer-type.torrent {
  background-color: rgba(76, 175, 80, 0.2);
  color: #4caf50;
  border: 1px solid #4caf50;
}

.indexer-type.usenet {
  background-color: rgba(33, 150, 243, 0.2);
  color: #2196f3;
  border: 1px solid #2196f3;
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
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
  font-size: 0.9rem;
}

.detail-row i {
  color: #007acc;
  font-size: 1rem;
}

.detail-label {
  color: #999;
  min-width: 100px;
}

.detail-value {
  color: #ccc;
  word-break: break-all;
}

.detail-value.success {
  color: #4caf50;
}

.detail-value.error {
  color: #f44336;
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
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid #007acc;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.error-message {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(244, 67, 54, 0.1);
  border: 1px solid #f44336;
  border-radius: 4px;
  color: #f44336;
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
</style>