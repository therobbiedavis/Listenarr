<template>
  <div class="activity-view">
    <div class="page-header">
      <h1>
        <PhActivity />
        Activity
      </h1>
      <div class="activity-actions">
        <button class="btn btn-secondary" @click="refreshQueue" :disabled="loading">
          <component :is="loading ? PhSpinner : PhArrowClockwise" />
          Refresh
        </button>
      </div>
    </div>

    <div class="activity-filters">
      <div class="filter-tabs">
        <button 
          v-for="tab in filterTabs" 
          :key="tab.value"
          :class="['tab', { active: selectedTab === tab.value }]"
          @click="selectedTab = tab.value"
        >
          {{ tab.label }}
          <span v-if="tab.count > 0" class="tab-badge">{{ tab.count }}</span>
        </button>
      </div>
    </div>

    <!-- Queue List -->
    <div v-if="filteredQueue.length > 0" ref="scrollContainer" class="queue-list-container" @scroll="updateVisibleRange">
      <div class="queue-list-spacer" :style="{ height: `${totalHeight}px` }">
        <div class="queue-list" :style="{ transform: `translateY(${topPadding}px)` }">
          <div 
            v-for="item in visibleQueueItems" 
            :key="item.id"
            v-memo="[item.id, item.status, item.progress, item.eta]"
            class="queue-item"
          >
        <div class="queue-icon">
          <PhDownloadSimple />
        </div>
        
        <div class="queue-info">
          <div class="queue-title-row">
            <h3 class="queue-title">{{ item.title }}</h3>
          </div>
          
          <div class="queue-meta">
            <span v-if="item.downloadClient" class="queue-client">
              <PhDesktop />
              {{ item.downloadClient }}
            </span>
            <span v-if="item.quality && item.quality !== '*'" class="queue-quality">{{ item.quality }}</span>
          </div>

          <div class="queue-progress-container">
            <div class="queue-stats-top">
              <span class="progress-text">{{ item.progress.toFixed(1) }}%</span>
              <span class="size-info">{{ formatSize(item.downloaded) }} / {{ formatSize(item.size) }}</span>
              <span v-if="item.downloadSpeed > 0" class="download-speed">
                <PhArrowDown />
                {{ formatSpeed(item.downloadSpeed) }}
              </span>
              <span v-if="item.eta" class="eta">
                <PhClock />
                {{ formatEta(item.eta) }}
              </span>
            </div>
            <div class="progress-bar">
              <div 
                class="progress-fill" 
                :style="{ width: `${item.progress}%` }"
                :class="getProgressClass(item.status)"
              ></div>
            </div>
          </div>
        </div>

        <div class="queue-status">
          <span :class="['status-badge', item.status]">
            {{ formatStatus(item.status) }}
          </span>
        </div>

        <div class="queue-actions">
          <button 
            v-if="item.canRemove"
            class="btn-icon btn-danger"
            @click="removeFromQueue(item)"
            title="Remove from Queue"
          >
            <PhX />
          </button>
        </div>
      </div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div class="empty-state" v-else-if="filteredQueue.length === 0 && !loading">
      <div class="empty-icon">
        <PhQueue />
      </div>
      <h2>No Active Downloads</h2>
      <p>Downloads will appear here when you send items to your download clients.</p>
    </div>

    <!-- Loading State -->
    <div class="loading-state" v-if="loading && queue.length === 0">
      <PhSpinner class="ph-spin" />
      <p>Loading queue...</p>
    </div>

    <!-- Remove Confirmation Modal -->
    <div v-if="showRemoveModal" class="modal-overlay" @click="showRemoveModal = false">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>
            <PhWarningCircle />
            Remove from Queue
          </h3>
          <button class="modal-close" @click="showRemoveModal = false">
            <PhX />
          </button>
        </div>
        <div class="modal-body">
          <p>Are you sure you want to remove this download?</p>
          <div class="remove-item-info">
            <strong>{{ itemToRemove?.title }}</strong>
            <div class="item-details">
              <span v-if="itemToRemove?.downloadClient">
                <PhDesktop />
                {{ itemToRemove.downloadClient }}
              </span>
              <span>
                <PhChartBar />
                {{ itemToRemove?.progress.toFixed(1) }}% complete
              </span>
            </div>
          </div>
          <p class="warning-text">
            <PhInfo />
            This will remove the download from your download client. Files may or may not be deleted depending on your client settings.
          </p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showRemoveModal = false">
            Cancel
          </button>
          <button class="btn btn-danger" @click="confirmRemove" :disabled="removing">
            <component :is="removing ? PhSpinner : PhTrash" />
            {{ removing ? 'Removing...' : 'Remove' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { PhActivity, PhSpinner, PhArrowClockwise, PhDownloadSimple, PhDesktop, PhArrowDown, PhClock, PhX, PhQueue, PhWarningCircle, PhInfo, PhChartBar, PhTrash } from '@phosphor-icons/vue'
import { useToast } from '@/services/toastService'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { useDownloadsStore } from '@/stores/downloads'
import { useConfigurationStore } from '@/stores/configuration'
import type { QueueItem, Download } from '@/types'

const downloadsStore = useDownloadsStore()
const configStore = useConfigurationStore()
const selectedTab = ref('all')
const queue = ref<QueueItem[]>([])
const loading = ref(false)
const showRemoveModal = ref(false)
const itemToRemove = ref<QueueItem | null>(null)
const removing = ref(false)
let unsubscribeQueue: (() => void) | null = null
let queueRefreshInterval: ReturnType<typeof setInterval> | null = null

// Virtual scrolling setup
const scrollContainer = ref<HTMLElement | null>(null)
const ROW_HEIGHT = 120 // Approximate height of each queue item
const BUFFER_ROWS = 3 // Extra rows to render above and below viewport

const visibleRange = ref({ start: 0, end: 20 }) // Initially show first 20 items

const visibleQueueItems = computed(() => {
  return filteredQueue.value.slice(visibleRange.value.start, visibleRange.value.end)
})

// Update visible range based on scroll position
const updateVisibleRange = () => {
  if (!scrollContainer.value) return
  
  const scrollTop = scrollContainer.value.scrollTop
  const viewportHeight = scrollContainer.value.clientHeight
  
  // Calculate which items are visible
  const firstVisibleIndex = Math.floor(scrollTop / ROW_HEIGHT)
  const visibleItemCount = Math.ceil(viewportHeight / ROW_HEIGHT)
  
  // Add buffer
  const startIndex = Math.max(0, firstVisibleIndex - BUFFER_ROWS)
  const endIndex = Math.min(firstVisibleIndex + visibleItemCount + BUFFER_ROWS, filteredQueue.value.length)
  
  visibleRange.value = { start: startIndex, end: endIndex }
}

// Calculate total height for proper scrollbar
const totalHeight = computed(() => {
  return filteredQueue.value.length * ROW_HEIGHT
})

// Padding for offset positioning
const topPadding = computed(() => {
  return visibleRange.value.start * ROW_HEIGHT
})

// Convert Download to QueueItem format for unified display
const convertDownloadToQueueItem = (download: Download): QueueItem => {
  // Map Download status to queue status
  let status = 'downloading'
  if (download.status === 'Queued') status = 'queued'
  else if (download.status === 'Paused') status = 'paused'
  else if (download.status === 'Completed' || download.status === 'Ready') status = 'completed'
  else if (download.status === 'Failed') status = 'failed'
  else if (download.status === 'Downloading' || download.status === 'Processing') status = 'downloading'

  const clientName = ((download as unknown) as Record<string, unknown>)['downloadClientName'] as string | undefined
  return {
    id: download.id,
    title: download.title,
    status: status,
    progress: download.progress,
    size: download.totalSize,
    downloaded: download.downloadedSize,
    downloadSpeed: 0, // Not tracked for DDL
    eta: undefined, // Not available for DDL
    quality: '',
    downloadClient: clientName ?? download.downloadClientId ?? 'Unknown Client',
    downloadClientId: download.downloadClientId,
    downloadClientType: download.downloadClientId === 'DDL' ? 'DDL' : 'external',
    addedAt: download.startedAt,
    canPause: false,
    canRemove: true
  }
}

// Read user preference from configuration store: show completed external downloads
const showCompletedExternalDownloads = computed(() => configStore.applicationSettings?.showCompletedExternalDownloads ?? false)

// Merge queue items and active downloads (DDL) into unified list
const allActivityItems = computed(() => {
  // Get queue items from external clients (these are already filtered by backend to only show Listenarr-managed downloads)
  const queueItems = [...queue.value]
  
  // Get DDL downloads from database (since they don't have corresponding queue items)
  const ddlDownloadItems = downloadsStore.activeDownloads
    .filter(d => d.downloadClientId === 'DDL')
    .map(convertDownloadToQueueItem)
  
  // Combine queue items (external clients managed by Listenarr) and DDL downloads
  // Filter out completed items from external download clients (torrents/NZBs)
  // to avoid cluttering the activity view with finished transfers that are
  // already processed. Keep completed DDL downloads (internal) visible.
  const combined = [...queueItems, ...ddlDownloadItems]
  // Read user preference from configuration store: show completed external downloads
  const userPref = showCompletedExternalDownloads.value
  if (userPref) return combined

  const filtered = combined.filter(it => {
    // if item is from external client and completed, omit it
    if ((it.downloadClientType || '').toString().toLowerCase() !== 'ddl' && it.status === 'completed') return false
    return true
  })

  return filtered
})

const filterTabs = computed(() => [
  { label: 'All', value: 'all', count: allActivityItems.value.length },
  { label: 'Downloading', value: 'downloading', count: allActivityItems.value.filter(q => q.status === 'downloading').length },
  { label: 'Paused', value: 'paused', count: allActivityItems.value.filter(q => q.status === 'paused').length },
  { label: 'Queued', value: 'queued', count: allActivityItems.value.filter(q => q.status === 'queued').length },
  { label: 'Completed', value: 'completed', count: allActivityItems.value.filter(q => q.status === 'completed').length },
])

const filteredQueue = computed(() => {
  if (selectedTab.value === 'all') {
    return allActivityItems.value
  }
  return allActivityItems.value.filter(item => item.status === selectedTab.value)
})

const refreshQueue = async () => {
  loading.value = true
  try {
    queue.value = await apiService.getQueue()
  } catch (err) {
    console.error('Failed to fetch queue:', err)
  } finally {
    loading.value = false
  }
}

const removeFromQueue = async (item: QueueItem) => {
  itemToRemove.value = item
  showRemoveModal.value = true
}

const confirmRemove = async () => {
  if (!itemToRemove.value) return
  
  removing.value = true
  try {
    // Check if this is a DDL download (from database) or external queue item
    if (itemToRemove.value.downloadClientId === 'DDL' || itemToRemove.value.downloadClientType === 'DDL') {
      // DDL downloads: Cancel/delete from database
      console.log('[ActivityView] Canceling DDL download:', itemToRemove.value.id)
      await apiService.cancelDownload(itemToRemove.value.id)
      
      // Refresh downloads from store
      await downloadsStore.loadDownloads()
    } else {
      // External client downloads: Remove from queue
      console.log('[ActivityView] Removing from external client queue:', itemToRemove.value.id, itemToRemove.value.downloadClientId)
      await apiService.removeFromQueue(itemToRemove.value.id, itemToRemove.value.downloadClientId)
      
      // Refresh queue
      await refreshQueue()
    }
    
    showRemoveModal.value = false
    itemToRemove.value = null
  } catch (err) {
    console.error('Failed to remove download:', err)
    const toast = useToast()
    toast.error('Remove failed', (err as Error).message)
  } finally {
    removing.value = false
  }
}

const getProgressClass = (status: string): string => {
  if (status === 'completed') return 'completed'
  if (status === 'failed') return 'failed'
  if (status === 'downloading') return 'downloading'
  if (status === 'paused') return 'paused'
  return 'queued'
}

const formatStatus = (status: string): string => {
  return status.charAt(0).toUpperCase() + status.slice(1)
}

const formatSpeed = (bytesPerSecond: number): string => {
  if (bytesPerSecond === 0) return '0 B/s'
  
  const units = ['B/s', 'KB/s', 'MB/s', 'GB/s']
  let speed = bytesPerSecond
  let unitIndex = 0
  
  while (speed >= 1024 && unitIndex < units.length - 1) {
    speed /= 1024
    unitIndex++
  }
  
  return `${speed.toFixed(1)} ${units[unitIndex]}`
}

const formatEta = (seconds: number): string => {
  if (seconds < 60) return `${seconds}s`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`
  return `${Math.floor(seconds / 86400)}d`
}

const formatSize = (bytes: number): string => {
  if (!bytes || bytes === 0) return '0 B'
  
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unitIndex = 0
  
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  
  return `${size.toFixed(1)} ${units[unitIndex]}`
}

// Subscribe to SignalR for real-time updates (NO POLLING!)
onMounted(async () => {
  // Load initial downloads (includes DDL)
  await downloadsStore.loadDownloads()
  
  // Load application settings to ensure filtering works
  await configStore.loadApplicationSettings()
  
  // Subscribe to queue updates (external clients)
  unsubscribeQueue = signalRService.onQueueUpdate((updatedQueue) => {
    queue.value = updatedQueue
  })
  
  // Load initial queue state
  await refreshQueue()
  
  // Initialize virtual scrolling
  nextTick(() => {
    updateVisibleRange()
  })

  // Fallback polling: slow refresh as backup to SignalR (30 seconds)
  // Primary updates come from SignalR real-time events
  queueRefreshInterval = setInterval(async () => {
    try {
      await refreshQueue()
    } catch (err) {
      console.error('[ActivityView] Failed to refresh queue:', err)
    }
  }, 30000) // 30-second fallback polling (SignalR is primary update mechanism)
})

onUnmounted(() => {
  // Clean up subscription
  if (unsubscribeQueue) {
    unsubscribeQueue()
  }
  
  // Stop frontend polling when view is unmounted
  if (queueRefreshInterval) {
    clearInterval(queueRefreshInterval)
    queueRefreshInterval = null
  }
})
</script>

<style scoped>
.activity-view {
  padding: 2em;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

/* Virtual scrolling container */
.queue-list-container {
  height: calc(100vh - 280px); /* Adjust based on header/footer height */
  overflow-y: auto;
  position: relative;
}

.queue-list-spacer {
  position: relative;
  width: 100%;
}

.queue-list {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.page-header h1 {
  margin: 0;
  color: white;
  font-size: 2rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
}

.page-header h1 svg {
  width: 32px;
  height: 32px;
}

.activity-actions {
  display: flex;
  gap: 0.75rem;
}

.btn {
  padding: 0.65rem 1.25rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn-secondary {
  background-color: rgba(255, 255, 255, 0.08);
  color: #adb5bd;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-secondary:hover:not(:disabled) {
  background-color: rgba(255, 255, 255, 0.12);
  color: white;
  border-color: rgba(255, 255, 255, 0.15);
  transform: translateY(-1px);
}

.btn-secondary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn-icon {
  background: none;
  border: none;
  color: #adb5bd;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
  font-size: 1.25rem;
}

.btn-icon:hover {
  background-color: rgba(255, 255, 255, 0.08);
  color: white;
}

.btn-icon.btn-danger:hover {
  background-color: rgba(250, 82, 82, 0.15);
  color: #fa5252;
}

.activity-filters {
  margin-bottom: 2rem;
}

.filter-tabs {
  display: flex;
  gap: 0.5rem;
  border-bottom: 2px solid rgba(255, 255, 255, 0.1);
  overflow-x: auto;
  scrollbar-width: none;
}

.filter-tabs::-webkit-scrollbar {
  display: none;
}

.tab {
  background: none;
  border: none;
  color: #adb5bd;
  cursor: pointer;
  padding: 0.875rem 1.5rem;
  border-radius: 6px 6px 0 0;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  white-space: nowrap;
  position: relative;
}

.tab::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 2px;
  background: transparent;
  transition: background 0.2s;
}

.tab:hover {
  background-color: rgba(255, 255, 255, 0.05);
  color: white;
}

.tab.active {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  font-weight: 600;
}

.tab.active::after {
  background: #4dabf7;
}

.tab-badge {
  background-color: rgba(255, 255, 255, 0.15);
  border-radius: 10px;
  padding: 0.15rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  min-width: 20px;
  text-align: center;
}

.tab.active .tab-badge {
  background-color: rgba(77, 171, 247, 0.25);
  color: #74c0fc;
}

.queue-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.queue-item {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  padding: 1.25rem;
  background-color: #2a2a2a;
  border-radius: 8px;
  border-left: 4px solid #4dabf7;
  transition: all 0.2s;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.queue-item:hover {
  background-color: #2f2f2f;
  border-color: rgba(77, 171, 247, 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  border-left-color: #74c0fc;
}

.queue-icon {
  width: 48px;
  height: 48px;
  min-width: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  border-radius: 8px;
  color: white;
  font-size: 1.5rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.queue-icon svg {
  width: 24px;
  height: 24px;
}

.queue-info {
  flex: 1;
  min-width: 0;
}

.queue-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.queue-title {
  color: white;
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.queue-meta {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.75rem;
  font-size: 0.875rem;
  color: #868e96;
}

.queue-meta span {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.25rem 0.6rem;
  border-radius: 4px;
}

.queue-client {
  color: #4dabf7;
}

.queue-quality {
  color: #51cf66;
  font-weight: 500;
}

.queue-progress-container {
  width: 100%;
}

.queue-stats-top {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  margin-bottom: 0.5rem;
  font-size: 0.875rem;
  color: #adb5bd;
  flex-wrap: wrap;
}

.progress-text {
  font-weight: 600;
  color: white;
  font-size: 0.95rem;
}

.download-speed {
  color: #51cf66;
  display: flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(81, 207, 102, 0.1);
  padding: 0.25rem 0.6rem;
  border-radius: 4px;
}

.download-speed svg {
  width: 14px;
  height: 14px;
}

.eta {
  color: #ffd43b;
  display: flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(255, 212, 59, 0.1);
  padding: 0.25rem 0.6rem;
  border-radius: 4px;
}

.eta svg {
  width: 14px;
  height: 14px;
}

.size-info {
  color: #868e96;
}

.progress-bar {
  width: 100%;
  height: 10px;
  background-color: rgba(0, 0, 0, 0.3);
  border-radius: 6px;
  overflow: hidden;
  box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.3);
}

.progress-fill {
  height: 100%;
  border-radius: 6px;
  transition: width 0.3s ease;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
}

.progress-fill.downloading {
  background: linear-gradient(90deg, #1e88e5, #42a5f5);
  animation: progress-shimmer 2s infinite;
  background-size: 200% 100%;
}

.progress-fill.paused {
  background: linear-gradient(90deg, #ffd43b, #fcc419);
}

.progress-fill.completed {
  background: linear-gradient(90deg, #51cf66, #40c057);
}

.progress-fill.failed {
  background: linear-gradient(90deg, #fa5252, #ff6b6b);
}

.progress-fill.queued {
  background-color: #868e96;
}

@keyframes progress-shimmer {
  0% {
    background-position: -200% center;
  }
  100% {
    background-position: 200% center;
  }
}

.queue-status {
  margin: 0 0.5rem;
}

.status-badge {
  padding: 0.35rem 0.85rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.completed {
  background-color: rgba(81, 207, 102, 0.15);
  color: #51cf66;
  border: 1px solid rgba(81, 207, 102, 0.3);
}

.status-badge.downloading {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.status-badge.failed {
  background-color: rgba(250, 82, 82, 0.15);
  color: #fa5252;
  border: 1px solid rgba(250, 82, 82, 0.3);
}

.status-badge.paused {
  background-color: rgba(255, 212, 59, 0.15);
  color: #ffd43b;
  border: 1px solid rgba(255, 212, 59, 0.3);
}

.status-badge.queued {
  background-color: rgba(134, 142, 150, 0.15);
  color: #868e96;
  border: 1px solid rgba(134, 142, 150, 0.3);
}

.queue-actions {
  display: flex;
  gap: 0.5rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #adb5bd;
  background-color: #2a2a2a;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1.5rem;
  color: #495057;
}

.empty-icon svg {
  width: 80px;
  height: 80px;
}

.empty-state h2 {
  color: white;
  margin-bottom: 0.75rem;
  font-weight: 600;
  font-size: 1.5rem;
}

.empty-state p {
  color: #868e96;
  font-size: 1rem;
  line-height: 1.5;
  max-width: 400px;
  margin: 0 auto;
}

.loading-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #adb5bd;
  background-color: #2a2a2a;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.loading-state svg {
  font-size: 3rem;
  margin-bottom: 1rem;
  color: #4dabf7;
  width: 48px;
  height: 48px;
}

.loading-state i {
  font-size: 3rem;
  margin-bottom: 1rem;
  color: #4dabf7;
}

.loading-state p {
  font-size: 1.1rem;
  font-weight: 500;
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

/* Modal Styles */
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
  animation: fadeIn 0.2s ease;
  backdrop-filter: blur(4px);
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.modal-content {
  background-color: #2a2a2a;
  border-radius: 12px;
  width: 90%;
  max-width: 500px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
  animation: slideUp 0.3s ease;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

@keyframes slideUp {
  from {
    transform: translateY(20px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.modal-header h3 {
  margin: 0;
  color: white;
  font-size: 1.25rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.625rem;
}

.modal-header h3 svg {
  color: #ffd43b;
  width: 24px;
  height: 24px;
}

.modal-header h3 i {
  color: #ffd43b;
  font-size: 1.5rem;
}

.modal-close {
  background: none;
  border: none;
  color: #868e96;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
  font-size: 1.5rem;
  line-height: 1;
}

.modal-close:hover {
  background-color: rgba(255, 255, 255, 0.08);
  color: white;
}

.modal-close svg {
  width: 20px;
  height: 20px;
}

.modal-body {
  padding: 1.5rem;
}

.modal-body > p:first-child {
  color: #adb5bd;
  margin: 0 0 1rem 0;
  font-size: 1rem;
  line-height: 1.5;
}

.remove-item-info {
  background-color: rgba(0, 0, 0, 0.3);
  border-left: 4px solid #ffd43b;
  padding: 1rem;
  border-radius: 8px;
  margin-bottom: 1rem;
}

.remove-item-info strong {
  color: white;
  display: block;
  margin-bottom: 0.5rem;
  font-size: 1rem;
  font-weight: 600;
}

.item-details {
  display: flex;
  gap: 1rem;
  font-size: 0.875rem;
  color: #868e96;
  flex-wrap: wrap;
}

.item-details span {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.item-details svg {
  width: 14px;
  height: 14px;
}

.warning-text {
  color: #ffd43b;
  font-size: 0.875rem;
  display: flex;
  align-items: flex-start;
  gap: 0.625rem;
  background-color: rgba(255, 212, 59, 0.1);
  padding: 0.875rem;
  border-radius: 8px;
  margin: 0;
  border: 1px solid rgba(255, 212, 59, 0.25);
  line-height: 1.5;
}

.warning-text svg {
  flex-shrink: 0;
  margin-top: 0.1rem;
  width: 18px;
  height: 18px;
}

.warning-text i {
  flex-shrink: 0;
  margin-top: 0.1rem;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-danger {
  background: linear-gradient(135deg, #fa5252 0%, #e03131 100%);
  color: white;
  border: none;
  padding: 0.65rem 1.25rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s;
  font-size: 0.9rem;
  box-shadow: 0 2px 8px rgba(250, 82, 82, 0.3);
}

.btn-danger:hover:not(:disabled) {
  background: linear-gradient(135deg, #ff6b6b 0%, #fa5252 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(250, 82, 82, 0.4);
}

.btn-danger:active:not(:disabled) {
  transform: translateY(0);
}

.btn-danger:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn-danger svg {
  width: 16px;
  height: 16px;
}
</style>