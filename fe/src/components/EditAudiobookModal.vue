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
              <label class="radio-label">
                <input
                  type="radio"
                  v-model="formData.monitored"
                  :value="true"
                  name="monitored"
                />
                <span>Monitored</span>
              </label>
              <label class="radio-label">
                <input
                  type="radio"
                  v-model="formData.monitored"
                  :value="false"
                  name="monitored"
                />
                <span>Unmonitored</span>
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
              <option
                v-for="profile in qualityProfiles"
                :key="profile.id"
                :value="profile.id"
              >
                {{ profile.name }}{{ profile.isDefault ? ' (Default)' : '' }}
              </option>
            </select>
            <p class="help-text">
              Controls which quality standards to use for downloads and upgrades
            </p>
          </div>

          <!-- Action Buttons -->
          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" @click="close">
              Cancel
            </button>
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
import { apiService } from '@/services/api'
import type { Audiobook, QualityProfile } from '@/types'

interface Props {
  isOpen: boolean
  audiobook: Audiobook | null
}

interface FormData {
  monitored: boolean
  qualityProfileId: number
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
  saved: []
}>()

const qualityProfiles = ref<QualityProfile[]>([])
const rootFolders = ref<string[]>([])
const saving = ref(false)

const formData = ref<FormData>({
  monitored: true,
  qualityProfileId: 0
})

const hasChanges = computed(() => {
  if (!props.audiobook) return false

  return formData.value.monitored !== Boolean(props.audiobook.monitored) ||
    formData.value.qualityProfileId !== (props.audiobook.qualityProfileId || 0)
})

watch(() => props.isOpen, async (isOpen) => {
  if (isOpen && props.audiobook) {
    await loadData()
    initializeForm()
  }
})

async function loadData() {
  try {
    // Load quality profiles
    qualityProfiles.value = await apiService.getQualityProfiles()

    // Load root folders from configuration
    const appSettings = await apiService.getApplicationSettings()
    if (appSettings.outputPath) {
      rootFolders.value = [appSettings.outputPath]
      // TODO: If you implement multiple root folders, load them here
    }
  } catch (error) {
    console.error('Failed to load edit data:', error)
  }
}

function initializeForm() {
  if (!props.audiobook) return

  formData.value = {
    monitored: Boolean(props.audiobook.monitored),
    qualityProfileId: props.audiobook.qualityProfileId || 0
  }
}

async function handleSave() {
  if (!props.audiobook || !hasChanges.value) return

  saving.value = true
  try {
    // Build update payload with current form values
    const updates: Partial<Audiobook> = {
      monitored: formData.value.monitored,
      qualityProfileId: formData.value.qualityProfileId
    }

    // Call single update API
    await apiService.updateAudiobook(props.audiobook.id, updates)

    emit('saved')
    close()
  } catch (error) {
    console.error('Failed to save audiobook edits:', error)
    alert('Failed to save changes. Please try again.')
  } finally {
    saving.value = false
  }
}

function close() {
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
  border-radius: 8px;
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
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
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

.radio-label input[type="radio"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #007acc;
}

.radio-label input[type="radio"]:checked + span {
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
</style>