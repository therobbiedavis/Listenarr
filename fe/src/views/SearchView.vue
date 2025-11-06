<template>
  <div class="search-page">
    <div class="search-header">
      <h2>Search Media</h2>
      <p>Search for audiobooks and media across your configured APIs</p>
    </div>

    <div class="search-form">
      <div class="search-input-group">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Enter search query..."
          class="search-input"
          @keyup.enter="performSearch"
        />
        <button 
          @click="performSearch" 
          :disabled="!searchQuery.trim() || searchStore.isSearching"
          class="search-button"
        >
          {{ searchStore.isSearching ? 'Searching...' : 'Search' }}
        </button>
      </div>

      <div class="search-filters">
        <select v-model="selectedCategory" class="filter-select">
          <option value="">All Categories</option>
          <option value="audiobook">Audiobooks</option>
          <option value="music">Music</option>
          <option value="podcast">Podcasts</option>
        </select>
      </div>
    </div>

    <div v-if="searchStore.isSearching" class="loading">
      <PhSpinner class="ph-spin" />
      <p>Searching...</p>
    </div>

    <div v-else-if="searchStore.hasResults" class="search-results">
      <h3>Search Results ({{ searchStore.searchResults.length }})</h3>
      <div class="results-grid">
        <div 
          v-for="result in searchStore.searchResults" 
          :key="result.id"
          class="result-card"
        >
          <div class="result-info">
            <h4>{{ safeText(result.title) }}</h4>
            <p class="result-artist">{{ safeText(result.artist) }}</p>
            <p class="result-album">{{ safeText(result.album) }}</p>
            
            <!-- Quality Score Badge -->
            <div v-if="getResultScore(result.id)" class="quality-score">
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
                    <PhStar />
                    Score: {{ getResultScore(result.id)?.totalScore }}
                  </span>
                </template>
              </ScorePopover>
            </div>
            
            <!-- Audiobook metadata -->
            <div v-if="result.narrator || result.runtime || result.series" class="audiobook-meta">
              <p v-if="result.narrator" class="meta-narrator">
                Narrated by {{ safeText(result.narrator) }}
              </p>
              <div class="meta-details">
                <span v-if="result.runtime" class="meta-runtime">
                  ⏱ {{ formatRuntime(result.runtime) }}
                </span>
                <span v-if="result.series" class="meta-series">
                  Series: {{ safeText(result.series) }}<span v-if="result.seriesNumber"> #{{ result.seriesNumber }}</span>
                </span>
              </div>
            </div>
            
            <div class="result-meta">
              <span class="result-size">{{ formatFileSize(result.size) }}</span>
              <span class="result-quality">{{ result.quality }}</span>
              <span class="result-source">{{ result.source }}</span>
            </div>
            <div class="result-stats">
              <span class="seeders">↑ {{ result.seeders }}</span>
              <span class="leechers">↓ {{ result.leechers }}</span>
            </div>
          </div>
          <div class="result-actions">
            <button 
              @click="addToLibrary(result)"
              :class="['add-button', { 'added': addedResults.has(result.id) }]"
              :disabled="isAddingToLibrary || addedResults.has(result.id)"
            >
              <template v-if="addedResults.has(result.id)">
                <PhCheck />
              </template>
              <template v-else>
                <PhPlus />
              </template>
              {{ addedResults.has(result.id) ? 'Added' : 'Add to Library' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="searchQuery && !searchStore.isSearching" class="no-results">
      <p>No results found for "{{ searchQuery }}"</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onMounted } from 'vue'
import { PhSpinner, PhXCircle, PhStar, PhCheck, PhPlus } from '@phosphor-icons/vue'
import { useSearchStore } from '@/stores/search'
import { useLibraryStore } from '@/stores/library'
import { apiService } from '@/services/api'
import type { SearchResult, AudibleBookMetadata, QualityScore, QualityProfile } from '@/types'
import { useToast } from '@/services/toastService'
import { getScoreBreakdownTooltip } from '@/composables/useScore'
import ScorePopover from '@/components/ScorePopover.vue'
import { safeText } from '@/utils/textUtils'

const searchStore = useSearchStore()
const libraryStore = useLibraryStore()
const toast = useToast()

console.log('SearchView component loaded')
console.log('searchStore:', searchStore)
console.log('libraryStore:', libraryStore)

const searchQuery = ref('')
const selectedCategory = ref('')
const isAddingToLibrary = ref(false)
const addedResults = ref(new Set<string>())
const qualityScores = ref<Map<string, QualityScore>>(new Map())
const defaultProfile = ref<QualityProfile | null>(null)

// Load default quality profile on mount
onMounted(async () => {
  try {
    defaultProfile.value = await apiService.getDefaultQualityProfile()
  } catch (error) {
    console.warn('No default quality profile found:', error)
  }
})

const performSearch = async () => {
  console.log('=== performSearch START ===')
  console.log('Search query:', searchQuery.value)
  
  if (!searchQuery.value.trim()) {
    console.log('Search query is empty, aborting')
    return
  }
  
  // Clear added results and scores for new search
  console.log('Clearing added results and scores')
  addedResults.value.clear()
  qualityScores.value.clear()
  
  console.log('Calling searchStore.search()')
  await searchStore.search(
    searchQuery.value.trim(),
    selectedCategory.value || undefined
  )
  console.log('searchStore.search() completed')
  console.log('Search results count:', searchStore.searchResults.length)
  
  // Wait for next tick to ensure searchResults are updated
  console.log('Waiting for nextTick()')
  await nextTick()
  console.log('nextTick() completed')
  
  // Score search results if we have a default profile
  if (defaultProfile.value?.id && searchStore.searchResults.length > 0) {
    try {
      console.log('Scoring search results with profile:', defaultProfile.value.name)
      const scores = await apiService.scoreSearchResults(
        defaultProfile.value.id,
        searchStore.searchResults
      )
      // Map scores by search result ID
      scores.forEach(score => {
        qualityScores.value.set(score.searchResult.id, score)
      })
      console.log('Quality scores loaded:', scores.length)
    } catch (error) {
      console.warn('Failed to score search results:', error)
    }
  }
  
  // Check which results are already in library
  console.log('Calling checkExistingInLibrary()')
  checkExistingInLibrary()
  console.log('=== performSearch END ===')
}

const checkExistingInLibrary = () => {
  console.log('checkExistingInLibrary called')
  console.log('Library audiobooks count:', libraryStore.audiobooks.length)
  
  // Ensure library is loaded
  if (libraryStore.audiobooks.length === 0) {
    console.log('Library is empty, fetching...')
    libraryStore.fetchLibrary().then(() => {
      console.log('Library fetched, count:', libraryStore.audiobooks.length)
      markExistingResults()
    })
  } else {
    console.log('Library already loaded')
    markExistingResults()
  }
}

const markExistingResults = () => {
  console.log('markExistingResults called')
  console.log('Search results count:', searchStore.searchResults.length)
  
  const libraryAsins = new Set(
    libraryStore.audiobooks
      .filter(book => book.asin)
      .map(book => book.asin!)
  )
  
  console.log('Library ASINs:', Array.from(libraryAsins))
  
  searchStore.searchResults.forEach(result => {
    console.log('Checking result:', result.id, 'ASIN:', result.asin)
    if (result.asin && libraryAsins.has(result.asin)) {
      console.log('Match found! Adding result ID:', result.id)
      addedResults.value.add(result.id)
    }
  })
  
  console.log('Added results:', Array.from(addedResults.value))
}

// Watch for search results changes to mark existing audiobooks
watch(() => searchStore.searchResults, () => {
  if (searchStore.searchResults.length > 0) {
    checkExistingInLibrary()
  }
}, { deep: true })

const addToLibrary = async (result: SearchResult) => {
  console.log('addToLibrary called with result:', result)
  
  if (!result.asin) {
    console.warn('No ASIN available for result:', result)
    toast.warning('Cannot add', 'Cannot add to library: No ASIN available for this result')
    return
  }

  console.log('Adding to library, ASIN:', result.asin)
  isAddingToLibrary.value = true
  try {
    // Fetch full metadata from the backend
    console.log('Fetching metadata from /api/audible/metadata/' + result.asin)
    const metadata = await apiService.getAudibleMetadata<AudibleBookMetadata>(result.asin)
    console.log('Metadata fetched:', metadata)
    
    // Add to library
    console.log('Adding to library via /api/library/add')
    await apiService.addToLibrary(metadata, { searchResult: result })
    
  console.log('Successfully added to library')
  toast.success('Added to library', `"${metadata.title}" has been added to your library!`)
    
    // Mark this result as added
    addedResults.value.add(result.id)
  } catch (error: unknown) {
    console.error('Failed to add audiobook:', error)
    const errorMessage = error instanceof Error ? error.message : String(error)
    
    // Check if it's a conflict (already exists)
      if (errorMessage.includes('409') || errorMessage.includes('Conflict')) {
      toast.warning('Already exists', 'This audiobook is already in your library.')
    } else {
      toast.error('Add failed', 'Failed to add audiobook. Please try again.')
    }
  } finally {
    isAddingToLibrary.value = false
  }
}

const formatFileSize = (bytes: number): string => {
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  if (bytes === 0) return '0 Bytes'
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i]
}

