<template>
  <div class="audiobooks-view">
    <!-- Top Toolbar -->
    <div class="toolbar">
      <div class="toolbar-left">
        <button class="toolbar-btn active">
          <i class="ph ph-grid-four"></i>
        </button>
        <button class="toolbar-btn" @click="refreshLibrary">
          <i class="ph ph-arrow-clockwise"></i>
          Refresh
        </button>
        <button 
          v-if="selectedCount > 0" 
          class="toolbar-btn delete-btn"
          @click="confirmBulkDelete"
        >
          <i class="ph ph-trash"></i>
          Delete Selected ({{ selectedCount }})
        </button>
        <button 
          v-if="selectedCount > 0" 
          class="toolbar-btn"
          @click="libraryStore.clearSelection()"
        >
          Clear Selection
        </button>
        <button 
          v-if="audiobooks.length > 0 && selectedCount === 0" 
          class="toolbar-btn"
          @click="libraryStore.selectAll()"
        >
          <i class="ph ph-check-square"></i>
          Select All
        </button>
      </div>
      <div class="toolbar-right">
        <span v-if="audiobooks.length > 0" class="count-badge">
          {{ audiobooks.length }} book{{ audiobooks.length !== 1 ? 's' : '' }}
        </span>
      </div>
    </div>

    <!-- Audiobooks Grid -->
    <div v-if="loading" class="loading-state">
      <i class="ph ph-spinner ph-spin"></i>
      <p>Loading audiobooks...</p>
    </div>
    
    <div v-else-if="error" class="error-state">
      <div class="error-icon">
        <i class="ph ph-warning-circle"></i>
      </div>
      <h2>Error Loading Library</h2>
      <p>{{ error }}</p>
      <button @click="refreshLibrary" class="retry-button">
        <i class="ph ph-arrow-clockwise"></i>
        Retry
      </button>
    </div>
    
    <div v-else-if="audiobooks.length === 0" class="empty-state">
      <div class="empty-icon">
        <i class="ph ph-book-open"></i>
      </div>
      <template v-if="!hasRootFolderConfigured">
        <h2>Root Folder Not Configured</h2>
        <p>Please configure a root folder for your audiobook library in settings before adding audiobooks.</p>
        <router-link to="/settings" class="add-button">
          <i class="ph ph-gear"></i>
          Go to Settings
        </router-link>
      </template>
      <template v-else>
        <h2>No Audiobooks Yet</h2>
        <p>Your library is empty. Add audiobooks to get started!</p>
        <router-link to="/add-new" class="add-button">
          <i class="ph ph-plus"></i>
          Add Audiobooks
        </router-link>
      </template>
    </div>
    
    <div v-else class="audiobooks-grid">
      <div 
        v-for="audiobook in audiobooks" 
        :key="audiobook.id" 
        class="audiobook-item"
        :class="{ selected: libraryStore.isSelected(audiobook.id) }"
        @click="navigateToDetail(audiobook.id)"
      >
        <div class="selection-checkbox">
          <input 
            type="checkbox" 
            :checked="libraryStore.isSelected(audiobook.id)"
            @change="libraryStore.toggleSelection(audiobook.id)"
            @click.stop
          />
        </div>
        <div class="audiobook-poster-container">
          <img 
            :src="apiService.getImageUrl(audiobook.imageUrl) || `https://via.placeholder.com/300x450?text=No+Image`" 
            :alt="audiobook.title" 
            class="audiobook-poster"
          />
          <div class="status-overlay">
            <div class="audiobook-title">{{ audiobook.title }}</div>
            <div class="audiobook-author">{{ audiobook.authors?.join(', ') || 'Unknown Author' }}</div>
          </div>
          <div class="action-buttons">
            <button 
              class="action-btn delete-btn-small" 
              @click.stop="confirmDelete(audiobook)"
              title="Delete"
            >
              <i class="ph ph-trash"></i>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Dialog -->
    <div v-if="showDeleteDialog" class="dialog-overlay" @click="cancelDelete">
      <div class="dialog" @click.stop>
        <div class="dialog-header">
          <h3>
            <i class="ph ph-warning"></i>
            Confirm Deletion
          </h3>
        </div>
        <div class="dialog-body">
          <p v-if="deleteTarget">
            Are you sure you want to delete <strong>{{ deleteTarget.title }}</strong>?
          </p>
          <p v-else-if="bulkDeleteCount > 0">
            Are you sure you want to delete <strong>{{ bulkDeleteCount }} audiobook{{ bulkDeleteCount !== 1 ? 's' : '' }}</strong>?
          </p>
          <p class="warning-text">This action cannot be undone. The audiobook data and cached images will be permanently removed.</p>
        </div>
        <div class="dialog-actions">
          <button class="dialog-btn cancel-btn" @click="cancelDelete">
            Cancel
          </button>
          <button class="dialog-btn confirm-btn" @click="executeDelete" :disabled="deleting">
            <i v-if="deleting" class="ph ph-spinner ph-spin"></i>
            <i v-else class="ph ph-trash"></i>
            {{ deleting ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { useConfigurationStore } from '@/stores/configuration'
import { apiService } from '@/services/api'
import type { Audiobook } from '@/types'

const router = useRouter()
const libraryStore = useLibraryStore()
const configStore = useConfigurationStore()

const audiobooks = computed(() => libraryStore.audiobooks)
const loading = computed(() => libraryStore.loading)
const error = computed(() => libraryStore.error)
const selectedCount = computed(() => libraryStore.selectedIds.size)
const hasRootFolderConfigured = computed(() => {
  return configStore.applicationSettings?.outputPath && 
         configStore.applicationSettings.outputPath.trim().length > 0
})

const showDeleteDialog = ref(false)
const deleteTarget = ref<Audiobook | null>(null)
const bulkDeleteCount = ref(0)
const deleting = ref(false)

onMounted(async () => {
  await Promise.all([
    libraryStore.fetchLibrary(),
    configStore.loadApplicationSettings()
  ])
})

function navigateToDetail(id: number) {
  router.push(`/audiobooks/${id}`)
}

async function refreshLibrary() {
  await libraryStore.fetchLibrary()
}

function confirmDelete(audiobook: Audiobook) {
  deleteTarget.value = audiobook
  bulkDeleteCount.value = 0
  showDeleteDialog.value = true
}

function confirmBulkDelete() {
  deleteTarget.value = null
  bulkDeleteCount.value = libraryStore.selectedIds.size
  showDeleteDialog.value = true
}

function cancelDelete() {
  showDeleteDialog.value = false
  deleteTarget.value = null
  bulkDeleteCount.value = 0
}

async function executeDelete() {
  deleting.value = true
  try {
    if (deleteTarget.value) {
      // Single delete
      await libraryStore.removeFromLibrary(deleteTarget.value.id)
    } else if (bulkDeleteCount.value > 0) {
      // Bulk delete
      const idsToDelete = Array.from(libraryStore.selectedIds)
      await libraryStore.bulkRemoveFromLibrary(idsToDelete)
    }
    showDeleteDialog.value = false
    deleteTarget.value = null
    bulkDeleteCount.value = 0
  } catch (err) {
    console.error('Delete failed:', err)
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.audiobooks-view {
  padding: 0;
  background-color: #1a1a1a;
  min-height: 100vh;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
  margin-bottom: 20px;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.toolbar-btn:hover {
  background-color: #4a4a4a;
}

.toolbar-btn.active {
  background-color: #007acc;
  border-color: #007acc;
}

.toolbar-btn.delete-btn {
  background-color: #e74c3c;
  border-color: #c0392b;
}

.toolbar-btn.delete-btn:hover {
  background-color: #c0392b;
}

.count-badge {
  padding: 6px 12px;
  background-color: #3a3a3a;
  border-radius: 4px;
  color: #ccc;
  font-size: 12px;
}

.audiobooks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 20px;
  padding: 0 20px 20px;
}

.audiobook-item {
  cursor: pointer;
  transition: transform 0.2s ease;
  position: relative;
}

.audiobook-item:hover {
  transform: scale(1.05);
}

.audiobook-item.selected .audiobook-poster-container {
  outline: 3px solid #007acc;
  outline-offset: 2px;
}

.selection-checkbox {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 10;
}

.selection-checkbox input[type="checkbox"] {
  width: 20px;
  height: 20px;
  cursor: pointer;
  accent-color: #007acc;
}

.audiobook-poster-container {
  position: relative;
  aspect-ratio: 1/1;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
}

.audiobook-poster {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.status-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: linear-gradient(transparent, rgba(0, 0, 0, 0.9));
  padding: 30px 8px 8px;
}

.audiobook-title {
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.audiobook-author {
  font-size: 11px;
  color: #ccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.action-buttons {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}

.audiobook-item:hover .action-buttons {
  opacity: 1;
}

.action-btn {
  padding: 6px 8px;
  background-color: rgba(0, 0, 0, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.action-btn:hover {
  background-color: rgba(0, 0, 0, 0.95);
}

.delete-btn-small {
  background-color: rgba(231, 76, 60, 0.9);
  border-color: rgba(192, 57, 43, 0.5);
}

.delete-btn-small:hover {
  background-color: rgba(192, 57, 43, 1);
}

.loading-state, .empty-state, .error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
  color: #ccc;
  text-align: center;
}

.loading-state i, .empty-icon, .error-icon {
  font-size: 4rem;
  color: #555;
  margin-bottom: 1rem;
}

.loading-state i {
  color: #007acc;
}

.error-icon {
  color: #e74c3c;
}

.error-state h2 {
  color: white;
  margin-bottom: 0.5rem;
}

.error-state p {
  margin-bottom: 2rem;
  color: #e74c3c;
}

.retry-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 12px 24px;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.2s;
}

.retry-button:hover {
  background-color: #005fa3;
}

.empty-state h2 {
  color: white;
  margin-bottom: 0.5rem;
}

.empty-state p {
  margin-bottom: 2rem;
}

.add-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 12px 24px;
  background-color: #007acc;
  color: white;
  border-radius: 4px;
  text-decoration: none;
  font-weight: 500;
  transition: background-color 0.2s;
}

.add-button:hover {
  background-color: #005fa3;
}

/* Delete Dialog Styles */
.dialog-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.dialog {
  background-color: #2a2a2a;
  border-radius: 8px;
  border: 1px solid #444;
  width: 90%;
  max-width: 500px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.dialog-header {
  padding: 20px;
  border-bottom: 1px solid #444;
}

.dialog-header h3 {
  margin: 0;
  color: #fff;
  font-size: 18px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.dialog-header i {
  color: #f39c12;
}

.dialog-body {
  padding: 20px;
  color: #ccc;
}

.dialog-body p {
  margin: 0 0 12px 0;
  line-height: 1.5;
}

.dialog-body strong {
  color: #fff;
}

.warning-text {
  color: #f39c12;
  font-size: 14px;
}

.dialog-actions {
  padding: 20px;
  border-top: 1px solid #444;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.dialog-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
}

.cancel-btn {
  background-color: #3a3a3a;
  color: #fff;
}

.cancel-btn:hover {
  background-color: #4a4a4a;
}

.confirm-btn {
  background-color: #e74c3c;
  color: #fff;
}

.confirm-btn:hover:not(:disabled) {
  background-color: #c0392b;
}

.confirm-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
