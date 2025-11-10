<template>
  <div v-if="visible" class="modal-overlay" @click="closeModal">
    <div class="modal-content add-library-modal" @click.stop>
      <div class="modal-header">
        <h2>Add to Library</h2>
        <button class="close-btn" @click="closeModal">
          <i class="ph ph-x"></i>
        </button>
      </div>

      <div class="modal-body">
        <div class="book-layout">
          <!-- Book Image -->
          <div class="book-image">
            <img v-if="book.imageUrl" :src="apiService.getImageUrl(book.imageUrl)" :alt="book.title" loading="lazy" />
            <div v-else class="placeholder-cover">
              <i class="ph ph-image"></i>
              <span>No Cover</span>
            </div>
          </div>

          <!-- Book Details -->
          <div class="book-details">
            <div class="detail-section">
              <h3>{{ book.title }}</h3>
              <p v-if="book.authors?.length" class="authors">
                by {{ book.authors.join(', ') }}
              </p>
              <p v-if="book.narrators?.length" class="narrators">
                Narrated by {{ book.narrators.join(', ') }}
              </p>
            </div>

            <div v-if="book.description" class="detail-section">
              <h4>Description</h4>
              <div class="description" v-html="book.description"></div>
            </div>

            <div class="detail-section">
              <h4>Publication Information</h4>
              <div class="detail-grid">
                <div v-if="book.publisher" class="detail-item">
                  <span class="label">Publisher:</span>
                  <span class="value">{{ book.publisher }}</span>
                </div>
                <div v-if="book.publishYear" class="detail-item">
                  <span class="label">Published:</span>
                  <span class="value">{{ book.publishYear }}</span>
                </div>
                <div v-if="book.language" class="detail-item">
                  <span class="label">Language:</span>
                  <span class="value">{{ capitalizeFirst(book.language) }}</span>
                </div>
                <div v-if="book.runtime" class="detail-item">
                  <span class="label">Runtime:</span>
                  <span class="value">{{ formatRuntime(book.runtime) }}</span>
                </div>
                <div v-if="book.asin" class="detail-item">
                  <span class="label">ASIN:</span>
                  <span class="value">{{ book.asin }}</span>
                </div>
                <div v-if="book.isbn" class="detail-item">
                  <span class="label">ISBN:</span>
                  <span class="value">{{ book.isbn }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Customization Options -->
        <div class="customization-section">
          <h4>Library Options</h4>

          <div class="option-group">
            <label class="option-label">
              <input
                type="checkbox"
                v-model="options.monitored"
                class="option-checkbox"
              />
              <span class="option-text">
                <strong>Monitor for new releases</strong>
                <br>
                <small>Automatically search for better quality versions of this audiobook</small>
              </span>
            </label>
          </div>

          <div class="option-group">
            <label class="option-label">
              <input
                type="checkbox"
                v-model="options.autoSearch"
                class="option-checkbox"
              />
              <span class="option-text">
                <strong>Search for downloads immediately</strong>
                <br>
                <small>Start searching for available downloads right after adding to library</small>
              </span>
            </label>
          </div>

          <div class="option-group">
            <label class="form-label">Quality Profile</label>
            <select v-model="options.qualityProfileId" class="form-select">
              <option :value="null">Use Default Profile</option>
              <option
                v-for="profile in qualityProfiles"
                :key="profile.id"
                :value="profile.id"
              >
                {{ profile.name }}{{ profile.isDefault ? ' (Default)' : '' }}
              </option>
            </select>
            <small class="form-help">
              Choose which quality profile to use for automatic downloads. Leave as "Use Default Profile" to automatically use the default profile.
            </small>
          </div>
        </div>
      </div>

      <div class="modal-footer">
        <button class="btn btn-secondary" @click="closeModal">
          <i class="ph ph-x"></i>
          Cancel
        </button>
        <button
          class="btn btn-primary"
          @click="addToLibrary"
          :disabled="isAdding"
        >
          <i v-if="isAdding" class="ph ph-spinner ph-spin"></i>
          <i v-else class="ph ph-plus"></i>
          {{ isAdding ? 'Adding...' : 'Add to Library' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { AudibleBookMetadata, QualityProfile, Audiobook } from '@/types'
import { apiService } from '@/services/api'
import { useConfigurationStore } from '@/stores/configuration'
import { useToast } from '@/services/toastService'

interface Props {
  visible: boolean
  book: AudibleBookMetadata
}

interface Emits {
  (e: 'close'): void
  (e: 'added', audiobook: Audiobook): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const configStore = useConfigurationStore()
const toast = useToast()

const isAdding = ref(false)
const qualityProfiles = ref<QualityProfile[]>([])

const options = ref({
  monitored: true,
  qualityProfileId: null as number | null,
  autoSearch: false
})

// Load quality profiles when modal opens
onMounted(async () => {
  await configStore.loadQualityProfiles()
  qualityProfiles.value = configStore.qualityProfiles
})

const closeModal = () => {
  emit('close')
}

const addToLibrary = async () => {
  if (!props.book) return

  isAdding.value = true
  try {
    const result = await apiService.addToLibrary(props.book, {
      monitored: options.value.monitored,
      qualityProfileId: options.value.qualityProfileId || undefined,
      autoSearch: options.value.autoSearch
    })

  toast.success('Added', `"${props.book.title}" has been added to your library!`)
    emit('added', result.audiobook)
    closeModal()
  } catch (err: unknown) {
    console.error('Failed to add audiobook:', err)
    const errorMessage = err instanceof Error ? err.message : 'Failed to add audiobook. Please try again.'
  toast.error('Add failed', errorMessage)
  } finally {
    isAdding.value = false
  }
}

const formatRuntime = (minutes: number): string => {
  if (!minutes) return 'Unknown'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

const capitalizeFirst = (str: string): string => {
  if (!str) return ''
  return str.charAt(0).toUpperCase() + str.slice(1).toLowerCase()
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-content {
  background-color: #2a2a2a;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
  max-width: 900px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #444;
}

.modal-header h2 {
  margin: 0;
  color: white;
  font-size: 1.5rem;
}

.close-btn {
  background: none;
  border: none;
  color: #ccc;
  font-size: 1.5rem;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
  color: white;
}

.modal-body {
  padding: 1.5rem;
  flex: 1;
  overflow-y: auto;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding: 1.5rem;
  border-top: 1px solid #444;
}

.book-layout {
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
}

.book-image {
  width: 160px;
  height: 160px;
  flex-shrink: 0;
  background-color: #555;
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
}

.book-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.placeholder-cover {
  color: #888;
  text-align: center;
}

.placeholder-cover i {
  font-size: 2rem;
  display: block;
  margin-bottom: 0.5rem;
}

.book-details {
  flex: 1;
}

.detail-section {
  margin-bottom: 1.5rem;
}

.detail-section h3 {
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 1.4rem;
}

.detail-section h4 {
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.1rem;
  border-bottom: 1px solid #444;
  padding-bottom: 0.5rem;
}

.authors, .narrators {
  color: #007acc;
  margin: 0.25rem 0;
  font-weight: 500;
}

.description {
  color: #ccc;
  line-height: 1.6;
  max-height: 150px;
  overflow-y: auto;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 0.75rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-item .label {
  color: #999;
  font-size: 0.9rem;
  font-weight: 500;
}

.detail-item .value {
  color: white;
  font-size: 0.95rem;
}

.customization-section {
  border-top: 1px solid #444;
  padding-top: 1.5rem;
}

.customization-section h4 {
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.1rem;
}

.option-group {
  margin-bottom: 1.5rem;
}

.option-label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
  padding: 0.75rem;
  border-radius: 8px;
  transition: background-color 0.2s;
}

.option-label:hover {
  background-color: rgba(255, 255, 255, 0.05);
}

.option-checkbox {
  margin-top: 0.25rem;
  width: 1rem;
  height: 1rem;
  accent-color: #007acc;
}

.option-text {
  flex: 1;
  color: white;
}

.option-text small {
  color: #ccc;
  display: block;
  margin-top: 0.25rem;
}

.form-label {
  display: block;
  color: white;
  font-weight: 500;
  margin-bottom: 0.5rem;
}

.form-select {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #555;
  border-radius: 8px;
  background-color: #333;
  color: white;
  font-size: 1rem;
}

.form-select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 2px rgba(0, 122, 204, 0.2);
}

.form-help {
  display: block;
  color: #ccc;
  font-size: 0.85rem;
  margin-top: 0.5rem;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 120px;
  justify-content: center;
  transition: all 0.2s;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005fa3;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
}

/* Responsive design */
@media (max-width: 768px) {
  .book-layout {
    flex-direction: column;
    align-items: center;
  }

  .book-image {
    width: 120px;
    height: 120px;
  }

  .modal-content {
    max-width: 95vw;
    margin: 1rem;
  }

  .modal-header, .modal-body, .modal-footer {
    padding: 1rem;
  }

  .detail-grid {
    grid-template-columns: 1fr;
  }
}
</style>