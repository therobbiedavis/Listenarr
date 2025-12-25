<template>
  <div class="remote-path-mapping-form">
    <div class="form-header">
      <h4>
        <i :class="isEdit ? 'ph ph-pencil' : 'ph ph-plus'"></i>
        {{ isEdit ? 'Edit' : 'Add' }} Path Mapping
      </h4>
    </div>
    
    <form @submit.prevent="handleSubmit">
      <div v-if="error" class="error-banner">
        <i class="ph ph-warning-circle"></i>
        <span>{{ error }}</span>
      </div>

      <div class="form-group">
        <label for="name">Name (Optional)</label>
        <input
          id="name"
          v-model="formData.name"
          type="text"
          placeholder="e.g., Docker to Host Mapping"
          class="form-control"
        />
        <small class="help-text">Friendly name to identify this mapping</small>
      </div>

      <div class="form-group">
        <label for="remotePath" class="required">Remote Path</label>
        <div class="input-with-icon">
          <i class="ph ph-desktop"></i>
          <input
            id="remotePath"
            v-model="formData.remotePath"
            type="text"
            placeholder="/path/to/downloads"
            class="form-control"
            required
          />
        </div>
        <small class="help-text">
          Path as seen by the download client (in its Docker container or remote system)
        </small>
      </div>

      <div class="form-group">
        <label for="localPath" class="required">Local Path</label>
        <div class="input-with-icon">
          <i class="ph ph-folder-open"></i>
          <input
            id="localPath"
            v-model="formData.localPath"
            type="text"
            placeholder="/mnt/media/audiobooks"
            class="form-control"
            required
          />
        </div>
        <small class="help-text">
          Path as seen by Listenarr (on this system where Listenarr is running)
        </small>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn-secondary" @click="handleCancel">
          <i class="ph ph-x"></i>
          Cancel
        </button>
        <button type="submit" class="btn btn-primary" :disabled="!isValid || loading">
          <i :class="loading ? 'ph ph-spinner ph-spin' : 'ph ph-check'"></i>
          <span v-if="loading">Saving...</span>
          <span v-else>{{ isEdit ? 'Update' : 'Save' }}</span>
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { RemotePathMapping } from '@/types'

interface Props {
  downloadClientId: string
  mapping?: RemotePathMapping | null
}

interface Emits {
  (e: 'save', mapping: Omit<RemotePathMapping, 'id' | 'createdAt' | 'updatedAt'>): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const formData = ref({
  name: '',
  remotePath: '',
  localPath: ''
})

const loading = ref(false)
const error = ref<string | null>(null)

const isEdit = computed(() => !!props.mapping)

const isValid = computed(() => {
  return formData.value.remotePath.trim().length > 0 &&
         formData.value.localPath.trim().length > 0
})

// Load existing mapping data when in edit mode
watch(
  () => props.mapping,
  (mapping) => {
    if (mapping) {
      formData.value.name = mapping.name || ''
      formData.value.remotePath = mapping.remotePath
      formData.value.localPath = mapping.localPath
    } else {
      formData.value.name = ''
      formData.value.remotePath = ''
      formData.value.localPath = ''
    }
  },
  { immediate: true }
)

const handleSubmit = () => {
  if (!isValid.value) return

  error.value = null
  loading.value = true

  try {
    emit('save', {
      downloadClientId: props.downloadClientId,
      name: formData.value.name.trim() || undefined,
      remotePath: formData.value.remotePath.trim(),
      localPath: formData.value.localPath.trim()
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save mapping'
  } finally {
    loading.value = false
  }
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<style scoped>
.remote-path-mapping-form {
  background-color: #222;
  padding: 1.5rem;
  border-radius: 6px;
  border: 1px solid #444;
}

.form-header {
  margin-bottom: 1.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #444;
}

.form-header h4 {
  margin: 0;
  color: #fff;
  font-size: 1rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.form-header i {
  font-size: 1.1rem;
  color: #007acc;
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  background-color: rgba(220, 53, 69, 0.1);
  border: 1px solid rgba(220, 53, 69, 0.3);
  border-radius: 6px;
  color: #ff6b7a;
}

.error-banner i {
  font-size: 1.25rem;
  flex-shrink: 0;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group:last-of-type {
  margin-bottom: 0;
}

label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 600;
  color: #fff;
  font-size: 0.95rem;
}

label.required::after {
  content: ' *';
  color: #dc3545;
}

.input-with-icon {
  position: relative;
  display: flex;
  align-items: center;
}

.input-with-icon i {
  position: absolute;
  left: 0.75rem;
  color: #999;
  font-size: 1.1rem;
  pointer-events: none;
}

.input-with-icon .form-control {
  padding-left: 2.5rem;
}

.form-control {
  width: 100%;
  padding: 0.75rem;
  font-size: 0.95rem;
  border: 1px solid #444;
  border-radius: 6px;
  background-color: #1a1a1a;
  color: #fff;
  transition: all 0.2s;
  font-family: 'Courier New', Courier, monospace;
}

.form-control:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-control::placeholder {
  color: #666;
}

.help-text {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: #999;
  line-height: 1.4;
}

.form-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
  margin-top: 1.5rem;
  padding-top: 1.5rem;
  border-top: 1px solid #444;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn i {
  font-size: 1.1rem;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005a9e;
  transform: translateY(-1px);
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
  transform: translateY(-1px);
}

.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@media (max-width: 768px) {
  .form-actions {
    flex-direction: column-reverse;
  }

  .btn {
    width: 100%;
    justify-content: center;
  }
}
</style>
