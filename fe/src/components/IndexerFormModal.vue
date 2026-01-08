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

            <div class="form-group">
              <label for="implementation">Implementation *</label>
              <select id="implementation" v-model="formData.implementation" required>
                <option value="Newznab">Newznab</option>
                <option value="Torznab">Torznab</option>
                <option value="MyAnonamouse">MyAnonamouse</option>
                <option value="InternetArchive">Internet Archive</option>
                <option value="Custom">Custom</option>
              </select>
            </div>

            <div v-if="formData.implementation !== 'InternetArchive'" class="form-group">
              <label for="url">URL *</label>
              <input
                id="url"
                v-model="formData.url"
                type="url"
                required
                :placeholder="
                  formData.implementation === 'MyAnonamouse'
                    ? 'https://www.myanonamouse.net'
                    : 'https://indexer.example.com'
                "
              />
            </div>

            <!-- MyAnonamouse Authentication & Options -->
            <div v-if="formData.implementation === 'MyAnonamouse'" class="form-section">
              <h4>MyAnonamouse Settings</h4>
              <div class="form-group">
                <label for="mam-id">MAM ID *</label>
                <input
                  id="mam-id"
                  v-model="mamId"
                  type="text"
                  :required="formData.implementation === 'MyAnonamouse'"
                  placeholder="Your MyAnonamouse MAM ID"
                />
              </div>
              <small class="info-text">
                <i class="ph ph-info"></i>
                MyAnonamouse requires your MAM ID for authentication. This is a unique identifier
                for your account. These are stored securely and only used to search the indexer.
              </small>

              <div class="form-row mam-options">
                <label class="form-group">
                  <span>Filter</span>
                  <select v-model="mamFilter">
                    <option value="">Search everything</option>
                    <option value="Active">Active</option>
                    <option value="Freeleech">Freeleech</option>
                    <option value="FreeleechOrVip">Freeleech or VIP</option>
                    <option value="Vip">VIP only</option>
                    <option value="NotVip">Not VIP</option>
                  </select>
                </label>

                <label class="form-group">
                  <input type="checkbox" v-model="mamSearchInDescription" />
                  <span>Search in description</span>
                </label>

                <label class="form-group">
                  <input type="checkbox" v-model="mamSearchInSeries" />
                  <span>Search in series</span>
                </label>

                <label class="form-group">
                  <input type="checkbox" v-model="mamSearchInFilenames" />
                  <span>Search in filenames</span>
                </label>

                <label class="form-group">
                  <span>Language (numeric id)</span>
                  <input type="text" v-model="mamLanguage" placeholder="e.g., 1" />
                </label>

                <label class="form-group">
                  <span>Freeleech wedge</span>
                  <select v-model="mamFreeleechWedge">
                    <option value="">Never</option>
                    <option value="Preferred">Preferred</option>
                    <option value="Required">Required</option>
                  </select>
                </label>

                <label class="form-group">
                  <input type="checkbox" v-model="mamEnrichResults" />
                  <span>Enrich results (fetch item page for missing fields)</span>
                </label>

                <label class="form-group">
                  <span>Enrich top results</span>
                  <input type="number" v-model.number="mamEnrichTopResults" min="1" max="20" />
                </label>
              </div>
            </div>

            <!-- Internet Archive Collection Selection -->
            <div v-if="formData.implementation === 'InternetArchive'" class="form-section">
              <h4>Collection</h4>
              <div class="form-group">
                <label for="ia-collection">Collection *</label>
                <select
                  id="ia-collection"
                  v-model="iaCollection"
                  :required="formData.implementation === 'InternetArchive'"
                >
                  <option value="librivoxaudio">Librivox (Free Audiobooks)</option>
                  <option value="audio_bookspoetry">Audio Books & Poetry</option>
                </select>
              </div>
              <small class="info-text">
                <i class="ph ph-info"></i>
                Choose which Internet Archive collection to search. Librivox contains public domain
                audiobooks read by volunteers.
              </small>
            </div>

            <!-- API Key for other implementations -->
            <div
              v-if="
                formData.implementation !== 'MyAnonamouse' &&
                formData.implementation !== 'InternetArchive'
              "
              class="form-group"
            >
              <label for="apiKey">API Key</label>
              <input id="apiKey" v-model="formData.apiKey" type="text" placeholder="Your API key" />
            </div>

            <div v-if="formData.implementation !== 'InternetArchive'" class="form-group">
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

            <div v-if="formData.implementation !== 'InternetArchive'" class="checkbox-group">
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

              <div v-if="formData.implementation !== 'InternetArchive'" class="form-group">
                <label for="minimumAge">Minimum Age (minutes)</label>
                <input id="minimumAge" v-model.number="formData.minimumAge" type="number" min="0" />
                <small>Wait time before grabbing new releases (0 = disabled)</small>
              </div>
            </div>

            <div class="form-row" v-if="formData.implementation === 'Newznab'">
              <div class="form-group">
                <label for="retention">Retention (days)</label>
                <input id="retention" v-model.number="formData.retention" type="number" min="0" />
                <small>Usenet retention in days (0 = unlimited)</small>
              </div>
            </div>

            <div class="form-group">
              <label for="maximumSize">Maximum Size (MB)</label>
              <input id="maximumSize" v-model.number="formData.maximumSize" type="number" min="0" />
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
import {
  createIndexer,
  updateIndexer,
  testIndexerDraft as apiTestIndexerDraft,
} from '@/services/api'
import { useToast } from '@/services/toastService'

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
const toast = useToast()

