<template>
  <div class="downloads-page">
    <div class="downloads-header">
      <h2>Downloads</h2>
      <button @click="refreshDownloads" :disabled="downloadsStore.isLoading" class="refresh-button">
        {{ downloadsStore.isLoading ? 'Refreshing...' : 'Refresh' }}
      </button>
    </div>

    <div class="downloads-tabs">
      <button 
        @click="activeTab = 'active'" 
        :class="{ active: activeTab === 'active' }"
        class="tab-button"
      >
        Active ({{ downloadsStore.activeDownloads.length }})
      </button>
      <button 
        @click="activeTab = 'completed'" 
        :class="{ active: activeTab === 'completed' }"
        class="tab-button"
      >
        Completed ({{ downloadsStore.completedDownloads.length }})
      </button>
      <button 
        @click="activeTab = 'failed'" 
        :class="{ active: activeTab === 'failed' }"
        class="tab-button"
      >
        Failed ({{ downloadsStore.failedDownloads.length }})
      </button>
    </div>

    <div class="downloads-content">
      <div v-if="currentDownloads.length === 0" class="empty-state">
        <p>{{ getEmptyMessage() }}</p>
      </div>
      
      <div v-else class="downloads-list">
        <div 
          v-for="download in currentDownloads" 
          :key="download.id"
          class="download-card"
        >
          <div class="download-info">
            <h3>{{ download.title }}</h3>
            <p class="download-artist">{{ download.artist }}</p>
            <p class="download-album">{{ download.album }}</p>
            
            <div class="download-meta">
              <span class="download-size">{{ formatFileSize(download.totalSize) }}</span>
              <span class="download-client">{{ download.downloadClientId }}</span>
              <span class="download-date">{{ formatDate(download.startedAt) }}</span>
            </div>
          </div>

          <div class="download-status-section">
            <div class="download-status" :class="download.status.toLowerCase()">
              {{ download.status }}
            </div>
            
            <div v-if="download.status === 'Downloading'" class="download-progress">
              <div class="progress-bar">
                <div 
                  class="progress-fill" 
                  :style="{ width: download.progress + '%' }"
                ></div>
              </div>
              <div class="progress-info">
                <span>{{ Math.round(download.progress) }}%</span>
                <span>{{ formatFileSize(download.downloadedSize) }} / {{ formatFileSize(download.totalSize) }}</span>
              </div>
            </div>

            <div v-if="download.errorMessage" class="error-message">
              {{ download.errorMessage }}
            </div>
          </div>

          <div class="download-actions">
            <button 
              v-if="['Queued', 'Downloading'].includes(download.status)"
              @click="cancelDownload(download.id)"
              class="action-button cancel"
            >
              Cancel
            </button>
            
            <button 
              v-if="download.status === 'Failed'"
              @click="retryDownload(download)"
              class="action-button retry"
            >
              Retry
            </button>
            
            <button 
              v-if="download.finalPath"
              @click="openFolder(download.finalPath)"
              class="action-button open"
            >
              Open Folder
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useDownloadsStore } from '@/stores/downloads'
import type { Download } from '@/types'
import { useNotification } from '@/composables/useNotification'

const downloadsStore = useDownloadsStore()
const { success, error: showError, info } = useNotification()
const activeTab = ref<'active' | 'completed' | 'failed'>('active')

const currentDownloads = computed(() => {
  switch (activeTab.value) {
    case 'active':
      return downloadsStore.activeDownloads
    case 'completed':
      return downloadsStore.completedDownloads
    case 'failed':
      return downloadsStore.failedDownloads
    default:
      return []
  }
})

const refreshDownloads = async () => {
  await downloadsStore.loadDownloads()
}

