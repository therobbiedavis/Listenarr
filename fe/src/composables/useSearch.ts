import { ref, computed } from 'vue'
import type { SearchResult } from '@/types'
import { apiService } from '@/services/api'
import { logger } from '@/utils/logger'

export function useSearch() {
  // Reactive state
  const searchQuery = ref('')
  const searchLanguage = ref('english')
  const searchType = ref<'asin' | 'title' | 'isbn' | null>(null)
  const isSearching = ref(false)
  const searchError = ref('')
  const searchStatus = ref('')
  const searchDebounceTimer = ref<number | null>(null)

  // Abort controller for cancelling searches
  const searchAbortController = ref<AbortController | null>(null)

  // Computed properties
  const searchPlaceholder = computed(() => {
    if (searchType.value === 'asin') {
      return 'Enter ASIN (e.g., B08G9PRS1K)'
    } else if (searchType.value === 'title') {
      return 'Title, author, or ASIN (e.g., The Hobbit)'
    } else if (searchType.value === 'isbn') {
      return 'Enter ISBN (e.g., 9780547928227 or 0547928220)'
    }
    return 'Search by ASIN, ISBN, or title'
  })

  // Methods
  const lastResults = ref<SearchResult[] | null>(null)

  const detectSearchType = (query: string): 'asin' | 'title' | 'isbn' => {
    const trimmed = query.trim().toUpperCase()

    // Check for explicit prefixes first (ASIN:, ISBN:, AUTHOR:, TITLE:)
    if (trimmed.startsWith('ASIN:')) {
      return 'asin'
    }
    if (trimmed.startsWith('ISBN:')) {
      return 'isbn'
    }
    if (trimmed.startsWith('AUTHOR:') || trimmed.startsWith('TITLE:')) {
      return 'title'
    }

    // ISBN detection (more specific)
    // ISBN-13: 13 digits
    const isbn13 = /^\d{13}$/
    if (isbn13.test(trimmed)) {
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

    // If a search is currently running, cancel it immediately so new input
    // will trigger a fresh search (prevents overlapping searches)
    if (isSearching.value) {
      try {
        cancelSearch()
      } catch (e) {
        logger.debug('Error cancelling previous search on input', e)
      }
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
      return null
    }

    const detectedType = detectSearchType(query)
    logger.debug('Detected search type:', detectedType)
    searchType.value = detectedType

    let results
    if (detectedType === 'asin') {
      results = await searchByAsin(query)
    } else if (detectedType === 'isbn') {
      results = await searchByISBN(query)
    } else {
      results = await searchByTitle(query)
    }

    try {
      lastResults.value = results ?? null
    } catch {}
    return results
  }

  const searchByAsin = async (asin: string) => {
    logger.debug('searchByAsin called with:', asin)

    // Strip ASIN: prefix if present
    const cleanAsin = asin.replace(/^ASIN:/i, '').trim()

    // Validate ASIN using the same strict pattern as detection.
    if (!/^(B[0-9A-Z]{9})$/.test(cleanAsin.toUpperCase())) {
      searchError.value = 'Invalid ASIN format. Expected an Amazon ASIN like B08G9PRS1K'
      return
    }

    isSearching.value = true
    searchError.value = ''
    searchStatus.value = `Searching for ASIN ${cleanAsin}...`

    try {
      // Cancel any previous search and create controller for this request
      try {
        searchAbortController.value?.abort()
      } catch {}
      searchAbortController.value = new AbortController()

      const results = await apiService.searchByTitle(`ASIN:${cleanAsin}`, {
        signal: searchAbortController.value.signal,
        language: searchLanguage.value,
      })

      logger.debug('ASIN search results:', results)

      // Process results - this will be handled by the component
      return results
    } catch (error) {
      logger.error('ASIN search failed:', error)
      searchError.value = error instanceof Error ? error.message : 'Failed to search for audiobook'
      throw error
    } finally {
      isSearching.value = false
      // Keep a brief 'done' status then clear
      setTimeout(() => {
        searchStatus.value = ''
      }, 1200)
    }
  }

  const searchByTitle = async (query: string) => {
    logger.debug('searchByTitle called with:', query)

    isSearching.value = true
    searchError.value = ''
    searchStatus.value = 'Searching for audiobooks and fetching metadata...'

    try {
      // Cancel any previous search
      try {
        searchAbortController.value?.abort()
      } catch {}
      searchAbortController.value = new AbortController()

      const results = await apiService.searchByTitle(query, {
        signal: searchAbortController.value.signal,
        language: searchLanguage.value,
      })

      logger.debug('Title search returned:', results)
      logger.debug('Number of results:', results?.length)

      searchStatus.value = 'Processing search results...'

      return results
    } catch (error) {
      if (error && (error as Error).name === 'AbortError') {
        logger.debug('Title search aborted by user')
        searchError.value = 'Search cancelled'
      } else {
        logger.error('Title search failed:', error)
        searchError.value =
          error instanceof Error ? error.message : 'Failed to search for audiobooks'
      }
      throw error
    } finally {
      isSearching.value = false
      // Clear status shortly after completion
      setTimeout(() => {
        searchStatus.value = ''
      }, 1000)
    }
  }

  const searchByISBN = async (isbn: string) => {
    // This will be implemented when extracting the ISBN logic
    logger.debug('searchByISBN called with:', isbn)
    // For now, delegate to title search with ISBN prefix
    return await searchByTitle(`ISBN:${isbn}`)
  }

  const cancelSearch = () => {
    if (searchAbortController.value) {
      try {
        searchAbortController.value.abort()
        logger.debug('User requested search cancellation')
        searchStatus.value = 'Search cancelled'
      } catch (e) {
        logger.debug('Failed to abort search controller', e)
      }
    }
    isSearching.value = false
    // Clear controller reference
    try {
      searchAbortController.value = null
    } catch {}
  }

  return {
    // State
    searchQuery,
    searchLanguage,
    searchType,
    isSearching,
    searchError,
    searchStatus,

    // Computed
    searchPlaceholder,

    // Methods
    detectSearchType,
    handleSearchInput,
    performSearch,
    searchByAsin,
    searchByTitle,
    searchByISBN,
    cancelSearch,

    // Extras
    lastResults,
  }
}
