<template>
  <div class="add-new-view">
    <div class="page-header">
      <h1><i class="ph ph-plus-circle"></i> Add New Audiobook</h1>
    </div>

    <!-- Unified Search -->
    <div class="search-section">
      <div class="search-method">
        <label class="search-method-label">Search for Audiobooks</label>
        <p class="search-help">Enter an ASIN (e.g., B08G9PRS1K) or search by title and author</p>
      </div>
      
      <div class="unified-search-bar">
        <input
          ref="searchInput"
          v-model="searchQuery"
          type="text"
          :placeholder="searchPlaceholder"
          class="search-input"
          :class="{ error: searchError }"
          @input="handleSearchInput"
          @keyup.enter="performSearch"
        />
        <button 
          @click="performSearch" 
          :disabled="isSearching || !searchQuery.trim()"
          class="search-btn"
        >
          <i v-if="isSearching" class="ph ph-spinner ph-spin"></i>
          <i v-else class="ph ph-magnifying-glass"></i>
          {{ isSearching ? 'Searching...' : 'Search' }}
        </button>
      </div>
      
      <div class="search-hint">
        <i class="ph ph-info"></i>
        <span v-if="searchType === 'asin'">Searching by ASIN</span>
        <span v-else-if="searchType === 'title'">Searching by title/author</span>
        <span v-else-if="searchType === 'isbn'">Searching by ISBN</span>
        <span v-else>Type an ASIN, ISBN, or book title to search</span>
      </div>
      
      <div v-if="searchError" class="error-message">
        <i class="ph ph-warning-circle"></i>
        {{ searchError }}
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isSearching && !hasResults" class="loading-results">
      <div class="loading-spinner">
        <i class="ph ph-spinner ph-spin"></i>
        <p>Searching for audiobooks...</p>
      </div>
    </div>

    <!-- Results Section -->
    <div v-if="hasResults" class="search-results">
      <!-- ASIN Results -->
      <div v-if="searchType === 'asin' && audibleResult">
        <h2>Audiobook Found</h2>
        <div class="result-card">
          <div class="result-poster">
            <img v-if="audibleResult.imageUrl" :src="apiService.getImageUrl(audibleResult.imageUrl)" :alt="audibleResult.title" />
            <div v-else class="placeholder-cover">
              <i class="ph ph-image"></i>
            </div>
          </div>
          <div class="result-info">
            <h3>{{ audibleResult.title }}</h3>
            <p class="result-author">
              by {{ (audibleResult.authors || []).join(', ') || 'Unknown Author' }}
            </p>
            <p v-if="audibleResult.narrators?.length" class="result-narrator">
              Narrated by {{ audibleResult.narrators.join(', ') }}
            </p>
            <div class="result-stats">
              <span v-if="audibleResult.runtime" class="stat-item">
                <i class="ph ph-clock"></i>
                {{ formatRuntime(audibleResult.runtime) }}
              </span>
              <span v-if="audibleResult.language" class="stat-item">
                <i class="ph ph-globe"></i>
                {{ audibleResult.language }}
              </span>
            </div>
            <div class="result-description" v-html="audibleResult.description"></div>
            <div class="result-meta">
              <span v-if="audibleResult.publishYear">{{ audibleResult.publishYear }}</span>
              <span v-if="audibleResult.language">{{ audibleResult.language }}</span>
              <span v-if="audibleResult.publisher">Publisher: {{ audibleResult.publisher }}</span>
              <span v-if="audibleResult.isbn">ISBN: {{ audibleResult.isbn }}</span>
              <span v-if="audibleResult.asin">ASIN: {{ audibleResult.asin }}</span>
              <span v-if="audibleResult.series">Series: {{ audibleResult.series }}</span>
              <span v-if="audibleResult.explicit">Explicit</span>
              <span v-if="audibleResult.abridged">Abridged</span>
            </div>
            <div class="result-actions">
              <button class="btn btn-primary" @click="addToLibrary(audibleResult)">
                <i class="ph ph-plus"></i>
                Add to Library
              </button>
              <button class="btn btn-secondary" @click="viewDetails(audibleResult)">
                <i class="ph ph-eye"></i>
                View Details
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ISBN Auto-processing Status -->
      <div v-if="searchType === 'isbn' && isSearching" class="inline-status">
        <i class="ph ph-spinner ph-spin"></i>
        <span>Searching Amazon/Audible for audiobook...</span>
      </div>
      <div v-else-if="searchType === 'isbn' && !isSearching && isbnLookupMessage" class="inline-status" :class="{ warning: isbnLookupWarning }">
        <i :class="isbnLookupWarning ? 'ph ph-warning-circle' : 'ph ph-info'" />
        <span>{{ isbnLookupMessage }}</span>
      </div>

      <!-- Title Search Results -->
      <div v-if="searchType === 'title' && titleResults.length > 0">
        <h2>Found {{ titleResultsCount }} Book{{ titleResultsCount === 1 ? '' : 's' }}</h2>
        <div class="title-results">
          <div v-for="book in titleResults" :key="book.key" class="title-result-card">
            <div class="result-poster">
              <img v-if="getCoverUrl(book)" :src="getCoverUrl(book)" :alt="book.title" />
              <div v-else class="placeholder-cover">
                <i class="ph ph-book"></i>
              </div>
            </div>
            <div class="result-info">
              <h3>{{ book.title }}</h3>
              <p class="result-author">by {{ formatAuthors(book) }}</p>
              
              <!-- Audiobook metadata from search results -->
              <p v-if="book.searchResult?.narrator" class="result-narrator">
                Narrated by {{ book.searchResult.narrator }}
              </p>
              
              <div class="result-stats">
                <span v-if="book.searchResult?.runtime" class="stat-item">
                  <i class="ph ph-clock"></i>
                  {{ formatRuntime(book.searchResult.runtime) }}
                </span>
                <span v-if="book.searchResult?.series" class="stat-item">
                  <i class="ph ph-book-bookmark"></i>
                  {{ book.searchResult.series }}<span v-if="book.searchResult.seriesNumber"> #{{ book.searchResult.seriesNumber }}</span>
                </span>
              </div>
              
              <p v-if="book.first_publish_year" class="result-year">Published: {{ book.first_publish_year }}</p>
              <p v-if="book.publisher?.length" class="result-publisher">
                Publisher: {{ book.publisher[0] }}
              </p>
              
              <div class="result-meta">
                <span v-if="getAsin(book)">ASIN: {{ getAsin(book) }}</span>
                <span v-if="book.searchResult?.source">Source: {{ book.searchResult.source }}</span>
                <span v-if="book.first_publish_year">Published: {{ book.first_publish_year }}</span>
              </div>
            </div>
            <div class="result-actions">
              <button class="btn btn-primary" @click="selectTitleResult(book)">
                <i class="ph ph-plus"></i>
                Add to Library
              </button>
              <button class="btn btn-secondary" @click="viewTitleResultDetails(book)">
                <i class="ph ph-eye"></i>
                View Details
              </button>
            </div>
          </div>
        </div>
        
        <!-- Load More Button -->
        <div v-if="canLoadMore" class="load-more">
          <button @click="loadMoreTitleResults" :disabled="isLoadingMore" class="btn btn-secondary">
            <i v-if="isLoadingMore" class="ph ph-spinner ph-spin"></i>
            <i v-else class="ph ph-arrow-down"></i>
            {{ isLoadingMore ? 'Loading...' : 'Load More' }}
          </button>
        </div>
      </div>
    </div>

    <!-- No Results -->
    <div v-if="searchType === 'asin' && !audibleResult && !isSearching && searchQuery" class="empty-state">
      <div class="empty-icon">
        <i class="ph ph-magnifying-glass"></i>
      </div>
      <h2>No Audiobook Found</h2>
      <p>No audiobook was found with ASIN "{{ asinQuery }}". Please check the ASIN and try again.</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="searchMethod = 'title'">
          <i class="ph ph-magnifying-glass"></i>
          Try Title Search
        </button>
      </div>
    </div>

    <div v-if="searchType === 'title' && titleResults.length === 0 && !isSearching && searchQuery" class="empty-state">
      <div class="empty-icon">
        <i class="ph ph-book"></i>
      </div>
      <h2 v-if="!asinFilteringApplied">No Books Found</h2>
      <h2 v-else>No Audiobook Matches</h2>
      <p v-if="!asinFilteringApplied">No books were found matching "{{ titleQuery }}"{{ authorQuery ? ` by ${authorQuery}` : '' }}. Try different search terms.</p>
      <p v-else>No audiobooks found. Try refining your search terms.</p>
    </div>

    <!-- Error States -->
    <div v-if="hasError" class="error-state">
      <div class="error-icon">
        <i class="ph ph-warning-circle"></i>
      </div>
      <h2>Search Error</h2>
      <p>{{ errorMessage }}</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="retrySearch">
          <i class="ph ph-arrow-clockwise"></i>
          Try Again
        </button>
      </div>
    </div>

    <!-- Audiobook Details Modal -->
    <AudiobookDetailsModal
      :visible="showDetailsModal"
      :book="selectedBook"
      @close="closeDetailsModal"
      @add-to-library="handleAddToLibrary"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import type { AudibleBookMetadata } from '@/types'
