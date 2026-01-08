<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="close">
    <div class="modal-container">
      <div class="modal-header">
        <h2>
          <i class="ph ph-pencil"></i>
          Bulk Edit Audiobooks
        </h2>
        <button class="btn-close" @click="close">
          <i class="ph ph-x"></i>
        </button>
      </div>

      <div class="modal-body">
        <div class="info-section">
          <i class="ph ph-info"></i>
          <p>
            Editing <strong>{{ selectedCount }}</strong> audiobook{{
              selectedCount !== 1 ? 's' : ''
            }}. Only the fields you change will be updated.
          </p>
        </div>

        <form @submit.prevent="handleSave" class="edit-form">
          <!-- Monitored Status -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-eye"></i>
              Monitored Status
            </label>
            <div class="radio-group">
              <label class="radio-label">
                <input type="radio" v-model="formData.monitored" :value="null" name="monitored" />
                <span>No Change</span>
              </label>
              <label class="radio-label">
                <input type="radio" v-model="formData.monitored" :value="true" name="monitored" />
                <span>Monitored</span>
              </label>
              <label class="radio-label">
                <input type="radio" v-model="formData.monitored" :value="false" name="monitored" />
                <span>Unmonitored</span>
              </label>
            </div>
            <p class="help-text">
              Monitored audiobooks will be automatically upgraded when better quality releases are
              found
            </p>
          </div>

          <!-- Quality Profile -->
          <div class="form-group">
            <label class="form-label" for="quality-profile">
              <i class="ph ph-star"></i>
              Quality Profile
            </label>
            <select id="quality-profile" v-model="formData.qualityProfileId" class="form-select">
              <option :value="null">No Change</option>
              <option v-for="profile in qualityProfiles" :key="profile.id" :value="profile.id">
                {{ profile.name }}{{ profile.isDefault ? ' (Default)' : '' }}
              </option>
            </select>
            <p class="help-text">
              Controls which quality standards to use for downloads and upgrades
            </p>
          </div>

          <!-- Root Folder -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-folder"></i>
              Root Folder
            </label>

            <label class="radio-label">
              <input type="checkbox" v-model="formData.rootChangeEnabled" />
              <span style="margin-left: 0.5rem">Change root folder</span>
            </label>

            <div v-if="formData.rootChangeEnabled" style="margin-top: 0.75rem">
              <RootFolderSelect
                v-model:rootId="formData.rootId"
                v-model:customPath="formData.rootCustomPath"
                data-cy="bulk-root-select"
              />

              <p class="help-text">
                Select a named root or provide a custom path. If you choose "Use default" and no
                named default exists, the application default output path will be used.
              </p>
            </div>
          </div>

          <!-- Action Buttons -->
          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" @click="close">Cancel</button>
            <button type="submit" class="btn btn-primary" :disabled="saving || !hasChanges">
              <i v-if="saving" class="ph ph-spinner ph-spin"></i>
              <i v-else class="ph ph-check"></i>
              {{ saving ? 'Saving...' : 'Save Changes' }}
            </button>
          </div>
        </form>

        <div v-if="showResults" class="results-section">
          <h4>Bulk Update Results</h4>
          <p>
            {{ results.filter((r) => r.success).length }} succeeded,
            {{ results.filter((r) => !r.success).length }} failed
          </p>

          <div class="results-list">
            <div v-for="res in results" :key="res.id" class="result-item">
              <div class="result-header">
                <strong>Audiobook ID {{ res.id }}</strong>
                <span class="status" :class="{ success: res.success, error: !res.success }">{{
                  res.success ? 'Success' : 'Failed'
                }}</span>
              </div>
              <ul v-if="res.errors && res.errors.length > 0" class="error-list">
                <li v-for="(err, idx) in res.errors" :key="idx">{{ err }}</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Move confirmation modal (separate overlay) -->
  <div v-if="showMoveConfirm" class="confirm-overlay separate-modal" @click="cancelMoveConfirm">
    <div class="confirm-dialog" @click.stop>
      <div class="confirm-header">
        <i class="ph ph-folder-open"></i>
        <h3>Move Audiobook Files</h3>
      </div>
      <div class="confirm-body">
        <div class="confirm-description">
          <p>
            You're changing the root folder for <strong>{{ selectedCount }}</strong> audiobook{{
              selectedCount !== 1 ? 's' : ''
            }}. This will move all associated files to the new location.
          </p>
        </div>

        <div class="path-comparison">
          <div class="path-section">
            <div class="path-label">
              <i class="ph ph-arrow-down"></i>
              <span>New Root Folder:</span>
            </div>
            <div class="path-display">
              <code>{{ pendingRootPath || 'No destination path' }}</code>
            </div>
          </div>
        </div>

        <div class="confirm-options">
          <div class="checkbox-row">
            <label>
              <input type="checkbox" v-model="modalMoveFiles" />
              <div class="checkbox-content">
                <span class="checkbox-title">Move files now</span>
                <small>Copy all audiobook files to the new root folder (recommended)</small>
              </div>
            </label>
          </div>
          <div class="checkbox-row" v-if="modalMoveFiles">
            <label>
              <input type="checkbox" v-model="modalDeleteEmpty" />
              <div class="checkbox-content">
                <span class="checkbox-title">Clean up empty folders</span>
                <small>Delete the original folders if they become empty after moving</small>
              </div>
            </label>
          </div>
        </div>
      </div>
      <div class="confirm-actions">
        <button class="btn btn-secondary" @click="cancelMoveConfirm">
          <i class="ph ph-x"></i>
          Cancel
        </button>
        <button class="btn btn-secondary" @click="confirmChangeWithoutMoving">
          <i class="ph ph-database"></i>
          Update Path Only
        </button>
        <button class="btn btn-primary" :disabled="!modalMoveFiles" @click="confirmMove">
          <i class="ph ph-folder-open"></i>
          Move Files
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { apiService } from '@/services/api'
import { useToast } from '@/services/toastService'
import type { QualityProfile } from '@/types'
import { useRootFoldersStore } from '@/stores/rootFolders'
import RootFolderSelect from '@/components/RootFolderSelect.vue'