const cancelDownload = async (downloadId: string) => {
  try {
    await downloadsStore.cancelDownload(downloadId)
    success('Download canceled successfully')
  } catch (error) {
    console.error('Failed to cancel download:', error)
    showError('Failed to cancel download')
  }
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const retryDownload = async (download: Download) => {
  // For now, just show a message. In a real implementation, 
  // you would restart the download
  info('Retry functionality will be implemented soon', 'Coming Soon')
}

const openFolder = (path: string) => {
  // This would need to be implemented with a backend endpoint
  // that can open the folder in the OS file explorer
  info(`Would open folder: ${path}`, 'Open Folder')
}

const getEmptyMessage = () => {
  switch (activeTab.value) {
    case 'active':
      return 'No active downloads. Start a search to download media!'
    case 'completed':
      return 'No completed downloads yet.'
    case 'failed':
      return 'No failed downloads.'
    default:
      return 'No downloads found.'
  }
}

const formatFileSize = (bytes: number): string => {
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  if (bytes === 0) return '0 Bytes'
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i]
}

const formatDate = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString() + ' ' + 
         new Date(dateString).toLocaleTimeString()
}

onMounted(() => {
  // Load initial downloads from API
  refreshDownloads()
  
  // No polling needed - SignalR pushes updates in real-time!
  // The downloads store automatically receives updates via WebSocket
})
</script>

<style scoped>
.downloads-page {
  max-width: 1200px;
  margin: 0 auto;
}

.downloads-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.downloads-header h2 {
  margin: 0;
  color: #2c3e50;
}

.refresh-button {
  padding: 0.5rem 1rem;
  background-color: #3498db;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.refresh-button:hover:not(:disabled) {
  background-color: #2980b9;
}

.refresh-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.downloads-tabs {
  display: flex;
  gap: 0;
  margin-bottom: 2rem;
  border-bottom: 1px solid #ddd;
}

.tab-button {
  padding: 1rem 2rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 1rem;
  color: #666;
  transition: all 0.2s;
}

.tab-button:hover {
  background-color: #f8f9fa;
}

.tab-button.active {
  color: #3498db;
  border-bottom-color: #3498db;
}

.downloads-content {
  min-height: 400px;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #666;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.downloads-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.download-card {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 2rem;
  align-items: start;
}

.download-info {
  flex: 1;
}

.download-info h3 {
  margin: 0 0 0.5rem 0;
  color: #2c3e50;
}

.download-artist {
  margin: 0 0 0.25rem 0;
  font-weight: 600;
  color: #555;
}

.download-album {
  margin: 0 0 1rem 0;
  color: #777;
  font-style: italic;
}

.download-meta {
  display: flex;
  gap: 1rem;
  font-size: 0.9rem;
  color: #666;
}

.download-meta span {
  background-color: #f8f9fa;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
}

.download-status-section {
  min-width: 200px;
}

.download-status {
  display: inline-block;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  margin-bottom: 1rem;
}

.download-status.queued {
  background-color: #f39c12;
  color: white;
}

.download-status.downloading {
  background-color: #3498db;
  color: white;
}

.download-status.completed {
  background-color: #27ae60;
  color: white;
}

.download-status.failed {
  background-color: #e74c3c;
  color: white;
}

.download-status.processing {
  background-color: #9b59b6;
  color: white;
}

.download-status.ready {
  background-color: #27ae60;
  color: white;
}

.download-progress {
  margin-bottom: 1rem;
}

.progress-bar {
  width: 100%;
  height: 8px;
  background-color: #eee;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.progress-fill {
  height: 100%;
  background-color: #3498db;
  transition: width 0.3s ease;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  color: #666;
}

.error-message {
  padding: 0.5rem;
  background-color: #fee;
  border: 1px solid #fcc;
  border-radius: 4px;
  color: #c33;
  font-size: 0.9rem;
}

.download-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.action-button {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.action-button.cancel {
  background-color: #e74c3c;
  color: white;
}

.action-button.cancel:hover {
  background-color: #c0392b;
}

.action-button.retry {
  background-color: #f39c12;
  color: white;
}

.action-button.retry:hover {
  background-color: #d68910;
}

.action-button.open {
  background-color: #27ae60;
  color: white;
}

.action-button.open:hover {
  background-color: #229954;
}

@media (max-width: 768px) {
  .download-card {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
  
  .download-actions {
    flex-direction: row;
  }
}
</style>