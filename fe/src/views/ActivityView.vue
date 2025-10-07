<template>
  <div class="activity-view">
    <div class="page-header">
      <h1>
        <i class="ph ph-activity"></i>
        Activity
      </h1>
      <div class="activity-actions">
        <button class="btn btn-secondary" @click="refreshQueue" :disabled="loading">
          <i class="ph" :class="loading ? 'ph-spinner ph-spin' : 'ph-arrow-clockwise'"></i>
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
    <div v-if="filteredQueue.length > 0" class="queue-list">
      <div 
        v-for="item in filteredQueue" 
        :key="item.id"
        class="queue-item"
      >
        <div class="queue-icon">
          <i class="ph ph-download-simple"></i>
        </div>
        
        <div class="queue-info">
          <div class="queue-title-row">
            <h3 class="queue-title">{{ item.title }}</h3>
          </div>
          
          <div class="queue-meta">
            <span v-if="item.downloadClient" class="queue-client">
              <i class="ph ph-desktop"></i>
              {{ item.downloadClient }}
            </span>
            <span v-if="item.quality && item.quality !== '*'" class="queue-quality">{{ item.quality }}</span>
          </div>

          <div class="queue-progress-container">
            <div class="queue-stats-top">
              <span class="progress-text">{{ item.progress.toFixed(1) }}%</span>
              <span class="size-info">{{ formatSize(item.downloaded) }} / {{ formatSize(item.size) }}</span>
              <span v-if="item.downloadSpeed > 0" class="download-speed">
                <i class="ph ph-arrow-down"></i>
                {{ formatSpeed(item.downloadSpeed) }}
              </span>
              <span v-if="item.eta" class="eta">
                <i class="ph ph-clock"></i>
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
            <i class="ph ph-x"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div class="empty-state" v-else-if="!loading">
      <div class="empty-icon">
        <i class="ph ph-queue"></i>
      </div>
      <h2>No Active Downloads</h2>
      <p>Downloads will appear here when you send items to your download clients.</p>
    </div>

    <!-- Loading State -->
    <div class="loading-state" v-if="loading && queue.length === 0">
      <i class="ph ph-spinner ph-spin"></i>
      <p>Loading queue...</p>
    </div>

    <!-- Remove Confirmation Modal -->
    <div v-if="showRemoveModal" class="modal-overlay" @click="showRemoveModal = false">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>
            <i class="ph ph-warning-circle"></i>
            Remove from Queue
          </h3>
          <button class="modal-close" @click="showRemoveModal = false">
            <i class="ph ph-x"></i>
          </button>
        </div>
        <div class="modal-body">
          <p>Are you sure you want to remove this download?</p>
          <div class="remove-item-info">
            <strong>{{ itemToRemove?.title }}</strong>
            <div class="item-details">
              <span v-if="itemToRemove?.downloadClient">
                <i class="ph ph-desktop"></i>
                {{ itemToRemove.downloadClient }}
              </span>
              <span>
                <i class="ph ph-chart-bar"></i>
                {{ itemToRemove?.progress.toFixed(1) }}% complete
              </span>
            </div>
          </div>
          <p class="warning-text">
            <i class="ph ph-info"></i>
            This will remove the download from your download client. Files may or may not be deleted depending on your client settings.
          </p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showRemoveModal = false">
            Cancel
          </button>
          <button class="btn btn-danger" @click="confirmRemove" :disabled="removing">
            <i class="ph" :class="removing ? 'ph-spinner ph-spin' : 'ph-trash'"></i>
            {{ removing ? 'Removing...' : 'Remove' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { apiService } from '@/services/api'
import type { QueueItem } from '@/types'

const selectedTab = ref('all')
const queue = ref<QueueItem[]>([])
const loading = ref(false)
const showRemoveModal = ref(false)
const itemToRemove = ref<QueueItem | null>(null)
const removing = ref(false)
let pollInterval: number | undefined

const filterTabs = computed(() => [
  { label: 'All', value: 'all', count: queue.value.length },
  { label: 'Downloading', value: 'downloading', count: queue.value.filter(q => q.status === 'downloading').length },
  { label: 'Paused', value: 'paused', count: queue.value.filter(q => q.status === 'paused').length },
  { label: 'Queued', value: 'queued', count: queue.value.filter(q => q.status === 'queued').length },
  { label: 'Completed', value: 'completed', count: queue.value.filter(q => q.status === 'completed').length },
])

const filteredQueue = computed(() => {
  if (selectedTab.value === 'all') {
    return queue.value
  }
  return queue.value.filter(item => item.status === selectedTab.value)
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
    await apiService.removeFromQueue(itemToRemove.value.id, itemToRemove.value.downloadClientId)
    showRemoveModal.value = false
    itemToRemove.value = null
    await refreshQueue()
  } catch (err) {
    console.error('Failed to remove from queue:', err)
    alert('Failed to remove from queue: ' + (err as Error).message)
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

// Poll queue every 5 seconds
onMounted(() => {
  refreshQueue()
  pollInterval = window.setInterval(refreshQueue, 5000)
})

onUnmounted(() => {
  if (pollInterval) {
    clearInterval(pollInterval)
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

.page-header h1 {
  margin: 0;
  color: white;
  font-size: 2rem;
}

.activity-actions {
  display: flex;
  gap: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: background-color 0.2s;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover {
  background-color: #666;
}

.btn-icon {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.25rem;
}

.btn-icon:hover {
  background-color: #3a3a3a;
  color: white;
}

.btn-icon.btn-danger:hover {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
}

.activity-filters {
  margin-bottom: 2rem;
}

.filter-tabs {
  display: flex;
  gap: 0.5rem;
  border-bottom: 1px solid #3a3a3a;
}

.tab {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 1rem 1.5rem;
  border-radius: 4px 4px 0 0;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.tab:hover {
  background-color: #2a2a2a;
  color: white;
}

.tab.active {
  background-color: #007acc;
  color: white;
}

.tab-badge {
  background-color: rgba(255, 255, 255, 0.2);
  border-radius: 10px;
  padding: 0.2rem 0.5rem;
  font-size: 0.75rem;
  font-weight: bold;
}

.queue-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.queue-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: #2a2a2a;
  border-radius: 8px;
  border-left: 4px solid #007acc;
  transition: all 0.2s;
}

.queue-item:hover {
  background-color: #333;
  border-left-color: #0098ff;
}

.queue-icon {
  width: 40px;
  height: 40px;
  min-width: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #007acc;
  border-radius: 8px;
  color: white;
  font-size: 1.5rem;
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
  font-size: 1rem;
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
  font-size: 0.85rem;
  color: #999;
}

.queue-meta span {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.queue-client {
  color: #007acc;
}

.queue-quality {
  color: #27ae60;
  font-weight: 500;
}

.queue-progress-container {
  width: 100%;
}

.queue-stats-top {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  color: #ccc;
}

.progress-text {
  font-weight: 600;
  color: white;
}

.download-speed {
  color: #27ae60;
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.eta {
  color: #f39c12;
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.size-info {
  color: #999;
}

.progress-bar {
  width: 100%;
  height: 8px;
  background-color: #1a1a1a;
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.3s ease;
}

.progress-fill.downloading {
  background: linear-gradient(90deg, #007acc, #0098ff);
  animation: progress-shimmer 2s infinite;
}

.progress-fill.paused {
  background-color: #f39c12;
}

.progress-fill.completed {
  background-color: #27ae60;
}

.progress-fill.failed {
  background-color: #e74c3c;
}

.progress-fill.queued {
  background-color: #555;
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
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: bold;
  text-transform: uppercase;
}

.status-badge.completed {
  background-color: rgba(39, 174, 96, 0.2);
  color: #27ae60;
  border: 1px solid #27ae60;
}

.status-badge.downloading {
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid #007acc;
}

.status-badge.failed {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid #e74c3c;
}

.status-badge.paused {
  background-color: rgba(243, 156, 18, 0.2);
  color: #f39c12;
  border: 1px solid #f39c12;
}

.status-badge.queued {
  background-color: rgba(85, 85, 85, 0.2);
  color: #999;
  border: 1px solid #555;
}

.queue-actions {
  display: flex;
  gap: 0.5rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  color: #555;
}

.empty-state h2 {
  color: white;
  margin-bottom: 1rem;
}

.loading-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.loading-state i {
  font-size: 3rem;
  margin-bottom: 1rem;
  color: #007acc;
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
  background-color: rgba(0, 0, 0, 0.75);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  animation: fadeIn 0.2s ease;
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
  border-bottom: 1px solid #3a3a3a;
}

.modal-header h3 {
  margin: 0;
  color: white;
  font-size: 1.25rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.modal-header h3 i {
  color: #f39c12;
  font-size: 1.5rem;
}

.modal-close {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.5rem;
  line-height: 1;
}

.modal-close:hover {
  background-color: #3a3a3a;
  color: white;
}

.modal-body {
  padding: 1.5rem;
}

.modal-body > p:first-child {
  color: #ccc;
  margin: 0 0 1rem 0;
  font-size: 1rem;
}

.remove-item-info {
  background-color: #1a1a1a;
  border-left: 4px solid #f39c12;
  padding: 1rem;
  border-radius: 8px;
  margin-bottom: 1rem;
}

.remove-item-info strong {
  color: white;
  display: block;
  margin-bottom: 0.5rem;
  font-size: 1rem;
}

.item-details {
  display: flex;
  gap: 1rem;
  font-size: 0.875rem;
  color: #999;
}

.item-details span {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.warning-text {
  color: #f39c12;
  font-size: 0.875rem;
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  background-color: rgba(243, 156, 18, 0.1);
  padding: 0.75rem;
  border-radius: 6px;
  margin: 0;
  border: 1px solid rgba(243, 156, 18, 0.3);
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
  border-top: 1px solid #3a3a3a;
}

.btn-danger {
  background-color: #e74c3c;
  color: white;
  border: none;
  padding: 0.625rem 1.25rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s;
}

.btn-danger:hover:not(:disabled) {
  background-color: #c0392b;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.4);
}

.btn-danger:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>