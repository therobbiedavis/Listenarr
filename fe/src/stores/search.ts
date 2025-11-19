import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { SearchResult } from '@/types'
import { apiService } from '@/services/api'

export const useSearchStore = defineStore('search', () => {
  const searchResults = ref<SearchResult[]>([])
  const isSearching = ref(false)
  const searchQuery = ref('')
  const selectedCategory = ref<string>('')
  const selectedApiIds = ref<string[]>([])
  
  const hasResults = computed(() => searchResults.value.length > 0)

  // Expose store refs for debugging in browser DevTools
  try {
    ;(window as any).pinia_search = {
      searchResults,
      isSearching,
      searchQuery,
      selectedCategory,
      selectedApiIds,
      hasResults,
      // debug functions omitted to avoid forward reference issues
    }
  } catch {}
  
  const search = async (query: string, category?: string, apiIds?: string[]) => {
    isSearching.value = true
    searchQuery.value = query
    selectedCategory.value = category || ''
    selectedApiIds.value = apiIds || []
    
    try {
      // Default to intelligent (Amazon + Audible enrichment) search for unified searches
      const results = await apiService.intelligentSearch(query, category)
      console.log('Search results received:', results)
      console.log('First result:', results[0])
      searchResults.value = results
    } catch (error) {
      console.error('Search failed:', error)
      searchResults.value = []
    } finally {
      isSearching.value = false
    }
  }
  
  const clearResults = () => {
    searchResults.value = []
    searchQuery.value = ''
    selectedCategory.value = ''
    selectedApiIds.value = []
  }
  
  return {
    searchResults,
    isSearching,
    searchQuery,
    selectedCategory,
    selectedApiIds,
    hasResults,
    search,
    clearResults
  }
})