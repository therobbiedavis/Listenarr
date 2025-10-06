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
          <button @click="showClientForm = true" class="add-button">
            <i class="ph ph-plus"></i>
            Add Download Client
          </button>
        </div>

        <div v-if="configStore.downloadClientConfigurations.length === 0" class="empty-state">
          <i class="ph ph-download-simple"></i>
          <p>No download clients configured. Add one to start downloading media.</p>
        </div>

        <div v-else class="config-list">
          <div 
            v-for="client in configStore.downloadClientConfigurations" 
            :key="client.id"
            class="config-card"
          >
            <div class="config-info">
              <h4>{{ client.name }}</h4>
              <p class="config-url">{{ client.host }}:{{ client.port }}</p>
              <div class="config-meta">
                <span class="config-type">{{ client.type.toUpperCase() }}</span>
                <span class="config-status" :class="{ enabled: client.isEnabled }">
                  <i :class="client.isEnabled ? 'ph ph-check-circle' : 'ph ph-x-circle'"></i>
                  {{ client.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
                <span class="config-ssl" :class="{ enabled: client.useSSL }">
                  <i :class="client.useSSL ? 'ph ph-lock' : 'ph ph-lock-open'"></i>
                  {{ client.useSSL ? 'SSL' : 'No SSL' }}
                </span>
              </div>
            </div>
            <div class="config-actions">
              <button @click="editClientConfig(client)" class="edit-button" title="Edit">
                <i class="ph ph-pencil"></i>
              </button>
              <button @click="deleteClientConfig(client.id)" class="delete-button" title="Delete">
                <i class="ph ph-trash"></i>
              </button>
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
              <input v-model="settings.fileNamingPattern" type="text" placeholder="{Artist}/{Album}/{TrackNumber:00} - {Title}">
              <span class="form-help">Pattern for organizing audiobook files</span>
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

    <!-- Download Client Configuration Modal (placeholder) -->
    <div v-if="showClientForm" class="modal-overlay" @click="showClientForm = false">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ editingClient ? 'Edit' : 'Add' }} Download Client</h3>
          <button @click="showClientForm = false" class="modal-close">
            <i class="ph ph-x"></i>
          </button>
        </div>
        <div class="modal-body">
          <p>Download client configuration form would go here...</p>
        </div>
        <div class="modal-actions">
          <button @click="showClientForm = false" class="cancel-button">
            <i class="ph ph-x"></i>
            Cancel
          </button>
          <button @click="saveClientConfig" class="save-button">
            <i class="ph ph-check"></i>
            Save
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const activeTab = ref<'apis' | 'clients' | 'general'>('apis')
const showApiForm = ref(false)
const showClientForm = ref(false)
const editingApi = ref<ApiConfiguration | null>(null)
const editingClient = ref<DownloadClientConfiguration | null>(null)
const settings = ref<ApplicationSettings | null>(null)

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
      alert('API configuration deleted successfully')
    } catch (error) {
      console.error('Failed to delete API configuration:', error)
      alert('Failed to delete API configuration')
    }
  }
}

const deleteClientConfig = async (id: string) => {
  if (confirm('Are you sure you want to delete this download client configuration?')) {
    try {
      await configStore.deleteDownloadClientConfiguration(id)
      alert('Download client configuration deleted successfully')
    } catch (error) {
      console.error('Failed to delete download client configuration:', error)
      alert('Failed to delete download client configuration')
    }
  }
}

const saveApiConfig = () => {
  // Placeholder for API config save
  alert('API configuration form would be implemented here')
  showApiForm.value = false
  editingApi.value = null
}

const saveClientConfig = () => {
  // Placeholder for client config save
  alert('Download client configuration form would be implemented here')
  showClientForm.value = false
  editingClient.value = null
}

const saveSettings = async () => {
  if (!settings.value) return
  
  try {
    await configStore.saveApplicationSettings(settings.value)
    alert('Settings saved successfully')
  } catch (error) {
    console.error('Failed to save settings:', error)
    alert('Failed to save settings')
  }
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as 'apis' | 'clients' | 'general'
  if (hash && ['apis', 'clients', 'general'].includes(hash)) {
    activeTab.value = hash
  } else {
    // Default to apis and update URL
    activeTab.value = 'apis'
    router.replace({ hash: '#apis' })
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
    configStore.loadApplicationSettings()
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
}
</style>