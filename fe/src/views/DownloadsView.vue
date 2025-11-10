<template>
  <div class="downloads-page">
    <div class="downloads-header">
      <h2>Downloads</h2>
      <button @click="refreshDownloads" :disabled="downloadsStore.isLoading" class="refresh-button">
        {{ downloadsStore.isLoading ? 'Refreshing...' : 'Refresh' }}
      </button>
    </div>

    <div class="downloads-tabs">
      <!-- Mobile dropdown -->
      <div class="downloads-tabs-mobile">
        <CustomSelect
          v-model="activeTab"
          :options="mobileTabOptions"
          class="tab-dropdown"
        />
      </div>

      <!-- Desktop tabs -->
      <div class="downloads-tabs-desktop">
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
    </div>

    <div class="downloads-content">
      <div v-if="currentDownloads.length === 0" class="empty-state">
        <p>{{ getEmptyMessage() }}</p>
      </div>
      
      <div v-else ref="scrollContainer" class="downloads-list-container" @scroll="updateVisibleRange">
        <div class="downloads-list-spacer" :style="{ height: `${totalHeight}px` }">
          <div class="downloads-list" :style="{ transform: `translateY(${topPadding}px)` }">
            <div 
              v-for="download in visibleDownloads" 
              :key="download.id"
              v-memo="[download.id, download.status, download.progress]"
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
          </div>  <!-- Close downloads-list -->
        </div>    <!-- Close downloads-list-spacer -->
      </div>      <!-- Close downloads-list-container -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useDownloadsStore } from '@/stores/downloads'
import CustomSelect from '@/components/CustomSelect.vue'
import type { Download } from '@/types'
import { useToast } from '@/services/toastService'
import { PhDownloadSimple, PhCheckCircle, PhXCircle } from '@phosphor-icons/vue'

const downloadsStore = useDownloadsStore()
const toast = useToast()
const activeTab = ref<'active' | 'completed' | 'failed'>('active')

const mobileTabOptions = computed(() => [
  { value: 'active', label: 'Active', icon: PhDownloadSimple },
  { value: 'completed', label: 'Completed', icon: PhCheckCircle },
  { value: 'failed', label: 'Failed', icon: PhXCircle }
])

// const onTabChange = () => {
//   // Tab change handler for mobile dropdown
// }

// Virtual scrolling setup
const scrollContainer = ref<HTMLElement | null>(null)
const ROW_HEIGHT = 180 // Approximate height of each download card
const BUFFER_ROWS = 3

const visibleRange = ref({ start: 0, end: 20 })

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

const visibleDownloads = computed(() => {
  return currentDownloads.value.slice(visibleRange.value.start, visibleRange.value.end)
})

const updateVisibleRange = () => {
  if (!scrollContainer.value) return
  
  const scrollTop = scrollContainer.value.scrollTop
  const viewportHeight = scrollContainer.value.clientHeight
  
  const firstVisibleIndex = Math.floor(scrollTop / ROW_HEIGHT)
  const visibleItemCount = Math.ceil(viewportHeight / ROW_HEIGHT)
  
  const startIndex = Math.max(0, firstVisibleIndex - BUFFER_ROWS)
  const endIndex = Math.min(firstVisibleIndex + visibleItemCount + BUFFER_ROWS, currentDownloads.value.length)
  
  visibleRange.value = { start: startIndex, end: endIndex }
}

const totalHeight = computed(() => {
  return currentDownloads.value.length * ROW_HEIGHT
})

const topPadding = computed(() => {
  return visibleRange.value.start * ROW_HEIGHT
})

const refreshDownloads = async () => {
  await downloadsStore.loadDownloads()
}

const cancelDownload = async (downloadId: string) => {
  try {
    await downloadsStore.cancelDownload(downloadId)
    toast.success('Success', 'Download canceled successfully')
  } catch (error) {
    console.error('Failed to cancel download:', error)
    toast.error('Error', 'Failed to cancel download')
  }
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const retryDownload = async (download: Download) => {
  // For now, just show a message. In a real implementation,
  // you would restart the download
  // useToast expects (title, message)
  toast.info('Coming Soon', 'Retry functionality will be implemented soon')
}

const openFolder = (path: string) => {
  // This would need to be implemented with a backend endpoint
  // that can open the folder in the OS file explorer
  // map (message, title) -> (title, message)
  toast.info('Open Folder', `Would open folder: ${path}`)
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
  
  // Initialize virtual scrolling
  nextTick(() => {
    updateVisibleRange()
  })
  
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

/* Virtual scrolling container */
.downloads-list-container {
  height: calc(100vh - 280px);
  overflow-y: auto;
  position: relative;
}

.downloads-list-spacer {
  position: relative;
  width: 100%;
}

.downloads-list {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
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

  .downloads-tabs {
    flex-direction: column;
    gap: 1rem;
  }

  .downloads-tabs-mobile {
    display: block;
  }

  .downloads-tabs-desktop {
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

@media (min-width: 769px) {
  .downloads-tabs {
    flex-direction: row;
  }

  .downloads-tabs-mobile {
    display: none;
  }

  .downloads-tabs-desktop {
    display: flex;
  }
}
</style>