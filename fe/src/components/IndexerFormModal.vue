<template>
  <div v-if="visible" class="modal-overlay" @click="closeModal">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h2>{{ editingIndexer ? 'Edit Indexer' : 'Add Indexer' }}</h2>
        <button class="close-btn" @click="closeModal">
          <i class="ph ph-x"></i>
        </button>
      </div>
      
      <div class="modal-body">
        <form @submit.prevent="handleSubmit">
          <!-- Basic Information -->
          <div class="form-section">
            <h3>Basic Information</h3>
            
            <div class="form-group">
              <label for="name">Name *</label>
              <input 
                id="name" 
                v-model="formData.name" 
                type="text" 
                required 
                placeholder="e.g., My Indexer"
              />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label for="type">Type *</label>
                <select id="type" v-model="formData.type" required>
                  <option value="Torrent">Torrent</option>
                  <option value="Usenet">Usenet</option>
                </select>
              </div>

              <div class="form-group">
                <label for="implementation">Implementation *</label>
                <select id="implementation" v-model="formData.implementation" required>
                  <option value="Newznab">Newznab</option>
                  <option value="Torznab">Torznab</option>
                  <option value="Custom">Custom</option>
                </select>
              </div>
            </div>

            <div class="form-group">
              <label for="url">URL *</label>
              <input 
                id="url" 
                v-model="formData.url" 
                type="url" 
                required 
                placeholder="https://indexer.example.com"
              />
            </div>

            <div class="form-group">
              <label for="apiKey">API Key</label>
              <input 
                id="apiKey" 
                v-model="formData.apiKey" 
                type="text" 
                placeholder="Your API key"
              />
            </div>

            <div class="form-group">
              <label for="categories">Categories</label>
              <input 
                id="categories" 
                v-model="formData.categories" 
                type="text" 
                placeholder="Comma-separated category IDs (e.g., 3030,3040)"
              />
              <small>Leave empty to search all categories</small>
            </div>
          </div>

          <!-- Features -->
          <div class="form-section">
            <h3>Features</h3>
            
            <div class="checkbox-group">
              <label>
                <input type="checkbox" v-model="formData.enableRss" />
                <span>
                  <strong>Enable RSS</strong>
                  <small>Use RSS feeds to monitor for new releases</small>
                </span>
              </label>
            </div>

            <div class="checkbox-group">
              <label>
                <input type="checkbox" v-model="formData.enableAutomaticSearch" />
                <span>
                  <strong>Enable Automatic Search</strong>
                  <small>Use this indexer for automatic searches</small>
                </span>
              </label>
            </div>

            <div class="checkbox-group">
              <label>
                <input type="checkbox" v-model="formData.enableInteractiveSearch" />
                <span>
                  <strong>Enable Interactive Search</strong>
                  <small>Use this indexer for manual searches</small>
                </span>
              </label>
            </div>
          </div>

          <!-- Advanced Settings -->
          <div class="form-section">
            <h3>Advanced Settings</h3>
            
            <div class="form-row">
              <div class="form-group">
                <label for="priority">Priority</label>
                <input 
                  id="priority" 
                  v-model.number="formData.priority" 
                  type="number" 
                  min="1" 
                  max="100"
                />
                <small>Higher priority indexers are searched first (1-100)</small>
              </div>

              <div class="form-group">
                <label for="minimumAge">Minimum Age (minutes)</label>
                <input 
                  id="minimumAge" 
                  v-model.number="formData.minimumAge" 
                  type="number" 
                  min="0"
                />
                <small>Wait time before grabbing new releases (0 = disabled)</small>
              </div>
            </div>

            <div class="form-row" v-if="formData.type === 'Usenet'">
              <div class="form-group">
                <label for="retention">Retention (days)</label>
                <input 
                  id="retention" 
                  v-model.number="formData.retention" 
                  type="number" 
                  min="0"
                />
                <small>Usenet retention in days (0 = unlimited)</small>
              </div>
            </div>

            <div class="form-group">
              <label for="maximumSize">Maximum Size (MB)</label>
              <input 
                id="maximumSize" 
                v-model.number="formData.maximumSize" 
                type="number" 
                min="0"
              />
              <small>Maximum allowed size in megabytes (0 = unlimited)</small>
            </div>
          </div>
        </form>
      </div>
      
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" @click="closeModal">
          <i class="ph ph-x"></i>
          Cancel
        </button>
        <button type="button" class="btn btn-info" @click="testConnection" :disabled="testing">
          <i :class="testing ? 'ph ph-spinner ph-spin' : 'ph ph-check-circle'"></i>
          {{ testing ? 'Testing...' : 'Test' }}
        </button>
        <button type="button" class="btn btn-primary" @click="handleSubmit" :disabled="saving">
          <i :class="saving ? 'ph ph-spinner ph-spin' : 'ph ph-floppy-disk'"></i>
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { Indexer } from '@/types'
import { createIndexer, updateIndexer, testIndexer as apiTestIndexer } from '@/services/api'
import { useNotification } from '@/composables/useNotification'

