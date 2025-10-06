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
            <h4>{{ result.title }}</h4>
            <p class="result-artist">{{ result.artist }}</p>
            <p class="result-album">{{ result.album }}</p>
            
            <!-- Audiobook metadata -->
            <div v-if="result.narrator || result.runtime || result.series" class="audiobook-meta">
              <p v-if="result.narrator" class="meta-narrator">
                Narrated by {{ result.narrator }}
              </p>
              <div class="meta-details">
                <span v-if="result.runtime" class="meta-runtime">
                  ⏱ {{ formatRuntime(result.runtime) }}
                </span>
                <span v-if="result.series" class="meta-series">
                  Series: {{ result.series }}<span v-if="result.seriesNumber"> #{{ result.seriesNumber }}</span>
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
              class="add-button"
              :disabled="isAddingToLibrary"
            >
              <i class="ph ph-plus"></i>
              Add to Library
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
import { ref } from 'vue'
import { useSearchStore } from '@/stores/search'
import { apiService } from '@/services/api'
import type { SearchResult, AudibleBookMetadata } from '@/types'

const searchStore = useSearchStore()

const searchQuery = ref('')
const selectedCategory = ref('')
const isAddingToLibrary = ref(false)

const performSearch = async () => {
  if (!searchQuery.value.trim()) return
  
  await searchStore.search(
    searchQuery.value.trim(),
    selectedCategory.value || undefined
  )
}

const addToLibrary = async (result: SearchResult) => {
  console.log('addToLibrary called with result:', result)
  
  if (!result.asin) {
    console.warn('No ASIN available for result:', result)
    alert('Cannot add to library: No ASIN available for this result')
    return
  }

  console.log('Adding to library, ASIN:', result.asin)
  isAddingToLibrary.value = true
  try {
    // Fetch full metadata from the backend
    console.log('Fetching metadata from /api/audible/metadata/' + result.asin)
    const metadata = await apiService.request<AudibleBookMetadata>(`/audible/metadata/${result.asin}`)
    console.log('Metadata fetched:', metadata)
    
    // Add to library
    console.log('Adding to library via /api/library/add')
    await apiService.request('/library/add', {
      method: 'POST',
      body: JSON.stringify(metadata),
      headers: {
        'Content-Type': 'application/json'
      }
    })
    
    console.log('Successfully added to library')
    alert(`"${metadata.title}" has been added to your library!`)
  } catch (error: any) {
    console.error('Failed to add audiobook:', error)
    
    // Check if it's a conflict (already exists)
    if (error.message?.includes('409') || error.message?.includes('Conflict')) {
      alert('This audiobook is already in your library.')
    } else {
      alert('Failed to add audiobook. Please try again.')
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

.no-results {
  text-align: center;
  padding: 2rem;
  color: #666;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}
</style>