const saving = ref(false)
const testing = ref(false)

// MyAnonamouse authentication field
const mamId = ref('')
// MyAnonamouse options
const mamFilter = ref('')
const mamSearchInDescription = ref(false)
const mamSearchInSeries = ref(true)
const mamSearchInFilenames = ref(true)
const mamLanguage = ref('')
const mamFreeleechWedge = ref('')
const mamEnrichResults = ref(false)
const mamEnrichTopResults = ref(3)

// Internet Archive collection field
const iaCollection = ref('librivoxaudio')

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
  maximumSize: 0,
  additionalSettings: '',
}

const formData = ref({ ...defaultFormData })

type IndexerPayload = Omit<Indexer, 'id' | 'createdAt' | 'updatedAt'>

const buildIndexerPayload = (): IndexerPayload => {
  const payload = { ...formData.value } as IndexerPayload
  payload.additionalSettings = payload.additionalSettings || ''

  if (payload.implementation === 'MyAnonamouse') {
    const mamOpts: Record<string, unknown> = {
      searchInDescription: mamSearchInDescription.value,
      searchInSeries: mamSearchInSeries.value,
      searchInFilenames: mamSearchInFilenames.value,
      language: mamLanguage.value || undefined,
      filter: mamFilter.value || undefined,
      freeleechWedge: mamFreeleechWedge.value || undefined,
      enrichResults: mamEnrichResults.value,
      enrichTopResults: mamEnrichTopResults.value,
    }
    payload.additionalSettings = JSON.stringify({ mam_id: mamId.value, mam_options: mamOpts })
    payload.apiKey = ''
  } else if (payload.implementation === 'InternetArchive') {
    payload.additionalSettings = JSON.stringify({ collection: iaCollection.value })
    payload.apiKey = ''
    // Always set to archive.org since backend hardcodes this URL
    payload.url = 'https://archive.org'
    // Set sensible defaults for hidden fields
    payload.categories = ''
    payload.enableRss = false
    payload.minimumAge = 0
  }

  return payload
}

