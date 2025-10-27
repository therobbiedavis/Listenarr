<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="close">
    <div class="modal-container">
      <div class="modal-header">
        <h2>
          <PhMagnifyingGlass />
          Manual Search - {{ audiobook?.title }}
        </h2>
        <button class="btn-close" @click="close">
          <PhX />
        </button>
      </div>

      <div class="modal-body">
        <!-- Search Status -->
        <div v-if="searching" class="search-status">
          <PhSpinner class="ph-spin" />
          <span>Searching indexers... ({{ searchedIndexers }}/{{ totalIndexers }})</span>
        </div>

        <!-- Results Table -->
        <div v-if="displayResults.length > 0 || !searching" class="results-container">
          <div class="results-header">
            <!-- Search Bar -->
            <div class="search-bar">
              <div class="search-input-wrapper">
                <PhMagnifyingGlass class="search-icon" />
                <input
                  v-model="searchQuery"
                  type="text"
                  class="search-input"
                  placeholder="Search for audiobooks..."
                  @keyup.enter="search"
                  :disabled="searching"
                />
                <button 
                  class="search-btn"
                  @click="search"
                  :disabled="searching || !searchQuery.trim()"
                >
                  <template v-if="!searching">
                    <PhMagnifyingGlass />
                  </template>
                  <template v-else>
                    <PhSpinner class="ph-spin" />
                  </template>
                  Search
                </button>
              </div>
            </div>
            
            <div class="results-controls">
              <div class="results-count">
                {{ displayResults.length }} result{{ displayResults.length !== 1 ? 's' : '' }} found
              </div>
              <button 
                v-if="!searching" 
                class="btn btn-secondary btn-sm"
                @click="search"
              >
                <PhArrowClockwise />
                Refresh
              </button>
            </div>
          </div>

          <div v-if="displayResults.length === 0 && !searching" class="no-results">
            <PhMagnifyingGlass />
            <p>No results found</p>
            <p class="hint">Try adjusting your indexer settings or search criteria</p>
          </div>

          <div v-else class="results-table-wrapper">
            <table class="results-table">
              <thead>
                <tr>
                  <th class="col-source sortable" @click="setSort('Source')">
                    <span class="header-content">
                      Source
                      <component :is="getSortIcon('Source')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-age sortable" @click="setSort('PublishedDate')">
                    <span class="header-content">
                      Age
                      <component :is="getSortIcon('PublishedDate')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-title sortable" @click="setSort('Title')">
                    <span class="header-content">
                      Title
                      <component :is="getSortIcon('Title')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-indexer sortable" @click="setSort('Source')">
                    <span class="header-content">
                      Indexer
                      <component :is="getSortIcon('Source')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-size sortable" @click="setSort('Size')">
                    <span class="header-content">
                      Size
                      <component :is="getSortIcon('Size')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-peers sortable" @click="setSort('Seeders')">
                    <span class="header-content">
                      Peers
                      <component :is="getSortIcon('Seeders')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-language">Languages</th>
                  <th class="col-quality sortable" @click="setSort('Quality')">
                    <span class="header-content">
                      Quality
                      <component :is="getSortIcon('Quality')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-score sortable" @click="setSort('Score')">
                    <span class="header-content">
                      Score
                      <component :is="getSortIcon('Score')" class="sort-icon" />
                    </span>
                  </th>
                  <th class="col-actions"></th>
                </tr>
              </thead>
              <tbody>
                <tr 
                  v-for="result in displayResults" 
                  :key="result.id"
                  class="result-row"
                >
                  <td class="col-source">
                    <span :class="['source-badge', getSourceType(result)]">
                      {{ getSourceType(result) }}
                    </span>
                  </td>
                  <td class="col-age">{{ formatAge(result.publishedDate) }}</td>
                  <td class="col-title">
                    <div class="title-cell">
                      <span class="title-text">{{ safeText(result.title) }}</span>
                    </div>
                  </td>
                  <td class="col-indexer">
                    <span class="indexer-name">{{ result.source }}</span>
                  </td>
                  <td class="col-size">{{ formatSize(result.size) }}</td>
                  <td class="col-peers">
                    <div class="peers-cell">
                      <span class="seeders" :class="{ 'good': result.seeders > 10, 'medium': result.seeders > 0 && result.seeders <= 10 }">
                        <PhArrowUp /> {{ result.seeders }}
                      </span>
                      <span class="leechers">
                        <PhArrowDown /> {{ result.leechers }}
                      </span>
                    </div>
                  </td>
                  <td class="col-language">
                    <span v-if="result.language" class="language-badge">
                      {{ result.language }}
                    </span>
                    <span v-else class="language-badge unknown">Unknown</span>
                  </td>
                  <td class="col-quality">
                    <span v-if="result.quality" class="quality-badge">
                      {{ result.quality }}
                    </span>
                    <span v-else class="quality-badge unknown">-</span>
                  </td>
                  <td class="col-score">
                    <div v-if="getResultScore(result.id)" class="score-cell">
                      <ScorePopover :content="getScoreBreakdownTooltip(getResultScore(result.id))">
                        <template #default>
                          <span 
                            v-if="getResultScore(result.id)?.isRejected"
                            class="score-badge rejected"
                            :title="getResultScore(result.id)?.rejectionReasons.join(', ')"
                          >
                            <PhXCircle />
                            Rejected
                          </span>
                          <span v-else :class="['score-badge', getScoreClass(getResultScore(result.id)?.totalScore || 0)]">
                            {{ getResultScore(result.id)?.totalScore }}
                          </span>
                        </template>
                      </ScorePopover>
                    </div>
                    <span v-else class="score-badge loading">-</span>
                  </td>
                  <td class="col-actions">
                    <button 
                      class="btn-icon btn-download"
                      @click="downloadResult(result)"
                      :disabled="downloading[result.id]"
                      :title="downloading[result.id] ? 'Sending to download client...' : 'Download'"
                    >
                      <template v-if="!downloading[result.id]">
                        <PhDownloadSimple />
                      </template>
                      <template v-else>
                        <PhSpinner class="ph-spin" />
                      </template>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { PhMagnifyingGlass, PhX, PhSpinner, PhArrowClockwise, PhArrowUp, PhArrowDown, PhXCircle, PhDownloadSimple, PhArrowsDownUp } from '@phosphor-icons/vue'