const formatRuntime = (minutes: number): string => {
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (hours > 0 && mins > 0) {
    return `${hours}h ${mins}m`
  } else if (hours > 0) {
    return `${hours}h`
  } else {
    return `${mins}m`
  }
}

const getResultScore = (resultId: string): QualityScore | undefined => {
  return qualityScores.value.get(resultId)
}

const getScoreClass = (score: number): string => {
  if (score >= 80) return 'excellent'
  if (score >= 60) return 'good'
  if (score >= 40) return 'fair'
  return 'poor'
}

// getScoreBreakdownTooltip is provided by the useScore composable
</script>

<style scoped>
.search-page {
  max-width: 1200px;
  margin: 0 auto;
}

.search-header {
  text-align: center;
  margin-bottom: 2rem;
}

.search-header h2 {
  margin: 0 0 0.5rem 0;
  color: #2c3e50;
}

.search-form {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-bottom: 2rem;
}

.search-input-group {
  display: flex;
  gap: 1rem;
  margin-bottom: 1rem;
}

.search-input {
  flex: 1;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
}

.search-button {
  padding: 0.75rem 2rem;
  background-color: #3498db;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1rem;
  transition: background-color 0.2s;
}

.search-button:hover:not(:disabled) {
  background-color: #2980b9;
}

