<template>
  <div class="wanted-view">
    <div class="page-header">
      <h1>
        <i class="ph ph-heart"></i>
        Wanted
      </h1>
      <div class="wanted-actions">
        <button class="btn btn-primary" @click="searchMissing" :disabled="categorizedWanted.missing.length === 0">
          <i class="ph ph-robot"></i>
          Automatic Search All
        </button>
        <button class="btn btn-secondary" @click="openManualImport">
          <i class="ph ph-folder-plus"></i>
          Manual Import
        </button>
      </div>
    </div>

    <div class="wanted-filters">
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

    <!-- Loading State -->
    <div v-if="loading" class="loading-state">
      <i class="ph ph-spinner ph-spin"></i>
      <p>Loading wanted audiobooks...</p>
    </div>

    <!-- Wanted List -->
    <div v-else-if="filteredWanted.length > 0" class="wanted-list">
      <div 
        v-for="item in filteredWanted" 
        :key="item.id"
        class="wanted-item"
      >
        <div class="wanted-poster">
          <img 
            :src="apiService.getImageUrl(item.imageUrl) || `https://via.placeholder.com/60x90?text=No+Image`" 
            :alt="item.title" 
          />
        </div>
        <div class="wanted-info">
          <h3>{{ item.title }}</h3>
          <h4 v-if="item.authors?.length">by {{ item.authors.join(', ') }}</h4>
          <div class="wanted-meta">
            <span v-if="item.series">{{ item.series }}<span v-if="item.seriesNumber"> #{{ item.seriesNumber }}</span></span>
            <span v-if="item.publishYear">Released: {{ formatDate(item.publishYear) }}</span>
            <span v-if="item.runtime">{{ Math.floor(item.runtime / 60) }}h {{ item.runtime % 60 }}m</span>
          </div>
          <div class="wanted-quality">
            <template v-if="getQualityProfileForAudiobook(item)">
              Wanted Quality Profile:
              <span class="profile-name">{{ getQualityProfileForAudiobook(item)?.name ?? 'Unknown' }}</span>
            </template>
            <template v-else>
              Wanted Quality: {{ item.quality || 'Any' }}
            </template>
          </div>
          <div v-if="searchResults[item.id]" class="search-status">
            <i v-if="searching[item.id]" class="ph ph-spinner ph-spin"></i>
            {{ searchResults[item.id] }}
          </div>
        </div>
        <div class="wanted-status">
          <span :class="['status-badge', getStatusClass(item)]">
            {{ getStatusText(item) }}
          </span>
        </div>
        <div class="wanted-actions-cell">
          <button 
            class="btn-icon" 
            @click="searchAudiobook(item)"
            :disabled="searching[item.id]"
            title="Automatic Search"
          >
            <i class="ph ph-robot"></i>
          </button>
          <button 
            class="btn-icon" 
            @click="openManualSearch(item)"
            title="Manual Search"
          >
            <i class="ph ph-magnifying-glass"></i>
          </button>
          <button 
            class="btn-icon" 
            @click="markAsSkipped(item)"
            :disabled="searching[item.id]"
            title="Unmonitor Audiobook"
          >
            <i class="ph ph-x"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state">
      <div class="empty-icon">
        <i class="ph ph-check-circle"></i>
      </div>
      <h2>{{ getEmptyStateTitle() }}</h2>
      <p>{{ getEmptyStateMessage() }}</p>
    </div>

    <!-- Manual Search Modal -->
    <ManualSearchModal
      :is-open="showManualSearchModal"
      :audiobook="selectedAudiobook"
      @close="closeManualSearch"
      @downloaded="handleDownloaded"
    />

    <!-- Manual Import Modal -->
    <ManualImportModal
      :is-open="showManualImportModal"
      @close="closeManualImport"
      @imported="handleImported"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useLibraryStore } from '@/stores/library'
import { useConfigurationStore } from '@/stores/configuration'
import { apiService } from '@/services/api'
import ManualSearchModal from '@/components/ManualSearchModal.vue'
import ManualImportModal from '@/components/ManualImportModal.vue'
import type { Audiobook, SearchResult } from '@/types'

const libraryStore = useLibraryStore()

const configurationStore = useConfigurationStore()

const getQualityProfileForAudiobook = (audiobook: Audiobook) => {
  console.log('Getting quality profile for audiobook:', audiobook.title, 'qualityProfileId:', audiobook.qualityProfileId)
  if (!audiobook || !audiobook.qualityProfileId) {
    console.log('No qualityProfileId for audiobook:', audiobook.title)
    return null
  }
  const profile = configurationStore.qualityProfiles.find(
    (profile) => profile.id === audiobook.qualityProfileId
  )
  console.log('Found profile:', profile ? profile.name : 'null')
  return profile || null
}

const selectedTab = ref('all')
const loading = ref(false)
const searching = ref<Record<number, boolean>>({})
const searchResults = ref<Record<number, string>>({})
const showManualSearchModal = ref(false)
const selectedAudiobook = ref<Audiobook | null>(null)
const showManualImportModal = ref(false)