import { useToast } from '@/services/toastService'
import { apiService } from '@/services/api'
import type { Audiobook, SearchResult, QualityScore, QualityProfile, SearchSortBy, SearchSortDirection } from '@/types'
import { getScoreBreakdownTooltip } from '@/composables/useScore'
import ScorePopover from '@/components/ScorePopover.vue'
import { safeText } from '@/utils/textUtils'

interface Props {
  isOpen: boolean
  audiobook: Audiobook | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
  downloaded: [result: SearchResult]
}>()

const results = ref<SearchResult[]>([])
const searching = ref(false)
const downloading = ref<Record<string, boolean>>({})
const searchedIndexers = ref(0)
const totalIndexers = ref(0)
const qualityScores = ref<Map<string, QualityScore>>(new Map())
const qualityProfile = ref<QualityProfile | null>(null)
const sortBy = ref<SearchSortBy | 'Score'>('Score')
const sortDirection = ref<SearchSortDirection>('Descending')
const searchQuery = ref('')

watch(() => props.isOpen, (isOpen) => {
  if (isOpen && props.audiobook) {
    // Initialize search query with default query and auto-search
    searchQuery.value = buildSearchQuery()
    search()
  }
})

const displayResults = computed(() => {
  // When sorting by Score, return a sorted copy derived from `results` so
  // the view always reflects the desired order even if `results` is later
  // replaced by the search logic.
  if (sortBy.value !== 'Score') return results.value

  const asc = sortDirection.value === 'Ascending'
  const copy = results.value.slice()
  copy.sort((a, b) => {
    const qa = qualityScores.value.get(a.id)
    const qb = qualityScores.value.get(b.id)

    const rejectedA = Boolean(qa?.isRejected)
    const rejectedB = Boolean(qb?.isRejected)
    if (rejectedA !== rejectedB) return rejectedA ? 1 : -1

    const hasA = typeof qa?.totalScore === 'number'
    const hasB = typeof qb?.totalScore === 'number'
    if (hasA !== hasB) return hasA ? -1 : 1
    if (!hasA && !hasB) return 0

    const scoreA = qa!.totalScore
    const scoreB = qb!.totalScore
    if (scoreA === scoreB) return 0
    return asc ? (scoreA - scoreB) : (scoreB - scoreA)
  })
  return copy
})