import { apiService } from '@/services/api'
import { openLibraryService, type OpenLibraryBook } from '@/services/openlibrary'
import { isbnService, type ISBNBook } from '@/services/isbn'
import { useConfigurationStore } from '@/stores/configuration'
import AudiobookDetailsModal from '@/components/AudiobookDetailsModal.vue'

const router = useRouter()
const configStore = useConfigurationStore()

// Unified Search
const searchQuery = ref('')
const searchType = ref<'asin' | 'title' | 'isbn' | null>(null)
const isSearching = ref(false)
const searchError = ref('')
const searchDebounceTimer = ref<number | null>(null)

// Results
const audibleResult = ref<AudibleBookMetadata | null>(null)
const titleResults = ref<OpenLibraryBook[]>([])
const resolvedAsins = ref<Record<string, string>>({})
const asinFilteringApplied = ref(false)
const isbnResult = ref<ISBNBook | null>(null) // retained for potential enrichment but not directly rendered
const isbnLookupMessage = ref('')
const isbnLookupWarning = ref(false)
const titleResultsCount = ref(0)
const isLoadingMore = ref(false)
const currentPage = ref(0)
const resultsPerPage = 10

// General state
const errorMessage = ref('')

// Modal state
const showDetailsModal = ref(false)
const selectedBook = ref<AudibleBookMetadata>({} as AudibleBookMetadata)