onMounted(async () => {
  loading.value = true
  await libraryStore.fetchLibrary()
  await configurationStore.loadQualityProfiles()
  loading.value = false
  
  // Debug logging
  console.log('Total audiobooks in library:', libraryStore.audiobooks.length)
  console.log('Monitored audiobooks:', libraryStore.audiobooks.filter(a => a.monitored).length)
  console.log('Audiobooks without files:', libraryStore.audiobooks.filter(a => !a.filePath || a.filePath.trim() === '').length)
  console.log('Wanted audiobooks:', wantedAudiobooks.value.length)
  console.log('Quality profiles loaded:', configurationStore.qualityProfiles.length)
  
  // Log first few audiobooks for inspection
  if (libraryStore.audiobooks.length > 0) {
    console.log('Sample audiobook:', libraryStore.audiobooks[0])
  }
})

// Filter audiobooks that are monitored and missing files
// Prefer the server-provided `wanted` flag when present. If the server
// does not include the flag (migration / older records), fall back to a
// local computation: monitored && no files (or no primary filePath).
const wantedAudiobooks = computed(() => {
  return libraryStore.audiobooks.filter(audiobook => {
    const serverWanted = (audiobook as unknown as Record<string, unknown>)['wanted']

    // If server explicitly provided true/false, honor it
    if (serverWanted === true) return true
    if (serverWanted === false) return false

    // Fallback: treat as wanted when monitored and there are no files
    const hasFiles = Array.isArray(audiobook.files) ? audiobook.files.length > 0 : false
    const hasPrimaryFile = !!(audiobook.filePath && audiobook.filePath.toString().trim() !== '')

    return !!audiobook.monitored && !hasFiles && !hasPrimaryFile
  })
})

// Categorize wanted audiobooks by their current search state
const categorizedWanted = computed(() => {
  const all = wantedAudiobooks.value
  const searchingItems = all.filter(a => searching.value[a.id])
  const failedItems = all.filter(a => searchResults.value[a.id] && searchResults.value[a.id] !== 'Searching...' && !searching.value[a.id])
  const missingItems = all.filter(a => !searching.value[a.id] && !searchResults.value[a.id])
  const skippedItems: Audiobook[] = [] // For future use, currently empty

  return { all, searching: searchingItems, failed: failedItems, missing: missingItems, skipped: skippedItems }
})

// Count by status
const filterTabs = computed(() => [
  { label: 'All', value: 'all', count: categorizedWanted.value.all.length },
  { label: 'Missing', value: 'missing', count: categorizedWanted.value.missing.length },
  { label: 'Searching', value: 'searching', count: categorizedWanted.value.searching.length },
  { label: 'Failed', value: 'failed', count: categorizedWanted.value.failed.length },
  { label: 'Skipped', value: 'skipped', count: categorizedWanted.value.skipped.length }
])

const filteredWanted = computed(() => {
  switch (selectedTab.value) {
    case 'all':
      return categorizedWanted.value.all
    case 'missing':
      return categorizedWanted.value.missing
    case 'searching':
      return categorizedWanted.value.searching
    case 'failed':
      return categorizedWanted.value.failed
    case 'skipped':
      return categorizedWanted.value.skipped
    default:
      return categorizedWanted.value.all
  }
})

function getStatusClass(item: Audiobook): string {
  if (searching.value[item.id]) {
    return 'searching'
  }
  if (searchResults.value[item.id] && searchResults.value[item.id] !== 'Searching...') {
    return 'failed'
  }
  return 'missing'
}

function getStatusText(item: Audiobook): string {
  if (searching.value[item.id]) {
    return 'Searching'
  }
  if (searchResults.value[item.id] && searchResults.value[item.id] !== 'Searching...') {
    return 'Failed'
  }
  return 'Missing'
}

function getEmptyStateTitle(): string {
  switch (selectedTab.value) {
    case 'all':
      return 'No Wanted Audiobooks'
    case 'missing':
      return 'No Missing Audiobooks'
    case 'searching':
      return 'No Searching Audiobooks'
    case 'failed':
      return 'No Failed Audiobooks'
    case 'skipped':
      return 'No Skipped Audiobooks'
    default:
      return 'No Audiobooks'
  }
}

function getEmptyStateMessage(): string {
  switch (selectedTab.value) {
    case 'all':
      return 'All your monitored audiobooks have files!'
    case 'missing':
      return 'All wanted audiobooks are currently being searched or have been searched.'
    case 'searching':
      return 'No audiobooks are currently being searched.'
    case 'failed':
      return 'No audiobooks have failed searches.'
    case 'skipped':
      return 'No audiobooks have been skipped.'
    default:
      return 'No audiobooks in this category.'
  }
}

const formatDate = (date: string | undefined): string => {
  if (!date) return 'Unknown'
  try {
    return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
  } catch {
    return date
  }
}

const searchMissing = async () => {
  console.log('Automatic search for all missing audiobooks')
  
  // Search all missing audiobooks sequentially
  for (const audiobook of categorizedWanted.value.missing) {
    await searchAudiobook(audiobook)
    // Add a small delay between searches to avoid overwhelming indexers
    await new Promise(resolve => setTimeout(resolve, 1000))
  }
}