function setSort(column: SearchSortBy | 'Score') {
  if (sortBy.value === column) {
    // Toggle direction if same column
    sortDirection.value = sortDirection.value === 'Ascending' ? 'Descending' : 'Ascending'
  } else {
    // New column, default to descending
    sortBy.value = column as SearchSortBy
    sortDirection.value = 'Descending'
  }
  
  // For Score sorting, sort frontend results, otherwise re-search with backend sorting
  if (column === 'Score') {
    // Frontend sorting for Score column
    sortFrontendResults()
  } else {
    // Backend sorting for other columns
    search()
  }
}

function getSortIcon(column: SearchSortBy | 'Score') {
  // Return a component reference for the current sort icon state.
  if (sortBy.value !== column) {
    return PhArrowsDownUp
  }
  return sortDirection.value === 'Ascending' ? PhArrowUp : PhArrowDown
}

function sortFrontendResults() {
  const ascending = sortDirection.value === 'Ascending'

  results.value.sort((a, b) => {
    const qa = getResultScore(a.id)
    const qb = getResultScore(b.id)

    const rejectedA = Boolean(qa?.isRejected)
    const rejectedB = Boolean(qb?.isRejected)

    // Put rejected items at the end always
    if (rejectedA && !rejectedB) return 1
    if (!rejectedA && rejectedB) return -1

    // Now handle scored vs unscored: scored items should appear before unscored
    const hasA = typeof qa?.totalScore === 'number'
    const hasB = typeof qb?.totalScore === 'number'
    if (hasA && !hasB) return -1
    if (!hasA && hasB) return 1
    if (!hasA && !hasB) return 0

    // Both have numeric scores — compare numerically
    const scoreA = qa!.totalScore
    const scoreB = qb!.totalScore

    if (scoreA === scoreB) return 0
    return ascending ? (scoreA - scoreB) : (scoreB - scoreA)
  })
}

async function search() {
  if (!props.audiobook) return

  searching.value = true
  results.value = []
  searchedIndexers.value = 0
  totalIndexers.value = 0

  try {
    // Get count of enabled indexers first
    const enabledIndexers = await apiService.getEnabledIndexers()
    totalIndexers.value = enabledIndexers.length
    
    // Build search query from title and author (fallback if no manual query)
    const query = searchQuery.value.trim() || buildSearchQuery()
    
    // Search each indexer individually to show progress
    const allResults: SearchResult[] = []
    const searchPromises = enabledIndexers.map(async (indexer) => {
      try {
        const indexerResults = await apiService.searchByApi(indexer.id.toString(), query)
        allResults.push(...indexerResults)
        searchedIndexers.value++
      } catch (error) {
        console.warn(`Failed to search indexer ${indexer.name}:`, error)
        searchedIndexers.value++ // Still count as completed even if failed
      }
    })
    
    // Wait for all searches to complete
    await Promise.all(searchPromises)
    
    // Apply backend sorting if needed (for non-Score columns)
    if (sortBy.value !== 'Score') {
      const backendSortBy = sortBy.value as SearchSortBy
      // Sort results based on the current sort criteria
      allResults.sort((a, b) => {
        switch (backendSortBy) {
          case 'Seeders':
            return sortDirection.value === 'Ascending' 
              ? a.seeders - b.seeders 
              : b.seeders - a.seeders
          case 'Size':
            return sortDirection.value === 'Ascending' 
              ? a.size - b.size 
              : b.size - a.size
          case 'PublishedDate':
            return sortDirection.value === 'Ascending' 
              ? new Date(a.publishedDate).getTime() - new Date(b.publishedDate).getTime()
              : new Date(b.publishedDate).getTime() - new Date(a.publishedDate).getTime()
          case 'Title':
            return sortDirection.value === 'Ascending' 
              ? a.title.localeCompare(b.title) 
              : b.title.localeCompare(a.title)
          case 'Source':
            return sortDirection.value === 'Ascending' 
              ? a.source.localeCompare(b.source) 
              : b.source.localeCompare(a.source)
          case 'Quality':
            return sortDirection.value === 'Ascending' 
              ? a.quality.localeCompare(b.quality) 
              : b.quality.localeCompare(a.quality)
          default:
            return 0
        }
      })
    }
    
    // Deduplicate results by id (multiple indexers can return the same release)
    const seen = new Map<string, SearchResult>()
    for (const r of allResults) {
      if (!seen.has(r.id)) seen.set(r.id, r)
    }
    results.value = Array.from(seen.values())
    
    // Load quality profile and score results (always needed for Score column or display)
    await loadQualityProfileAndScore()
    
    // If sorting by Score, apply frontend sorting
    if (sortBy.value === 'Score') {
      sortFrontendResults()
    }
    
  } catch (err) {
    console.error('Manual search failed:', err)
  } finally {
    searching.value = false
  }
}

