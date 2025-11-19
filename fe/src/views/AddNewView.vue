<template>
  <div class="add-new-view">
    <div class="page-header">
  <h1><PhPlusCircle /> Add New Audiobook</h1>
    </div>

    <!-- Unified Search -->
    <div class="search-section">
      <div class="search-method">
        <label class="search-method-label">Search for Audiobooks</label>
        <p class="search-help">
          Enter an ASIN (e.g., B08G9PRS1K) or search by title and author. 
          <template v-if="enabledMetadataSources.length > 0">
            Metadata powered by: {{ enabledMetadataSources.map(s => s.name).join(', ') }}
          </template>
          <template v-else>
            <router-link to="/settings#apis" class="settings-link">Configure metadata sources</router-link>
          </template>
        </p>
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
          <template v-if="isSearching">
            <PhSpinner class="ph-spin" />
          </template>
          <template v-else>
            <PhMagnifyingGlass />
          </template>
          {{ isSearching ? 'Searching...' : 'Search' }}
        </button>
      </div>
      
      <div class="search-hint">
        <PhInfo />
        <span v-if="searchType === 'asin'">Searching by ASIN</span>
        <span v-else-if="searchType === 'title'">Searching by title/author</span>
        <span v-else-if="searchType === 'isbn'">Searching by ISBN</span>
        <span v-else>Type an ASIN or book title to search</span>
      </div>
      
      <div v-if="searchError" class="error-message">
        <PhWarningCircle />
        {{ searchError }}
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isSearching && !hasResults" class="loading-results">
      <div class="loading-spinner">
        <PhSpinner class="ph-spin" />
        <p>Searching for audiobooks...</p>
        <p v-if="searchStatus" class="search-status">{{ searchStatus }}</p>
      </div>
    </div>

    <!-- Results Section -->
    <div v-if="hasResults" class="search-results">
      <!-- ASIN Results -->
      <div v-if="searchType === 'asin' && audibleResult">
        <h2>Audiobook Found</h2>
        <div class="title-results">
          <div class="title-result-card">
            <div class="result-poster">
              <img v-if="audibleResult.imageUrl" :src="apiService.getImageUrl(audibleResult.imageUrl)" :alt="audibleResult.title" loading="lazy" />
              <div v-else class="placeholder-cover">
                <PhImage />
              </div>
            </div>
            <div class="result-info">
              <h3>
                {{ safeText(audibleResult.title) }}
              </h3>
              <p class="result-author">
                by {{ (audibleResult.authors || []).map(author => safeText(author)).join(', ') || 'Unknown Author' }}
              </p>
              
              <p v-if="audibleResult.narrators?.length" class="result-narrator">
                Narrated by {{ audibleResult.narrators.map(narrator => safeText(narrator)).join(', ') }}
              </p>
              
              <div class="result-stats">
                <span v-if="audibleResult.runtime" class="stat-item">
                  <PhClock />
                  {{ formatRuntime(audibleResult.runtime) }}
                </span>
                <span v-if="audibleResult.language" class="stat-item">
                  <PhGlobe />
                  {{ capitalizeLanguage(audibleResult.language) }}
                </span>
                <span v-if="audibleResult.series" class="stat-item">
                  <PhBook />
                  {{ safeText(audibleResult.series) }}<span v-if="audibleResult.seriesNumber"> #{{ audibleResult.seriesNumber }}</span>
                </span>
              </div>
              
              <p v-if="audibleResult.publishYear" class="result-year">Published: {{ audibleResult.publishYear }}</p>
              <p v-if="audibleResult.publisher" class="result-publisher">
                Publisher: {{ safeText(audibleResult.publisher) }}
              </p>
              
              <div class="result-meta">
                <span v-if="audibleResult.source" class="metadata-source-badge" :data-source="audibleResult.source">
                  <PhCloud />
                  {{ audibleResult.source }}
                </span>
                <span v-if="audibleResult.asin">ASIN: {{ audibleResult.asin }}</span>
                <span v-if="audibleResult.isbn">ISBN: {{ audibleResult.isbn }}</span>
                <span v-if="audibleResult.explicit">Explicit</span>
                <span v-if="audibleResult.abridged">Abridged</span>
              </div>
            </div>
            <div class="result-actions">
              <button 
                :class="['btn', addedAsins.has(audibleResult.asin) ? 'btn-success' : 'btn-primary']"
                @click="addToLibrary(audibleResult)"
                :disabled="addedAsins.has(audibleResult.asin)"
              >
                <component :is="addedAsins.has(audibleResult.asin) ? PhCheck : PhPlus" />
                {{ addedAsins.has(audibleResult.asin) ? 'Added' : 'Add to Library' }}
              </button>
              <button class="btn btn-secondary" @click="viewDetails(audibleResult)">
                <PhEye />
                View Details
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ISBN Auto-processing Status -->
      <div v-if="searchType === 'isbn' && isSearching" class="inline-status">
        <PhSpinner class="ph-spin" />
        <span>Searching Amazon/Audible for audiobook...</span>
      </div>
      <div v-else-if="searchType === 'isbn' && !isSearching && isbnLookupMessage" class="inline-status" :class="{ warning: isbnLookupWarning }">
        <component :is="isbnLookupWarning ? PhWarningCircle : PhInfo" />
        <span>{{ isbnLookupMessage }}</span>
      </div>

      <!-- Title Search Results -->
      <div v-if="searchType === 'title' && titleResults.length > 0">
        <h2>Found {{ titleResultsCount }} Book{{ titleResultsCount === 1 ? '' : 's' }}</h2>
        <div class="title-results">
          <div v-for="book in titleResults" :key="book.key" class="title-result-card">
              <div class="result-poster">
              <img v-if="getCoverUrl(book)" :src="getCoverUrl(book)" :alt="book.title" loading="lazy" />
              <div v-else class="placeholder-cover">
                <PhBook />
              </div>
            </div>
            <div class="result-info">
              <h3>
                {{ safeText(book.title) }}
              </h3>
              <p class="result-author">by {{ formatAuthors(book) }}</p>
              
              <!-- Audiobook metadata from enriched results -->
              <p v-if="book.searchResult?.narrator" class="result-narrator">
                Narrated by {{ book.searchResult.narrator }}
              </p>
              
              <div class="result-stats">
                <span v-if="book.searchResult?.runtime" class="stat-item">
                  <PhClock />
                  {{ formatRuntime(book.searchResult.runtime) }}
                </span>
                <span v-if="book.searchResult?.language" class="stat-item">
                  <PhGlobe />
                  {{ capitalizeLanguage(book.searchResult.language) }}
                </span>
                <span v-if="book.searchResult?.series" class="stat-item">
                  <PhBook />
                  {{ safeText(book.searchResult.series) }}<span v-if="book.searchResult.seriesNumber"> #{{ book.searchResult.seriesNumber }}</span>
                </span>
              </div>
              
              <p v-if="book.first_publish_year" class="result-year">Published: {{ book.first_publish_year }}</p>
              <p v-if="book.publisher?.length" class="result-publisher">
                Publisher: {{ safeText(book.publisher[0]) }}
              </p>
              <p v-if="getAsin(book)" class="result-asin">ASIN: {{ getAsin(book) }}</p>
              
              <div class="result-meta">
                <a v-if="book.metadataSource && getMetadataSourceUrl(book)" 
                   :href="getMetadataSourceUrl(book)!" 
                   target="_blank" 
                   rel="noopener noreferrer"
                   class="metadata-source-link"
                   :data-source="book.metadataSource">
                  Metadata: {{ book.metadataSource }}
                </a>
                <span v-else-if="book.metadataSource" class="metadata-source-badge" :data-source="book.metadataSource">
                  Metadata: {{ book.metadataSource }}
                </span>
                <a v-if="book.searchResult?.productUrl && book.searchResult?.source" 
                   :href="book.searchResult.productUrl" 
                   target="_blank" 
                   rel="noopener noreferrer"
                   class="source-link">
                  Source: {{ book.searchResult.source }}
                </a>
                <span v-else-if="book.searchResult?.source">Source: {{ book.searchResult.source }}</span>
              </div>
            </div>
            <div class="result-actions">
              <button 
                :class="['btn', getAsin(book) && addedAsins.has(getAsin(book)!) ? 'btn-success' : 'btn-primary']"
                @click="selectTitleResult(book)"
                :disabled="!!(getAsin(book) && addedAsins.has(getAsin(book)!))"
              >
                <component :is="getAsin(book) && addedAsins.has(getAsin(book)!) ? PhCheck : PhPlus" />
                {{ getAsin(book) && addedAsins.has(getAsin(book)!) ? 'Added' : 'Add to Library' }}
              </button>
              <button class="btn btn-secondary" @click="viewTitleResultDetails(book)">
                <PhEye />
                View Details
              </button>
            </div>
          </div>
        </div>
        
        <!-- Load More Button -->
        <div v-if="canLoadMore" class="load-more">
          <button @click="loadMoreTitleResults" :disabled="isLoadingMore" class="btn btn-secondary">
            <template v-if="isLoadingMore">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhArrowDown />
            </template>
            {{ isLoadingMore ? 'Loading...' : 'Load More' }}
          </button>
        </div>
      </div>
    </div>

    <!-- No Results -->
    <div v-if="searchType === 'asin' && !audibleResult && !isSearching && searchQuery" class="empty-state">
      <div class="empty-icon">
        <PhMagnifyingGlass />
      </div>
      <h2>No Audiobook Found</h2>
      <p>No audiobook was found with ASIN "{{ asinQuery }}". Please check the ASIN and try again.</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="searchQuery = ''; searchType = 'title'">
          <PhMagnifyingGlass />
          Try Title Search
        </button>
      </div>
    </div>

    <div v-if="searchType === 'title' && titleResults.length === 0 && !isSearching && searchQuery" class="empty-state">
      <div class="empty-icon">
        <PhBook />
      </div>
      <h2 v-if="!asinFilteringApplied">No Books Found</h2>
      <h2 v-else>No Audiobook Matches</h2>
      <p v-if="!asinFilteringApplied">No books were found matching "{{ titleQuery }}"{{ authorQuery ? ` by ${authorQuery}` : '' }}. Try different search terms.</p>
      <p v-else>No audiobooks found. Try refining your search terms.</p>
    </div>

    <!-- Error States -->
    <div v-if="hasError" class="error-state">
      <div class="error-icon">
        <PhWarningCircle />
      </div>
      <h2>Search Error</h2>
      <p>{{ errorMessage }}</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="retrySearch">
          <PhArrowClockwise />
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

    <!-- Add to Library Modal -->
    <AddLibraryModal
      :visible="showAddLibraryModal"
      :book="selectedBookForLibrary"
      @close="closeAddLibraryModal"
      @added="handleLibraryAdded"
    />
    
    <!-- Confirm dialog removed: using centralized showConfirm service mounted in App.vue -->
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { PhPlusCircle, PhSpinner, PhMagnifyingGlass, PhInfo, PhWarningCircle, PhImage, PhClock, PhGlobe, PhCheck, PhPlus, PhEye, PhBook, PhArrowDown, PhArrowClockwise, PhCloud } from '@phosphor-icons/vue'
import { useRouter } from 'vue-router'
import type { AudibleBookMetadata, SearchResult, Audiobook, AudimetaAuthor, AudimetaNarrator, AudimetaGenre } from '@/types'
import { apiService } from '@/services/api'
import type { OpenLibraryBook } from '@/services/openlibrary'
import { isbnService, type ISBNBook } from '@/services/isbn'
import { signalRService } from '@/services/signalr'
import { useConfigurationStore } from '@/stores/configuration'
import { useLibraryStore } from '@/stores/library'
import AudiobookDetailsModal from '@/components/AudiobookDetailsModal.vue'
import AddLibraryModal from '@/components/AddLibraryModal.vue'
import { useToast } from '@/services/toastService'
import { safeText } from '@/utils/textUtils'
import { logger } from '@/utils/logger'