function openManualSearch(item: Audiobook) {
  selectedAudiobook.value = item
  showManualSearchModal.value = true
}

function openManualImport() {
  showManualImportModal.value = true
}

function closeManualImport() {
  showManualImportModal.value = false
}

async function handleImported(result: { imported: number }) {
  console.log('Manual import completed, imported:', result.imported)
  // Refresh library
  await libraryStore.fetchLibrary()
  closeManualImport()
}

function closeManualSearch() {
  showManualSearchModal.value = false
  selectedAudiobook.value = null
}

function handleDownloaded(result: SearchResult) {
  console.log('Downloaded:', result)
  // Refresh library after successful download
  setTimeout(async () => {
    await libraryStore.fetchLibrary()
    closeManualSearch()
  }, 2000)
}

const searchAudiobook = async (item: Audiobook) => {
  console.log('Searching audiobook:', item.title)
  
  searching.value[item.id] = true
  searchResults.value[item.id] = 'Searching...'
  
  try {
    const result = await apiService.searchAndDownload(item.id)
    
    if (result.success) {
      searchResults.value[item.id] = `Found on ${result.indexerUsed}, downloading...`
      
      // Refresh library to update status
      setTimeout(async () => {
        await libraryStore.fetchLibrary()
        delete searching.value[item.id]
        delete searchResults.value[item.id]
      }, 2000)
    } else {
      searchResults.value[item.id] = result.message || 'No matches found'
      setTimeout(() => {
        delete searching.value[item.id]
        delete searchResults.value[item.id]
      }, 5000)
    }
  } catch (err) {
    console.error('Search failed:', err)
    searchResults.value[item.id] = 'Search failed'
    setTimeout(() => {
      delete searching.value[item.id]
      delete searchResults.value[item.id]
    }, 5000)
  }
}

const markAsSkipped = async (item: Audiobook) => {
  console.log('Mark as skipped:', item.title)
  
  try {
    await apiService.updateAudiobook(item.id, { monitored: false })
    await libraryStore.fetchLibrary()
  } catch (err) {
    console.error('Failed to unmonitor audiobook:', err)
  }
}
</script>

<style scoped>
.wanted-view {
  padding: 2rem;
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
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.page-header h1 i {
  color: #e74c3c;
}

.wanted-actions {
  display: flex;
  gap: 1rem;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005fa3;
}

.btn-icon {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.2rem;
}

.btn-icon:hover:not(:disabled) {
  background-color: #3a3a3a;
  color: white;
}

.btn-icon:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Robot icon for automatic search */
.btn-icon .ph-robot {
  color: #4caf50;
}

.btn-icon:hover:not(:disabled) .ph-robot {
  color: #66bb6a;
}

.wanted-filters {
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
  font-size: 14px;
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

.loading-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.loading-state i {
  font-size: 3rem;
  color: #007acc;
  margin-bottom: 1rem;
}

.wanted-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.wanted-item {
  display: flex;
  align-items: center;
  padding: 1rem;
  background-color: #2a2a2a;
  border-radius: 8px;
  border-left: 4px solid #e74c3c;
  transition: background-color 0.2s;
}

.wanted-item:hover {
  background-color: #333;
}

.wanted-poster {
  width: 80px;
  height: 80px;
  margin-right: 1rem;
  background-color: #555;
  border-radius: 4px;
  overflow: hidden;
  flex-shrink: 0;
}

.wanted-poster img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.wanted-info {
  flex: 1;
  min-width: 0;
}

.wanted-info h3 {
  margin: 0 0 0.25rem 0;
  color: white;
  font-size: 1.1rem;
}

.wanted-info h4 {
  margin: 0 0 0.5rem 0;
  color: #007acc;
  font-size: 0.9rem;
  font-weight: 500;
}

.wanted-meta {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.25rem;
  color: #999;
  font-size: 0.85rem;
  flex-wrap: wrap;
}

.wanted-quality {
  color: #f39c12;
  font-size: 0.85rem;
  font-weight: 500;
}

.search-status {
  margin-top: 0.5rem;
  color: #007acc;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-status i {
  font-size: 1rem;
}

.wanted-status {
  margin: 0 1rem;
  flex-shrink: 0;
}

.status-badge {
  padding: 0.4rem 0.8rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: bold;
  text-transform: uppercase;
}

.status-badge.missing {
  background-color: #e74c3c;
  color: white;
}

.status-badge.searching {
  background-color: #007acc;
  color: white;
}

.status-badge.failed {
  background-color: #95a5a6;
  color: white;
}

.status-badge.skipped {
  background-color: #555;
  color: white;
}

.wanted-actions-cell {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  color: #2ecc71;
}

.empty-state h2 {
  color: white;
  margin-bottom: 1rem;
}

@media (max-width: 768px) {
  .wanted-item {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .wanted-poster {
    margin-bottom: 1rem;
  }
  
  .wanted-status,
  .wanted-actions-cell {
    margin: 1rem 0 0 0;
  }
}
</style>