async function loadQualityProfileAndScore() {
  try {
    // Get the audiobook's quality profile or default
    if (props.audiobook?.qualityProfileId) {
      qualityProfile.value = await apiService.getQualityProfileById(props.audiobook.qualityProfileId)
    } else {
      qualityProfile.value = await apiService.getDefaultQualityProfile()
    }
    
    // Score the search results
    if (qualityProfile.value?.id && results.value.length > 0) {
      const scores = await apiService.scoreSearchResults(
        qualityProfile.value.id,
        results.value
      )
      
      // Map scores by search result ID
      qualityScores.value.clear()
      scores.forEach(score => {
        qualityScores.value.set(score.searchResult.id, score)
      })
    }
  } catch (error) {
    console.warn('Failed to load quality profile or score results:', error)
  }
}

function buildSearchQuery(): string {
  if (!props.audiobook) return ''
  
  const parts: string[] = []
  
  if (props.audiobook.title) {
    parts.push(props.audiobook.title)
  }
  
  if (props.audiobook.authors && props.audiobook.authors.length > 0 && props.audiobook.authors[0]) {
    parts.push(props.audiobook.authors[0])
  }
  
  return parts.join(' ')
}

async function downloadResult(result: SearchResult) {
  downloading.value[result.id] = true
  const toast = useToast()
  
  try {
    // Check if this is a DDL
    const isDDL = getSourceType(result) === 'ddl'
    const audiobookId = props.audiobook?.id
    
    if (isDDL) {
      // For DDL, start download in background and add to activity
      console.log('Starting DDL download:', result.title)
      console.log('Download type:', result.downloadType)
      console.log('Download URL:', result.torrentUrl)
      console.log('Audiobook ID:', audiobookId)
      
      const response = await apiService.sendToDownloadClient(result, undefined, audiobookId)
      console.log('DDL download started:', response)
      
      // Add to activity/downloads view (will be tracked there)
      // Show success message
      emit('downloaded', result)
      
      // Show feedback briefly
      setTimeout(() => {
        delete downloading.value[result.id]
      }, 1000)
    } else {
      // For torrents/NZB, send to download client (also pass audiobookId for future processing)
      const response = await apiService.sendToDownloadClient(result, undefined, audiobookId)
      console.log('Download started:', response)
      emit('downloaded', result)
      
      // Show success feedback briefly, then remove
      setTimeout(() => {
        delete downloading.value[result.id]
      }, 2000)
    }
  } catch (err) {
    console.error('Download failed:', err)
    const errorMessage = err instanceof Error ? err.message : 'Unknown error'
    
    // Show error in alert with more context
    let userMessage = `Download failed: ${errorMessage}`
    if (errorMessage.includes('Output path not configured')) {
      userMessage = 'Download path not configured. Please go to Settings and configure the Output Path before downloading.'
    }
    
    // Show error as a non-blocking toast instead of a modal alert
    toast.error('Download failed', userMessage)
    delete downloading.value[result.id]
  }
}

function close() {
  emit('close')
}