// Extended type for title search results that includes search metadata
type TitleSearchResult = OpenLibraryBook & { 
  searchResult?: SearchResult // Store the enriched SearchResult from intelligent search
  imageUrl?: string // For results that have direct image URLs
  metadataSource?: string // Store which metadata source was used
}

const router = useRouter()
const configStore = useConfigurationStore()
const libraryStore = useLibraryStore()
const toast = useToast()

// Get enabled metadata sources
// Note: temporarily exclude OpenLibrary from the Add New search UI.
// This filters out any API configuration whose name is 'OpenLibrary' (case-insensitive).
const enabledMetadataSources = computed(() => {
  return configStore.apiConfigurations
    .filter(api => api.isEnabled && api.type === 'metadata' && api.name.toLowerCase() !== 'openlibrary')
    .sort((a, b) => a.priority - b.priority) // Sort by priority (lower = higher priority)
})

// Small helper to decode basic HTML entities (covers &amp;, &lt;, &gt;, &quot;, &#39;)
// const decodeHtml = (input?: string | null): string => {
//   if (!input) return ''
//   return input
//     .replace(/&amp;/g, '&')
//     .replace(/&lt;/g, '<')
//     .replace(/&gt;/g, '>')
//     .replace(/&quot;/g, '"')
//     .replace(/&#39;/g, "'")
// }