interface Props {
  isOpen: boolean
  selectedCount: number
  selectedIds: Set<number>
}

interface FormData {
  monitored: boolean | null
  qualityProfileId: number | null
  // root change controls
  rootChangeEnabled: boolean
  rootId: number | null | 0
  rootCustomPath: string | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
  saved: []
}>()

const qualityProfiles = ref<QualityProfile[]>([])
const rootFolders = ref<string[]>([])
const rootStore = useRootFoldersStore()
const saving = ref(false)

// Root change helper values
const defaultOutputPath = ref<string | null>(null)
const results = ref<Array<{ id: number; success: boolean; errors: string[] }>>([])
const showResults = ref(false)

// Move confirmation modal state
const showMoveConfirm = ref(false)
const pendingRootPath = ref<string | null>(null)
const modalMoveFiles = ref(true)
const modalDeleteEmpty = ref(true)
let moveConfirmResolver:
  | ((r: { proceed: boolean; moveFiles: boolean; deleteEmptySource: boolean }) => void)
  | null = null

const formData = ref<FormData>({
  monitored: null,
  qualityProfileId: null,
  rootChangeEnabled: false,
  rootId: null,
  rootCustomPath: null,
})

const hasChanges = computed(() => {
  return (
    formData.value.monitored !== null ||
    formData.value.qualityProfileId !== null ||
    (formData.value.rootChangeEnabled === true &&
      (formData.value.rootId !== null ||
        (formData.value.rootCustomPath && formData.value.rootCustomPath.length > 0)))
  )
})

const toast = useToast()

watch(
  () => props.isOpen,
  async (isOpen) => {
    if (isOpen) {
      await loadData()
      resetForm()
    }
  },
)

async function loadData() {
  try {
    // Load quality profiles
    qualityProfiles.value = await apiService.getQualityProfiles()

    // Load root folders from configuration
    await rootStore.load()
    if (rootStore.folders.length > 0) {
      rootFolders.value = rootStore.folders.map((f) => f.path)
      // Capture default output path for fallback when user picks "Use default"
      const def = rootStore.folders.find((f) => f.isDefault)
      defaultOutputPath.value = def?.path ?? null
    } else {
      const appSettings = await apiService.getApplicationSettings()
      if (appSettings.outputPath) {
        rootFolders.value = [appSettings.outputPath]
        defaultOutputPath.value = appSettings.outputPath
      }
    }
  } catch (error) {
    console.error('Failed to load bulk edit data:', error)
  }
}

function resetForm() {
  formData.value = {
    monitored: null,
    qualityProfileId: null,
    rootChangeEnabled: false,
    rootId: null,
    rootCustomPath: null,
  }
}

function askMoveConfirmation(newRootPath: string) {
  modalMoveFiles.value = true
  modalDeleteEmpty.value = true
  pendingRootPath.value = newRootPath
  showMoveConfirm.value = true
  return new Promise<{ proceed: boolean; moveFiles: boolean; deleteEmptySource: boolean }>(
    (resolve) => {
      moveConfirmResolver = resolve
    },
  )
}