function getSourceType(result: SearchResult): string {
  // Check downloadType first if it's set
  if (result.downloadType) {
    return result.downloadType.toLowerCase()
  }
  
  // Fallback to legacy detection logic
  // Check for torrent indicators
  if (result.magnetLink || result.torrentUrl) {
    return 'torrent'
  }
  // Check for NZB indicator
  if (result.nzbUrl) {
    return 'nzb'
  }
  // Check source name
  if (result.source?.toLowerCase().includes('torrent')) {
    return 'torrent'
  }
  // Default to NZB for usenet
  return 'nzb'
}

function formatAge(date: Date | string): string {
  const now = new Date()
  const published = new Date(date)
  const diffMs = now.getTime() - published.getTime()
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24))
  
  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return '1 day'
  if (diffDays < 30) return `${diffDays} days`
  if (diffDays < 365) {
    const months = Math.floor(diffDays / 30)
    return `${months} month${months !== 1 ? 's' : ''}`
  }
  const years = Math.floor(diffDays / 365)
  return `${years} year${years !== 1 ? 's' : ''}`
}

function formatSize(bytes: number): string {
  if (!bytes || bytes === 0) return '-'
  
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unitIndex = 0
  
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  
  return `${size.toFixed(1)} ${units[unitIndex]}`
}

function getResultScore(resultId: string): QualityScore | undefined {
  return qualityScores.value.get(resultId)
}

function getScoreClass(score: number): string {
  if (score >= 80) return 'excellent'
  if (score >= 60) return 'good'
  if (score >= 40) return 'fair'
  return 'poor'
}

// useScore composable provides getScoreBreakdownTooltip
</script>

<style scoped>
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
  padding: 2rem;
}

.modal-container {
  background-color: #1e1e1e;
  border-radius: 8px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #3a3a3a;
}