logger.debug('AddNewView component loaded')
logger.debug('libraryStore:', libraryStore)

// Library checking functions
const checkExistingInLibrary = async () => {
  logger.debug('Checking existing audiobooks in library...')
  
  // Ensure library is loaded
  if (!libraryStore.audiobooks || libraryStore.audiobooks.length === 0) {
    logger.debug('Loading library...')
    await libraryStore.fetchLibrary()
  }
  
  logger.debug('Library has', libraryStore.audiobooks.length, 'audiobooks')
  markExistingResults()
}

const markExistingResults = () => {
  logger.debug('Marking existing results...')
  const libraryAsins = new Set(
    libraryStore.audiobooks
      .map(book => book.asin)
      .filter((asin): asin is string => !!asin)
  )
  
  logger.debug('Library ASINs:', Array.from(libraryAsins))
  
  // Clean up addedAsins: remove ASINs that are no longer in the library
  const currentAddedAsins = Array.from(addedAsins.value)
  for (const asin of currentAddedAsins) {
    if (!libraryAsins.has(asin)) {
      logger.debug('Removing ASIN from addedAsins (no longer in library):', asin)
      addedAsins.value.delete(asin)
    }
  }
  
  // Check ASIN search result
  if (audibleResult.value?.asin) {
    logger.debug('Checking ASIN result:', audibleResult.value.asin)
    if (libraryAsins.has(audibleResult.value.asin)) {
      logger.debug('ASIN result is in library - marking as added')
      addedAsins.value.add(audibleResult.value.asin)
    }
  }
  
  // Check title search results
  if (titleResults.value.length > 0) {
    logger.debug('Checking', titleResults.value.length, 'title results')
    titleResults.value.forEach((book, index) => {
      const asin = getAsin(book)
      if (asin) {
        logger.debug(`Title result ${index}: ASIN=${asin}, inLibrary=${libraryAsins.has(asin)}`)
        if (libraryAsins.has(asin)) {
          logger.debug(`Marking title result ${index} as added`)
          addedAsins.value.add(asin)
        }
      }
    })
  }
  
  logger.debug('Added ASINs after cleanup and marking:', Array.from(addedAsins.value))
}

// Unified Search
const searchQuery = ref('')

// Local storage key for persisting search query
const SEARCH_QUERY_KEY = 'listenarr.addNewSearchQuery'

// Initialize search query from localStorage
try {
  const stored = localStorage.getItem(SEARCH_QUERY_KEY)
  if (stored !== null) searchQuery.value = stored
} catch {}

// Watch search query changes and persist to localStorage
watch(searchQuery, (v) => {
  try { localStorage.setItem(SEARCH_QUERY_KEY, v) } catch {}
})
const searchType = ref<'asin' | 'title' | 'isbn' | null>(null)
const isSearching = ref(false)
const searchError = ref('')
const searchDebounceTimer = ref<number | null>(null)
const searchStatus = ref('')

// Results
const audibleResult = ref<AudibleBookMetadata | null>(null)
const titleResults = ref<TitleSearchResult[]>([])
const resolvedAsins = ref<Record<string, string>>({})
const asinFilteringApplied = ref(false)
const isbnResult = ref<ISBNBook | null>(null) // retained for potential enrichment but not directly rendered
const isbnLookupMessage = ref('')
const isbnLookupWarning = ref(false)
const titleResultsCount = ref(0)
const isLoadingMore = ref(false)
const currentPage = ref(0)
// const resultsPerPage = 10

// Parsed search query components (for error messages)
const asinQuery = ref('')
const titleQuery = ref('')
const authorQuery = ref('')

// Library tracking
const addedAsins = ref(new Set<string>())

// General state
const errorMessage = ref('')

// Modal state
const showDetailsModal = ref(false)
const selectedBook = ref<AudibleBookMetadata>({} as AudibleBookMetadata)
const showAddLibraryModal = ref(false)
const selectedBookForLibrary = ref<AudibleBookMetadata>({} as AudibleBookMetadata)

// Local storage keys for persisting search results
const RESULTS_KEY = 'listenarr.addNewResults'
const SEARCH_TYPE_KEY = 'listenarr.addNewSearchType'
const TITLE_RESULTS_COUNT_KEY = 'listenarr.addNewTitleResultsCount'
const ASIN_FILTERING_KEY = 'listenarr.addNewAsinFiltering'
const RESOLVED_ASINS_KEY = 'listenarr.addNewResolvedAsins'
const ADDED_ASINS_KEY = 'listenarr.addNewAddedAsins'