function cancelMoveConfirm() {
  if (moveConfirmResolver)
    moveConfirmResolver({ proceed: false, moveFiles: false, deleteEmptySource: false })
  moveConfirmResolver = null
  showMoveConfirm.value = false
  pendingRootPath.value = null
}

function confirmChangeWithoutMoving() {
  if (moveConfirmResolver)
    moveConfirmResolver({ proceed: true, moveFiles: false, deleteEmptySource: false })
  moveConfirmResolver = null
  showMoveConfirm.value = false
  pendingRootPath.value = null
}

function confirmMove() {
  if (moveConfirmResolver)
    moveConfirmResolver({
      proceed: true,
      moveFiles: Boolean(modalMoveFiles.value),
      deleteEmptySource: Boolean(modalDeleteEmpty.value),
    })
  moveConfirmResolver = null
  showMoveConfirm.value = false
  pendingRootPath.value = null
}

import { logger } from '@/utils/logger'

async function handleSave() {
  if (!hasChanges.value) return

  saving.value = true
  try {
    // Build update payload with only changed fields
    const updates: Record<string, boolean | number | string> = {}

    if (formData.value.monitored !== null) {
      updates.monitored = formData.value.monitored
    }

    if (formData.value.qualityProfileId !== null) {
      updates.qualityProfileId = formData.value.qualityProfileId
    }

    // Handle root folder change with move confirmation
    let userWantsMove = false
    let userWantsDeleteEmpty = false
    let newRootPath: string | null = null

    // Store original basePaths before updating if we're changing root folders
    const originalBasePaths = new Map<number, string>()
    
    if (formData.value.rootChangeEnabled === true) {
      // Resolve chosen root path: named root, custom path or default
      if (formData.value.rootId === 0) {
        newRootPath = formData.value.rootCustomPath || null
      } else if (formData.value.rootId && formData.value.rootId > 0) {
        const found = rootStore.folders.find((f) => f.id === formData.value.rootId)
        newRootPath = found?.path ?? null
      } else if (formData.value.rootId === null) {
        // Use default path (if available)
        newRootPath = defaultOutputPath.value
      }

      if (newRootPath !== null) {
        // Fetch all audiobooks BEFORE updating to capture their original basePaths
        const ids = Array.from(props.selectedIds)
        for (const id of ids) {
          try {
            const audiobook = await apiService.getAudiobook(id)
            if (audiobook?.basePath) {
              originalBasePaths.set(id, audiobook.basePath)
            }
          } catch (err) {
            console.error(`Failed to fetch audiobook ${id} before update:`, err)
          }
        }

        // Ask user if they want to move files
        const choice = await askMoveConfirmation(newRootPath)
        if (!choice || !choice.proceed) {
          saving.value = false
          return // User cancelled
        }
        userWantsMove = Boolean(choice.moveFiles)
        userWantsDeleteEmpty = Boolean(choice.deleteEmptySource)
        updates.rootFolder = newRootPath
      }
    }

    // Add move options if root folder is being changed
    if (formData.value.rootChangeEnabled && newRootPath) {
      ;(updates as { moveFiles?: boolean; deleteEmptySource?: boolean }).moveFiles = userWantsMove
      ;(updates as { moveFiles?: boolean; deleteEmptySource?: boolean }).deleteEmptySource =
        userWantsDeleteEmpty
    }

    // Convert Set to Array for API call
    const ids = Array.from(props.selectedIds)

    // Debug logging: payload and environment
    // This will help diagnose NS_ERROR_CONNECTION_REFUSED in browser
    try {
      logger.debug('[BulkEditModal] Preparing bulk update', {
        endpoint: '/api/library/bulk-update',
        origin: window?.location?.origin,
        ids,
        updates,
        navigatorOnline: typeof navigator !== 'undefined' ? navigator.onLine : undefined,
        timestamp: new Date().toISOString(),
      })
    } catch {
      // ignore logging errors in non-browser envs
    }

    // Call bulk update API
    const resp = await apiService.bulkUpdateAudiobooks(ids, updates)

    // Save per-id results for display
    results.value = resp.results || []
    showResults.value = true

    // If user wants to move files, enqueue move jobs for each audiobook
    if (userWantsMove && newRootPath) {
      console.log('[BulkEditModal] Starting move job enqueue process', { 
        userWantsMove, 
        newRootPath, 
        totalIds: ids.length,
        originalBasePathsSize: originalBasePaths.size 
      })
      
      let moveCount = 0
      for (const id of ids) {
        // Only enqueue move for audiobooks that were successfully updated
        const result = results.value.find((r) => r.id === id)
        console.log(`[BulkEditModal] Processing audiobook ${id}`, { 
          hasResult: !!result, 
          success: result?.success 
        })
        
        if (result && result.success) {
          try {
            // Get the ORIGINAL basePath (before update) and the NEW basePath (after update)
            const originalBasePath = originalBasePaths.get(id)
            const audiobook = await apiService.getAudiobook(id)
            const newBasePath = audiobook?.basePath
            
            console.log(`[BulkEditModal] Audiobook ${id} paths:`, {
              originalBasePath,
              newBasePath,
              pathsAreDifferent: originalBasePath !== newBasePath
            })
            
            if (originalBasePath && newBasePath && originalBasePath !== newBasePath) {
              console.log(`[BulkEditModal] Enqueueing move for audiobook ${id}`, {
                destination: newBasePath,
                source: originalBasePath,
                deleteEmpty: userWantsDeleteEmpty
              })
              
              const moveResult = await apiService.moveAudiobook(id, newBasePath, {
                sourcePath: originalBasePath,
                moveFiles: true,
                deleteEmptySource: userWantsDeleteEmpty,
              })
              
              console.log(`[BulkEditModal] Move enqueued for audiobook ${id}:`, moveResult)
              moveCount++
            } else {
              console.warn(`[BulkEditModal] Skipping move for audiobook ${id} - invalid paths or paths are the same`)
            }
          } catch (moveErr) {
            console.error(`Failed to enqueue move for audiobook ${id}:`, moveErr)
            // Don't fail the entire operation, just log the error
          }
        }
      }

      console.log(`[BulkEditModal] Finished move enqueue process. Queued ${moveCount} moves.`)
      
      if (moveCount > 0) {
        toast.info('Move jobs queued', `Queued ${moveCount} move job(s). Files will be moved in the background.`)
      } else {
        console.warn('[BulkEditModal] No move jobs were queued!')
      }
    } else {
      console.log('[BulkEditModal] Skipping move job enqueue', { userWantsMove, newRootPath })
    }

    // Count successes
    const successCount = results.value.filter((r) => r.success).length
    toast.success('Bulk update', `Updated ${successCount} of ${results.value.length} audiobook(s)`)

    // Notify parent that changes were saved
    emit('saved')
    
    // Close the modal after successful operation
    close()
  } catch (error) {
    // Enhanced error logging so browser console shows more details
    try {
      const err = error as Error & { url?: string }
      console.error('[BulkEditModal] Failed to save bulk edits:', {
        name: err?.name,
        message: err?.message,
        stack: err?.stack,
        url: err?.url || '/api/library/bulk-update',
      })
    } catch {
      // fallback
      console.error('Failed to save bulk edits (minimal):', error)
    }

    // Try to extract a readable message from the API error
    let message = 'Failed to save changes. Please try again.'
    try {
      const err = error as Error & { body?: string }
      if (err.body) {
        try {
          const parsed = JSON.parse(err.body)
          if (parsed && parsed.message) message = parsed.message
        } catch {
          message = err.body
        }
      } else if (err.message) {
        message = err.message
      }
    } catch {
      // fallback
    }
    toast.error('Bulk update failed', message)
  } finally {
    saving.value = false
  }
}

