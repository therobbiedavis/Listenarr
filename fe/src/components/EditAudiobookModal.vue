<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="close">
    <div class="modal-container">
      <div class="modal-header">
        <h2>
          <i class="ph ph-pencil"></i>
          Edit Audiobook
        </h2>
        <button class="btn-close" @click="close">
          <i class="ph ph-x"></i>
        </button>
      </div>

      <div class="modal-body">
        <div class="info-section">
          <i class="ph ph-info"></i>
          <p>
            Editing <strong>{{ audiobook?.title }}</strong> by
            <strong>{{ audiobook?.authors?.join(', ') || 'Unknown Author' }}</strong>
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
              <label class="radio-label" :class="{ active: formData.monitored === true }">
                <input type="radio" v-model="formData.monitored" :value="true" name="monitored" />
                <div class="radio-content">
                  <span class="radio-title">Monitored</span>
                  <small>Automatically search for and upgrade releases</small>
                </div>
              </label>
              <label class="radio-label" :class="{ active: formData.monitored === false }">
                <input type="radio" v-model="formData.monitored" :value="false" name="monitored" />
                <div class="radio-content">
                  <span class="radio-title">Unmonitored</span>
                  <small>Do not search for new releases</small>
                </div>
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
              <option :value="null">Use Default Profile</option>
              <option v-for="profile in qualityProfiles" :key="profile.id" :value="profile.id">
                {{ profile.name }}{{ profile.isDefault ? ' (Default)' : '' }}
              </option>
            </select>
            <p class="help-text">
              Controls which quality standards to use for downloads and upgrades. Leave as "Use
              Default Profile" to automatically use the default profile.
            </p>
          </div>

          <!-- Destination / Base Path -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-folder"></i>
              Destination Folder
            </label>
            <div class="destination-display">
              <!-- Read-only display mode -->
              <div v-if="!editingDestination" class="destination-readonly">
                <input
                  type="text"
                  :value="combinedBasePath() || 'No destination set'"
                  class="form-input readonly-input"
                  readonly
                  disabled
                />
                <button
                  type="button"
                  class="btn-edit-destination"
                  @click="startEditingDestination"
                  title="Edit destination"
                >
                  <PhPencil :size="16" />
                  <span class="btn-text">Edit</span>
                </button>
              </div>
              <!-- Edit mode -->
              <div v-else class="destination-edit">
                <div class="destination-row">
                  <div class="root-select">
                    <RootFolderSelect
                      v-model:rootId="selectedRootId"
                      v-model:customPath="customRootPath"
                    />
                  </div>
                  <input
                    type="text"
                    v-model="formData.relativePath"
                    class="form-input relative-input"
                    placeholder="e.g. Author/Title"
                  />
                </div>
                <div class="destination-actions">
                  <button
                    type="button"
                    class="btn btn-secondary btn-sm"
                    @click="editingDestination = false"
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    class="btn btn-primary btn-sm"
                    @click="editingDestination = false"
                  >
                    Done
                  </button>
                </div>
              </div>
              <p class="help-text">
                <span v-if="!editingDestination"
                  >Click the edit button to change the destination folder.</span
                >
                <span v-else
                  >Select a named root (or custom path) and edit the path relative to it on the
                  right.</span
                >
              </p>
            </div>
          </div>

          <!-- Tags -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-tag"></i>
              Tags
            </label>
            <div class="tags-container">
              <div class="tags-list">
                <span v-for="(tag, index) in formData.tags" :key="index" class="tag-item">
                  {{ tag }}
                  <button
                    type="button"
                    class="tag-remove"
                    @click="removeTag(index)"
                    title="Remove tag"
                  >
                    <PhX :size="16" weight="bold" />
                  </button>
                </span>
                <span v-if="formData.tags.length === 0" class="tags-empty">
                  No tags added yet
                </span>
              </div>
              <div class="tag-input-group">
                <input
                  type="text"
                  v-model="newTag"
                  @keypress.enter.prevent="addTag"
                  placeholder="Add a tag..."
                  class="tag-input"
                />
                <button
                  type="button"
                  @click="addTag"
                  class="btn-add-tag"
                  :disabled="!newTag.trim()"
                >
                  <i class="ph ph-plus"></i>
                  Add
                </button>
              </div>
            </div>
            <p class="help-text">Custom tags for organizing and filtering audiobooks</p>
          </div>

          <!-- Content Flags -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-info"></i>
              Content Information
            </label>
            <div class="checkbox-group">
              <label class="checkbox-label">
                <div class="checkbox-wrapper">
                  <input type="checkbox" v-model="formData.abridged" />
                  <div class="checkbox-content">
                    <span class="checkbox-title">Abridged</span>
                    <small>This is an abridged (shortened) version</small>
                  </div>
                </div>
              </label>
              <label class="checkbox-label">
                <div class="checkbox-wrapper">
                  <input type="checkbox" v-model="formData.explicit" />
                  <div class="checkbox-content">
                    <span class="checkbox-title">Explicit Content</span>
                    <small>Contains explicit language or mature content</small>
                  </div>
                </div>
              </label>
            </div>
          </div>

          <!-- Action Buttons -->
          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" @click="close">Cancel</button>
            <div v-if="moveJob" class="move-status">
              <small
                ><strong>Move Job</strong>: {{ moveJob.jobId }} —
                <em>{{ moveJob.status }}</em></small
              >
              <div v-if="moveJob.target">
                <small>Target: {{ moveJob.target }}</small>
              </div>
            </div>
            <button type="submit" class="btn btn-primary" :disabled="saving || !hasChanges">
              <i v-if="saving" class="ph ph-spinner ph-spin"></i>
              <i v-else class="ph ph-check"></i>
              {{ saving ? 'Saving...' : 'Save Changes' }}
            </button>
          </div>


        </form>
      </div>
    </div>
  </div>

  <!-- Separate move confirmation modal (sibling overlay) -->
  <div v-if="showMoveConfirm" class="confirm-overlay separate-modal" @click="cancelMoveConfirm">
    <div class="confirm-dialog" @click.stop>
      <div class="confirm-header">
        <i class="ph ph-folder-open"></i>
        <h3>Move Audiobook Files</h3>
      </div>
      <div class="confirm-body">
        <div class="confirm-description">
          <p>You're changing the destination folder for this audiobook. This will move all associated files.</p>
        </div>

        <div class="path-comparison">
          <div class="path-section">
            <div class="path-label">
              <i class="ph ph-arrow-right"></i>
              <span>From:</span>
            </div>
            <div class="path-display">
              <code>{{ pendingMove?.original || 'No current path' }}</code>
            </div>
          </div>

          <div class="path-section">
            <div class="path-label">
              <i class="ph ph-arrow-down"></i>
              <span>To:</span>
            </div>
            <div class="path-display">
              <code>{{ pendingMove?.combined || 'No destination path' }}</code>
            </div>
          </div>
        </div>

        <div class="confirm-options">
          <div class="checkbox-row">
            <label>
              <input type="checkbox" v-model="modalMoveFiles" />
              <div class="checkbox-content">
                <span class="checkbox-title">Move files now</span>
                <small>Copy all audiobook files to the new location (recommended)</small>
              </div>
            </label>
          </div>
          <div class="checkbox-row" v-if="modalMoveFiles">
            <label>
              <input type="checkbox" v-model="modalDeleteEmpty" />
              <div class="checkbox-content">
                <span class="checkbox-title">Clean up empty folders</span>
                <small>Delete the original folder if it becomes empty after moving</small>
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
import { useToast } from '@/services/toastService'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import { logger } from '@/utils/logger'
import type { Audiobook, QualityProfile } from '@/types'
import { PhX, PhPencil } from '@phosphor-icons/vue'
import { useConfigurationStore } from '@/stores/configuration'
import RootFolderSelect from '@/components/RootFolderSelect.vue'
import { useRootFoldersStore } from '@/stores/rootFolders'

