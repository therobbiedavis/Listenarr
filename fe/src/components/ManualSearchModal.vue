<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="close">
    <div class="modal-container">
      <div class="modal-header">
        <h2>
          <i class="ph ph-magnifying-glass"></i>
          Manual Search - {{ audiobook?.title }}
        </h2>
        <button class="btn-close" @click="close">
          <i class="ph ph-x"></i>
        </button>
      </div>

      <div class="modal-body">
        <!-- Search Status -->
        <div v-if="searching" class="search-status">
          <i class="ph ph-spinner ph-spin"></i>
          <span>Searching indexers... ({{ searchedIndexers }}/{{ totalIndexers }})</span>
        </div>

        <!-- Results Table -->
        <div v-if="results.length > 0 || !searching" class="results-container">
          <div class="results-header">
            <div class="results-count">
              {{ results.length }} result{{ results.length !== 1 ? 's' : '' }} found
            </div>
            <button 
              v-if="!searching" 
              class="btn btn-secondary btn-sm"
              @click="search"
            >
              <i class="ph ph-arrow-clockwise"></i>
              Refresh
            </button>
          </div>

          <div v-if="results.length === 0 && !searching" class="no-results">
            <i class="ph ph-magnifying-glass"></i>
            <p>No results found</p>
            <p class="hint">Try adjusting your indexer settings or search criteria</p>
          </div>

          <div v-else class="results-table-wrapper">
            <table class="results-table">
              <thead>
                <tr>
                  <th class="col-source">Source</th>
                  <th class="col-age">Age</th>
                  <th class="col-title">Title</th>
                  <th class="col-indexer">Indexer</th>
                  <th class="col-size">Size</th>
                  <th class="col-peers">Peers</th>
                  <th class="col-language">Languages</th>
                  <th class="col-quality">Quality</th>
                  <th class="col-actions"></th>
                </tr>
              </thead>
              <tbody>
                <tr 
                  v-for="result in sortedResults" 
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
                      <span class="title-text">{{ result.title }}</span>
                      <div class="title-meta">
                        <span v-if="result.artist">{{ result.artist }}</span>
                      </div>
                    </div>
                  </td>
                  <td class="col-indexer">
                    <span class="indexer-name">{{ result.source }}</span>
                  </td>
                  <td class="col-size">{{ formatSize(result.size) }}</td>
                  <td class="col-peers">
                    <div class="peers-cell">
                      <span class="seeders" :class="{ 'good': result.seeders > 10, 'medium': result.seeders > 0 && result.seeders <= 10 }">
                        <i class="ph ph-arrow-up"></i> {{ result.seeders }}
                      </span>
                      <span class="leechers">
                        <i class="ph ph-arrow-down"></i> {{ result.leechers }}
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
                  <td class="col-actions">
                    <button 
                      class="btn-icon btn-download"
                      @click="downloadResult(result)"
                      :disabled="downloading[result.id]"
                      :title="downloading[result.id] ? 'Sending to download client...' : 'Download'"
                    >
                      <i v-if="!downloading[result.id]" class="ph ph-download-simple"></i>
                      <i v-else class="ph ph-spinner ph-spin"></i>
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
import { apiService } from '@/services/api'
import type { Audiobook, SearchResult } from '@/types'

interface Props {
  isOpen: boolean
  audiobook: Audiobook | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
  downloaded: [result: SearchResult]
}>()

const searching = ref(false)
const results = ref<SearchResult[]>([])
const downloading = ref<Record<string, boolean>>({})
const searchedIndexers = ref(0)
const totalIndexers = ref(0)

watch(() => props.isOpen, (isOpen) => {
  if (isOpen && props.audiobook) {
    search()
  }
})

const sortedResults = computed(() => {
  return [...results.value].sort((a, b) => {
    // Sort by seeders (descending) for torrents, then by date
    if (a.seeders !== b.seeders) {
      return b.seeders - a.seeders
    }
    return new Date(b.publishedDate).getTime() - new Date(a.publishedDate).getTime()
  })
})

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
    
    // Build search query from title and author
    const query = buildSearchQuery()
    
    // Search all configured indexers (not Amazon/Audible)
    const searchResults = await apiService.searchIndexers(query)
    results.value = searchResults
    
    // Mark all indexers as searched when complete
    searchedIndexers.value = totalIndexers.value
    
  } catch (err) {
    console.error('Manual search failed:', err)
  } finally {
    searching.value = false
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
  
  try {
    // Check if this is a DDL
    const isDDL = getSourceType(result) === 'ddl'
    
    if (isDDL) {
      // For DDL, start download in background and add to activity
      console.log('Starting DDL download:', result.title)
      console.log('Download type:', result.downloadType)
      console.log('Download URL:', result.torrentUrl)
      console.log('Audiobook ID:', props.audiobook.id)
      
      const response = await apiService.sendToDownloadClient(result, undefined, props.audiobook.id)
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
      const response = await apiService.sendToDownloadClient(result, undefined, props.audiobook.id)
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
    
    alert(userMessage)
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
  max-width: 1400px;
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
  justify-content: space-between;
  align-items: center;
  padding-bottom: 1rem;
}

.results-count {
  color: #ccc;
  font-size: 0.9rem;
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

.title-meta {
  font-size: 0.8rem;
  color: #999;
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