function close() {
  // Reset results when closing
  results.value = []
  showResults.value = false
  emit('close')
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
  border-radius: 6px;
  width: 100%;
  max-width: 600px;
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
  border-radius: 6px;
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
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.results-section {
  margin-top: 1.5rem;
  padding: 1rem;
  background: #141414;
  border: 1px solid #2a2a2a;
  border-radius: 6px;
}

.results-list {
  margin-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.result-item {
  padding: 0.75rem;
  background: #1a1a1a;
  border: 1px solid #2e2e2e;
  border-radius: 6px;
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.status.success {
  color: #4caf50;
}

.status.error {
  color: #f44336;
}

.error-list {
  margin: 0.5rem 0 0 0;
  padding-left: 1.25rem;
  color: #f44336;
}

.info-section {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background-color: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 6px;
  margin-bottom: 2rem;
  color: #3498db;
}

.info-section i {
  font-size: 1.25rem;
  flex-shrink: 0;
  margin-top: 0.125rem;
}

.info-section p {
  margin: 0;
  color: #ccc;
  line-height: 1.5;
}

.info-section strong {
  color: white;
}

.edit-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.form-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: white;
  font-weight: 500;
  font-size: 0.95rem;
}

.form-label i {
  color: #007acc;
}

.radio-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.radio-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  color: #ccc;
}

.radio-label:hover {
  background-color: #333;
  border-color: #007acc;
}

.radio-label input[type='radio'] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #007acc;
}