interface Props {
  isOpen: boolean
  audiobook: Audiobook | null
}

interface FormData {
  monitored: boolean
  qualityProfileId: number | null
  tags: string[]
  abridged: boolean
  explicit: boolean
  basePath?: string | null
  relativePath?: string | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
  saved: []
}>()

const qualityProfiles = ref<QualityProfile[]>([])
const configStore = useConfigurationStore()
const rootStore = useRootFoldersStore()
const selectedRootId = ref<number | null>(null) // null/use default, 0 = custom
const customRootPath = ref<string | null>(null)
const rootPath = ref<string | null>(null)
const saving = ref(false)
const newTag = ref('')
const editingDestination = ref(false)
const toast = useToast()

const formData = ref<FormData>({
  monitored: true,
  qualityProfileId: null,
  tags: [],
  abridged: false,
  explicit: false,
  basePath: null,
  relativePath: ''
})

// Move job tracking (shows queued/processing/completed/failed state)
const moveJob = ref<{ jobId: string; status: string; target?: string; error?: string } | null>(null)
const moveUnsub = ref<(() => void) | null>(null)

// In-component move confirmation modal state
const showMoveConfirm = ref(false)
const pendingMove = ref<{ original?: string; combined?: string } | null>(null)
const modalMoveFiles = ref(true)
const modalDeleteEmpty = ref(true)
let moveConfirmResolver:
  | ((r: { proceed: boolean; moveFiles: boolean; deleteEmptySource: boolean }) => void)
  | null = null

