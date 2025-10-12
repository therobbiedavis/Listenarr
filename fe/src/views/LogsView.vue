<template>
  <div class="logs-view">
    <div class="page-header">
      <div class="header-content">
        <h1><i class="ph ph-file-text"></i> Application Logs</h1>
        <p>View and search application log entries</p>
      </div>
      <div class="header-actions">
        <button class="action-button" @click="refreshLogs" :disabled="loading">
          <i :class="loading ? 'ph ph-spinner ph-spin' : 'ph ph-arrow-clockwise'"></i>
          {{ loading ? 'Refreshing...' : 'Refresh' }}
        </button>
        <button class="action-button primary" @click="downloadLogs">
          <i class="ph ph-download-simple"></i>
          Download Logs
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="filter-group">
        <label>Log Level</label>
        <select v-model="selectedLevel" @change="applyFilters">
          <option value="">All Levels</option>
          <option value="Info">Info</option>
          <option value="Warning">Warning</option>
          <option value="Error">Error</option>
          <option value="Debug">Debug</option>
        </select>
      </div>
      <div class="filter-group">
        <label>Search</label>
        <div class="search-input">
          <i class="ph ph-magnifying-glass"></i>
          <input 
            type="text" 
            v-model="searchQuery" 
            @input="applyFilters"
            placeholder="Search log messages..."
          >
          <button 
            v-if="searchQuery" 
            class="clear-button" 
            @click="clearSearch"
            title="Clear search"
          >
            <i class="ph ph-x"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Error State -->
    <div v-if="error" class="error-message">
      <i class="ph ph-warning-circle"></i>
      <div>
        <strong>Error loading logs</strong>
        <p>{{ error }}</p>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading && logs.length === 0" class="loading-state">
      <i class="ph ph-spinner ph-spin"></i>
      <p>Loading logs...</p>
    </div>

    <!-- Logs Table -->
    <div v-else-if="filteredLogs.length > 0" class="logs-container">
      <div class="logs-table">
        <div class="table-header">
          <div class="col-timestamp">Timestamp</div>
          <div class="col-level">Level</div>
          <div class="col-source">Source</div>
          <div class="col-message">Message</div>
        </div>
        <div class="table-body">
          <div 
            v-for="log in paginatedLogs" 
            :key="log.id"
            :class="['log-row', `level-${log.level.toLowerCase()}`]"
          >
            <div class="col-timestamp">
              <i :class="getLogIcon(log.level)"></i>
              {{ formatTimestamp(log.timestamp) }}
            </div>
            <div class="col-level">
              <span :class="['level-badge', log.level.toLowerCase()]">
                {{ log.level }}
              </span>
            </div>
            <div class="col-source">{{ log.source || '-' }}</div>
            <div class="col-message">
              <div class="message-text">{{ log.message }}</div>
              <div v-if="log.exception" class="exception-text">
                <i class="ph ph-warning"></i>
                {{ log.exception }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div class="pagination">
        <div class="pagination-info">
          Showing {{ startIndex + 1 }} - {{ endIndex }} of {{ filteredLogs.length }} logs
        </div>
        <div class="pagination-controls">
          <button 
            class="page-button" 
            @click="previousPage" 
            :disabled="currentPage === 1"
            title="Previous page"
          >
            <i class="ph ph-caret-left"></i>
          </button>
          
          <button
            v-for="page in visiblePages"
            :key="page"
            :class="['page-button', { active: page === currentPage }]"
            @click="goToPage(page)"
          >
            {{ page }}
          </button>

          <button 
            class="page-button" 
            @click="nextPage" 
            :disabled="currentPage === totalPages"
            title="Next page"
          >
            <i class="ph ph-caret-right"></i>
          </button>
        </div>
        <div class="pagination-size">
          <label>Per page:</label>
          <select v-model.number="pageSize" @change="changePageSize">
            <option :value="10">10</option>
            <option :value="25">25</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
          </select>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state">
      <i class="ph ph-file-text"></i>
      <h3>No logs found</h3>
      <p v-if="searchQuery || selectedLevel">Try adjusting your filters</p>
      <p v-else>No log entries available</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getLogs, downloadLogs as downloadLogsApi } from '@/services/api'
import type { LogEntry } from '@/types'

const logs = ref<LogEntry[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const searchQuery = ref('')
const selectedLevel = ref('')
const currentPage = ref(1)
const pageSize = ref(25)

// Load logs
const loadLogs = async () => {
  loading.value = true
  error.value = null
  
  try {
    const data = await getLogs(1000) // Get up to 1000 logs
    logs.value = data
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load logs'
    console.error('Error loading logs:', err)
  } finally {
    loading.value = false
  }
}

// Filter logs
const filteredLogs = computed(() => {
  let filtered = [...logs.value]
  
  // Filter by level
  if (selectedLevel.value) {
    filtered = filtered.filter(log => 
      log.level.toLowerCase() === selectedLevel.value.toLowerCase()
    )
  }
  
  // Filter by search query
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    filtered = filtered.filter(log => 
      log.message.toLowerCase().includes(query) ||
      log.source?.toLowerCase().includes(query) ||
      log.exception?.toLowerCase().includes(query)
    )
  }
  
  return filtered
})

