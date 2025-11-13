<template>
  <div class="audiobooks-view">
    <!-- Top Toolbar -->
    <div class="toolbar">
      <div class="toolbar-left">
        <button class="toolbar-btn" :class="{ active: viewMode === 'grid' }" @click="toggleViewMode" title="Toggle view">
          <PhGridFour v-if="viewMode === 'grid'" />
          <PhList v-else />
        </button>
        <button class="toolbar-btn" @click="refreshLibrary">
          <PhArrowClockwise />
          Refresh
        </button>
        <button 
          v-if="selectedCount > 0" 
          class="toolbar-btn edit-btn"
          @click="showBulkEdit"
        >
          <PhPencil />
          Edit Selected
        </button>
        <button 
          v-if="selectedCount > 0" 
          class="toolbar-btn delete-btn"
          @click="confirmBulkDelete"
        >
          <PhTrash />
          Delete Selected ({{ selectedCount }})
        </button>
        <button 
          v-if="selectedCount > 0" 
          class="toolbar-btn"
          @click="libraryStore.clearSelection()"
        >
          Clear Selection
        </button>
        <button 
          v-if="audiobooks.length > 0 && selectedCount === 0" 
          class="toolbar-btn"
          @click="libraryStore.selectAll()"
        >
          <PhCheckSquare />
          Select All
        </button>
      </div>
      <div class="toolbar-right">
        <span v-if="audiobooks.length > 0" class="count-badge">
          {{ audiobooks.length }} book{{ audiobooks.length !== 1 ? 's' : '' }}
        </span>
      </div>
    </div>

    <!-- Audiobooks Grid -->
    <div v-if="loading" class="loading-state">
      <PhSpinner class="ph-spin" />
      <p>Loading audiobooks...</p>
    </div>
    
    <div v-else-if="error" class="error-state">
      <div class="error-icon">
        <PhWarningCircle />
      </div>
      <h2>Error Loading Library</h2>
      <p>{{ error }}</p>
      <button @click="refreshLibrary" class="retry-button">
        <PhArrowClockwise />
        Retry
      </button>
    </div>
    
    <div v-else-if="audiobooks.length === 0" class="empty-state">
      <div class="empty-icon">
        <PhBookOpen />
      </div>
      <template v-if="!hasRootFolderConfigured">
        <h2>Root Folder Not Configured</h2>
        <p>Please configure a root folder for your audiobook library in settings before adding audiobooks.</p>
        <router-link to="/settings" class="add-button">
          <PhGear />
          Go to Settings
        </router-link>
      </template>
      <template v-else>
        <h2>No Audiobooks Yet</h2>
        <p>Your library is empty. Add audiobooks to get started!</p>
        <router-link to="/add-new" class="add-button">
          <PhPlus />
          Add Audiobooks
        </router-link>
      </template>
    </div>
    
  <div v-else ref="scrollContainer" :class="['audiobooks-scroll-container', { 'has-selection': selectedCount > 0 }]" @scroll="updateVisibleRange">
      <div class="audiobooks-scroll-spacer" :style="{ height: `${totalHeight}px` }">
        <div v-if="viewMode === 'grid'" class="audiobooks-grid" :style="{ transform: `translateY(${topPadding}px)` }">
          <div 
            v-for="audiobook in visibleAudiobooks" 
            :key="audiobook.id"
            tabindex="0"
            @keydown.enter="navigateToDetail(audiobook.id)"
            v-memo="[audiobook.id, audiobook.monitored, libraryStore.isSelected(audiobook.id), getAudiobookStatus(audiobook)]"
            class="audiobook-item"
            :class="{ 
              selected: libraryStore.isSelected(audiobook.id),
              'status-no-file': getAudiobookStatus(audiobook) === 'no-file',
              'status-quality-mismatch': getAudiobookStatus(audiobook) === 'quality-mismatch',
              'status-quality-match': getAudiobookStatus(audiobook) === 'quality-match'
            }"
            @click="navigateToDetail(audiobook.id)"
          >
            <div class="selection-checkbox" @click.stop="handleCheckboxClick(audiobook, 0, $event)" @mousedown.prevent>
              <input
                type="checkbox"
                :checked="libraryStore.isSelected(audiobook.id)"
                @change="onCheckboxChange(audiobook, $event)"
                @keydown.space.prevent="handleCheckboxKeydown(audiobook, $event)"
              />
            </div>
            <div class="audiobook-poster-container">
              <img 
                :src="apiService.getImageUrl(audiobook.imageUrl) || `https://via.placeholder.com/300x450?text=No+Image`" 
                :alt="audiobook.title" 
                class="audiobook-poster"
                loading="lazy"
              />
              <div class="status-overlay">
                <div class="audiobook-title">{{ safeText(audiobook.title) }}</div>
                <div class="audiobook-author">{{ audiobook.authors?.map(author => safeText(author)).join(', ') || 'Unknown Author' }}</div>
                <div v-if="getQualityProfileName(audiobook.qualityProfileId)" class="quality-profile-badge">
                  <PhStar />
                  {{ getQualityProfileName(audiobook.qualityProfileId) }}
                </div>
                <div class="monitored-badge" :class="{ 'unmonitored': !audiobook.monitored }">
                  <component :is="audiobook.monitored ? PhEye : PhEyeSlash" />
                  {{ audiobook.monitored ? 'Monitored' : 'Unmonitored' }}
                </div>
              </div>
              <div class="action-buttons">
                <button 
                  class="action-btn edit-btn-small" 
                  @click.stop="openEditModal(audiobook)"
                  title="Edit"
                >
                  <PhPencil />
                </button>
                <button 
                  class="action-btn delete-btn-small" 
                  @click.stop="confirmDelete(audiobook)"
                  title="Delete"
                >
                  <PhTrash />
                </button>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="audiobooks-list" :style="{ transform: `translateY(${topPadding}px)` }">
          <div v-if="audiobooks.length > 0" class="list-header" :style="{ transform: `translateY(${topPadding}px)` }">
            <div class="col-select"> </div>
            <div class="col-cover">Cover</div>
            <div class="col-title">Title / Author</div>
            <div class="col-status">Status</div>
            <div class="col-actions">Actions</div>
          </div>
          <div
            v-for="audiobook in visibleAudiobooks"
            :key="`list-${audiobook.id}`"
            tabindex="0"
            @keydown.enter="navigateToDetail(audiobook.id)"
            class="audiobook-list-item"
            :class="{
              selected: libraryStore.isSelected(audiobook.id),
              'status-no-file': getAudiobookStatus(audiobook) === 'no-file',
              'status-quality-mismatch': getAudiobookStatus(audiobook) === 'quality-mismatch',
              'status-quality-match': getAudiobookStatus(audiobook) === 'quality-match',
              'status-downloading': getAudiobookStatus(audiobook) === 'downloading'
            }"
            @click="navigateToDetail(audiobook.id)"
          >
            <div class="selection-checkbox" @click.stop="handleCheckboxClick(audiobook, 0, $event)" @mousedown.prevent>
              <input
                type="checkbox"
                :checked="libraryStore.isSelected(audiobook.id)"
                @change="onCheckboxChange(audiobook, $event)"
                @keydown.space.prevent="handleCheckboxKeydown(audiobook, $event)"
              />
            </div>
            <img
              class="list-thumb"
              :src="apiService.getImageUrl(audiobook.imageUrl) || `https://via.placeholder.com/80x80?text=No+Image`"
              :alt="audiobook.title"
              loading="lazy"
            />
            <div class="list-details">
              <div class="audiobook-title">{{ safeText(audiobook.title) }}</div>
              <div class="audiobook-author">{{ audiobook.authors?.map(author => safeText(author)).join(', ') || 'Unknown Author' }}</div>
            </div>
            <div class="list-badges">
              <div v-if="getQualityProfileName(audiobook.qualityProfileId)" class="quality-profile-badge">
                <PhStar />
                {{ getQualityProfileName(audiobook.qualityProfileId) }}
              </div>
              <div class="monitored-badge" :class="{ 'unmonitored': !audiobook.monitored }">
                <component :is="audiobook.monitored ? PhEye : PhEyeSlash" />
                {{ audiobook.monitored ? 'Monitored' : 'Unmonitored' }}
              </div>
            </div>
            <div class="list-actions">
              <button class="action-btn edit-btn-small" @click.stop="openEditModal(audiobook)" title="Edit">
                <PhPencil />
              </button>
              <button class="action-btn delete-btn-small" @click.stop="confirmDelete(audiobook)" title="Delete">
                <PhTrash />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Dialog -->
    <div v-if="showDeleteDialog" class="dialog-overlay" @click="cancelDelete">
      <div class="dialog" @click.stop>
        <div class="dialog-header">
          <h3>
            <PhWarning />
            Confirm Deletion
          </h3>
        </div>
        <div class="dialog-body">
          <p v-if="deleteTarget">
            Are you sure you want to delete <strong>{{ deleteTarget.title }}</strong>?
          </p>
          <p v-else-if="bulkDeleteCount > 0">
            Are you sure you want to delete <strong>{{ bulkDeleteCount }} audiobook{{ bulkDeleteCount !== 1 ? 's' : '' }}</strong>?
          </p>
          <p class="warning-text">This action cannot be undone. The audiobook data and cached images will be permanently removed.</p>
        </div>
        <div class="dialog-actions">
          <button class="dialog-btn cancel-btn" @click="cancelDelete">
            Cancel
          </button>
          <button class="dialog-btn confirm-btn" @click="executeDelete" :disabled="deleting">
            <component v-if="deleting" :is="PhSpinner" class="ph-spin" />
            <PhTrash v-else />
            {{ deleting ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Bulk Edit Modal -->
    <BulkEditModal
      :is-open="showBulkEditModal"
      :selected-count="selectedCount"
      :selected-ids="libraryStore.selectedIds"
      @close="closeBulkEdit"
      @saved="handleBulkEditSaved"
    />

    <!-- Edit Audiobook Modal -->
    <EditAudiobookModal
      :is-open="showEditModal"
      :audiobook="editAudiobook"
      @close="closeEditModal"
      @saved="handleEditSaved"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { PhGridFour, PhList, PhArrowClockwise, PhPencil, PhTrash, PhCheckSquare, PhBookOpen, PhGear, PhPlus, PhStar, PhEye, PhEyeSlash, PhSpinner, PhWarningCircle, PhWarning } from '@phosphor-icons/vue'
import { useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { useConfigurationStore } from '@/stores/configuration'
import { useDownloadsStore } from '@/stores/downloads'
import { apiService } from '@/services/api'
import BulkEditModal from '@/components/BulkEditModal.vue'
import EditAudiobookModal from '@/components/EditAudiobookModal.vue'
import type { Audiobook, QualityProfile } from '@/types'
import { safeText } from '@/utils/textUtils'

const router = useRouter()
const libraryStore = useLibraryStore()
const configStore = useConfigurationStore()
const downloadsStore = useDownloadsStore()

const audiobooks = computed(() => libraryStore.audiobooks || [])
const loading = computed(() => libraryStore.loading)
const error = computed(() => libraryStore.error)
const selectedCount = computed(() => libraryStore.selectedIds.size)
const hasRootFolderConfigured = computed(() => {
  return configStore.applicationSettings?.outputPath && 
         configStore.applicationSettings.outputPath.trim().length > 0
})

// Virtual scrolling supporting grid and list layouts
const scrollContainer = ref<HTMLElement | null>(null)
const ITEMS_PER_ROW = ref(4) // Will be recalculated for grid; list uses 1
const LIST_ROW_HEIGHT = 80
const GRID_ROW_HEIGHT = 320 // item height + gap for grid
const BUFFER_ROWS = 2 // Extra rows to render above and below viewport

const viewMode = ref<'grid' | 'list'>('grid')

const visibleRange = ref({ start: 0, end: 20 }) // Initially show first 20 items

const visibleAudiobooks = computed(() => {
  return audiobooks.value.slice(visibleRange.value.start, visibleRange.value.end)
})

function getRowHeight() {
  return viewMode.value === 'grid' ? GRID_ROW_HEIGHT : LIST_ROW_HEIGHT
}

// Update visible range based on scroll position
const updateVisibleRange = () => {
  if (!scrollContainer.value) return

  const scrollTop = scrollContainer.value.scrollTop
  const viewportHeight = scrollContainer.value.clientHeight

  const rowHeight = getRowHeight()

  // Calculate which rows are visible
  const firstVisibleRow = Math.floor(scrollTop / rowHeight)
  const visibleRowCount = Math.ceil(viewportHeight / rowHeight)

  // Items per row already set (1 for list, >1 for grid)
  const totalRows = Math.ceil(audiobooks.value.length / ITEMS_PER_ROW.value)
  const startRow = Math.max(0, firstVisibleRow - BUFFER_ROWS)
  const endRow = Math.min(firstVisibleRow + visibleRowCount + BUFFER_ROWS, totalRows)

  // Convert to item indices
  const startIndex = startRow * ITEMS_PER_ROW.value
  const endIndex = Math.min(endRow * ITEMS_PER_ROW.value, audiobooks.value.length)

  visibleRange.value = { start: startIndex, end: endIndex }
}

// Calculate total height for proper scrollbar
const totalHeight = computed(() => {
  const totalRows = Math.ceil(audiobooks.value.length / ITEMS_PER_ROW.value)
  return totalRows * getRowHeight()
})

// Padding for offset positioning
const topPadding = computed(() => {
  const firstVisibleRow = Math.floor(visibleRange.value.start / ITEMS_PER_ROW.value)
  return firstVisibleRow * getRowHeight()
})

const showDeleteDialog = ref(false)
const deleteTarget = ref<Audiobook | null>(null)
const bulkDeleteCount = ref(0)
const deleting = ref(false)
const qualityProfiles = ref<QualityProfile[]>([])
const showBulkEditModal = ref(false)
const showEditModal = ref(false)
const editAudiobook = ref<Audiobook | null>(null)
const lastClickedIndex = ref<number | null>(null)

// Get the download status for an audiobook
// Returns:
// - 'downloading': Currently being downloaded (blue border)
// - 'no-file': No file downloaded yet (red border)
// - 'quality-mismatch': Has file but doesn't meet quality cutoff (blue border)
// - 'quality-match': Has file and meets quality cutoff (green border)
function getAudiobookStatus(audiobook: Audiobook): 'downloading' | 'no-file' | 'quality-mismatch' | 'quality-match' {
  // Check if this audiobook is currently being downloaded
  const isDownloading = downloadsStore.activeDownloads.some(d => d.audiobookId === audiobook.id)
  if (isDownloading) {
    return 'downloading'
  }

  // If there are no files at all, treat as no-file
  if (!audiobook.files || audiobook.files.length === 0) {
    return 'no-file'
  }

  const profile = qualityProfiles.value.find(p => p.id === audiobook.qualityProfileId)

  // If no profile or no preferredFormats defined, fall back to the simple existing behavior
  if (!profile) {
    const hasFile = audiobook.filePath && audiobook.fileSize && audiobook.fileSize > 0
    return hasFile ? 'quality-match' : 'no-file'
  }

  // Helper: normalize strings
  const normalize = (s?: string) => (s || '').toString().toLowerCase()

  // Find any file that matches one of the profile's preferred formats
  const preferredFormats = (profile.preferredFormats || []).map(f => normalize(f))

  // If no preferred formats configured, treat any file as a candidate
  const candidateFiles = audiobook.files.filter(f => {
    if (!f) return false
    const fileFormat = normalize(f.format) || normalize(f.container) || ''
    if (preferredFormats.length === 0) return true
    return preferredFormats.includes(fileFormat) || preferredFormats.some(pf => fileFormat.includes(pf))
  })

  if (candidateFiles.length === 0) {
    // No files in preferred formats - treat as no-file (or could be considered mismatch)
    return 'no-file'
  }

  // If no cutoff defined, assume match
  if (!profile.cutoffQuality || !profile.qualities || profile.qualities.length === 0) {
    return 'quality-match'
  }

  // Build a map of quality -> priority for quick lookup
  const qualityPriority = new Map<string, number>()
  for (const q of profile.qualities) {
    if (!q || !q.quality) continue
    qualityPriority.set(normalize(q.quality), q.priority)
  }

  const cutoff = normalize(profile.cutoffQuality)
  const cutoffPriority = qualityPriority.has(cutoff) ? qualityPriority.get(cutoff)! : Number.POSITIVE_INFINITY

  // Helper to derive a quality string for a given file/audiobook
  type FileInfo = {
    bitrate?: number | string
    container?: string
    codec?: string
    format?: string
  }

  function deriveQualityLabel(file: FileInfo | undefined): string {
    // Prefer the denormalized audiobook.quality if present
    if (audiobook.quality) return normalize(audiobook.quality)

    if (file && file.bitrate) {
      const br = Number(file.bitrate)
      if (!isNaN(br)) {
        if (br >= 320) return '320kbps'
        if (br >= 256) return '256kbps'
        if (br >= 192) return '192kbps'
        return `${Math.round(br)}kbps`
      }
    }

    // If container or codec suggests lossless
    const container = normalize(file?.container)
    const codec = normalize(file?.codec)
    if (container.includes('flac') || codec.includes('flac') || codec.includes('alac') || codec.includes('wav')) {
      return 'lossless'
    }

    // Fallback: use format string
    if (file && file.format) return normalize(file.format)

    return ''
  }

  // If any candidate file meets or exceeds the cutoff (lower priority number == better), return match
  for (const f of candidateFiles) {
    const label = deriveQualityLabel(f)
    if (!label) continue
    const p = qualityPriority.has(label) ? qualityPriority.get(label)! : Number.POSITIVE_INFINITY
    if (p <= cutoffPriority) {
      return 'quality-match'
    }
  }

  // Otherwise at least one preferred-format file exists but doesn't meet cutoff
  return 'quality-mismatch'
}

onMounted(async () => {
  await Promise.all([
    libraryStore.fetchLibrary(),
    configStore.loadApplicationSettings(),
    loadQualityProfiles()
  ])
  
  // Calculate items per row based on container width
  if (scrollContainer.value) {
    const minItemWidth = 180
    const gap = 20

    const recalcItemsPerRow = () => {
      if (!scrollContainer.value) return
      const containerWidth = scrollContainer.value.clientWidth - 40 // Subtract padding
      const newItems = viewMode.value === 'list' ? 1 : Math.floor((containerWidth + gap) / (minItemWidth + gap)) || 1
      if (newItems !== ITEMS_PER_ROW.value) {
        ITEMS_PER_ROW.value = newItems
      }
    }

    // Initial calculation
    recalcItemsPerRow()
    // Initialize visible range
    updateVisibleRange()

    // Add resize observer to recalculate on window resize
    const resizeObserver = new ResizeObserver(() => {
      // Guard against null - element may be unmounted during navigation
      if (!scrollContainer.value) return
      recalcItemsPerRow()
      updateVisibleRange()
    })
    resizeObserver.observe(scrollContainer.value)

    // Watch for view mode changes to recalc item layout
    const stopWatch = watch(viewMode, async () => {
      recalcItemsPerRow()
      // wait a tick for layout to update then recalc range
      await nextTick()
      updateVisibleRange()
    })

    // Clean up observer when component unmounts
    onUnmounted(() => {
      resizeObserver.disconnect()
      stopWatch()
    })
  }
})

async function loadQualityProfiles() {
  try {
    qualityProfiles.value = await apiService.getQualityProfiles()
  } catch (error) {
    console.warn('Failed to load quality profiles:', error)
  }
}

  function getQualityProfileName(profileId?: number): string | null {
  if (!profileId) return null
  const profile = qualityProfiles.value.find(p => p.id === profileId)
  return profile?.name ?? null
}

function navigateToDetail(id: number) {
  router.push(`/audiobooks/${id}`)
}

function toggleViewMode() {
  viewMode.value = viewMode.value === 'grid' ? 'list' : 'grid'
}

async function refreshLibrary() {
  await libraryStore.fetchLibrary()
}

function confirmDelete(audiobook: Audiobook) {
  deleteTarget.value = audiobook
  bulkDeleteCount.value = 0
  showDeleteDialog.value = true
}

function confirmBulkDelete() {
  deleteTarget.value = null
  bulkDeleteCount.value = libraryStore.selectedIds.size
  showDeleteDialog.value = true
}

function cancelDelete() {
  showDeleteDialog.value = false
  deleteTarget.value = null
  bulkDeleteCount.value = 0
}

async function executeDelete() {
  deleting.value = true
  try {
    if (deleteTarget.value) {
      // Single delete
      await libraryStore.removeFromLibrary(deleteTarget.value.id)
    } else if (bulkDeleteCount.value > 0) {
      // Bulk delete
      const idsToDelete = Array.from(libraryStore.selectedIds)
      await libraryStore.bulkRemoveFromLibrary(idsToDelete)
    }
    showDeleteDialog.value = false
    deleteTarget.value = null
    bulkDeleteCount.value = 0
  } catch (err) {
    console.error('Delete failed:', err)
  } finally {
    deleting.value = false
  }
}

function showBulkEdit() {
  showBulkEditModal.value = true
}

function closeBulkEdit() {
  showBulkEditModal.value = false
}

async function handleBulkEditSaved() {
  // Refresh library to show updated data
  await libraryStore.fetchLibrary()
  // Clear selection after successful bulk edit
  libraryStore.clearSelection()
}

function openEditModal(audiobook: Audiobook) {
  // Always get the latest audiobook from the store to ensure we have the most recent data
  // This is important after edits that update the audiobook (like quality profile changes)
  const freshAudiobook = libraryStore.audiobooks.find(book => book.id === audiobook.id)
  editAudiobook.value = freshAudiobook || audiobook
  showEditModal.value = true
}

function closeEditModal() {
  showEditModal.value = false
  editAudiobook.value = null
}

async function handleEditSaved() {
  // Refresh library to show updated data
  await libraryStore.fetchLibrary()
  
  // Update the editAudiobook reference with the fresh data
  if (editAudiobook.value) {
    const updated = libraryStore.audiobooks.find(book => book.id === editAudiobook.value!.id)
    if (updated) {
      editAudiobook.value = updated
    }
  }
}

function handleCheckboxClick(audiobook: Audiobook, virtualIndex: number, event: MouseEvent) {
  event.preventDefault() // Prevent browser text selection
  
  // Get the actual index from the full audiobooks array
  const currentIndex = audiobooks.value.findIndex(book => book.id === audiobook.id)
  
  if (event.shiftKey && lastClickedIndex.value !== null) {
    // Shift+click: select range
    const startIndex = Math.min(lastClickedIndex.value, currentIndex)
    const endIndex = Math.max(lastClickedIndex.value, currentIndex)
    
    // Clear current selection and select the range
    libraryStore.clearSelection()
    for (let i = startIndex; i <= endIndex; i++) {
      const book = audiobooks.value[i]
      if (!book) continue
      libraryStore.toggleSelection(book.id)
    }
  } else {
    // Regular click: toggle selection
    libraryStore.toggleSelection(audiobook.id)
  }
  
  // Update last clicked index
  lastClickedIndex.value = currentIndex
}

function onCheckboxChange(audiobook: Audiobook, event: Event) {
  // Handle native input change (e.g. mouse click)
  const currentIndex = audiobooks.value.findIndex(book => book.id === audiobook.id)

  // Support Shift+click range selection when available
  const shift = (event as MouseEvent | KeyboardEvent).shiftKey
  if (shift && lastClickedIndex.value !== null) {
    const startIndex = Math.min(lastClickedIndex.value, currentIndex)
    const endIndex = Math.max(lastClickedIndex.value, currentIndex)
    libraryStore.clearSelection()
    for (let i = startIndex; i <= endIndex; i++) {
      const book = audiobooks.value[i]
      if (!book) continue
      libraryStore.toggleSelection(book.id)
    }
  } else {
    libraryStore.toggleSelection(audiobook.id)
  }

  lastClickedIndex.value = currentIndex
}

function handleCheckboxKeydown(audiobook: Audiobook, event: KeyboardEvent) {
  // Handle keyboard spacebar toggle and support Shift+Space range selection
  const currentIndex = audiobooks.value.findIndex(book => book.id === audiobook.id)
  if (event.shiftKey && lastClickedIndex.value !== null) {
    const startIndex = Math.min(lastClickedIndex.value, currentIndex)
    const endIndex = Math.max(lastClickedIndex.value, currentIndex)
    libraryStore.clearSelection()
    for (let i = startIndex; i <= endIndex; i++) {
      const book = audiobooks.value[i]
      if (!book) continue
      libraryStore.toggleSelection(book.id)
    }
  } else {
    libraryStore.toggleSelection(audiobook.id)
  }

  lastClickedIndex.value = currentIndex
}
</script>

<style scoped>
.audiobooks-view {
  margin-top: 60px; /* Add margin to account for fixed toolbar */
  background-color: #1a1a1a;
  min-height: calc(100vh - 120px);
}

.toolbar {
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

@media (max-width: 768px) {
  .toolbar {
    left: 0; /* Full width on mobile */
  }
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-btn {
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

.toolbar-btn:hover {
  background-color: #4a4a4a;
}

.toolbar-btn.active {
  background-color: #007acc;
  border-color: #007acc;
}

.toolbar-btn.edit-btn {
  background-color: #3498db;
  border-color: #2980b9;
}

.toolbar-btn.edit-btn:hover {
  background-color: #2980b9;
}

.toolbar-btn.delete-btn {
  background-color: #e74c3c;
  border-color: #c0392b;
}

.toolbar-btn.delete-btn:hover {
  background-color: #c0392b;
}

.count-badge {
  padding: 6px 12px;
  background-color: #3a3a3a;
  border-radius: 4px;
  color: #ccc;
  font-size: 12px;
}

.audiobooks-scroll-container {
  height: calc(100vh - 130px); /* Account for toolbar and header */
  overflow-y: auto;
  overflow-x: hidden;
  padding: 0 20px;
}

.audiobooks-scroll-spacer {
  position: relative;
  width: 100%;
}

.audiobooks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 20px;
  padding: 10px 0;
  user-select: none;
  -webkit-user-select: none;
  -moz-user-select: none;
  -ms-user-select: none;
  will-change: transform;
}

.audiobook-item {
  cursor: pointer;
  transition: transform 0.2s ease;
  position: relative;
}

.audiobook-item:hover {
  transform: scale(1.05);
}

.audiobook-item.selected .audiobook-poster-container {
  outline: 3px solid #007acc;
  outline-offset: 2px;
}

.audiobook-item.status-no-file .audiobook-poster-container {
  border-bottom: 3px solid #e74c3c;
}

.audiobook-item.status-downloading .audiobook-poster-container {
  border-bottom: 3px solid #3498db;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% {
    border-bottom-color: #3498db;
  }
  50% {
    border-bottom-color: #5dade2;
  }
}

.audiobook-item.status-quality-mismatch .audiobook-poster-container {
  border-bottom: 3px solid #f39c12;
}

.audiobook-item.status-quality-match .audiobook-poster-container {
  border-bottom: 3px solid #2ecc71;
}


.selection-checkbox {
  /* default used in grid; overridden in list below */
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 10;
  height: 22px;
  width: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  box-sizing: border-box;
  background-color: rgba(0, 0, 0, 0.45);
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.12s ease;
  opacity: 0;
  user-select: none;
  -webkit-user-select: none;
  -moz-user-select: none;
  -ms-user-select: none;
}
/* Hide the native input visually but keep it accessible and interactive */
.selection-checkbox input[type="checkbox"] {
  position: absolute;
  inset: 0;
  margin: 0;
  padding: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  cursor: pointer;
  z-index: 3;
}

/* Draw a custom box and checkmark using container pseudo-elements */
.selection-checkbox::before {
  content: '';
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: 14px;
  height: 14px;
  border-radius: 3px;
  border: 2px solid rgba(255,255,255,0.14);
  background: transparent;
  box-sizing: border-box;
  transition: border-color 0.12s ease, background-color 0.12s ease, box-shadow 0.12s ease;
  z-index: 1;
}

.selection-checkbox::after {
  content: '';
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%) rotate(45deg) scale(0);
  width: 6px;
  height: 10px;
  border-right: 2px solid transparent;
  border-bottom: 2px solid transparent;
  z-index: 2;
  transition: transform 0.12s ease, border-color 0.12s ease;
}

/* Hide the old pseudo-element checkmark (we now use the native control) */
.selection-checkbox::after {
  display: none !important;
}

.selection-checkbox:hover {
  background-color: rgba(0, 0, 0, 0.6);
  border-color: rgba(255,255,255,0.18);
}

/* Custom checkmark */


/* Remove container hover darkening when focusing the native checkbox so contrast stays good */
.selection-checkbox:hover input[type="checkbox"] {
  transform: translateY(0);
}

/* Only show checkbox when hovered or selected */

.audiobook-item:hover .selection-checkbox,
.audiobook-item.selected .selection-checkbox,
.audiobook-list-item:hover .selection-checkbox,
.audiobook-list-item.selected .selection-checkbox,
.audiobooks-scroll-container.has-selection .selection-checkbox {
  opacity: 1;
}

/* When the item is selected, style the custom box and show the check */
.audiobook-item.selected .selection-checkbox::before,
.audiobook-list-item.selected .selection-checkbox::before {
  background-color: #2196F3;
  border-color: #2196F3;
  box-shadow: 0 0 0 4px rgba(33,150,243,0.12);
}

.audiobook-item.selected .selection-checkbox::after,
.audiobook-list-item.selected .selection-checkbox::after {
  border-right-color: #fff;
  border-bottom-color: #fff;
  transform: translate(-50%, -50%) rotate(45deg) scale(1);
}

/* Checked state for grid and list rows */
.audiobook-item.selected .selection-checkbox input[type="checkbox"],
.audiobook-list-item.selected .selection-checkbox input[type="checkbox"] {
  /* keep native checked UI; add slight background for custom look */
  background-color: transparent;
}

/* Focus outlines for keyboard navigation */
.selection-checkbox input[type="checkbox"]:focus-visible {
  outline: 2px solid rgba(0,122,204,0.9);
  outline-offset: 2px;
}

.audiobook-list-item:focus, .audiobook-list-item:focus-within,
.audiobook-item:focus, .audiobook-item:focus-within {
  outline: 2px solid rgba(0,122,204,0.18);
  outline-offset: 2px;
  background-color: rgba(255,255,255,0.02);
}

/* List-specific override for the checkbox so it participates in the grid */
.audiobooks-list .selection-checkbox {
  position: relative;
  top: auto;
  left: auto;
  z-index: auto;
  height: 20px;
  width: 20px;
  margin: 0;
  background-color: rgba(0,0,0,0.0);
  border: 1px solid rgba(255,255,255,0.06);
  display: flex;
  align-items: center;
  justify-content: center;
}

.audiobooks-list .selection-checkbox {
  justify-self: center;
}

.audiobooks-list .selection-checkbox::after {
  left: 6px;
  top: 2px;
}

.audiobook-poster-container {
  position: relative;
  aspect-ratio: 1/1;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
}

.audiobook-poster {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.status-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: linear-gradient(transparent, rgba(0, 0, 0, 0.9));
  padding: 8px;
  transition: padding 0.2s ease;
}

.audiobook-poster-container:hover .status-overlay {
  padding: 80px 8px 8px;
}

.audiobook-title {
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.audiobook-author {
  font-size: 11px;
  color: #ccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.audiobook-poster-container:hover .audiobook-title,
.audiobook-poster-container:hover .audiobook-author {
  opacity: 1;
}

.quality-profile-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  margin-top: 0.5rem;
  padding: 0.25rem 0.5rem;
  margin-right: 0.5rem;
  background-color: rgba(52, 152, 219, 0.2);
  border: 1px solid rgba(52, 152, 219, 0.4);
  border-radius: 8px;
  font-size: 10px;
  font-weight: 600;
  color: #3498db;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.quality-profile-badge i {
  font-size: 12px;
  flex-shrink: 0;
}

.monitored-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  margin-top: 0.5rem;
  padding: 0.25rem 0.5rem;
  margin-left: 0.25rem;
  background-color: rgba(46, 204, 113, 0.2);
  border: 1px solid rgba(46, 204, 113, 0.4);
  border-radius: 8px;
  font-size: 10px;
  font-weight: 600;
  color: #2ecc71;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.monitored-badge.unmonitored {
  background-color: rgba(231, 76, 60, 0.2);
  border-color: rgba(231, 76, 60, 0.4);
  color: #e74c3c;
}

.monitored-badge i {
  font-size: 12px;
  flex-shrink: 0;
}

.action-buttons {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}

.audiobook-item:hover .action-buttons {
  opacity: 1;
}

.action-btn {
  padding: 6px 8px;
  background-color: rgba(0, 0, 0, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.action-btn:hover {
  background-color: rgba(0, 0, 0, 0.95);
}

.delete-btn-small {
  background-color: rgba(231, 76, 60, 0.9);
  border-color: rgba(192, 57, 43, 0.5);
}

.delete-btn-small:hover {
  background-color: rgba(192, 57, 43, 1);
}

.edit-btn-small {
  background-color: rgba(52, 152, 219, 0.9);
  border-color: rgba(41, 128, 185, 0.5);
}

.edit-btn-small:hover {
  background-color: rgba(41, 128, 185, 1);
}

.loading-state, .empty-state, .error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
  color: #ccc;
  text-align: center;
}

.loading-state i, .empty-icon, .error-icon {
  font-size: 4rem;
  color: #555;
  margin-bottom: 1rem;
}

.loading-state i {
  color: #007acc;
}

.error-icon {
  color: #e74c3c;
}

.error-state h2 {
  color: white;
  margin-bottom: 0.5rem;
}

.error-state p {
  margin-bottom: 2rem;
  color: #e74c3c;
}

.retry-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 12px 24px;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.2s;
}

.retry-button:hover {
  background-color: #005fa3;
}

.empty-state h2 {
  color: white;
  margin-bottom: 0.5rem;
}

.empty-state p {
  margin-bottom: 2rem;
}

.add-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 12px 24px;
  background-color: #007acc;
  color: white;
  border-radius: 4px;
  text-decoration: none;
  font-weight: 500;
  transition: background-color 0.2s;
}

.add-button:hover {
  background-color: #005fa3;
}

/* Delete Dialog Styles */
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

/* List view styles */
.audiobooks-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px 0;
}

.audiobook-list-item {
  display: grid;
  grid-template-columns: 40px 64px 1fr auto 120px;
  gap: 12px;
  align-items: center;
  padding: 10px 12px;
  height: 80px; /* match LIST_ROW_HEIGHT */
  background-color: transparent;
  border-radius: 6px;
  transition: background-color 0.12s, transform 0.12s;
  border-bottom: 1px solid rgba(255,255,255,0.03);
}

.audiobook-list-item:hover {
  background-color: rgba(255,255,255,0.02);
  transform: translateY(-1px);
}

.list-thumb {
  width: 56px;
  height: 56px;
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
}

.list-details {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.list-details .audiobook-title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 14px;
  color: #fff;
}

.list-details .audiobook-author {
  font-size: 12px;
  color: #ccc;
}

.list-actions {
  margin-left: 0;
  display: flex;
  gap: 8px;
  align-items: center;
  justify-self: end;
}

/* Header row to mimic table columns */
.list-header {
  display: grid;
  grid-template-columns: 40px 64px 1fr auto 120px;
  gap: 12px;
  padding: 8px 12px;
  color: #aaa;
  font-size: 12px;
  border-bottom: 1px solid rgba(255,255,255,0.04);
  align-items: center;
}

.list-header .col-cover { opacity: 0.9 }
.list-header .col-title { opacity: 0.9 }
.list-header .col-status { opacity: 0.9 }
.list-header .col-actions { text-align: right }

/* Position badges between details and actions */
.list-badges {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-left: 12px;
  justify-self: start;
}

/* Make sure badges don't push actions out of view on narrow screens */
@media (max-width: 480px) {
  .list-badges {
    display: none;
  }
}

/* Ensure list view text is visible (overrides poster-hover rules) */
.audiobooks-list .audiobook-title,
.audiobooks-list .audiobook-author {
  opacity: 1;
  transition: none;
  color: inherit;
}
</style>