function askMoveConfirmation(original: string, combined: string) {
  modalMoveFiles.value = true
  modalDeleteEmpty.value = true
  pendingMove.value = { original, combined }
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
  pendingMove.value = null
}

function confirmChangeWithoutMoving() {
  if (moveConfirmResolver)
    moveConfirmResolver({ proceed: true, moveFiles: false, deleteEmptySource: false })
  moveConfirmResolver = null
  showMoveConfirm.value = false
  pendingMove.value = null
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
  pendingMove.value = null
}

const hasChanges = computed(() => {
  if (!props.audiobook) return false

  const tagsChanged =
    JSON.stringify([...formData.value.tags].sort()) !==
    JSON.stringify([...(props.audiobook.tags || [])].sort())

  const basePathChanged = (props.audiobook?.basePath || '') !== (combinedBasePath() || '')

  return (
    formData.value.monitored !== Boolean(props.audiobook.monitored) ||
    formData.value.qualityProfileId !== (props.audiobook.qualityProfileId ?? null) ||
    tagsChanged ||
    formData.value.abridged !== Boolean(props.audiobook.abridged) ||
    formData.value.explicit !== Boolean(props.audiobook.explicit) ||
    basePathChanged
  )
})

watch(
  () => props.isOpen,
  async (isOpen) => {
    if (isOpen && props.audiobook) {
      await loadData()
      await initializeForm()
    }
  },
  { immediate: true },
)

async function loadData() {
  try {
    // Load quality profiles
    qualityProfiles.value = await apiService.getQualityProfiles()

    // Load root folders from settings store
    await configStore.loadApplicationSettings()
    await rootStore.load()

    const appSettings = configStore.applicationSettings
    if (appSettings && appSettings.outputPath) {
      // Fallback default
      rootPath.value = appSettings.outputPath
    }

    // If there are named root folders, prefer them
    if (rootStore.folders.length > 0) {
      // Use default root if any
      const def = rootStore.folders.find((f) => f.isDefault) || rootStore.folders[0]
      rootPath.value = def?.path || rootPath.value
      // pre-select default
      selectedRootId.value = def?.id ?? null
    } else {
      selectedRootId.value = null
    }
  } catch (error) {
    console.error('Failed to load edit data:', error)
  }
}