// Computed properties
const hasResults = computed(() => {
  return (searchType.value === 'asin' && audibleResult.value) ||
         (searchType.value === 'title' && titleResults.value.length > 0) ||
         (searchType.value === 'isbn' && audibleResult.value)
})

const hasError = computed(() => {
  return Boolean(errorMessage.value)
})

const canLoadMore = computed(() => {
  return titleResults.value.length < titleResultsCount.value
})

const searchPlaceholder = computed(() => {
  if (searchType.value === 'asin') {
    return 'Enter ASIN (e.g., B08G9PRS1K)'
  } else if (searchType.value === 'title') {
    return 'Enter book title and author (e.g., "The Hobbit by J.R.R. Tolkien")'
  } else if (searchType.value === 'isbn') {
    return 'Enter ISBN (e.g., 9780547928227 or 0547928220)'
  }
  return 'Search by ASIN, ISBN, or book title...'
})

// Unified Search Methods
const detectSearchType = (query: string): 'asin' | 'title' | 'isbn' => {
  const trimmed = query.trim().toUpperCase()
  
  // ISBN detection first (more specific)
  if (isbnService.detectISBN(trimmed)) {
    return 'isbn'
  }
  
  // ASIN detection: 10 alphanumeric characters, usually starting with B
  if (/^[A-Z0-9]{10}$/.test(trimmed) && trimmed.startsWith('B')) {
    return 'asin'
  }
  // If it looks like an ASIN but doesn't match perfectly, still try ASIN first
  if (/^[A-Z0-9]{8,12}$/.test(trimmed)) {
    return 'asin'
  }
  
  return 'title'
}