// Pagination
const totalPages = computed(() => Math.ceil(filteredLogs.value.length / pageSize.value))

const startIndex = computed(() => (currentPage.value - 1) * pageSize.value)
const endIndex = computed(() => Math.min(startIndex.value + pageSize.value, filteredLogs.value.length))

const paginatedLogs = computed(() => {
  return filteredLogs.value.slice(startIndex.value, endIndex.value)
})

const visiblePages = computed(() => {
  const pages: number[] = []
  const maxVisible = 5
  let start = Math.max(1, currentPage.value - Math.floor(maxVisible / 2))
  const end = Math.min(totalPages.value, start + maxVisible - 1)
  
  if (end - start < maxVisible - 1) {
    start = Math.max(1, end - maxVisible + 1)
  }
  
  for (let i = start; i <= end; i++) {
    pages.push(i)
  }
  
  return pages
})

const goToPage = (page: number) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }
}

const nextPage = () => {
  if (currentPage.value < totalPages.value) {
    goToPage(currentPage.value + 1)
  }
}

const previousPage = () => {
  if (currentPage.value > 1) {
    goToPage(currentPage.value - 1)
  }
}

const changePageSize = () => {
  currentPage.value = 1 // Reset to first page when changing page size
}

// Actions
const refreshLogs = async () => {
  await loadLogs()
}

const downloadLogs = () => {
  downloadLogsApi()
}

const applyFilters = () => {
  currentPage.value = 1 // Reset to first page when filtering
}

const clearSearch = () => {
  searchQuery.value = ''
  applyFilters()
}

