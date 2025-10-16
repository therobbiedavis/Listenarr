<template>
  <div class="audiobook-detail" v-if="!loading && audiobook">
    <!-- Top Navigation Bar -->
    <div class="top-nav">
      <button class="nav-btn" @click="goBack">
        <i class="ph ph-arrow-left"></i>
        Back
      </button>
      <div class="nav-actions">
        <button class="nav-btn" @click="refresh">
          <i class="ph ph-arrow-clockwise"></i>
          Refresh
        </button>
        <button class="nav-btn" @click="toggleMonitored">
          <i class="ph ph-bookmark" :class="{ 'ph-fill': audiobook.monitored }"></i>
          {{ audiobook.monitored ? 'Monitored' : 'Monitor' }}
        </button>
        <!-- Scan button moved to top nav: enqueues a background scan and shows queued feedback -->
        <button class="nav-btn" :disabled="scanning || scanQueued" @click="scanFiles">
          <i v-if="scanning" class="ph ph-spinner ph-spin"></i>
          <i v-else-if="scanQueued" class="ph ph-clock"></i>
          <i v-else class="ph ph-magnifying-glass"></i>
          <span v-if="scanning">Scanning...</span>
          <span v-else-if="scanQueued">Scan queued</span>
          <span v-else>Scan Folder</span>
        </button>
        <button class="nav-btn delete-btn" @click="confirmDelete">
          <i class="ph ph-trash"></i>
          Delete
        </button>
      </div>
    </div>

    <!-- Hero Section -->
    <div class="hero-section">
      <div class="backdrop" :style="{ backgroundImage: `url(${apiService.getImageUrl(audiobook.imageUrl)})` }"></div>
      <div class="hero-content">
        <div class="poster-container">
          <img 
            :src="apiService.getImageUrl(audiobook.imageUrl) || `https://via.placeholder.com/300x450?text=No+Image`" 
            :alt="audiobook.title"
            class="poster"
          />
        </div>
        <div class="info-section">
          <h1 class="title">{{ audiobook.title }}</h1>
          <div class="subtitle" v-if="audiobook.subtitle">{{ audiobook.subtitle }}</div>
          
          <div class="meta-info">
            <span class="runtime" v-if="audiobook.runtime">
              <i class="ph ph-clock"></i>
              {{ formatRuntime(audiobook.runtime) }}
            </span>
            <span class="genre">{{ audiobook.genres?.join(', ') || 'Audiobook' }}</span>
            <span class="year" v-if="audiobook.publishYear">{{ audiobook.publishYear }}</span>
          </div>

          <div class="key-details">
            <div class="detail-item" v-if="audiobook.filePath">
              <i class="ph ph-folder"></i>
              <span>{{ audiobook.filePath }}</span>
            </div>
            <div class="detail-item" v-if="audiobook.fileSize">
              <i class="ph ph-database"></i>
              <span>{{ formatFileSize(audiobook.fileSize) }}</span>
            </div>
            <div class="detail-item" v-if="audiobook.quality">
              <i class="ph ph-speaker-high"></i>
              <span>{{ audiobook.quality }}</span>
            </div>
            <div class="detail-item" v-if="audiobook.language">
              <i class="ph ph-globe"></i>
              <span>{{ audiobook.language }}</span>
            </div>
            <div class="detail-item">
              <i class="ph ph-tag"></i>
              <span>{{ audiobook.abridged ? 'Abridged' : 'Unabridged' }}</span>
            </div>
          </div>

          <div class="status-badges">
            <span class="badge monitored" v-if="audiobook.monitored">
              <i class="ph ph-bookmark-fill"></i>
              Monitored
            </span>
            <span class="badge quality-profile" v-if="assignedProfileName">
              <i class="ph ph-star"></i>
              Quality: {{ assignedProfileName }}
            </span>
            <span class="badge language">
              <i class="ph ph-chat-circle"></i>
              {{ audiobook.language || 'English' }}
            </span>
            <span class="badge tlc" v-if="audiobook.version">
              <i class="ph ph-music-notes"></i>
              {{ audiobook.version }}
            </span>
          </div>

          <div class="description" v-if="audiobook.description">
            <div 
              class="description-content" 
              :class="{ expanded: showFullDescription }"
              v-html="audiobook.description"
            ></div>
            <button 
              v-if="!showFullDescription" 
              class="show-more-btn" 
              @click="showFullDescription = true"
            >
              Show More
            </button>
            <button 
              v-else 
              class="show-more-btn" 
              @click="showFullDescription = false"
            >
              Show Less
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Tabs Section -->
    <div class="tabs-container">
      <div class="tabs">
        <button 
          class="tab" 
          :class="{ active: activeTab === 'details' }"
          @click="activeTab = 'details'"
        >
          <i class="ph ph-info"></i>
          Details
        </button>
        <button 
          class="tab" 
          :class="{ active: activeTab === 'files' }"
          @click="activeTab = 'files'"
        >
          <i class="ph ph-file"></i>
          Files
        </button>
        <button 
          class="tab" 
          :class="{ active: activeTab === 'history' }"
          @click="activeTab = 'history'"
        >
          <i class="ph ph-clock-counter-clockwise"></i>
          History
        </button>
      </div>
    </div>

    <!-- Tab Content -->
    <div class="tab-content">
      <!-- Details Tab -->
        <div id="details" v-if="activeTab === 'details'" class="details-content">
        <div class="details-grid">
          <div class="detail-card">
            <h3>Author Information</h3>
            <div class="detail-row" v-if="audiobook.authors">
              <span class="label">Author(s):</span>
              <span class="value">{{ audiobook.authors.join(', ') }}</span>
            </div>
            <div class="detail-row" v-if="audiobook.narrators">
              <span class="label">Narrator(s):</span>
              <span class="value">{{ audiobook.narrators.join(', ') }}</span>
            </div>
          </div>

          <div class="detail-card">
            <h3>Publication Details</h3>
            <div class="detail-row" v-if="audiobook.publisher">
              <span class="label">Publisher:</span>
              <span class="value">{{ audiobook.publisher }}</span>
            </div>
            <div class="detail-row" v-if="audiobook.publishYear">
              <span class="label">Year:</span>
              <span class="value">{{ audiobook.publishYear }}</span>
            </div>
            <div class="detail-row" v-if="audiobook.language">
              <span class="label">Language:</span>
              <span class="value">{{ audiobook.language }}</span>
            </div>
          </div>

          <div class="detail-card" v-if="audiobook.series">
            <h3>Series Information</h3>
            <div class="detail-row">
              <span class="label">Series:</span>
              <span class="value">{{ audiobook.series }}</span>
            </div>
            <div class="detail-row" v-if="audiobook.seriesNumber">
              <span class="label">Book #:</span>
              <span class="value">{{ audiobook.seriesNumber }}</span>
            </div>
          </div>

          <div class="detail-card">
            <h3>Identifiers</h3>
            <div class="detail-row" v-if="audiobook.asin">
              <span class="label">ASIN:</span>
              <span class="value">{{ audiobook.asin }}</span>
            </div>
            <div class="detail-row" v-if="audiobook.isbn">
              <span class="label">ISBN:</span>
              <span class="value">{{ audiobook.isbn }}</span>
            </div>
          </div>

          <div class="detail-card" v-if="audiobook.genres && audiobook.genres.length">
            <h3>Genres</h3>
            <div class="genre-tags">
              <span v-for="genre in audiobook.genres" :key="genre" class="genre-tag">
                {{ genre }}
              </span>
            </div>
          </div>

          <div class="detail-card" v-if="audiobook.tags && audiobook.tags.length">
            <h3>Tags</h3>
            <div class="genre-tags">
              <span v-for="tag in audiobook.tags" :key="tag" class="genre-tag">
                {{ tag }}
              </span>
            </div>
          </div>
        </div>
      </div>

  <!-- Files Tab -->
  <div id="files" v-if="activeTab === 'files'" class="files-content">
        <div class="files-header">
          <h3>Files</h3>
          <div class="files-actions">
            <!-- Scan job status (updated via SignalR) -->
            <div v-if="scanJobId" class="scan-job-status">
              <div class="job-row">
                <i class="ph ph-clock"></i>
                <strong>Scan job:</strong>
                <span class="job-id">{{ scanJobId }}</span>
              </div>
              <div class="job-status">
                <span v-if="scanQueued" class="status queued">Queued / Processing</span>
                <span v-else class="status completed">No active scan</span>
              </div>
            </div>
          </div>
        </div>
        <div v-if="audiobook.files && audiobook.files.length" class="file-list">
          <div v-for="f in audiobook.files" :key="f.id" class="file-item" :class="{ 'expanded': isFileAccordionExpanded(f.id) }">
            <div class="file-header" @click="toggleFileAccordion(f.id)">
              <div class="file-info">
                <i class="ph ph-file-audio"></i>
                <span class="file-name">{{ getFileName(f.path) }}</span>
                <small class="file-meta">• {{ f.format ? f.format.toUpperCase() : '' }} {{ f.durationSeconds ? '• ' + formatDuration(f.durationSeconds) : '' }}</small>
              </div>
              <div class="file-actions">
                <span class="file-size" v-if="f.size">{{ formatFileSize(f.size) }}</span>
                <span class="file-size" v-else>Unknown size</span>
                <i class="ph ph-chevron-down accordion-toggle" :class="{ 'rotated': isFileAccordionExpanded(f.id) }"></i>
              </div>
            </div>
            <div v-if="isFileAccordionExpanded(f.id)" class="file-accordion">
              <table class="metadata-table">
                <tbody>
                  <tr v-if="f.path">
                    <td class="metadata-label">Path:</td>
                    <td class="metadata-value">{{ f.path }}</td>
                  </tr>
                  <tr v-if="f.size !== undefined">
                    <td class="metadata-label">Size:</td>
                    <td class="metadata-value">{{ formatFileSize(f.size) }}</td>
                  </tr>
                  <tr v-if="f.durationSeconds !== undefined">
                    <td class="metadata-label">Duration:</td>
                    <td class="metadata-value">{{ formatDuration(f.durationSeconds) }}</td>
                  </tr>
                  <tr v-if="f.format">
                    <td class="metadata-label">Format:</td>
                    <td class="metadata-value">{{ f.format.toUpperCase() }}</td>
                  </tr>
                  <tr v-if="f.container">
                    <td class="metadata-label">Container:</td>
                    <td class="metadata-value">{{ f.container }}</td>
                  </tr>
                  <tr v-if="f.codec">
                    <td class="metadata-label">Codec:</td>
                    <td class="metadata-value">{{ f.codec }}</td>
                  </tr>
                  <tr v-if="f.bitrate !== undefined">
                    <td class="metadata-label">Bitrate:</td>
                    <td class="metadata-value">{{ f.bitrate }} kbps</td>
                  </tr>
                  <tr v-if="f.sampleRate !== undefined">
                    <td class="metadata-label">Sample Rate:</td>
                    <td class="metadata-value">{{ f.sampleRate }} Hz</td>
                  </tr>
                  <tr v-if="f.channels !== undefined">
                    <td class="metadata-label">Channels:</td>
                    <td class="metadata-value">{{ f.channels }}</td>
                  </tr>
                  <tr v-if="f.createdAt">
                    <td class="metadata-label">Created:</td>
                    <td class="metadata-value">{{ formatDate(f.createdAt) }}</td>
                  </tr>
                  <tr v-if="f.source">
                    <td class="metadata-label">Source:</td>
                    <td class="metadata-value">{{ f.source }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
        <div v-else-if="audiobook.filePath" class="file-list">
          <div class="file-item">
            <div class="file-info">
              <i class="ph ph-file-audio"></i>
              <span class="file-name">{{ getFileName(audiobook.filePath) }}</span>
            </div>
            <span class="file-size" v-if="audiobook.fileSize">{{ formatFileSize(audiobook.fileSize) }}</span>
            <span class="file-size" v-else>Unknown size</span>
          </div>
        </div>
        <div v-else class="empty-files">
          <i class="ph ph-file-dashed"></i>
          <p>No files available</p>
          <p class="hint">This audiobook hasn't been downloaded yet</p>
        </div>
      </div>

  <!-- History Tab -->
  <div id="history" v-if="activeTab === 'history'" class="history-content">
        <div class="history-header">
          <h3>History</h3>
          <button v-if="historyEntries.length > 0" class="refresh-btn" @click="loadHistory" :disabled="historyLoading">
            <i class="ph ph-arrow-clockwise" :class="{ 'ph-spin': historyLoading }"></i>
            Refresh
          </button>
        </div>
        
        <!-- Loading State -->
        <div v-if="historyLoading" class="history-loading">
          <i class="ph ph-spinner ph-spin"></i>
          <p>Loading history...</p>
        </div>
        
        <!-- Error State -->
        <div v-else-if="historyError" class="history-error">
          <i class="ph ph-warning-circle"></i>
          <p>{{ historyError }}</p>
          <button class="retry-btn" @click="loadHistory">Retry</button>
        </div>
        
        <!-- History List -->
        <div v-else-if="historyEntries.length > 0" class="history-list">
          <div v-for="entry in historyEntries" :key="entry.id" class="history-entry">
            <div class="history-icon" :class="getEventTypeClass(entry.eventType)">
              <i :class="getEventIcon(entry.eventType)"></i>
            </div>
            <div class="history-details">
              <div class="history-event">
                <span class="event-type">{{ entry.eventType }}</span>
                <span v-if="entry.source" class="event-source">from {{ entry.source }}</span>
              </div>
              <div v-if="entry.message" class="history-message">{{ entry.message }}</div>
              <div class="history-time">{{ formatHistoryTime(entry.timestamp) }}</div>
            </div>
          </div>
        </div>
        
        <!-- Empty State -->
        <div v-else class="empty-history">
          <i class="ph ph-clock-counter-clockwise"></i>
          <p>No history available</p>
          <p class="hint">Activity for this audiobook will appear here</p>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Dialog -->
    <div v-if="showDeleteDialog" class="dialog-overlay" @click="cancelDelete">
      <div class="dialog" @click.stop>
        <div class="dialog-header">
          <h3>
            <i class="ph ph-warning"></i>
            Confirm Deletion
          </h3>
        </div>
        <div class="dialog-body">
          <p>Are you sure you want to delete <strong>{{ audiobook.title }}</strong>?</p>
          <p class="warning-text">This action cannot be undone. The audiobook data and cached images will be permanently removed.</p>
        </div>
        <div class="dialog-actions">
          <button class="dialog-btn cancel-btn" @click="cancelDelete">
            Cancel
          </button>
          <button class="dialog-btn confirm-btn" @click="executeDelete" :disabled="deleting">
            <i v-if="deleting" class="ph ph-spinner ph-spin"></i>
            <i v-else class="ph ph-trash"></i>
            {{ deleting ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>
    </div>
  </div>

  <!-- Loading State -->
  <div v-else-if="loading" class="loading-container">
    <i class="ph ph-spinner ph-spin"></i>
    <p>Loading audiobook details...</p>
  </div>

  <!-- Error State -->
  <div v-else-if="error" class="error-container">
    <i class="ph ph-warning-circle"></i>
    <h2>Error Loading Audiobook</h2>
    <p>{{ error }}</p>
    <button @click="goBack" class="back-btn">
      <i class="ph ph-arrow-left"></i>
      Back to Library
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useToast } from '@/services/toastService'
import type { Audiobook as AudiobookType } from '@/types'
import { useRoute, useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import type { Audiobook, History } from '@/types'

const route = useRoute()
const router = useRouter()
const libraryStore = useLibraryStore()

const audiobook = ref<Audiobook | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const activeTab = ref('details')
const showDeleteDialog = ref(false)
const deleting = ref(false)
const showFullDescription = ref(false)
const scanning = ref(false)
const scanQueued = ref(false)
const scanJobId = ref<string | null>(null)

// History state
const historyEntries = ref<History[]>([])
const historyLoading = ref(false)
const historyError = ref<string | null>(null)
const qualityProfiles = ref<import('@/types').QualityProfile[]>([])
const expandedFileAccordions = ref<Set<number>>(new Set())

const assignedProfileName = computed(() => {
  if (!audiobook.value) return null
  const id = audiobook.value.qualityProfileId
  if (!id) return null
  const p = qualityProfiles.value.find(q => q.id === id)
  return p ? p.name : null
})

// Watch for tab changes to load history when needed
watch(activeTab, async (newTab) => {
  if (newTab === 'history' && audiobook.value && historyEntries.value.length === 0) {
    await loadHistory()
  }
})

onMounted(async () => {
  await loadAudiobook()
  // subscribe to scan job updates
  signalRService.onScanJobUpdate((job) => {
    if (!audiobook.value) return
    if (String(job.audiobookId) !== String(audiobook.value.id)) return
    // update local job state
    scanJobId.value = job.jobId
    scanQueued.value = job.status === 'Queued' || job.status === 'Processing'
    // if completed or failed, clear queued flag when appropriate
    if (job.status === 'Completed' || job.status === 'Failed') {
      // small delay to allow AudiobookUpdate to arrive and merge files
      setTimeout(() => {
        scanQueued.value = false
      }, 500)
    }
  })
})

// If the URL contains a hash (#details/#files/#history) navigate to it
onMounted(() => {
  const hash = (route.hash || '').replace('#', '')
  if (hash === 'details' || hash === 'files' || hash === 'history') {
    activeTab.value = hash
    // small timeout to allow DOM to render
    // setTimeout(() => scrollToAnchor(hash), 150)
  }
})

// When the active tab changes update the hash and scroll
watch(activeTab, (newTab) => {
  if (!newTab) return
  try {
    history.replaceState(null, '', `#${newTab}`)
  } catch {}
  // Scroll to anchored section
  // setTimeout(() => scrollToAnchor(newTab), 120)
})

async function loadAudiobook() {
  loading.value = true
  error.value = null
  
  try {
    const id = parseInt(route.params.id as string)
    
    // If library is already loaded, find the audiobook
    if (libraryStore.audiobooks.length > 0) {
      const book = libraryStore.audiobooks.find(b => b.id === id)
      if (book) {
        audiobook.value = book
        await afterLoad()
      } else {
        error.value = 'Audiobook not found'
      }
    } else {
      // Load library first
      await libraryStore.fetchLibrary()
      const book = libraryStore.audiobooks.find(b => b.id === id)
      if (book) {
        audiobook.value = book
        await afterLoad()
      } else {
        error.value = 'Audiobook not found'
      }
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load audiobook'
    console.error('Failed to load audiobook:', err)
  } finally {
    loading.value = false
  }
}

// After loading audiobook, also fetch quality profiles so we can display the assigned profile
async function afterLoad() {
  await loadQualityProfilesForDetail()
}

async function loadQualityProfilesForDetail() {
  try {
    qualityProfiles.value = await apiService.getQualityProfiles()
  } catch (err) {
    console.warn('Failed to load quality profiles for detail view:', err)
  }
}

function goBack() {
  router.push('/audiobooks')
}

async function refresh() {
  await loadAudiobook()
  // Reload history if history tab is active
  if (activeTab.value === 'history') {
    await loadHistory()
  }
}

async function loadHistory() {
  if (!audiobook.value) return
  
  historyLoading.value = true
  historyError.value = null
  
  try {
    historyEntries.value = await apiService.getHistoryByAudiobookId(audiobook.value.id)
    console.log('Loaded history:', historyEntries.value)
  } catch (err) {
    historyError.value = err instanceof Error ? err.message : 'Failed to load history'
    console.error('Failed to load history:', err)
  } finally {
    historyLoading.value = false
  }
}

async function scanFiles() {
  if (!audiobook.value) return
  scanning.value = true
  scanQueued.value = false
  scanJobId.value = null
  try {
    const res = await apiService.scanAudiobook(audiobook.value.id) as { message: string; scannedPath?: string; found: number; created: number; audiobook?: AudiobookType; jobId?: string }
    console.log('Scan result:', res)
    // If backend enqueued the job it will return 202 Accepted with { jobId }
    if (res?.jobId) {
      scanQueued.value = true
      scanJobId.value = res.jobId
      // keep scanning spinner off - queued state shows separately
    }

    // If API returned updated audiobook (blocking fallback), apply it
    if (res?.audiobook) {
      audiobook.value = res.audiobook
    } else if (!scanQueued.value) {
      // If neither queued nor audiobook returned, refresh to pick up any changes
      await loadAudiobook()
    }
  } catch (err) {
    console.error('Scan failed:', err)
    // Show a non-blocking toast instead of an alert
    const toast = useToast()
    toast.error('Scan failed', (err instanceof Error ? err.message : String(err)))
  } finally {
    scanning.value = false
  }
}

// Watch library store for updates (SignalR pushes) and refresh audiobook object reactively
watch(() => libraryStore.audiobooks, () => {
  if (!audiobook.value) return
  const updated = libraryStore.audiobooks.find(b => b.id === audiobook.value!.id)
  if (updated) {
    // Merge fields to preserve reactivity where possible
    audiobook.value = { ...audiobook.value, ...updated }
    // If files were added, clear queued indicators
    if (scanQueued.value && updated.files && updated.files.length > 0) {
      scanQueued.value = false
      scanJobId.value = null
    }
  }
}, { deep: true })

function toggleMonitored() {
  if (audiobook.value) {
    const newMonitoredValue = !audiobook.value.monitored
    audiobook.value = { ...audiobook.value, monitored: newMonitoredValue }
    
    // Persist to API
    apiService.updateAudiobook(audiobook.value.id, { monitored: newMonitoredValue })
      .then(() => {
        console.log('Monitored status updated successfully')
      })
      .catch((err) => {
        console.error('Failed to update monitored status:', err)
        // Revert on error
        if (audiobook.value) {
          audiobook.value = { ...audiobook.value, monitored: !newMonitoredValue }
        }
      })
  }
}

function confirmDelete() {
  showDeleteDialog.value = true
}

function cancelDelete() {
  showDeleteDialog.value = false
}

async function executeDelete() {
  if (!audiobook.value) return
  
  deleting.value = true
  try {
    const success = await libraryStore.removeFromLibrary(audiobook.value.id)
    if (success) {
      // Navigate back to library after successful deletion
      router.push('/audiobooks')
    }
  } catch (err) {
    console.error('Delete failed:', err)
  } finally {
    deleting.value = false
    showDeleteDialog.value = false
  }
}

function formatRuntime(minutes: number): string {
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

function formatFileSize(bytes?: number): string {
  if (!bytes || bytes === 0) return 'Unknown'
  
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unitIndex = 0
  
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  
  return `${size.toFixed(1)} ${units[unitIndex]}`
}

function formatHistoryTime(timestamp: string): string {
  const date = new Date(timestamp)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMins / 60)
  const diffDays = Math.floor(diffHours / 24)
  
  if (diffMins < 1) return 'Just now'
  if (diffMins < 60) return `${diffMins} minute${diffMins !== 1 ? 's' : ''} ago`
  if (diffHours < 24) return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`
  if (diffDays < 7) return `${diffDays} day${diffDays !== 1 ? 's' : ''} ago`
  
  return date.toLocaleDateString('en-US', { 
    year: 'numeric', 
    month: 'short', 
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function getEventIcon(eventType: string): string {
  const icons: Record<string, string> = {
    'Added': 'ph ph-plus-circle',
    'Downloaded': 'ph ph-download',
    'Imported': 'ph ph-upload',
    'Deleted': 'ph ph-trash',
    'Updated': 'ph ph-pencil',
    'Monitored': 'ph ph-bookmark',
    'Unmonitored': 'ph ph-bookmark-simple',
    'Grabbed': 'ph ph-hand-grabbing',
    'Failed': 'ph ph-warning-circle',
    'File Added': 'ph ph-file-plus',
    'File Removed': 'ph ph-file-minus'
  }
  return icons[eventType] || 'ph ph-circle'
}

function getEventTypeClass(eventType: string): string {
  const classes: Record<string, string> = {
    'Added': 'event-success',
    'Downloaded': 'event-success',
    'Imported': 'event-info',
    'Deleted': 'event-danger',
    'Updated': 'event-info',
    'Monitored': 'event-info',
    'Unmonitored': 'event-warning',
    'Grabbed': 'event-info',
    'Failed': 'event-danger',
    'File Added': 'event-success',
    'File Removed': 'event-warning'
  }
  return classes[eventType] || 'event-default'
}

function getFileName(filePath?: string): string {
  if (!filePath) return 'Unknown'
  const parts = filePath.split(/[\\/]/)
  const fileName = parts[parts.length - 1]
  return fileName || 'Unknown'
}

function formatDuration(seconds?: number): string {
  if (!seconds || seconds <= 0) return ''
  const sec = Math.floor(seconds)
  const hrs = Math.floor(sec / 3600)
  const mins = Math.floor((sec % 3600) / 60)
  const s = sec % 60
  if (hrs > 0) return `${hrs}h ${mins}m ${s}s`
  if (mins > 0) return `${mins}m ${s}s`
  return `${s}s`
}

function isFileAccordionExpanded(fileId: number): boolean {
  return expandedFileAccordions.value.has(fileId)
}

function toggleFileAccordion(fileId: number): void {
  if (expandedFileAccordions.value.has(fileId)) {
    expandedFileAccordions.value.delete(fileId)
  } else {
    expandedFileAccordions.value.add(fileId)
  }
}

function formatDate(dateString?: string): string {
  if (!dateString) return 'Unknown'
  // Ensure the date is treated as UTC by appending 'Z' if not present
  const utcDateString = dateString.endsWith('Z') ? dateString : dateString + 'Z'
  const date = new Date(utcDateString)
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<style scoped>
.audiobook-detail {
  min-height: 100vh;
  background-color: #1a1a1a;
}

.top-nav {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
}

.nav-actions {
  display: flex;
  gap: 8px;
}

.nav-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.nav-btn:hover {
  background-color: #4a4a4a;
}

.nav-btn.delete-btn {
  background-color: #e74c3c;
  border-color: #c0392b;
}

.nav-btn.delete-btn:hover {
  background-color: #c0392b;
}

.hero-section {
  position: relative;
  padding: 40px 40px;
  overflow: hidden;
}

@media (max-width: 768px) {
  .hero-section {
    padding: 40px 20px;
  }
}

.backdrop {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-size: cover;
  background-position: center;
  filter: blur(20px) brightness(0.3);
  transform: scale(1.1);
}

.hero-content {
  position: relative;
  display: flex;
  gap: 40px;
  max-width: 1600px;
  margin: 0 auto;
  z-index: 1;
}

@media (min-width: 1200px) {
  .hero-content {
    gap: 40px;
  }
}

@media (max-width: 768px) {
  .hero-content {
    flex-direction: column;
    gap: 20px;
  }
}

.poster-container {
  flex-shrink: 0;
}

.poster {
  width: 350px;
  height: 350px;
  object-fit: cover;
  border-radius: 8px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6);
}

@media (max-width: 768px) {
  .poster {
    width: 250px;
    height: 250px;
  }
}

.info-section {
  flex: 1;
  color: #fff;
  min-width: 0;
}

.title {
  font-size: 3rem;
  font-weight: 700;
  margin: 0 0 12px 0;
  color: #fff;
  line-height: 1.2;
}

@media (max-width: 768px) {
  .title {
    font-size: 2rem;
  }
}

.subtitle {
  font-size: 1.4rem;
  color: #ccc;
  margin-bottom: 20px;
}

@media (max-width: 768px) {
  .subtitle {
    font-size: 1rem;
  }
}

.meta-info {
  display: flex;
  align-items: center;
  gap: 20px;
  margin-bottom: 24px;
  font-size: 15px;
  color: #ccc;
  flex-wrap: wrap;
}

.meta-info span {
  display: flex;
  align-items: center;
  gap: 4px;
}

.runtime i, .rating i {
  color: #007acc;
}

.key-details {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.detail-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background-color: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  font-size: 14px;
}

.detail-item span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-item i {
  color: #007acc;
}

.status-badges {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.badge {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 500;
}

.badge.monitored {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
  border: 1px solid #2ecc71;
}

.badge.quality-profile {
  background-color: rgba(46, 204, 113, 0.15);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.3);
  font-weight: 600;
  font-size: 14px;
}

.badge.profile {
  background-color: rgba(52, 152, 219, 0.12);
  color: #3498db;
  border: 1px solid rgba(52, 152, 219, 0.25);
}

.badge.continuing {
  background-color: rgba(52, 152, 219, 0.2);
  color: #3498db;
  border: 1px solid #3498db;
}

.badge.language {
  background-color: rgba(155, 89, 182, 0.2);
  color: #9b59b6;
  border: 1px solid #9b59b6;
}

.badge.tlc {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
  border: 1px solid #f1c40f;
}

.description {
  color: #ccc;
  line-height: 1.6;
  max-width: 900px;
  position: relative;
}

.description-content {
  position: relative;
  max-height: 140px;
  overflow: hidden;
  transition: max-height 0.3s ease;
}

@media (max-width: 768px) {
  .description-content {
    max-height: 100px;
  }
}

.description-content:not(.expanded)::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 40px;
  pointer-events: none;
}

.description-content:not(.expanded) {
  mask-image: linear-gradient(to bottom, white 70%, transparent 100%);
  -webkit-mask-image: linear-gradient(to bottom, white 70%, transparent 100%);
}

.description-content.expanded {
  max-height: none;
}

.show-more-btn {
  margin-top: 12px;
  padding: 8px 16px;
  background-color: rgba(0, 122, 204, 0.1);
  border: 1px solid #007acc;
  border-radius: 4px;
  color: #007acc;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.show-more-btn:hover {
  background-color: rgba(0, 122, 204, 0.2);
  transform: translateY(-1px);
}

.description :deep(p) {
  margin: 0 0 12px 0;
}

.description :deep(br) {
  display: block;
  margin: 8px 0;
}

.description :deep(strong),
.description :deep(b) {
  color: #fff;
  font-weight: 600;
}

.description :deep(em),
.description :deep(i) {
  font-style: italic;
}

.description :deep(a) {
  color: #007acc;
  text-decoration: none;
}

.description :deep(a:hover) {
  text-decoration: underline;
}

.description :deep(ul),
.description :deep(ol) {
  margin: 12px 0;
  padding-left: 24px;
}

.description :deep(li) {
  margin: 4px 0;
}

.tabs-container {
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
  padding: 0 40px;
}

@media (max-width: 768px) {
  .tabs-container {
    padding: 0 20px;
  }
}

.tabs {
  display: flex;
  gap: 4px;
  max-width: 1600px;
  margin: 0 auto;
}

.tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 12px 20px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: #999;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}

.tab:hover {
  color: #fff;
}

.tab.active {
  color: #007acc;
  border-bottom-color: #007acc;
}

.tab-content {
  padding: 40px 40px;
  max-width: 1600px;
  margin: 0 auto;
}

@media (max-width: 768px) {
  .tab-content {
    padding: 30px 20px;
  }
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
  gap: 24px;
}

@media (min-width: 1200px) {
  .details-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 768px) {
  .details-grid {
    grid-template-columns: 1fr;
  }
}

.detail-card {
  background-color: #2a2a2a;
  border: 1px solid #333;
  border-radius: 8px;
  padding: 20px;
}

.detail-card h3 {
  margin: 0 0 16px 0;
  color: #fff;
  font-size: 16px;
  border-bottom: 1px solid #333;
  padding-bottom: 12px;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid #333;
}

.detail-row:last-child {
  border-bottom: none;
}

.detail-row .label {
  color: #999;
  font-size: 14px;
}

.detail-row .value {
  color: #fff;
  font-size: 14px;
  text-align: right;
}

.genre-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.genre-tag {
  padding: 6px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 12px;
}

.files-content, .history-content {
  background-color: #2a2a2a;
  border: 1px solid #333;
  border-radius: 8px;
  padding: 20px;
}

.files-header, .history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 12px;
  border-bottom: 1px solid #333;
}

.files-header h3, .history-header h3 {
  margin: 0;
  color: #fff;
}

.action-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.action-btn:hover {
  background-color: #4a4a4a;
}

.file-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.file-item {
  display: flex;
  flex-direction: column;
  padding: 12px;
  background-color: #333;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.file-item.expanded {
  background-color: #3a3a3a;
}

.file-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
  width: 100%;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
  color: #fff;
  flex: 1;
}

.file-info i {
  font-size: 24px;
  color: #007acc;
}

.file-name {
  font-weight: 500;
}

.file-meta {
  color: #999;
}

.file-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.accordion-toggle {
  color: #999;
  transition: transform 0.2s ease;
  font-size: 16px;
}

.accordion-toggle.rotated {
  transform: rotate(180deg);
}

.file-accordion {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #444;
  animation: slideDown 0.2s ease-out;
}

@keyframes slideDown {
  from {
    opacity: 0;
    max-height: 0;
  }
  to {
    opacity: 1;
    max-height: 500px;
  }
}

.metadata-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.metadata-table tbody tr {
  border-bottom: 1px solid #444;
}

.metadata-table tbody tr:last-child {
  border-bottom: none;
}

.metadata-label {
  color: #999;
  padding: 8px 12px 8px 0;
  font-weight: 500;
  width: 120px;
  vertical-align: top;
}

.metadata-value {
  color: #fff;
  padding: 8px 0;
  word-break: break-word;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
  color: #fff;
}

.file-info i {
  font-size: 24px;
  color: #007acc;
}

.file-size {
  color: #999;
  font-size: 14px;
}

.empty-history {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #666;
}

.empty-history i {
  font-size: 48px;
  margin-bottom: 12px;
}

.empty-history .hint {
  font-size: 14px;
  color: #555;
  margin-top: 8px;
}

.empty-files {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #666;
}

.empty-files i {
  font-size: 48px;
  margin-bottom: 12px;
}

.empty-files .hint {
  font-size: 14px;
  color: #555;
  margin-top: 8px;
}

/* History Styles */
.history-loading, .history-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  color: #999;
}

.history-loading i {
  font-size: 36px;
  margin-bottom: 12px;
}

.history-error i {
  font-size: 36px;
  margin-bottom: 12px;
  color: #e74c3c;
}

.retry-btn, .refresh-btn {
  margin-top: 12px;
  padding: 8px 16px;
  background-color: #007acc;
  border: none;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.retry-btn:hover, .refresh-btn:hover {
  background-color: #005fa3;
}

.refresh-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.history-entry {
  display: flex;
  gap: 16px;
  padding: 16px;
  background-color: #333;
  border-radius: 8px;
  border-left: 3px solid #555;
  transition: transform 0.2s, box-shadow 0.2s;
}

.history-entry:hover {
  transform: translateX(4px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.history-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  flex-shrink: 0;
}

.history-icon i {
  font-size: 20px;
}

.event-success {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
}

.event-info {
  background-color: rgba(52, 152, 219, 0.2);
  color: #3498db;
}

.event-warning {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
}

.event-danger {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
}

.event-default {
  background-color: rgba(149, 165, 166, 0.2);
  color: #95a5a6;
}

.history-details {
  flex: 1;
}

.history-event {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.event-type {
  font-weight: 600;
  color: #fff;
  font-size: 14px;
}

.event-source {
  font-size: 12px;
  color: #999;
  padding: 2px 8px;
  background-color: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
}

.history-message {
  color: #ccc;
  font-size: 14px;
  margin-bottom: 8px;
  line-height: 1.4;
}

.history-time {
  color: #777;
  font-size: 12px;
}

.loading-container, .error-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  color: #ccc;
  background-color: #1a1a1a;
}

.loading-container i, .error-container i {
  font-size: 48px;
  margin-bottom: 16px;
}

.loading-container i {
  color: #007acc;
}

.error-container i {
  color: #e74c3c;
}

.error-container h2 {
  color: #fff;
  margin: 0 0 8px 0;
}

.back-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 20px;
  padding: 12px 24px;
  background-color: #007acc;
  border: none;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.back-btn:hover {
  background-color: #005fa3;
}

/* Delete Dialog */
.dialog-overlay {
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

.dialog {
  background-color: #2a2a2a;
  border-radius: 8px;
  border: 1px solid #444;
  width: 90%;
  max-width: 500px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.dialog-header {
  padding: 20px;
  border-bottom: 1px solid #444;
}

.dialog-header h3 {
  margin: 0;
  color: #fff;
  font-size: 18px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.dialog-header i {
  color: #f39c12;
}

.dialog-body {
  padding: 20px;
  color: #ccc;
}

.dialog-body p {
  margin: 0 0 12px 0;
  line-height: 1.5;
}

.dialog-body strong {
  color: #fff;
}

.warning-text {
  color: #f39c12;
  font-size: 14px;
}

.dialog-actions {
  padding: 20px;
  border-top: 1px solid #444;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.dialog-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
}

.cancel-btn {
  background-color: #3a3a3a;
  color: #fff;
}

.cancel-btn:hover {
  background-color: #4a4a4a;
}

.confirm-btn {
  background-color: #e74c3c;
  color: #fff;
}

.confirm-btn:hover:not(:disabled) {
  background-color: #c0392b;
}

.confirm-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

</style>