.radio-label input[type='radio']:checked + span {
  color: white;
  font-weight: 500;
}

.form-select {
  padding: 0.75rem 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  color: white;
  font-size: 0.95rem;
  cursor: pointer;
  transition: all 0.2s;
}

.form-select:hover {
  border-color: #555;
}

.form-select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.help-text {
  margin: 0;
  font-size: 0.85rem;
  color: #999;
  line-height: 1.4;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 1rem;
  padding-top: 1.5rem;
  border-top: 1px solid #3a3a3a;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background-color: #3a3a3a;
  color: #ccc;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #4a4a4a;
  color: white;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005fa3;
}

.btn i.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Move confirmation modal styles */
.confirm-overlay.separate-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1001;
  padding: 1rem;
}

.confirm-dialog {
  background: #1e1e1e;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
  max-width: 600px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  border: 1px solid #333;
}

.confirm-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1.5rem 1.5rem 1rem;
  border-bottom: 1px solid #333;
  margin-bottom: 0;
}

.confirm-header i {
  font-size: 1.5rem;
  color: #007acc;
}

.confirm-header h3 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
  color: #ffffff;
}

.confirm-body {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.confirm-description p {
  margin: 0;
  color: #cccccc;
  line-height: 1.5;
}

.path-comparison {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  background: #252526;
  border-radius: 8px;
  padding: 1rem;
  border: 1px solid #333;
}

.path-section {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.path-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  color: #ffffff;
  font-size: 0.9rem;
}

.path-label i {
  color: #007acc;
  font-size: 1rem;
}

.path-display {
  background: #1e1e1e;
  border: 1px solid #333;
  border-radius: 6px;
  padding: 0.75rem;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 0.85rem;
  color: #cccccc;
  word-break: break-all;
  line-height: 1.4;
}

.path-display code {
  background: transparent;
  color: inherit;
  padding: 0;
  border: none;
  font-family: inherit;
}

.confirm-options {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.checkbox-row {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #252526;
  border-radius: 8px;
  border: 1px solid #333;
  transition: all 0.2s ease;
}

.checkbox-row:hover {
  background: #2d2d30;
  border-color: #007acc;
}

.checkbox-row label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
  width: 100%;
  margin: 0;
}

.checkbox-row input[type='checkbox'] {
  margin-top: 0.125rem;
  width: 1rem;
  height: 1rem;
  accent-color: #007acc;
  cursor: pointer;
}

.checkbox-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkbox-title {
  font-weight: 500;
  color: #ffffff;
  font-size: 0.95rem;
}

.checkbox-content small {
  color: #aaaaaa;
  font-size: 0.8rem;
  line-height: 1.3;
}

.confirm-actions {
  display: flex;
  gap: 0.75rem;
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid #333;
  justify-content: flex-end;
  flex-wrap: wrap;
}

.confirm-actions .btn {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.25rem;
  border-radius: 6px;
  font-weight: 500;
  font-size: 0.9rem;
  transition: all 0.2s ease;
  border: 1px solid transparent;
  cursor: pointer;
  min-width: fit-content;
}

.confirm-actions .btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.confirm-actions .btn-secondary {
  background: #2d2d30;
  color: #cccccc;
  border-color: #333;
}

.confirm-actions .btn-secondary:hover:not(:disabled) {
  background: #3c3c3c;
  border-color: #555;
}

.confirm-actions .btn-primary {
  background: #007acc;
  color: #ffffff;
}

.confirm-actions .btn-primary:hover:not(:disabled) {
  background: #0056b3;
}

/* Mobile responsive adjustments */
@media (max-width: 640px) {
  .confirm-overlay.separate-modal {
    padding: 0.5rem;
  }

  .confirm-dialog {
    max-width: 100%;
    margin: 0;
  }

  .confirm-header {
    padding: 1rem 1rem 0.75rem;
  }

  .confirm-header h3 {
    font-size: 1.1rem;
  }

  .confirm-body {
    padding: 1rem;
    gap: 1rem;
  }

  .path-comparison {
    padding: 0.75rem;
  }

  .path-display {
    padding: 0.5rem;
    font-size: 0.8rem;
  }

  .checkbox-row {
    padding: 0.5rem;
  }

  .confirm-actions {
    padding: 0.75rem 1rem 1rem;
    gap: 0.5rem;
  }

  .confirm-actions .btn {
    padding: 0.625rem 1rem;
    font-size: 0.85rem;
    flex: 1;
    justify-content: center;
  }
}
</style>