// Initialize search results from localStorage
try {
  const storedResults = localStorage.getItem(RESULTS_KEY)
  if (storedResults) {
    const parsed = JSON.parse(storedResults)
    if (parsed.audibleResult) audibleResult.value = parsed.audibleResult
    if (parsed.titleResults) titleResults.value = parsed.titleResults
    if (parsed.isbnResult) isbnResult.value = parsed.isbnResult
  }
  
  const storedSearchType = localStorage.getItem(SEARCH_TYPE_KEY)
  if (storedSearchType) {
    searchType.value = storedSearchType as 'asin' | 'title' | 'isbn' | null
  }
  
  const storedCount = localStorage.getItem(TITLE_RESULTS_COUNT_KEY)
  if (storedCount) {
    titleResultsCount.value = parseInt(storedCount, 10)
  }
  
  const storedFiltering = localStorage.getItem(ASIN_FILTERING_KEY)
  if (storedFiltering) {
    asinFilteringApplied.value = storedFiltering === 'true'
  }
  
  const storedResolved = localStorage.getItem(RESOLVED_ASINS_KEY)
  if (storedResolved) {
    resolvedAsins.value = JSON.parse(storedResolved)
  }
  
  const storedAdded = localStorage.getItem(ADDED_ASINS_KEY)
  if (storedAdded) {
    addedAsins.value = new Set(JSON.parse(storedAdded))
  }
} catch (error) {
  console.warn('Failed to restore persisted state:', error)
}

// Watch search results changes and persist to localStorage
watch([audibleResult, titleResults, isbnResult], () => {
  try {
    const results = {
      audibleResult: audibleResult.value,
      titleResults: titleResults.value,
      isbnResult: isbnResult.value
    }
    localStorage.setItem(RESULTS_KEY, JSON.stringify(results))
  } catch {}
})

watch(searchType, (v) => {
  try { localStorage.setItem(SEARCH_TYPE_KEY, v || '') } catch {}
})

watch(titleResultsCount, (v) => {
  try { localStorage.setItem(TITLE_RESULTS_COUNT_KEY, v.toString()) } catch {}
})

watch(asinFilteringApplied, (v) => {
  try { localStorage.setItem(ASIN_FILTERING_KEY, v.toString()) } catch {}
})

watch(resolvedAsins, (v) => {
  try { localStorage.setItem(RESOLVED_ASINS_KEY, JSON.stringify(v)) } catch {}
}, { deep: true })

watch(addedAsins, (v) => {
  try { localStorage.setItem(ADDED_ASINS_KEY, JSON.stringify(Array.from(v))) } catch {}
}, { deep: true })



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

  // ASIN / ISBN-10 pattern (ASINs often start with 'B' followed by 9 alphanumerics).
  // Use a strict regex to avoid misclassifying short title-like strings as ASINs.
  // Pattern covers: 'B' + 9 alnum (typical ASIN) OR 10-digit ISBN-10 (ending with digit or 'X').
  const asinOrIsbn10 = /^(B[0-9A-Z]{9}|\d{9}(?:X|\d))$/
  if (asinOrIsbn10.test(trimmed)) {
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
  logger.debug('performSearch called with query:', query)
  
  if (!query) {
    searchError.value = 'Please enter a search term'
    return
  }

  const detectedType = detectSearchType(query)
  logger.debug('Detected search type:', detectedType)
  searchType.value = detectedType
  searchStatus.value = ''

  if (detectedType === 'asin') {
    await searchByAsin(query)
  } else if (detectedType === 'isbn') {
    await searchByISBNChain(query)
  } else {
    await searchByTitle(query)
  }
}