// Watch for editing indexer changes
watch(
  () => props.editingIndexer,
  (newIndexer) => {
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
        maximumSize: newIndexer.maximumSize,
        additionalSettings: newIndexer.additionalSettings || '',
      }

      // Parse MyAnonamouse credentials from additionalSettings
      if (newIndexer.implementation === 'MyAnonamouse' && newIndexer.additionalSettings) {
        try {
          const settings = JSON.parse(newIndexer.additionalSettings)
          mamId.value = settings.mam_id || ''
          // Parse options if present
          if (settings.mam_options) {
            mamSearchInDescription.value =
              settings.mam_options.searchInDescription ?? mamSearchInDescription.value
            mamSearchInSeries.value = settings.mam_options.searchInSeries ?? mamSearchInSeries.value
            mamSearchInFilenames.value =
              settings.mam_options.searchInFilenames ?? mamSearchInFilenames.value
            mamLanguage.value = settings.mam_options.language ?? mamLanguage.value
            mamFilter.value = settings.mam_options.filter ?? mamFilter.value
            mamFreeleechWedge.value = settings.mam_options.freeleechWedge ?? mamFreeleechWedge.value
            mamEnrichResults.value = settings.mam_options.enrichResults ?? mamEnrichResults.value
            mamEnrichTopResults.value =
              settings.mam_options.enrichTopResults ?? mamEnrichTopResults.value
          } else {
            // Also allow flat properties for backward compatibility
            mamSearchInDescription.value =
              settings.searchInDescription ?? mamSearchInDescription.value
            mamSearchInSeries.value = settings.searchInSeries ?? mamSearchInSeries.value
            mamSearchInFilenames.value = settings.searchInFilenames ?? mamSearchInFilenames.value
            mamLanguage.value = settings.language ?? mamLanguage.value
            mamFilter.value = settings.filter ?? mamFilter.value
            mamFreeleechWedge.value = settings.freeleechWedge ?? mamFreeleechWedge.value
            mamEnrichResults.value = settings.enrichResults ?? mamEnrichResults.value
            mamEnrichTopResults.value = settings.enrichTopResults ?? mamEnrichTopResults.value
          }
        } catch (e) {
          console.error('Failed to parse MyAnonamouse settings:', e)
          mamId.value = ''
        }
      }

      // Parse Internet Archive collection from additionalSettings
      if (newIndexer.implementation === 'InternetArchive' && newIndexer.additionalSettings) {
        try {
          const settings = JSON.parse(newIndexer.additionalSettings)
          iaCollection.value = settings.collection || 'librivoxaudio'
        } catch (e) {
          console.error('Failed to parse Internet Archive settings:', e)
          iaCollection.value = 'librivoxaudio'
        }
      }
    } else {
      formData.value = { ...defaultFormData }
      mamId.value = ''
      iaCollection.value = 'librivoxaudio'
    }
  },
  { immediate: true },
)

// Watch for implementation changes to auto-set type
watch(
  () => formData.value.implementation,
  (newImplementation) => {
    // Internet Archive is DDL only, set type to Usenet
    if (newImplementation === 'InternetArchive') {
      formData.value.type = 'Usenet'
    }
    // MyAnonamouse is torrent only
    else if (newImplementation === 'MyAnonamouse') {
      formData.value.type = 'Torrent'
    }
    // Torznab defaults to Torrent
    else if (newImplementation === 'Torznab') {
      formData.value.type = 'Torrent'
    }
    // Newznab defaults to Usenet
    else if (newImplementation === 'Newznab') {
      formData.value.type = 'Usenet'
    }
  },
)

const closeModal = () => {
  formData.value = { ...defaultFormData }
  emit('close')
}

const testConnection = async () => {
  testing.value = true
  try {
    const result = await apiTestIndexerDraft(buildIndexerPayload())
    if (result.success) {
      toast.success('Test successful', result.message || '')
    } else {
      toast.error('Test failed', result.error || result.message || '')
    }
  } catch (error: unknown) {
    console.error('Failed to test indexer:', error)

    // Try to parse error response body for detailed message
    let errorMessage = 'Failed to test indexer connection'
    const err = error as { body?: unknown; message?: string }
    if (err?.body) {
      try {
        const errorData =
          typeof err.body === 'string'
            ? JSON.parse(err.body)
            : (err.body as Record<string, unknown>)
        errorMessage =
          (errorData as { message?: string; error?: string }).message ||
          (errorData as { message?: string; error?: string }).error ||
          errorMessage
      } catch {
        // If body isn't JSON, use it as-is if it's a string
        if (typeof err.body === 'string' && err.body.length > 0) {
          errorMessage = err.body
        }
      }
    } else if (err?.message) {
      errorMessage = err.message
    }

    toast.error('Test failed', errorMessage)
  } finally {
    testing.value = false
  }
}

const handleSubmit = async () => {
  saving.value = true
  try {
    const submitData = buildIndexerPayload()

    if (props.editingIndexer) {
      // Update existing indexer
      await updateIndexer(props.editingIndexer.id, submitData)
      toast.success('Indexer saved', 'Indexer updated successfully')
    } else {
      // Create new indexer
      await createIndexer(submitData)
      toast.success('Indexer saved', 'Indexer created successfully')
    }

    emit('saved')
    closeModal()
  } catch (error) {
    console.error('Failed to save indexer:', error)
    toast.error('Save failed', 'Failed to save indexer')
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.checkbox-group label:hover {
  border-color: #007acc;
  background-color: #222;
}

.checkbox-group input[type='checkbox'] {
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
  border-radius: 6px;
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