interface Props {
  visible: boolean
  editingIndexer: Indexer | null
}

interface Emits {
  (e: 'close'): void
  (e: 'saved'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const { success, error: showError } = useNotification()

const saving = ref(false)
const testing = ref(false)

const defaultFormData = {
  name: '',
  type: 'Torrent',
  implementation: 'Torznab',
  url: '',
  apiKey: '',
  categories: '',
  enableRss: true,
  enableAutomaticSearch: true,
  enableInteractiveSearch: true,
  enableAnimeStandardSearch: false,
  isEnabled: true,
  priority: 25,
  minimumAge: 0,
  retention: 0,
  maximumSize: 0
}

const formData = ref({ ...defaultFormData })

// Watch for editing indexer changes
watch(() => props.editingIndexer, (newIndexer) => {
  if (newIndexer) {
    formData.value = {
      name: newIndexer.name,
      type: newIndexer.type,
      implementation: newIndexer.implementation,
      url: newIndexer.url,
      apiKey: newIndexer.apiKey || '',
      categories: newIndexer.categories || '',
      enableRss: newIndexer.enableRss,
      enableAutomaticSearch: newIndexer.enableAutomaticSearch,
      enableInteractiveSearch: newIndexer.enableInteractiveSearch,
      enableAnimeStandardSearch: newIndexer.enableAnimeStandardSearch,
      isEnabled: newIndexer.isEnabled,
      priority: newIndexer.priority,
      minimumAge: newIndexer.minimumAge,
      retention: newIndexer.retention,
      maximumSize: newIndexer.maximumSize
    }
  } else {
    formData.value = { ...defaultFormData }
  }
}, { immediate: true })

const closeModal = () => {
  formData.value = { ...defaultFormData }
  emit('close')
}

const testConnection = async () => {
  if (!props.editingIndexer) {
    showError('Please save the indexer first before testing')
    return
  }

  testing.value = true
  try {
    const result = await apiTestIndexer(props.editingIndexer.id)
    if (result.success) {
      success(`Test successful: ${result.message}`)
    } else {
      showError(`Test failed: ${result.error || result.message}`)
    }
  } catch (error) {
    console.error('Failed to test indexer:', error)
    showError('Failed to test indexer connection')
  } finally {
    testing.value = false
  }
}

const handleSubmit = async () => {
  saving.value = true
  try {
    if (props.editingIndexer) {
      // Update existing indexer
      await updateIndexer(props.editingIndexer.id, formData.value)
      success('Indexer updated successfully')
    } else {
      // Create new indexer
      await createIndexer(formData.value)
      success('Indexer created successfully')
    }
    
    emit('saved')
    closeModal()
  } catch (error) {
    console.error('Failed to save indexer:', error)
    showError('Failed to save indexer')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.modal-overlay {
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
  overflow-y: auto;
  padding: 2rem;
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 8px;
  max-width: 700px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #444;
}

.modal-header h2 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
}

.close-btn {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  background-color: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.form-section {
  margin-bottom: 2rem;
}

.form-section:last-child {
  margin-bottom: 0;
}

.form-section h3 {
  color: #fff;
  font-size: 1.1rem;
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #444;
}

.form-group {
  margin-bottom: 1.5rem;
  flex: 1;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  color: #fff;
  font-weight: 600;
  font-size: 0.95rem;
}

.form-group input,
.form-group select {
  width: 100%;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 4px;
  color: #fff;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-group small {
  display: block;
  margin-top: 0.5rem;
  color: #999;
  font-size: 0.85rem;
}

.form-row {
  display: flex;
  gap: 1rem;
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1rem;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.checkbox-group label:hover {
  border-color: #007acc;
  background-color: #222;
}

.checkbox-group input[type="checkbox"] {
  margin-top: 0.25rem;
  width: auto;
  cursor: pointer;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
}

.checkbox-group label small {
  color: #999;
  font-size: 0.85rem;
  font-weight: normal;
}

.modal-footer {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
  transform: translateY(-1px);
}

.btn-info {
  background-color: #2196f3;
  color: white;
}

.btn-info:hover:not(:disabled) {
  background-color: #1976d2;
  transform: translateY(-1px);
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005a9e;
  transform: translateY(-1px);
}

.ph-spin {
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

@media (max-width: 768px) {
  .modal-overlay {
    padding: 1rem;
  }

  .form-row {
    flex-direction: column;
  }

  .modal-footer {
    flex-wrap: wrap;
  }

  .btn {
    flex: 1;
    justify-content: center;
  }
}
</style>