const searchByAsin = async (asin: string) => {
  logger.debug('searchByAsin called with:', asin)
  
  // Validate ASIN using the same strict pattern as detection.
  if (!/^(B[0-9A-Z]{9})$/.test(asin.toUpperCase())) {
    searchError.value = 'Invalid ASIN format. Expected an Amazon ASIN like B08G9PRS1K'
    return
  }
  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  errorMessage.value = ''
  asinQuery.value = asin
  
  // Check if metadata sources are configured
  if (enabledMetadataSources.value.length === 0) {
    searchStatus.value = 'No metadata sources configured'
    errorMessage.value = 'Please configure at least one metadata source in Settings to fetch audiobook information.'
    isSearching.value = false
    return
  }
  
  searchStatus.value = `Searching for ASIN ${asin}...`
  
  try {
    // Use the new metadata endpoint that respects configured sources
    const response = await apiService.getMetadata(asin, 'us', true)
    const result = response.metadata
    const sourceName = response.source
    
    logger.debug('Metadata response:', { sourceName, hasMetadata: !!result })
    
    searchStatus.value = `Metadata received from ${sourceName}, processing...`
    
    // Convert audimeta response to our audiobook format
    if (result) {
      // Extract year from publishDate if available
      let publishYear: string | undefined
      if (result.publishDate || result.releaseDate) {
        const dateStr = result.publishDate || result.releaseDate
        const yearMatch = dateStr?.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }
      
      audibleResult.value = {
        asin: result.asin || asin,
        title: result.title || 'Unknown Title',
        subtitle: result.subtitle,
        authors: result.authors?.map(a => a.name).filter(n => n) as string[] || [],
        narrators: result.narrators?.map(n => n.name).filter(n => n) as string[] || [],
        publisher: result.publisher,
        publishYear: publishYear,
        description: result.description,
        imageUrl: result.imageUrl,
        runtime: result.lengthMinutes ? result.lengthMinutes * 60 : undefined,
        language: result.language,
        genres: result.genres?.map(g => g.name).filter(n => n) as string[] || [],
        series: result.series?.[0]?.name,
        seriesNumber: result.series?.[0]?.position,
        abridged: result.bookFormat?.toLowerCase().includes('abridged') || false,
        isbn: result.isbn,
        source: sourceName
      }
      
      logger.debug('audibleResult set with source:', audibleResult.value.source)
    }
    
  // Check library status after getting result
  searchStatus.value = 'Checking library for existing copies...'
  await checkExistingInLibrary()
  // Finalize status
  searchStatus.value = audibleResult.value ? `Metadata ready from ${sourceName}` : 'No metadata available'
  } catch (error) {
    logger.error('ASIN search failed:', error)
    errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobook'
  } finally {
    isSearching.value = false
    // Keep a brief 'done' status then clear
    setTimeout(() => { searchStatus.value = '' }, 1200)
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
  
  // Parse query for display in error messages
  const parsed = parseSearchQuery(query)
  titleQuery.value = parsed.title
  authorQuery.value = parsed.author || ''
  
  searchStatus.value = 'Searching for audiobooks and fetching metadata...'
  try {
    // Use intelligent search API that searches Audible/Amazon, gets ASINs, and enriches with metadata
    const results = await apiService.searchByTitle(query)
    logger.debug('Intelligent search returned:', results)
    logger.debug('Number of results:', results?.length)
    
    searchStatus.value = 'Processing search results...'
    
    // Convert enriched SearchResult to display format
    titleResults.value = []
    const processedAsins = new Set<string>()
    
    for (const result of results) {
      const asin = result.asin
      
      if (asin && !processedAsins.has(asin) && result.isEnriched) {
        processedAsins.add(asin)
        resolvedAsins.value[`search-${asin}`] = asin
        
        // Create a book object with metadata from the enriched SearchResult
        const displayBook: TitleSearchResult = {
          key: `search-${asin}`,
          title: result.title || 'Unknown Title',
          author_name: result.artist ? [result.artist] : [],
          isbn: [],
          first_publish_year: result.publishedDate ? 
            parseInt(result.publishedDate.match(/\d{4}/)?.[0] || '0', 10) || undefined : undefined,
          publisher: result.publisher ? [result.publisher] : undefined,
          metadataSource: result.metadataSource, // Which metadata source enriched it (Audimeta, Audnexus, etc.)
          imageUrl: result.imageUrl,
          searchResult: result // Store the full enriched SearchResult
        }
        titleResults.value.push(displayBook)
      }
    }
    
    asinFilteringApplied.value = true
    titleResultsCount.value = titleResults.value.length
    
    if (titleResults.value.length === 0) {
      errorMessage.value = 'No audiobooks found. Try refining your search terms.'
    }
    
    // Check library status after getting results
    searchStatus.value = 'Checking library for existing matches...'
    await checkExistingInLibrary()
    searchStatus.value = `Search complete — found ${titleResults.value.length} items`
  } catch (error) {
    logger.error('Title search failed:', error)
    errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobooks'
  } finally {
    isSearching.value = false
    // Clear status shortly after completion so UI isn't stale
    setTimeout(() => { searchStatus.value = '' }, 1000)
  }
}

const parseSearchQuery = (query: string): { title: string; author?: string } => {
  // Try to parse "title by author" format
  const byMatch = query.match(/^(.+?)\s+by\s+(.+)$/i)
  if (byMatch && byMatch[1] && byMatch[2]) {
    return {
      title: byMatch[1].trim(),
      author: byMatch[2].trim()
    }
  }
  
  // Try to parse "author - title" format
  const dashMatch = query.match(/^(.+?)\s*-\s*(.+)$/)
  if (dashMatch && dashMatch[1] && dashMatch[2]) {
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
  logger.debug('Load more not needed - all Amazon/Audible results already loaded')
}

// const clearTitleError = () => {
//   searchError.value = ''
// }

// Helper methods for Open Library results
const getCoverUrl = (book: TitleSearchResult): string => {
  // Check for audimeta result image URL first
  if (book.imageUrl) {
    return apiService.getImageUrl(book.imageUrl)
  }
  // Fall back to search result image URL
  const imageUrl = book.searchResult?.imageUrl || ''
  return apiService.getImageUrl(imageUrl)
}

const formatAuthors = (book: TitleSearchResult): string => {
  return book.author_name?.join(', ') || book.searchResult?.artist || 'Unknown Author'
}

const getAsin = (book: TitleSearchResult): string | null => {
  return book.searchResult?.asin || resolvedAsins.value[book.key] || null
}

const getMetadataSourceUrl = (book: TitleSearchResult): string | null => {
  const asin = getAsin(book)
  if (!asin) return null
  
  const source = book.metadataSource
  if (!source) return null
  
  // Map metadata source to URL
  if (source.toLowerCase().includes('audimeta')) {
    return `https://audimeta.de/book/${asin}`
  } else if (source.toLowerCase().includes('audnex')) {
    // Audnexus API format
    return `https://api.audnex.us/books/${asin}`
  } else if (source === 'Amazon') {
    return `https://www.amazon.com/dp/${asin}`
  } else if (source === 'Audible') {
    return `https://www.audible.com/pd/${asin}`
  }
  
  return null
}

// Removed manual ASIN helper methods (createAsinSearchHint, openAmazonSearch, useBookForAsinSearch)

// Common methods for both search types
const selectTitleResult = async (book: TitleSearchResult) => {
  logger.debug('selectTitleResult called with book:', book)
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin
  
  if (!asin) {
    logger.error('No ASIN available for selected book')
    toast.warning('Cannot add', 'Cannot add to library: No ASIN available')
    return
  }

  logger.debug('Adding audiobook with ASIN:', asin)
  
  try {
    // If we have enriched search result, use it directly
    if (book.searchResult && book.searchResult.isEnriched) {
      const result = book.searchResult
      logger.debug('Using enriched metadata from intelligent search:', result)
      
      // Extract publish year from date string if available
      let publishYear: string | undefined
      if (result.publishedDate) {
        const yearMatch = result.publishedDate.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }
      
      const metadata: AudibleBookMetadata = {
        asin: result.asin || asin,
        title: result.title || 'Unknown Title',
        subtitle: undefined,
        authors: result.artist ? [result.artist] : [],
        narrators: result.narrator ? [result.narrator] : [],
        publisher: result.publisher,
        publishYear: publishYear,
        description: result.description,
        imageUrl: result.imageUrl,
        runtime: result.runtime,
        language: result.language,
        genres: [],
        series: result.series,
        seriesNumber: result.seriesNumber,
        abridged: false,
        isbn: undefined,
        source: book.metadataSource || result.source
      }
      
      // Add to library directly
      await addToLibrary(metadata)
    } else {
      // Fallback: fetch metadata if not already enriched
      logger.debug('Fetching metadata for ASIN:', asin)
      toast.info('Fetching metadata', `Getting book details from configured sources...`)
      const response = await apiService.getMetadata(asin, 'us', true)
      const audimetaData = response.metadata
      logger.debug(`Metadata fetched from ${response.source}:`, audimetaData)
      toast.success('Metadata retrieved', `Book details fetched from ${response.source}`)
      
      // Store the metadata source in the book object so it shows in the UI
      book.metadataSource = response.source
      
      // Convert audimeta response to AudibleBookMetadata format
      let publishYear: string | undefined
      if (audimetaData.publishDate || audimetaData.releaseDate) {
        const dateStr = audimetaData.publishDate || audimetaData.releaseDate
        const yearMatch = dateStr?.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }
      
      const metadata: AudibleBookMetadata = {
        asin: audimetaData.asin || asin,
        title: audimetaData.title || 'Unknown Title',
        subtitle: audimetaData.subtitle,
        authors: audimetaData.authors?.map((a: AudimetaAuthor) => a.name).filter((n: string | undefined) => n) as string[] || [],
        narrators: audimetaData.narrators?.map((n: AudimetaNarrator) => n.name).filter((n: string | undefined) => n) as string[] || [],
        publisher: audimetaData.publisher,
        publishYear: publishYear,
        description: audimetaData.description,
        imageUrl: audimetaData.imageUrl,
        runtime: audimetaData.lengthMinutes ? audimetaData.lengthMinutes * 60 : undefined,
        language: audimetaData.language,
        genres: audimetaData.genres?.map((g: AudimetaGenre) => g.name).filter((n: string | undefined) => n) as string[] || [],
        series: audimetaData.series?.[0]?.name,
        seriesNumber: audimetaData.series?.[0]?.position,
        abridged: audimetaData.bookFormat?.toLowerCase().includes('abridged') || false,
        isbn: audimetaData.isbn,
        source: response.source
      }
      
      // Add to library directly
      await addToLibrary(metadata)
    }
  } catch (error) {
    logger.error('Failed to add audiobook:', error)
    toast.error('Add failed', 'Failed to add audiobook. Please try again.')
  }
}

const viewTitleResultDetails = async (book: TitleSearchResult) => {
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin
  if (asin) {
    try {
      // If we have enriched search result, use it directly
      if (book.searchResult && book.searchResult.isEnriched) {
        const result = book.searchResult
        logger.debug('Using enriched metadata from intelligent search:', result)
        
        // Extract publish year from date string if available
        let publishYear: string | undefined
        if (result.publishedDate) {
          const yearMatch = result.publishedDate.match(/\d{4}/)
          publishYear = yearMatch ? yearMatch[0] : undefined
        }
        
        selectedBook.value = {
          asin: result.asin || asin,
          title: result.title || 'Unknown Title',
          subtitle: undefined,
          authors: result.artist ? [result.artist] : [],
          narrators: result.narrator ? [result.narrator] : [],
          publisher: result.publisher,
          publishYear: publishYear,
          description: result.description,
          imageUrl: result.imageUrl,
          runtime: result.runtime,
          language: result.language,
          genres: [],
          series: result.series,
          seriesNumber: result.seriesNumber,
          abridged: false,
          isbn: undefined,
          source: book.metadataSource || result.source
        }
      } else {
        // Fallback: fetch metadata if not already enriched
        const response = await apiService.getMetadata(asin, 'us', true)
        const audimetaData = response.metadata
        book.metadataSource = response.source
        
        // Convert to AudibleBookMetadata format
        let publishYear: string | undefined
        if (audimetaData.publishDate || audimetaData.releaseDate) {
          const dateStr = audimetaData.publishDate || audimetaData.releaseDate
          const yearMatch = dateStr?.match(/\d{4}/)
          publishYear = yearMatch ? yearMatch[0] : undefined
        }
        
        selectedBook.value = {
          asin: audimetaData.asin || asin,
          title: audimetaData.title || 'Unknown Title',
          subtitle: audimetaData.subtitle,
          authors: audimetaData.authors?.map((a: AudimetaAuthor) => a.name).filter((n: string | undefined) => n) as string[] || [],
          narrators: audimetaData.narrators?.map((n: AudimetaNarrator) => n.name).filter((n: string | undefined) => n) as string[] || [],
          publisher: audimetaData.publisher,
          publishYear: publishYear,
          description: audimetaData.description,
          imageUrl: audimetaData.imageUrl,
          runtime: audimetaData.lengthMinutes ? audimetaData.lengthMinutes * 60 : undefined,
          language: audimetaData.language,
          genres: audimetaData.genres?.map((g: AudimetaGenre) => g.name).filter((n: string | undefined) => n) as string[] || [],
          series: audimetaData.series?.[0]?.name,
          seriesNumber: audimetaData.series?.[0]?.position,
          abridged: audimetaData.bookFormat?.toLowerCase().includes('abridged') || false,
          isbn: audimetaData.isbn,
          source: response.source
        }
      }
      showDetailsModal.value = true
  } catch (error) {
  logger.error('Failed to fetch detailed metadata:', error)
  toast.error('Fetch failed', 'Failed to fetch audiobook details. Please try again.')
    }
  } else {
    logger.error('No ASIN available for selected book')
  }
}

// Common methods for both search types
const addToLibrary = async (book: AudibleBookMetadata) => {
  // Check if root folder is configured
  if (!configStore.applicationSettings?.outputPath) {
    toast.warning('Root folder not configured', 'Please configure the root folder in Settings before adding audiobooks.')
    router.push('/settings')
    return
  }

  // Show the add to library modal instead of directly adding
  selectedBookForLibrary.value = book
  showAddLibraryModal.value = true
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

const closeAddLibraryModal = () => {
  showAddLibraryModal.value = false
}

const handleLibraryAdded = (audiobook: Audiobook) => {
  // Mark as added in the UI
  if (audiobook.asin) {
    logger.debug('Marking ASIN as added:', audiobook.asin)
    addedAsins.value.add(audiobook.asin)
  }
  
  // Reset search if needed
  if (searchType.value === 'asin') {
    searchQuery.value = ''
    audibleResult.value = null
  }
}

const retrySearch = () => {
  errorMessage.value = ''
  searchStatus.value = ''
  performSearch()
}

// Formatting helpers
// const formatDate = (dateString: string): string => {
//   try {
//     const date = new Date(dateString)
//     return date.toLocaleDateString()
//   } catch {
//     return dateString
//   }
// }

const formatRuntime = (minutes: number): string => {
  if (!minutes) return 'Unknown'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

const capitalizeLanguage = (language: string | undefined): string => {
  if (!language) return ''
  return language.charAt(0).toUpperCase() + language.slice(1).toLowerCase()
}

// Search by ISBN: prefer ISBN->ASIN lookup (strip dashes) and fetch metadata directly.
// Fall back to title-based search only if ASIN resolution fails.
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

  // Normalize ISBN (remove dashes/spaces)
  const cleanedIsbn = isbn.replace(/[-\s]/g, '')

  try {
    // Per requirements: do not convert ISBN to ASIN. Search directly using the ISBN digits.
    searchStatus.value = `Searching Amazon/Audible for ISBN ${cleanedIsbn}...`
    const searchQuery = cleanedIsbn

    // Use the existing title search pipeline but pass the ISBN as the query so
    // backend will attempt to match ISBNs via stripbooks or general search.
    await searchByTitle(searchQuery)
    searchType.value = 'title'
    searchStatus.value = 'ISBN search completed'

    if (titleResults.value.length === 0) {
      isbnLookupWarning.value = true
      isbnLookupMessage.value = 'No audiobooks found for this ISBN. The book may not be available as an audiobook.'
    }
  } catch (error) {
    logger.error('ISBN search failed', error)
    isbnLookupWarning.value = true
    isbnLookupMessage.value = 'ISBN search failed'
  } finally {
    isSearching.value = false
    setTimeout(() => { searchStatus.value = '' }, 1000)
  }
}

// Load application settings and API configurations on mount
onMounted(async () => {
  await configStore.loadApplicationSettings()
  await configStore.loadApiConfigurations()
  
  // Initialize added status on mount
  await checkExistingInLibrary()
  
  // Subscribe to server-side search progress updates
  const unsub = signalRService.onSearchProgress((payload) => {
    if (payload && payload.message) {
      searchStatus.value = payload.message
    }
  })
  // When component is unmounted, unsubscribe
  onUnmounted(() => {
    try { unsub() } catch {}
  })

  // Watch for library changes to update added status
  const stopWatchingLibrary = watch(
    () => libraryStore.audiobooks,
    async (newAudiobooks, oldAudiobooks) => {
      // Only update if the library actually changed (not just on initial load)
      if (oldAudiobooks && oldAudiobooks.length !== newAudiobooks.length) {
        logger.debug('Library changed, updating added status...')
        await checkExistingInLibrary()
      }
    },
    { deep: false } // We don't need deep watching since we're just checking length
  )

  // Cleanup watcher on unmount
  onUnmounted(() => {
    stopWatchingLibrary()
  })
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

.settings-link {
  color: #2196f3;
  text-decoration: none;
  font-weight: 500;
}

.settings-link:hover {
  text-decoration: underline;
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
  margin-bottom: 2.5rem;
  background-color: #2a2a2a;
  padding: 1.5rem;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.search-method {
  margin-bottom: 1.25rem;
}

.search-method-label {
  display: block;
  color: white;
  font-weight: 600;
  font-size: 1.25rem;
  margin-bottom: 0.5rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-help {
  color: #adb5bd;
  font-size: 0.9rem;
  margin: 0;
  line-height: 1.5;
}

.search-help .settings-link {
  color: #4dabf7;
  text-decoration: none;
  font-weight: 500;
  transition: color 0.2s ease;
}

.search-help .settings-link:hover {
  color: #74c0fc;
  text-decoration: underline;
}

/* Unified Search */
.unified-search-bar {
  display: flex;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.unified-search-bar .search-input {
  flex: 1;
  padding: 0.875rem 1.125rem;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  background-color: rgba(0, 0, 0, 0.2);
  color: white;
  font-size: 1rem;
  font-family: inherit;
  text-transform: none;
  transition: all 0.2s ease;
}

.unified-search-bar .search-input:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
}

.unified-search-bar .search-input::placeholder {
  color: #6c757d;
}

.search-hint {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #6c757d;
  font-size: 0.875rem;
  padding: 0.5rem 0.75rem;
  background-color: rgba(255, 255, 255, 0.03);
  border-radius: 6px;
  margin-bottom: 0;
}

.search-hint svg {
  color: #4dabf7;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

/* ASIN Search */
.search-bar {
  display: flex;
  gap: 1rem;
}

.search-input {
  flex: 1;
  padding: 0.875rem 1.125rem;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  background-color: rgba(0, 0, 0, 0.2);
  color: white;
  font-size: 1rem;
  text-transform: uppercase;
  font-family: 'Courier New', monospace;
  transition: all 0.2s ease;
}

.search-input.error {
  border-color: #fa5252;
  background-color: rgba(250, 82, 82, 0.05);
  box-shadow: 0 0 0 3px rgba(250, 82, 82, 0.1);
}

.search-input:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
}

.search-input::placeholder {
  color: #6c757d;
  text-transform: none;
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
  padding: 0.875rem 1.75rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  min-width: 140px;
  justify-content: center;
  font-size: 0.95rem;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.search-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
  transform: translateY(-1px);
}

.search-btn:active:not(:disabled) {
  transform: translateY(0);
}

.search-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.search-btn svg {
  width: 18px;
  height: 18px;
}

.search-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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
  min-width: 100px;
  justify-content: center;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.btn-primary:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
  transform: translateY(-1px);
}

.btn-primary:active:not(:disabled) {
  transform: translateY(0);
}

.btn-success {
  background: linear-gradient(135deg, #2ecc71 0%, #27ae60 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(46, 204, 113, 0.3);
}

.btn-success:disabled {
  opacity: 0.7;
  cursor: not-allowed;
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

.btn-secondary:active:not(:disabled) {
  transform: translateY(0);
}

/* Error Messages */
/* Error Messages */
.error-message {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  color: #fff;
  background-color: rgba(250, 82, 82, 0.15);
  border: 1px solid rgba(250, 82, 82, 0.3);
  border-radius: 8px;
  padding: 0.875rem 1.125rem;
  font-size: 0.9rem;
  margin-top: 1rem;
}

.error-message svg {
  color: #fa5252;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

/* Loading Results */
.loading-results {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 4rem 2rem;
  min-height: 300px;
  background-color: #2a2a2a;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.loading-spinner {
  text-align: center;
  color: #adb5bd;
}

.loading-spinner svg {
  font-size: 3rem;
  color: #4dabf7;
  margin-bottom: 1rem;
  width: 48px;
  height: 48px;
}

.loading-spinner i {
  font-size: 3rem;
  color: #4dabf7;
  margin-bottom: 1rem;
}

.loading-spinner p {
  font-size: 1.1rem;
  margin: 0;
  font-weight: 500;
}

.search-status {
  font-size: 0.875rem;
  color: #6c757d;
  margin: 0.75rem 0 0 0;
  font-style: italic;
}

/* Results */
.search-results h2 {
  color: white;
  margin-bottom: 1.5rem;
  font-size: 1.5rem;
  font-weight: 600;
}

/* ASIN Result Card */
.result-card {
  display: flex;
  background-color: #2a2a2a;
  border-radius: 8px;
  overflow: hidden;
  padding: 1.25rem;
  gap: 1.25rem;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.result-card:hover {
  background-color: #2f2f2f;
  border-color: rgba(33, 150, 243, 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.result-poster {
  width: 140px;
  height: 140px;
  flex-shrink: 0;
  background-color: #555;
  border-radius: 6px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  transition: transform 0.2s ease;
}

.result-card:hover .result-poster {
  transform: scale(1.02);
}

.result-poster img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.placeholder-cover {
  color: #888;
  font-size: 2.5rem;
}

.result-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}

.result-info h3 {
  margin: 0;
  color: white;
  font-size: 1.4rem;
  line-height: 1.3;
  font-weight: 600;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-author {
  color: #4dabf7;
  margin: 0;
  font-weight: 500;
  font-size: 0.95rem;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-narrator {
  color: #adb5bd;
  margin: 0;
  font-style: italic;
  font-size: 0.9rem;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-stats {
  display: flex;
  gap: 0.75rem;
  margin: 0.25rem 0;
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  color: #adb5bd;
  font-size: 0.875rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  white-space: nowrap;
  transition: background-color 0.2s ease;
}

.stat-item:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.stat-item svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.stat-item i {
  color: #4dabf7;
}

.result-description {
  color: #ccc;
  margin: 0.5rem 0;
  line-height: 1.5;
  flex-grow: 1;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  -webkit-box-orient: vertical;
}

.result-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin: 0.75rem 0 0 0;
  color: #999;
  font-size: 0.875rem;
}

.result-meta span,
.result-meta a.source-link {
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  text-decoration: none;
  color: #adb5bd;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.result-meta span:hover,
.result-meta a.source-link:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.metadata-source-badge,
.metadata-source-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(33, 150, 243, 0.15) !important;
  color: #4dabf7 !important;
  font-weight: 500;
  padding: 0.35rem 0.7rem !important;
  border-radius: 6px !important;
  text-decoration: none;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.metadata-source-link:hover {
  background-color: rgba(33, 150, 243, 0.25) !important;
  color: #74c0fc !important;
  transform: translateY(-1px);
}

.metadata-source-badge svg,
.metadata-source-link svg {
  width: 14px;
  height: 14px;
}

.result-meta a.source-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(255, 255, 255, 0.05);
  color: #adb5bd;
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  text-decoration: none;
  transition: all 0.2s ease;
}

.result-meta a.source-link:hover {
  background-color: rgba(33, 150, 243, 0.15);
  color: #4dabf7;
  transform: translateY(-1px);
}

.result-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.result-actions .btn {
  flex: 1;
  min-width: 0;
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

.result-year, .result-publisher, .result-asin {
  color: #868e96;
  margin: 0;
  font-size: 0.875rem;
  line-height: 1.5;
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
    margin: 0 auto;
  }

  .result-info {
    width: 100%;
  }

  .result-stats, .result-meta {
    margin: 0 auto;
  }
  
  .result-actions, .helper-actions {
    justify-content: center;
    flex-wrap: wrap;
  }
  
  .quick-actions {
    flex-direction: column;
    align-items: center;
  }

  /* Mobile improvements: stack unified search and make CTAs full width */
  .unified-search-bar {
    flex-direction: column;
    gap: 0.5rem;
  }

  .unified-search-bar .search-input {
    width: 100%;
    font-size: 1rem;
  }

  .unified-search-bar .search-btn {
    width: 100%;
    min-width: 0;
    padding: 0.875rem;
  }

  /* Make result action buttons stack and be larger on mobile */
  .title-result-card .result-actions,
  .result-card .result-actions {
    flex-direction: column;
    gap: 0.5rem;
    width: 100%;
  }

  .title-result-card .result-actions .btn,
  .result-card .result-actions .btn {
    width: 100%;
    padding: 0.9rem 1rem;
    font-size: 1rem;
  }

  /* Reduce page padding for small devices to maximize content space */
  .add-new-view {
    padding: 1rem;
  }

  /* Ensure results area keeps a stable gutter for scrollbars */
  .search-results,
  .title-results,
  .search-section {
    scrollbar-gutter: stable both-edges;
  }

  /* Allow long metadata badges and names to wrap instead of overflowing */
  .result-meta span,
  .metadata-source-badge,
  .metadata-source-link {
    white-space: normal;
    overflow-wrap: anywhere;
  }
}
</style>