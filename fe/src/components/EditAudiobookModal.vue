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
            Editing <strong>{{ audiobook?.title }}</strong> by <strong>{{ audiobook?.authors?.join(', ') || 'Unknown Author' }}</strong>
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
                <input
                  type="radio"
                  v-model="formData.monitored"
                  :value="true"
                  name="monitored"
                />
                <div class="radio-content">
                  <span class="radio-title">Monitored</span>
                  <small>Automatically search for and upgrade releases</small>
                </div>
              </label>
              <label class="radio-label" :class="{ active: formData.monitored === false }">
                <input
                  type="radio"
                  v-model="formData.monitored"
                  :value="false"
                  name="monitored"
                />
                <div class="radio-content">
                  <span class="radio-title">Unmonitored</span>
                  <small>Do not search for new releases</small>
                </div>
              </label>
            </div>
            <p class="help-text">
              Monitored audiobooks will be automatically upgraded when better quality releases are found
            </p>
          </div>

          <!-- Quality Profile -->
          <div class="form-group">
            <label class="form-label" for="quality-profile">
              <i class="ph ph-star"></i>
              Quality Profile
            </label>
            <select
              id="quality-profile"
              v-model="formData.qualityProfileId"
              class="form-select"
            >
              <option :value="null">Use Default Profile</option>
              <option
                v-for="profile in qualityProfiles"
                :key="profile.id"
                :value="profile.id"
              >
                {{ profile.name }}{{ profile.isDefault ? ' (Default)' : '' }}
              </option>
            </select>
            <p class="help-text">
              Controls which quality standards to use for downloads and upgrades. Leave as "Use Default Profile" to automatically use the default profile.
            </p>
          </div>

          <!-- Destination / Base Path -->
          <div class="form-group">
            <label class="form-label">
              <i class="ph ph-folder"></i>
              Destination Folder
            </label>
            <div class="destination-display">
              <div class="destination-row">
                <div class="root-label">{{ rootPath || 'Not configured' }}\</div>
                <input type="text" v-model="formData.relativePath" class="form-input relative-input" placeholder="e.g. Author/Title" />
              </div>
              <p class="help-text">Root (left) is read-only — edit the output path relative to it on the right.</p>
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
                <span 
                  v-for="(tag, index) in formData.tags" 
                  :key="index"
                  class="tag-item"
                >
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
            <p class="help-text">
              Custom tags for organizing and filtering audiobooks
            </p>
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
                  <input
                    type="checkbox"
                    v-model="formData.abridged"
                  />
                  <div class="checkbox-content">
                    <span class="checkbox-title">Abridged</span>
                    <small>This is an abridged (shortened) version</small>
                  </div>
                </div>
              </label>
              <label class="checkbox-label">
                <div class="checkbox-wrapper">
                  <input
                    type="checkbox"
                    v-model="formData.explicit"
                  />
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
            <button type="button" class="btn btn-secondary" @click="close">
              Cancel
            </button>
            <div v-if="moveJob" class="move-status">
              <small><strong>Move Job</strong>: {{ moveJob.jobId }} — <em>{{ moveJob.status }}</em></small>
              <div v-if="moveJob.target"><small>Target: {{ moveJob.target }}</small></div>
            </div>
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="saving || !hasChanges"
            >
              <i v-if="saving" class="ph ph-spinner ph-spin"></i>
              <i v-else class="ph ph-check"></i>
              {{ saving ? 'Saving...' : 'Save Changes' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from '@/services/toastService'
import { apiService } from '@/services/api'
import { showConfirm } from '@/composables/useConfirm'
import { signalRService } from '@/services/signalr'
import type { Audiobook, QualityProfile } from '@/types'
import { PhX } from '@phosphor-icons/vue'
import { useConfigurationStore } from '@/stores/configuration'

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
const rootFolders = ref<string[]>([])
const configStore = useConfigurationStore()
const rootPath = ref<string | null>(null)
const saving = ref(false)
const newTag = ref('')
const toast = useToast()

const formData = ref<FormData>({
  monitored: true,
  qualityProfileId: null,
  tags: [],
  abridged: false,
  explicit: false
  ,basePath: null
})

// Move job tracking (shows queued/processing/completed/failed state)
const moveJob = ref<{ jobId: string; status: string; target?: string; error?: string } | null>(null)
const moveUnsub = ref<(() => void) | null>(null)

const hasChanges = computed(() => {
  if (!props.audiobook) return false

  const tagsChanged = JSON.stringify([...formData.value.tags].sort()) !== JSON.stringify([...(props.audiobook.tags || [])].sort())

  const basePathChanged = (props.audiobook?.basePath || '') !== (combinedBasePath() || '')

  return formData.value.monitored !== Boolean(props.audiobook.monitored) ||
    formData.value.qualityProfileId !== (props.audiobook.qualityProfileId ?? null) ||
    tagsChanged ||
    formData.value.abridged !== Boolean(props.audiobook.abridged) ||
    formData.value.explicit !== Boolean(props.audiobook.explicit) ||
    basePathChanged
})

watch(() => props.isOpen, async (isOpen) => {
  if (isOpen && props.audiobook) {
    await loadData()
    await initializeForm()
  }
})

async function loadData() {
  try {
    // Load quality profiles
    qualityProfiles.value = await apiService.getQualityProfiles()

    // Load root folders from configuration via the configuration store
    await configStore.loadApplicationSettings()
    const appSettings = configStore.applicationSettings
    if (appSettings && appSettings.outputPath) {
      rootFolders.value = [appSettings.outputPath]
      rootPath.value = appSettings.outputPath
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
    basePath: props.audiobook.basePath ?? null
    ,relativePath: null
  }

    // If there's an existing basePath that uses the configured root, derive the relative path
    try {
      if (formData.value.basePath && rootPath.value) {
        const base = formData.value.basePath
        const root = rootPath.value
        if (base.startsWith(root)) {
          const rel = base.slice(root.length).replace(/^[/\\]+/, '')
          formData.value.relativePath = rel
        }
      }

      // IMPORTANT: Do not use metadata to fill the destination input for edits.
      // If the audiobook has a stored basePath we must use that value from the DB
      // and must not overwrite it with metadata-derived previews. Only when there
      // is no basePath present could we consider a preview (not applied here).
      return
    } catch (err) {
      // Non-fatal: any error deriving relative path from stored basePath
      console.debug('Preview path unavailable:', err)
    }
}

function combinedBasePath(): string | null {
  const r = rootPath.value || ''
  const rel = (formData.value.relativePath || '').trim()
  if (!r && !rel) return null
  if (!r) return rel
  if (!rel) return r
  const needsSep = !(r.endsWith('/') || r.endsWith('\\'))
  return r + (needsSep ? '/' : '') + rel
}

async function handleSave() {
  if (!props.audiobook || !hasChanges.value) return
  // If the base path (destination) changed, confirm with the user before proceeding
  const combined = combinedBasePath()
  const originalBase = props.audiobook.basePath || ''
  if ((combined || '') !== originalBase) {
    const message = `You're changing the destination from:\n\n${originalBase || '<none>'}\n\nto:\n\n${combined || '<none>'}\n\nEverything in the current destination will be moved to the new destination and the current destination will be deleted. Do you want to continue?`
    // Use centralized app confirm so UI is consistent and non-blocking
    const ok = await showConfirm(message, 'Move Audiobook', { confirmText: 'Move', cancelText: 'Cancel', danger: true })
    if (!ok) return
  }

  saving.value = true
  try {
    // Build update payload with current form values
    const updates: Partial<Audiobook> = {
      monitored: formData.value.monitored,
      tags: formData.value.tags,
      abridged: formData.value.abridged,
      explicit: formData.value.explicit
    }

    // If user changed destination/base path, include the combined root+relative value in updates
    const combined = combinedBasePath()
    if ((combined || '') !== (props.audiobook.basePath || '')) {
      ;(updates as Partial<Audiobook>).basePath = combined ?? undefined
    }
    
    // If qualityProfileId is null, send -1 to signal "use default"
    // Otherwise send the actual ID
    if (formData.value.qualityProfileId === null) {
      (updates as {qualityProfileId?: number}).qualityProfileId = -1 // -1 means "use default profile"
    } else {
      updates.qualityProfileId = formData.value.qualityProfileId
    }

    // Call single update API
    await apiService.updateAudiobook(props.audiobook.id, updates)

    // If base path changed, enqueue server-side move and show progress via SignalR
    if ((combined || '') !== (props.audiobook.basePath || '')) {
      try {
        const res = await apiService.moveAudiobook(props.audiobook.id, combined ?? '', originalBase || undefined)
        toast.info('Move queued', `Move job queued (${res.jobId}). Moving files in background.`)

        // Record initial move job state and subscribe to updates
        moveJob.value = { jobId: res.jobId, status: 'Queued', target: combined ?? undefined }
        moveUnsub.value = signalRService.onMoveJobUpdate((job) => {
          if (!job || !job.jobId) return
          if (String(job.jobId).toLowerCase() !== String(res.jobId).toLowerCase()) return

          // Update local job state
          moveJob.value = { jobId: job.jobId, status: job.status, target: job.target, error: job.error }

            if (job.status === 'Completed') {
              toast.success('Move completed', `Files moved to ${job.target || combined}`)
              try { if (moveUnsub.value) moveUnsub.value() } catch {}
              moveUnsub.value = null
            } else if (job.status === 'Failed') {
              toast.error('Move failed', job.error || 'Move job failed. Check logs for details.')
              try { if (moveUnsub.value) moveUnsub.value() } catch {}
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
      try { moveUnsub.value() } catch {}
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

.radio-label input[type="radio"] {
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

.move-status {
  display: inline-flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  margin-right: 1rem;
  color: #dfe6ff;
}
.move-status small {
  color: #cfd8ff;
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

.checkbox-label input[type="checkbox"] {
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

.checkbox-label:has(input[type="checkbox"]:checked) .checkbox-title {
  color: white;
}

.checkbox-label:has(input[type="checkbox"]:checked) {
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
  box-shadow: 0 0 0 3px rgba(0,122,204,0.06);
}

/* Row layout for destination: root left, input right */
.destination-row {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.root-label {
  width: fit-content;
  max-width: 40%;
  padding: 0.45rem 0 0,45rem 0.6rem;
  color: #ccc;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', 'Segoe UI Mono', monospace;
  font-size: 0.9rem;
  white-space: nowrap;
}

.relative-input {
  flex: 1 1 auto;
}
</style>