// Formatting
const formatTimestamp = (timestamp: string): string => {
  const date = new Date(timestamp)
  return date.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const getLogIcon = (level: string): string => {
  switch (level.toLowerCase()) {
    case 'info':
      return 'ph ph-info'
    case 'warning':
      return 'ph ph-warning'
    case 'error':
      return 'ph ph-x-circle'
    case 'debug':
      return 'ph ph-bug'
    default:
      return 'ph ph-info'
  }
}

// Load logs on mount
onMounted(() => {
  loadLogs()
})
</script>

<style scoped>
.logs-view {
  padding: 2rem;
  max-width: 1600px;
  margin: 0 auto;
}

/* Page Header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 2rem;
  gap: 2rem;
}

.header-content h1 {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 2rem;
  font-weight: 600;
}

.header-content h1 i {
  font-size: 2rem;
  color: #007acc;
}

.header-content p {
  color: #999;
  font-size: 1rem;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.action-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.65rem 1.25rem;
  background: #2a2a2a;
  color: #fff;
  border: 1px solid #444;
  border-radius: 6px;
  font-size: 0.95rem;
  cursor: pointer;
  transition: all 0.2s;
}

.action-button:hover:not(:disabled) {
  background: #333;
  border-color: #007acc;
  transform: translateY(-1px);
}

.action-button.primary {
  background: #007acc;
  border-color: #007acc;
}

.action-button.primary:hover:not(:disabled) {
  background: #005a9e;
}

.action-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.action-button i {
  font-size: 1.1rem;
}

/* Filters Section */
.filters-section {
  display: flex;
  gap: 1rem;
  margin-bottom: 1.5rem;
  padding: 1.25rem;
  background: #1e1e1e;
  border: 1px solid #333;
  border-radius: 8px;
}

.filter-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-group label {
  color: #999;
  font-size: 0.85rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.filter-group select,
.filter-group input {
  padding: 0.65rem;
  background: #252525;
  color: #fff;
  border: 1px solid #444;
  border-radius: 6px;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.filter-group select:focus,
.filter-group input:focus {
  outline: none;
  border-color: #007acc;
  background: #2a2a2a;
}

.search-input {
  position: relative;
  display: flex;
  align-items: center;
}

.search-input i.ph-magnifying-glass {
  position: absolute;
  left: 0.75rem;
  color: #666;
  font-size: 1rem;
}

.search-input input {
  flex: 1;
  padding-left: 2.5rem;
  padding-right: 2.5rem;
}

.clear-button {
  position: absolute;
  right: 0.5rem;
  padding: 0.25rem;
  background: transparent;
  color: #666;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.clear-button:hover {
  color: #fff;
  background: #333;
}

/* Error Message */
.error-message {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 8px;
  color: #e74c3c;
  margin-bottom: 2rem;
}

.error-message i {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.error-message strong {
  display: block;
  margin-bottom: 0.25rem;
}

.error-message p {
  margin: 0;
  font-size: 0.9rem;
}

/* Loading State */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 4rem;
  color: #999;
}

.loading-state i {
  font-size: 2.5rem;
  color: #007acc;
}

/* Logs Table */
.logs-container {
  background: #1e1e1e;
  border: 1px solid #333;
  border-radius: 8px;
  overflow: hidden;
}

.logs-table {
  overflow-x: auto;
}

.table-header {
  display: grid;
  grid-template-columns: 200px 100px 150px 1fr;
  gap: 1rem;
  padding: 1rem;
  background: #252525;
  border-bottom: 1px solid #333;
  color: #999;
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.table-body {
  max-height: calc(100vh - 400px);
  overflow-y: auto;
}

.log-row {
  display: grid;
  grid-template-columns: 200px 100px 150px 1fr;
  gap: 1rem;
  padding: 1rem;
  border-bottom: 1px solid #2a2a2a;
  transition: background 0.2s;
}

.log-row:hover {
  background: #252525;
}

.log-row:last-child {
  border-bottom: none;
}

.col-timestamp {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #999;
  font-size: 0.9rem;
  font-family: 'Courier New', monospace;
}

.col-timestamp i {
  font-size: 1.1rem;
  flex-shrink: 0;
}

.level-info .col-timestamp i { color: #3498db; }
.level-warning .col-timestamp i { color: #f39c12; }
.level-error .col-timestamp i { color: #e74c3c; }
.level-debug .col-timestamp i { color: #9b59b6; }

.col-level {
  display: flex;
  align-items: center;
}

.level-badge {
  padding: 0.35rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.level-badge.info {
  background: rgba(52, 152, 219, 0.15);
  color: #3498db;
  border: 1px solid rgba(52, 152, 219, 0.3);
}

.level-badge.warning {
  background: rgba(243, 156, 18, 0.15);
  color: #f39c12;
  border: 1px solid rgba(243, 156, 18, 0.3);
}

.level-badge.error {
  background: rgba(231, 76, 60, 0.15);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.level-badge.debug {
  background: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.col-source {
  display: flex;
  align-items: center;
  color: #999;
  font-size: 0.9rem;
}

.col-message {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.message-text {
  color: #fff;
  font-size: 0.95rem;
  line-height: 1.5;
}

.exception-text {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(231, 76, 60, 0.1);
  border-left: 3px solid #e74c3c;
  border-radius: 4px;
  color: #e74c3c;
  font-size: 0.85rem;
  font-family: 'Courier New', monospace;
  line-height: 1.4;
}

.exception-text i {
  flex-shrink: 0;
  margin-top: 0.2rem;
}

/* Pagination */
.pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: #252525;
  border-top: 1px solid #333;
}

.pagination-info {
  color: #999;
  font-size: 0.9rem;
}

.pagination-controls {
  display: flex;
  gap: 0.5rem;
}

.page-button {
  padding: 0.5rem 0.75rem;
  background: transparent;
  color: #999;
  border: 1px solid #444;
  border-radius: 6px;
  font-size: 0.9rem;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-button:hover:not(:disabled) {
  background: #333;
  color: #fff;
  border-color: #007acc;
}

.page-button.active {
  background: #007acc;
  color: #fff;
  border-color: #007acc;
}

.page-button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.pagination-size {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.pagination-size label {
  color: #999;
  font-size: 0.9rem;
}

.pagination-size select {
  padding: 0.5rem;
  background: #1e1e1e;
  color: #fff;
  border: 1px solid #444;
  border-radius: 6px;
  font-size: 0.9rem;
  cursor: pointer;
}

.pagination-size select:focus {
  outline: none;
  border-color: #007acc;
}

/* Empty State */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 4rem;
  color: #666;
  text-align: center;
}

.empty-state i {
  font-size: 4rem;
  color: #444;
}

.empty-state h3 {
  margin: 0;
  color: #999;
  font-size: 1.25rem;
}

.empty-state p {
  margin: 0;
  color: #666;
  font-size: 0.95rem;
}

/* Responsive */
@media (max-width: 1200px) {
  .table-header,
  .log-row {
    grid-template-columns: 180px 90px 120px 1fr;
  }
}

@media (max-width: 768px) {
  .logs-view {
    padding: 1rem;
  }

  .page-header {
    flex-direction: column;
    gap: 1rem;
  }

  .header-actions {
    width: 100%;
  }

  .action-button {
    flex: 1;
  }

  .filters-section {
    flex-direction: column;
  }

  .table-header {
    display: none;
  }

  .log-row {
    grid-template-columns: 1fr;
    gap: 0.75rem;
    padding: 0.75rem;
  }

  .col-timestamp,
  .col-level,
  .col-source,
  .col-message {
    display: flex;
  }

  .pagination {
    flex-direction: column;
    gap: 1rem;
  }

  .pagination-controls {
    order: 2;
  }

  .pagination-info {
    order: 1;
  }

  .pagination-size {
    order: 3;
  }
}
</style>