const handleSearchInput = () => {
  searchError.value = ''
  const query = searchQuery.value.trim()
  
  // Clear existing timer
  if (searchDebounceTimer.value) {
    clearTimeout(searchDebounceTimer.value)
    searchDebounceTimer.value = null
  }
  
  if (query) {
    searchType.value = detectSearchType(query)
    
    // Auto-search after 1 second of inactivity
    searchDebounceTimer.value = setTimeout(() => {
      performSearch()
    }, 1000) as unknown as number
  } else {
    searchType.value = null
  }
}

const performSearch = async () => {
  const query = searchQuery.value.trim()
  if (!query) {
    searchError.value = 'Please enter a search term'
    return
  }

  const detectedType = detectSearchType(query)
  searchType.value = detectedType

  if (detectedType === 'asin') {
    await searchByAsin(query)
  } else if (detectedType === 'isbn') {
    await searchByISBNChain(query)
  } else {
    await searchByTitle(query)
  }
}

const searchByAsin = async (asin: string) => {
  if (!/^[A-Z0-9]{10}$/.test(asin)) {
    searchError.value = 'Invalid ASIN format. Must be 10 alphanumeric characters (e.g., B08G9PRS1K)'
    return
  }
  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  errorMessage.value = ''
  try {
    const result = await apiService.request<AudibleBookMetadata>(`/audible/metadata/${asin}`)
    audibleResult.value = result
  } catch (error) {
    console.error('ASIN search failed:', error)
    errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobook'
  } finally {
    isSearching.value = false
  }
}

const searchByTitle = async (query: string) => {
  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  titleResultsCount.value = 0
  currentPage.value = 0
  errorMessage.value = ''
  resolvedAsins.value = {}
  asinFilteringApplied.value = false
  
  try {
    // Use backend search API which now searches Amazon/Audible directly
    const searchResults = await apiService.search(query)
    
    // Convert SearchResult objects to displayable format and extract unique ASINs
    titleResults.value = []
    const processedAsins = new Set<string>()
    
    for (const result of searchResults) {
      if (result.asin && !processedAsins.has(result.asin)) {
        processedAsins.add(result.asin)
        resolvedAsins.value[`search-${result.asin}`] = result.asin
        
        // Create a simplified book object for display
        const displayBook = {
          key: `search-${result.asin}`,
          title: result.title,
          author_name: result.artist ? [result.artist] : [],
          first_publish_year: null,
          isbn: [],
          cover_i: null,
          searchResult: result // Store the full search result for metadata
        }
        titleResults.value.push(displayBook)
      }
    }
    
    asinFilteringApplied.value = true
    titleResultsCount.value = titleResults.value.length
    
    if (titleResults.value.length === 0) {
      errorMessage.value = 'No audiobooks found. Try refining your search terms.'
    }
  } catch (error) {
    console.error('Title search failed:', error)
    errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobooks'
  } finally {
    isSearching.value = false
  }
}

const parseSearchQuery = (query: string): { title: string; author?: string } => {
  // Try to parse "title by author" format
  const byMatch = query.match(/^(.+?)\s+by\s+(.+)$/i)
  if (byMatch) {
    return {
      title: byMatch[1].trim(),
      author: byMatch[2].trim()
    }
  }
  
  // Try to parse "author - title" format
  const dashMatch = query.match(/^(.+?)\s*-\s*(.+)$/)
  if (dashMatch) {
    return {
      title: dashMatch[2].trim(),
      author: dashMatch[1].trim()
    }
  }
  
  // Default to treating the entire query as title
  return { title: query }
}

const loadMoreTitleResults = async () => {
  // Since backend search returns all Amazon/Audible results at once,
  // we don't need pagination like OpenLibrary. This function is now a no-op.
  // Results are already loaded in searchByTitle()
  console.log('Load more not needed - all Amazon/Audible results already loaded')
}

const clearTitleError = () => {
  searchError.value = ''
}

// Helper methods for Open Library results
const getCoverUrl = (book: any): string => {
  const imageUrl = book.searchResult?.imageUrl || book.imageUrl || ''
  return apiService.getImageUrl(imageUrl)
}

const formatAuthors = (book: any): string => {
  return book.author_name?.join(', ') || book.searchResult?.artist || 'Unknown Author'
}