async function initializeForm() {
  if (!props.audiobook) return

  formData.value = {
    monitored: Boolean(props.audiobook.monitored),
    qualityProfileId: props.audiobook.qualityProfileId ?? null,
    tags: [...(props.audiobook.tags || [])],
    abridged: Boolean(props.audiobook.abridged),
    explicit: Boolean(props.audiobook.explicit),
    basePath: props.audiobook.basePath ?? null,
    relativePath: null,
  }

  // Determine which root folder matches the existing basePath
  if (props.audiobook?.basePath && rootStore.folders.length > 0) {
    // Check if basePath starts with any configured root folder
    const matchingRoot = rootStore.folders.find((folder) => {
      const normBase = props.audiobook!.basePath!.replace(/\\/g, '/')
      const normRoot = folder.path.replace(/\\/g, '/')
      const rootWithSlash = normRoot.endsWith('/') ? normRoot : normRoot + '/'
      return normBase.toLowerCase().startsWith(rootWithSlash.toLowerCase())
    })

    if (matchingRoot) {
      // Found a matching configured root folder
      selectedRootId.value = matchingRoot.id
      customRootPath.value = null
    } else {
      // No matching configured root folder - use custom path
      selectedRootId.value = 0
      customRootPath.value = props.audiobook.basePath
    }
  } else if (props.audiobook.basePath) {
    // No configured root folders, but there's a basePath - use custom
    selectedRootId.value = 0
    customRootPath.value = props.audiobook.basePath
  } else {
    // No basePath - use default selection
    selectedRootId.value = null
    customRootPath.value = null
  }

  // helper functions have been moved to module scope above so they are callable from template
  // previewPath() and deriveRelativeFromBase() now live at module scope

  function startEditingDestination() {
    // Ensure we have the latest relative path derived before showing the edit controls
    previewPath()
    editingDestination.value = true
  }

  // If there's an existing basePath that uses the configured root, derive the relative path
  try {
    // If there's a named root selected, derive relative path from that
    let chosenRoot = rootPath.value
    if (selectedRootId.value && selectedRootId.value > 0) {
      const found = rootStore.folders.find((f) => f.id === selectedRootId.value)
      if (found) chosenRoot = found.path
    } else if (selectedRootId.value === 0 && customRootPath.value) {
      chosenRoot = customRootPath.value
    }

    if (formData.value.basePath && chosenRoot) {
      formData.value.relativePath = deriveRelativeFromBase(formData.value.basePath, chosenRoot)
    } else if (formData.value.basePath && !chosenRoot) {
      // No configured root — show the full base path so user can edit it
      formData.value.relativePath = formData.value.basePath || null
    }

    // If there are no named root folders, show the destination edit controls
    // by default so users can set an explicit path. When named roots exist we
    // show the readonly display and require the user to click Edit.
    if (rootStore.folders.length === 0) editingDestination.value = true

    // IMPORTANT: Do not use metadata to fill the destination input for edits.
    // If the audiobook has a stored basePath we must use that value from the DB
    // and must not overwrite it with metadata-derived previews. Only when there
    // is no basePath present could we consider a preview (not applied here).
    return
  } catch (err) {
    // Non-fatal: any error deriving relative path from stored basePath
    logger.debug('Preview path unavailable:', err)
  }
}

function resolveSelectedRootPath(): string | null {
  if (selectedRootId.value === 0) {
    return customRootPath.value || null
  }
  if (selectedRootId.value && selectedRootId.value > 0) {
    const r = rootStore.folders.find((f) => f.id === selectedRootId.value)
    return r?.path ?? (rootPath.value || null)
  }
  return rootPath.value || null
}

function combinedBasePath(): string | null {
  const r = resolveSelectedRootPath() || ''
  const rel = (formData.value.relativePath || '').trim()
  if (!r && !rel) return null
  if (!r) return rel
  if (!rel) return r
  const needsSep = !(r.endsWith('/') || r.endsWith('\\'))
  return r + (needsSep ? '/' : '') + rel
}