.search-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.search-filters {
  display: flex;
  gap: 1rem;
}

.filter-select {
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: #666;
}

.loading i {
  font-size: 2rem;
  color: #3498db;
  display: block;
  margin-bottom: 1rem;
}

.search-results h3 {
  margin-bottom: 1rem;
  color: #2c3e50;
}

.results-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1rem;
}

.result-card {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.result-info {
  flex: 1;
}

.result-info h4 {
  margin: 0 0 0.5rem 0;
  color: #2c3e50;
  font-size: 1.1rem;
}

.result-artist {
  margin: 0 0 0.25rem 0;
  font-weight: 600;
  color: #555;
}

.result-album {
  margin: 0 0 0.5rem 0;
  color: #777;
  font-style: italic;
}

.audiobook-meta {
  margin: 0.75rem 0;
  padding: 0.5rem 0;
  border-top: 1px solid #f0f0f0;
  border-bottom: 1px solid #f0f0f0;
}

.meta-narrator {
  margin: 0 0 0.5rem 0;
  color: #555;
  font-size: 0.9rem;
}

.meta-details {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.85rem;
}

.meta-runtime {
  color: #3498db;
  font-weight: 500;
}

.meta-series {
  color: #9b59b6;
  font-weight: 500;
}

.result-meta {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.result-meta span {
  background-color: #f8f9fa;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  color: #666;
}

.quality-score {
  margin: 0.5rem 0;
}

.score-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: 600;
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

.result-stats {
  display: flex;
  gap: 1rem;
  font-size: 0.9rem;
}

.seeders {
  color: #27ae60;
  font-weight: 600;
}

.leechers {
  color: #e74c3c;
  font-weight: 600;
}

.result-actions {
  margin-left: 1rem;
}

.add-button {
  padding: 0.5rem 1rem;
  background-color: #27ae60;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.add-button:hover:not(:disabled) {
  background-color: #229954;
}

.add-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.add-button.added {
  background-color: #27ae60;
  opacity: 1;
}

.add-button.added:disabled {
  opacity: 1;
  cursor: not-allowed;
}

.no-results {
  text-align: center;
  padding: 2rem;
  color: #666;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}
</style>