const getAsin = (book: any): string | null => {
  return book.searchResult?.asin || resolvedAsins.value[book.key] || null
}

// Removed manual ASIN helper methods (createAsinSearchHint, openAmazonSearch, useBookForAsinSearch)

// Common methods for both search types
const selectTitleResult = async (book: any) => {
  console.log('selectTitleResult called with book:', book)
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin
  
  if (!asin) {
    console.error('No ASIN available for selected book')
    alert('Cannot add to library: No ASIN available')
    return
  }

  console.log('Adding audiobook with ASIN:', asin)
  
  try {
    // Fetch full metadata
    console.log('Fetching metadata from /api/audible/metadata/' + asin)
    const metadata = await apiService.request<AudibleBookMetadata>(`/audible/metadata/${asin}`)
    console.log('Metadata fetched:', metadata)
    
    // Add to library directly
    await addToLibrary(metadata)
  } catch (error) {
    console.error('Failed to add audiobook:', error)
    alert('Failed to add audiobook. Please try again.')
  }
}

const viewTitleResultDetails = async (book: any) => {
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin
  if (asin) {
    try {
      const result = await apiService.request<AudibleBookMetadata>(`/audible/metadata/${asin}`)
      selectedBook.value = result
      showDetailsModal.value = true
    } catch (error) {
      console.error('Failed to fetch detailed metadata:', error)
      alert('Failed to fetch audiobook details. Please try again.')
    }
  } else {
    console.error('No ASIN available for selected book')
  }
}

// Common methods for both search types
const addToLibrary = async (book: AudibleBookMetadata) => {
  // Check if root folder is configured
  if (!configStore.applicationSettings?.outputPath) {
    if (confirm('Root folder is not configured. Would you like to configure it now in Settings?')) {
      router.push('/settings')
    }
    return
  }

  try {
    // Call the backend API to add the audiobook to library
    await apiService.request('/library/add', {
      method: 'POST',
      body: JSON.stringify(book),
      headers: {
        'Content-Type': 'application/json'
      }
    })
    
    alert(`"${book.title}" has been added to your library!`)
    
    // Reset search if needed
    if (searchType.value === 'asin') {
      searchQuery.value = ''
      audibleResult.value = null
    }
  } catch (error: any) {
    console.error('Failed to add audiobook:', error)
    
    // Check if it's a conflict (already exists)
    if (error.message?.includes('409') || error.message?.includes('Conflict')) {
      alert('This audiobook is already in your library.')
    } else {
      alert('Failed to add audiobook. Please try again.')
    }
  }
}

const viewDetails = (book: AudibleBookMetadata) => {
  selectedBook.value = book
  showDetailsModal.value = true
}

const closeDetailsModal = () => {
  showDetailsModal.value = false
}

const handleAddToLibrary = (book: AudibleBookMetadata) => {
  addToLibrary(book)
}

const retrySearch = () => {
  errorMessage.value = ''
  performSearch()
}

// Formatting helpers
const formatDate = (dateString: string): string => {
  try {
    const date = new Date(dateString)
    return date.toLocaleDateString()
  } catch {
    return dateString
  }
}