.modal-header h2 {
  margin: 0;
  color: white;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.btn-close {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-close:hover {
  background-color: #3a3a3a;
  color: white;
}

.modal-body {
  padding: 1.5rem 2rem;
  overflow-y: auto;
  flex: 1;
}

.search-status {
  text-align: center;
  padding: 3rem 2rem;
  color: #007acc;
  font-size: 1.1rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.search-status i {
  font-size: 3rem;
}

.results-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.results-header {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding-bottom: 1rem;
}

.results-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.results-count {
  color: #ccc;
  font-size: 0.9rem;
}

.search-bar {
  width: 100%;
}

.search-input-wrapper {
  position: relative;
  display: flex;
  align-items: stretch; /* ensure input and button match height */
  gap: 0.5rem;
  max-width: 600px;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  top: 50%;
  transform: translateY(-50%);
  color: #8a8a8a;
  font-size: 1rem;
  z-index: 2;
  pointer-events: none; /* make icon non-interactive so clicks go to the input */
}

.search-input {
  flex: 1;
  padding: 0.5rem 1rem 0.5rem 2.5rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  color: white;
  font-size: 1rem;
  transition: border-color 0.2s, box-shadow 0.2s;
  height: 40px;
  box-sizing: border-box;
}

.search-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 2px rgba(0, 122, 204, 0.2);
}

.search-input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.search-btn {
  padding: 0 1rem;
  white-space: nowrap;
  min-width: 96px;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  transition: all 0.2s;
  height: 40px; /* match input height */
  box-sizing: border-box;
}

.search-btn:hover:not(:disabled) {
  background-color: #0056b3;
}

.search-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Ensure icon color follows button color (spinner inherits text color) */
.search-btn .ph {
  color: inherit;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #0056b3;
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
  transition: all 0.2s;
}

.btn-secondary {
  background-color: #3a3a3a;
  color: white;
}

.btn-secondary:hover {
  background-color: #4a4a4a;
}

.btn-sm {
  padding: 0.4rem 0.8rem;
  font-size: 0.875rem;
}

.no-results {
  text-align: center;
  padding: 4rem 2rem;
  color: #999;
}

.no-results i {
  font-size: 4rem;
  margin-bottom: 1rem;
  color: #555;
}

.no-results p {
  margin: 0.5rem 0;
  color: #ccc;
}

.no-results .hint {
  font-size: 0.9rem;
  color: #999;
}

.results-table-wrapper {
  overflow-x: auto;
  border: 1px solid #3a3a3a;
  border-radius: 4px;
}

.results-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.results-table thead {
  background-color: #2a2a2a;
  position: sticky;
  top: 0;
  z-index: 1;
}

.results-table th {
  padding: 0.75rem;
  text-align: left;
  color: #ccc;
  font-weight: 600;
  text-transform: uppercase;
  font-size: 0.75rem;
  letter-spacing: 0.5px;
  border-bottom: 2px solid #3a3a3a;
}

.sortable {
  cursor: pointer;
  user-select: none;
  transition: background-color 0.2s;
}

.sortable:hover {
  background-color: #3a3a3a;
}

.header-content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.sort-icon {
  font-size: 0.8rem;
  margin-left: 0.5rem;
  opacity: 0.6;
  transition: opacity 0.2s;
}

.sort-icon-inactive {
  opacity: 0.3;
}

.sort-icon-active {
  opacity: 1;
  color: #007acc;
}

.results-table tbody tr {
  border-bottom: 1px solid #2a2a2a;
  transition: background-color 0.2s;
}

.results-table tbody tr:hover {
  background-color: #2a2a2a;
}

.results-table td {
  padding: 0.75rem;
  color: #ddd;
  vertical-align: middle;
}

.col-source {
  width: 60px;
}

.col-age {
  width: 100px;
}

.col-title {
  min-width: 300px;
}

.col-indexer {
  width: 150px;
}

.col-size {
  width: 100px;
}

.col-peers {
  width: 120px;
}

.col-language {
  width: 100px;
}

.col-quality {
  width: 120px;
}

.col-actions {
  width: 60px;
  text-align: center;
}

.source-badge {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.source-badge.nzb {
  background-color: #3498db;
  color: white;
}

.source-badge.torrent {
  background-color: #2ecc71;
  color: white;
}

.source-badge.ddl {
  background-color: #9b59b6;
  color: white;
}

.source-badge.usenet {
  background-color: #3498db;
  color: white;
}

.title-cell {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.title-text {
  color: white;
  font-weight: 500;
}

.indexer-name {
  color: #007acc;
}

.peers-cell {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.seeders,
.leechers {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.85rem;
}

.seeders {
  color: #999;
}

.seeders.good {
  color: #2ecc71;
  font-weight: 600;
}

.seeders.medium {
  color: #f39c12;
}

.leechers {
  color: #999;
}

.language-badge,
.quality-badge {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background-color: #3a3a3a;
  border-radius: 4px;
  font-size: 0.8rem;
  color: #ccc;
}

.score-cell {
  display: flex;
  align-items: center;
  justify-content: center;
}

.score-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: 600;
  white-space: nowrap;
}

.score-badge.loading {
  background-color: transparent;
  color: #666;
  border: none;
  padding: 0;
}

.score-badge.rejected {
  background-color: rgba(231, 76, 60, 0.15);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.score-badge.excellent {
  background-color: rgba(39, 174, 96, 0.15);
  color: #27ae60;
  border: 1px solid rgba(39, 174, 96, 0.3);
}

.score-badge.good {
  background-color: rgba(52, 152, 219, 0.15);
  color: #3498db;
  border: 1px solid rgba(52, 152, 219, 0.3);
}

.score-badge.fair {
  background-color: rgba(241, 196, 15, 0.15);
  color: #f39c12;
  border: 1px solid rgba(241, 196, 15, 0.3);
}

.score-badge.poor {
  background-color: rgba(149, 165, 166, 0.15);
  color: #7f8c8d;
  border: 1px solid rgba(149, 165, 166, 0.3);
}

.language-badge.unknown,
.quality-badge.unknown {
  border-radius: 4px;
  font-size: 0.8rem;
  color: #ddd;
}

.language-badge.unknown,
.quality-badge.unknown {
  color: #666;
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

.btn-download:hover:not(:disabled) {
  background-color: #007acc;
  color: white;
}

@media (max-width: 1200px) {
  .modal-container {
    max-width: 95%;
  }
  
  .results-table {
    font-size: 0.8rem;
  }
  
  .col-title {
    min-width: 200px;
  }
}
</style>
