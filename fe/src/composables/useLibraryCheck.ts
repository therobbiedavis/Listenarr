import { ref } from 'vue'
import { useLibraryStore } from '@/stores/library'
import { logger } from '@/utils/logger'

export function useLibraryCheck() {
  const libraryStore = useLibraryStore()

  // Library tracking
  const addedAsins = ref(new Set<string>())
  const addedOpenLibraryIds = ref(new Set<string>())

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

    // Also collect stored OpenLibrary IDs from the library (if any)
    const libraryOlIds = new Set(
      libraryStore.audiobooks
        .map(book => book.openLibraryId)
        .filter((id: unknown): id is string => !!id)
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

    // Clean up OpenLibrary IDs previously marked as added
    const currentAddedOl = Array.from(addedOpenLibraryIds.value)
    for (const olid of currentAddedOl) {
      if (!libraryOlIds.has(olid)) {
        logger.debug('Removing OLID from addedOpenLibraryIds (no longer in library):', olid)
        addedOpenLibraryIds.value.delete(olid)
      }
    }

    logger.debug('Added ASINs after cleanup and marking:', Array.from(addedAsins.value))
  }

  const isAudibleAdded = (audibleResult: any): boolean => {
    if (!audibleResult) return false
    if (audibleResult.asin && addedAsins.value.has(audibleResult.asin)) return true
    if (audibleResult.openLibraryId && addedOpenLibraryIds.value.has(audibleResult.openLibraryId)) return true
    return false
  }

  const isTitleResultAdded = (book: any): boolean => {
    const asin = book.searchResult?.asin || book.asin
    const olid = book.searchResult?.id
    if (asin && addedAsins.value.has(asin)) return true
    if (!asin && olid && addedOpenLibraryIds.value.has(olid)) return true
    return false
  }

  const markAsinAdded = (asin: string) => {
    addedAsins.value.add(asin)
  }

  const markOpenLibraryIdAdded = (olid: string) => {
    addedOpenLibraryIds.value.add(olid)
  }

  return {
    addedAsins,
    addedOpenLibraryIds,
    checkExistingInLibrary,
    markExistingResults,
    isAudibleAdded,
    isTitleResultAdded,
    markAsinAdded,
    markOpenLibraryIdAdded
  }
}