const formatRuntime = (minutes: number): string => {
  if (!minutes) return 'Unknown'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

// Search by ISBN: fetch basic metadata, then search by title instead of ISBN-to-ASIN conversion
const searchByISBNChain = async (isbn: string) => {
  if (!isbnService.validateISBN(isbn)) {
    searchError.value = 'Invalid ISBN format. Please enter a valid ISBN-10 or ISBN-13'
    return
  }
  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  isbnLookupMessage.value = ''
  isbnLookupWarning.value = false
  errorMessage.value = ''

  try {
    // Fetch metadata from ISBN to get title and author for search
    isbnLookupMessage.value = 'Looking up book details from ISBN...'
    let searchQuery = isbn // fallback to ISBN if metadata lookup fails
    
    try {
      const meta = await isbnService.searchByISBN(isbn)
      if (meta.found && meta.book) {
        isbnResult.value = meta.book
        // Use title and author for Amazon/Audible search
        const title = meta.book.title || ''
        const author = meta.book.authors?.join(', ') || ''
        searchQuery = author ? `${title} by ${author}` : title
      }
    } catch (error) {
      console.warn('ISBN metadata lookup failed, will search by ISBN directly:', error)
    }

    isbnLookupMessage.value = 'Searching Amazon/Audible for audiobook...'
    
    // Search Amazon/Audible using title/author or ISBN
    await searchByTitle(searchQuery)
    searchType.value = 'title' // Update search type since we're now doing title search
    isbnLookupMessage.value = ''
    
    if (titleResults.value.length === 0) {
      isbnLookupWarning.value = true
      isbnLookupMessage.value = 'No audiobooks found for this ISBN. The book may not be available as an audiobook.'
    }
  } catch (error) {
    console.error('ISBN search failed', error)
    isbnLookupWarning.value = true
    isbnLookupMessage.value = 'ISBN search failed'
  } finally {
    isSearching.value = false
  }
}

// Load application settings on mount
onMounted(async () => {
  await configStore.loadApplicationSettings()
})
</script>

<style scoped>
.add-new-view {
  padding: 2em;
}

.page-header h1 {
  margin: 0 0 2rem 0;
  color: white;
  font-size: 2rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

/* Search Tabs */
.search-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 1px solid #444;
}

.tab-btn {
  padding: 1rem 1.5rem;
  background: transparent;
  border: none;
  color: #ccc;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  transition: all 0.2s;
}

.tab-btn:hover {
  color: white;
  background-color: rgba(255, 255, 255, 0.05);
}

.tab-btn.active {
  color: #007acc;
  border-bottom-color: #007acc;
}

/* Search Section */
.search-section {
  margin-bottom: 2rem;
}

.search-method {
  margin-bottom: 1rem;
}

.search-method-label {
  display: block;
  color: white;
  font-weight: 600;
  font-size: 1.1rem;
  margin-bottom: 0.5rem;
}

.search-help {
  color: #ccc;
  font-size: 0.9rem;
  margin: 0;
}

/* Unified Search */
.unified-search-bar {
  display: flex;
  gap: 1rem;
  margin-bottom: 1rem;
}

.unified-search-bar .search-input {
  flex: 1;
  padding: 1rem;
  border: 1px solid #555;
  border-radius: 8px;
  background-color: #2a2a2a;
  color: white;
  font-size: 1rem;
  font-family: inherit;
  text-transform: none;
}

.search-hint {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #888;
  font-size: 0.9rem;
  margin-bottom: 1rem;
}

.search-hint .ph {
  color: #007acc;
}

/* ASIN Search */
.search-bar {
  display: flex;
  gap: 1rem;
}

.search-input {
  flex: 1;
  padding: 1rem;
  border: 1px solid #555;
  border-radius: 8px;
  background-color: #2a2a2a;
  color: white;
  font-size: 1rem;
  text-transform: uppercase;
  font-family: 'Courier New', monospace;
}

.search-input.error {
  border-color: #e74c3c;
  box-shadow: 0 0 0 2px rgba(231, 76, 60, 0.2);
}

.search-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 2px rgba(0, 122, 204, 0.2);
}

/* Title Search Form */
.title-search-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-row {
  display: flex;
  gap: 1rem;
}

.form-group {
  flex: 1;
}

.form-group label {
  display: block;
  color: white;
  font-weight: 500;
  margin-bottom: 0.5rem;
}

.form-input {
  width: 100%;
  padding: 1rem;
  border: 1px solid #555;
  border-radius: 8px;
  background-color: #2a2a2a;
  color: white;
  font-size: 1rem;
}

.form-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 2px rgba(0, 122, 204, 0.2);
}

/* Buttons */
.search-btn {
  padding: 1rem 2rem;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  min-width: 120px;
  justify-content: center;
  align-self: flex-start;
}

.search-btn:hover:not(:disabled) {
  background-color: #005fa3;
}

.search-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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
  min-width: 100px;
  justify-content: center;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005fa3;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
}

/* Error Messages */
.error-message {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #e74c3c;
  font-size: 0.9rem;
  margin-top: 0.5rem;
}