// Helper: derive relative path from full base and configured root (moved to module scope so it can be reused)
function deriveRelativeFromBase(
  base: string | null | undefined,
  root: string | null | undefined,
): string {
  if (!base) return ''
  if (!root) return base

  const normBase = base.replace(/\\/g, '/')
  const normRoot = root.replace(/\\/g, '/')
  const rootWithSlash = normRoot.endsWith('/') ? normRoot : normRoot + '/'

  if (normBase.toLowerCase() === normRoot.toLowerCase()) return ''
  if (normBase.toLowerCase().startsWith(rootWithSlash.toLowerCase())) {
    const rel = normBase.slice(rootWithSlash.length).replace(/^\/+/, '')
    const useBackslash = root.includes('\\')
    return useBackslash ? rel.replace(/\//g, '\\') : rel
  }

  // Not under root: return full base so users can edit the absolute path
  return base
}

function previewPath() {
  try {
    const chosenRoot = resolveSelectedRootPath() || rootPath.value

    if (formData.value.basePath && chosenRoot) {
      formData.value.relativePath = deriveRelativeFromBase(formData.value.basePath, chosenRoot)
    } else if (formData.value.basePath && !chosenRoot) {
      formData.value.relativePath = formData.value.basePath || ''
    } else {
      formData.value.relativePath = ''
    }
  } catch (err) {
    logger.debug('Preview path unavailable:', err)
    formData.value.relativePath = ''
  }
}

function startEditingDestination() {
  // Ensure we have the latest relative path derived before showing the edit controls
  previewPath()
  editingDestination.value = true
}

async function handleSave() {
  if (!props.audiobook || !hasChanges.value) return
  // If the base path (destination) changed, prompt the user with rich options
  const combined = combinedBasePath()
  const originalBase = props.audiobook.basePath || ''
  let userWantsMove = true
  let userWantsDeleteEmpty = true
  if ((combined || '') !== originalBase) {
    const choice = await askMoveConfirmation(originalBase || '', combined || '')
    if (!choice || !choice.proceed) return
    userWantsMove = Boolean(choice.moveFiles)
    userWantsDeleteEmpty = Boolean(choice.deleteEmptySource)
  }

  saving.value = true
  try {
    // Build update payload with current form values
    const updates: Partial<Audiobook> = {
      monitored: formData.value.monitored,
      tags: formData.value.tags,
      abridged: formData.value.abridged,
      explicit: formData.value.explicit,
    }

    // If user changed destination/base path, include the combined root+relative value in updates
    const combined = combinedBasePath()
    if ((combined || '') !== (props.audiobook.basePath || '')) {
      ;(updates as Partial<Audiobook>).basePath = combined ?? undefined
    }

    // If qualityProfileId is null, send -1 to signal "use default"
    // Otherwise send the actual ID
    if (formData.value.qualityProfileId === null) {
      ;(updates as { qualityProfileId?: number }).qualityProfileId = -1 // -1 means "use default profile"
    } else {
      updates.qualityProfileId = formData.value.qualityProfileId
    }

    // Call single update API
    await apiService.updateAudiobook(props.audiobook.id, updates)

    // If base path changed, either update DB without moving or enqueue server-side move and show progress via SignalR
    if ((combined || '') !== (props.audiobook.basePath || '')) {
      if (!userWantsMove) {
        // User requested a DB-only change
        toast.info('Destination updated', 'Destination changed without moving files.')
      } else {
        try {
          const res = await apiService.moveAudiobook(props.audiobook.id, combined ?? '', {
            sourcePath: originalBase || undefined,
            moveFiles: true,
            deleteEmptySource: userWantsDeleteEmpty,
          })
          toast.info('Move queued', `Move job queued (${res.jobId}). Moving files in background.`)

          // Record initial move job state and subscribe to updates
          moveJob.value = {
            jobId: String(res.jobId),
            status: 'Queued',
            target: combined || '',
          }
          moveUnsub.value = signalRService.onMoveJobUpdate((job) => {
            if (!job || !job.jobId) return
            if (String(job.jobId).toLowerCase() !== String(res.jobId).toLowerCase()) return

            // Update local job state
            moveJob.value = {
              jobId: job.jobId,
              status: job.status,
              target: job.target,
              error: job.error,
            }

            if (job.status === 'Completed') {
              toast.success('Move completed', `Files moved to ${job.target || combined}`)
              try {
                if (moveUnsub.value) moveUnsub.value()
              } catch {}
              moveUnsub.value = null
            } else if (job.status === 'Failed') {
              toast.error('Move failed', job.error || 'Move job failed. Check logs for details.')
              try {
                if (moveUnsub.value) moveUnsub.value()
              } catch {}
              moveUnsub.value = null
            } else if (job.status === 'Processing') {
              toast.info('Move in progress', `Moving files to ${job.target || combined}`)
            }
          })
        } catch (moveErr) {
          console.error('Failed to enqueue move job:', moveErr)
          toast.error('Move failed', 'Failed to enqueue move job. Please try again.')
        }
      }
    }

    emit('saved')
    close()
  } catch (error) {
    console.error('Failed to save audiobook edits:', error)
    toast.error('Save failed', 'Failed to save changes. Please try again.')
  } finally {
    saving.value = false
  }
}

function addTag() {
  const tag = newTag.value.trim()
  if (tag && !formData.value.tags.includes(tag)) {
    formData.value.tags.push(tag)
    newTag.value = ''
  }
}

function removeTag(index: number) {
  formData.value.tags.splice(index, 1)
}

function close() {
  // If there's an active move subscription, unsubscribe to avoid leaks
  try {
    if (moveUnsub.value) {
      try {
        moveUnsub.value()
      } catch {}
      moveUnsub.value = null
    }
  } catch {}
  moveJob.value = null
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
  max-width: 650px;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  overflow: hidden;
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

.modal-body::-webkit-scrollbar {
  width: 8px;
}

.modal-body::-webkit-scrollbar-track {
  background: #1e1e1e;
}

.modal-body::-webkit-scrollbar-thumb {
  background: #555;
  border-radius: 6px;
}

.modal-body::-webkit-scrollbar-thumb:hover {
  background: #666;
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
  font-size: 0.95rem;
  line-height: 1.4;
}

.info-section strong {
  color: #5dade2;
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

.checkbox-row input[type="checkbox"] {
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
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.radio-label:hover {
  background-color: #333;
  border-color: #555;
}

.radio-label.active {
  background-color: rgba(0, 122, 204, 0.15);
  border-color: #007acc;
}

.radio-label input[type='radio'] {
  width: 20px;
  height: 20px;
  cursor: pointer;
  accent-color: #007acc;
  margin-top: 0.125rem;
  flex-shrink: 0;
}

.radio-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.radio-title {
  color: #ccc;
  font-weight: 500;
  font-size: 0.95rem;
  transition: color 0.2s;
}

.radio-label.active .radio-title {
  color: white;
}

.radio-content small {
  color: #999;
  font-size: 0.85rem;
  line-height: 1.4;
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
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  margin-top: 1rem;
  padding-top: 1.5rem;
  border-top: 1px solid #3a3a3a;
  flex-wrap: wrap;
}

.modal-actions > .btn {
  flex-shrink: 0;
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

.move-status {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  padding: 0.75rem 1rem;
  background-color: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 6px;
  color: #3498db;
  font-size: 0.85rem;
  flex: 1;
  min-width: 200px;
}

.move-status small {
  color: #87ceeb;
  line-height: 1.3;
}

.btn i.ph-spin {
  animation: spin 1s linear infinite;
}

.tags-container {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 1rem;
  background-color: #1e1e1e;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  min-height: 3.5rem;
  align-items: flex-start;
  align-content: flex-start;
}

.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.75rem;
  background-color: #2a2a2a;
  color: #e0e0e0;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  border: 1px solid #3a3a3a;
  transition: all 0.2s ease;
}

.tag-item:hover {
  background-color: #333;
  border-color: #007acc;
  color: white;
}

.tag-item:hover::before {
  opacity: 1;
}

.tags-empty {
  color: #888;
  font-size: 0.875rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  padding: 0.5rem;
}

.tag-remove {
  background: rgba(0, 0, 0, 0.2);
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.25rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s ease;
  flex-shrink: 0;
  margin-left: 0.25rem;
}

.tag-remove:hover {
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
}

.tag-remove:active {
  background: rgba(255, 255, 255, 0.25);
}

.tag-input-group {
  display: flex;
  gap: 0.5rem;
}

.tag-input {
  flex: 1;
  padding: 0.75rem 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  color: white;
  font-size: 0.95rem;
  transition: all 0.2s ease;
}

.tag-input:hover {
  border-color: #555;
}

.tag-input:focus {
  outline: none;
  border-color: #007acc;
  background-color: #2d2d2d;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.tag-input::placeholder {
  color: #666;
}

.btn-add-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.btn-add-tag:hover:not(:disabled) {
  background-color: #005fa3;
}

.btn-add-tag:active:not(:disabled) {
  transform: translateY(1px);
}

.btn-add-tag:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checkbox-label {
  padding: 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: block;
}

.checkbox-label:hover {
  background-color: #333;
  border-color: #555;
}

.checkbox-wrapper {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
}

.checkbox-label input[type='checkbox'] {
  width: 20px;
  height: 20px;
  cursor: pointer;
  accent-color: #007acc;
  margin-top: 0.125rem;
  flex-shrink: 0;
}

.checkbox-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkbox-title {
  color: #ccc;
  font-weight: 500;
  font-size: 0.95rem;
  transition: color 0.2s;
}

.checkbox-label:has(input[type='checkbox']:checked) .checkbox-title {
  color: white;
}

.checkbox-label:has(input[type='checkbox']:checked) {
  background-color: rgba(0, 122, 204, 0.15);
  border-color: #007acc;
}

.checkbox-content small {
  color: #999;
  font-size: 0.85rem;
  line-height: 1.4;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Destination display styles (shared with AddLibraryModal) */
.destination-display {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.5rem 0;
}

/* Read-only destination display */
.destination-readonly {
  display: flex;
  gap: 0.75rem;
  align-items: stretch;
  padding: 0.5rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
}

.readonly-input {
  flex: 1;
  background-color: transparent !important;
  color: #ccc !important;
  cursor: default;
  border: none !important;
  box-shadow: none !important;
  padding: 0.6rem 0.75rem;
}

.btn-edit-destination {
  padding: 0.6rem 1rem;
  border: 1px solid #555;
  border-radius: 6px;
  background-color: #333;
  color: #ccc;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  font-weight: 500;
  white-space: nowrap;
}

.btn-edit-destination:hover {
  border-color: #007acc;
  background-color: #007acc;
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 2px 6px rgba(0, 122, 204, 0.3);
}

.btn-edit-destination:active {
  background-color: #0056b3;
  transform: translateY(0);
}

/* Edit mode destination */
.destination-edit {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.destination-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.btn-sm {
  padding: 0.4rem 0.75rem;
  font-size: 0.85rem;
  min-width: auto;
}

/* root-label is used instead of readonly-path */

.form-input {
  width: 100%;
  padding: 0.6rem 0.75rem;
  border-radius: 6px;
  border: 1px solid #3a3a3a;
  background-color: #2a2a2a;
  color: #fff;
  font-size: 0.95rem;
}

.form-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.06);
}

/* Row layout for destination: root left, input right */
.destination-row {
  display: flex;
  gap: 0.75rem;
  align-items: stretch;
  flex-wrap: wrap;
}

.root-select {
  flex: 1;
  min-width: 200px;
  max-width: 300px;
}

.relative-input {
  flex: 2;
  min-width: 200px;
}

/* Responsive design */
@media (max-width: 768px) {
  .modal-overlay {
    padding: 1rem;
  }

  .modal-container {
    max-width: 100%;
    max-height: 90vh;
  }

  .modal-header {
    padding: 1rem 1.5rem;
  }

  .modal-body {
    padding: 1.5rem;
  }

  .destination-row {
    flex-direction: column;
    align-items: stretch;
  }

  .root-select,
  .relative-input {
    flex: 1;
    min-width: auto;
  }

  .modal-actions {
    flex-direction: column;
    align-items: stretch;
  }

  .modal-actions > .btn {
    width: 100%;
    justify-content: center;
  }

  .move-status {
    order: -1;
    width: 100%;
  }
}
</style>