/* Loading Results */
.loading-results {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 4rem 2rem;
  min-height: 300px;
}

.loading-spinner {
  text-align: center;
  color: #ccc;
}

.loading-spinner i {
  font-size: 3rem;
  color: #007acc;
  margin-bottom: 1rem;
}

.loading-spinner p {
  font-size: 1.1rem;
  margin: 0;
}

/* Results */
.search-results h2 {
  color: white;
  margin-bottom: 1rem;
}

/* ASIN Result Card */
.result-card {
  display: flex;
  background-color: #2a2a2a;
  border-radius: 8px;
  overflow: hidden;
  padding: 1rem;
  gap: 1rem;
}

.result-poster {
  width: 120px;
  height: 120px;
  flex-shrink: 0;
  background-color: #555;
  border-radius: 4px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
}

.result-poster img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.placeholder-cover {
  color: #888;
  font-size: 2rem;
}

.result-info {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.result-info h3 {
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 1.3rem;
}

.result-author {
  color: #007acc;
  margin: 0 0 0.25rem 0;
  font-weight: 500;
}

.result-narrator {
  color: #ccc;
  margin: 0 0 1rem 0;
  font-style: italic;
}

.result-stats {
  display: flex;
  gap: 1rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  color: #999;
  font-size: 0.9rem;
  background-color: #333;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
}

.stat-item i {
  color: #007acc;
}

.result-description {
  color: #ccc;
  margin-bottom: 1rem;
  line-height: 1.5;
  flex-grow: 1;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.result-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1rem;
  color: #999;
  font-size: 0.875rem;
}

.result-meta span {
  background-color: #333;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
}

.result-actions {
  display: flex;
  gap: 1rem;
}

.result-actions .btn {
  width: 100%;
}

/* Title Search Results */
.title-results {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.title-result-card {
  display: flex;
  background-color: #2a2a2a;
  border-radius: 8px;
  padding: 1rem;
  gap: 1rem;
  align-items: flex-start;
}

.title-result-card .result-info {
  flex: 1;
}

.title-result-card .result-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 150px;
}

.result-year, .result-publisher {
  color: #999;
  margin: 0.25rem 0;
  font-size: 0.9rem;
}

/* ASIN helper styles removed */

/* Load More */
.load-more {
  text-align: center;
  margin-top: 2rem;
}

/* Empty States */
.getting-started, .empty-state, .error-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.welcome-icon, .empty-icon, .error-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  color: #555;
}

.error-icon {
  color: #e74c3c;
}

.getting-started h2, .empty-state h2, .error-state h2 {
  color: white;
  margin-bottom: 1rem;
}

.help-section {
  margin: 2rem 0;
  text-align: left;
  max-width: 500px;
  margin-left: auto;
  margin-right: auto;
}

.help-section h3 {
  color: white;
  margin-bottom: 1rem;
}

.help-section ul {
  color: #ccc;
  line-height: 1.6;
}

.help-section li {
  margin-bottom: 0.5rem;
}

.quick-actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-top: 2rem;
}

/* Inline status */
.inline-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background-color: rgba(0, 122, 204, 0.1);
  border: 1px solid #007acc;
  border-radius: 8px;
  color: #007acc;
  margin-bottom: 1rem;
}

.inline-status.warning {
  background-color: rgba(241, 196, 15, 0.1);
  border-color: #f1c40f;
  color: #f1c40f;
}

/* Animations */
.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Responsive design */
@media (max-width: 768px) {
  .search-tabs {
    flex-direction: column;
  }
  
  .tab-btn {
    justify-content: center;
  }
  
  .form-row {
    flex-direction: column;
  }
  
  .search-bar {
    flex-direction: column;
  }
  
  .result-card, .title-result-card {
    flex-direction: column;
    text-align: center;
  }
  
  .result-poster {
    width: 100px;
    height: 100px;
    margin: 0 auto 1rem;
  }
  
  .result-actions, .helper-actions {
    justify-content: center;
    flex-wrap: wrap;
  }
  
  .quick-actions {
    flex-direction: column;
    align-items: center;
  }
}
</style>

<style>
/* Global animation for spinner (non-scoped to work properly) */
@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>