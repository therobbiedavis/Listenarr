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
              <label class="form-label">Destination</label>
              <div class="destination-display">
                <div class="destination-row">
                  <div class="root-label">{{ rootPath || 'Not configured' }}\</div>
                  <input type="text" v-model="options.relativePath" class="form-input relative-input" placeholder="e.g. Author/Title" />
                </div>
                <small class="form-help">Root (left) is read-only — edit the output path relative to it on the right.</small>
              </div>
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
import { ref, onMounted, watch } from 'vue'
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
  autoSearch: false,
  // editable relative path portion (relative to rootPath)
  relativePath: '' as string | null
})

const rootPath = ref<string>('')
const previewFull = ref<string>('')
const previewRelative = ref<string>('')

// Hold an enriched metadata object (populate if metadata sources available)
const enriched = ref<AudibleBookMetadata | null>(null)

// Local types for audimeta response to avoid `any`
interface AudimetaPerson { name?: string }
interface AudimetaSeries { name?: string; position?: string | number }
interface AudimetaGenre { name?: string }
interface Audimeta {
  asin?: string
  title?: string
  subtitle?: string
  authors?: AudimetaPerson[]
  narrators?: AudimetaPerson[]
  publisher?: string
  publishDate?: string
  releaseDate?: string
  description?: string
  imageUrl?: string
  lengthMinutes?: number
  language?: string
  genres?: AudimetaGenre[]
  series?: AudimetaSeries[]
  bookFormat?: string
  isbn?: string
}

// Helper to map audimeta response to AudibleBookMetadata
const mapAudimetaToAudible = (audimeta: Partial<Audimeta> | undefined, source?: string): AudibleBookMetadata => {
  let publishYear: string | undefined
  const dateStr = audimeta?.publishDate || audimeta?.releaseDate
  if (dateStr && typeof dateStr === 'string') {
    const yearMatch = dateStr.match(/\d{4}/)
    publishYear = yearMatch ? yearMatch[0] : undefined
  }

  const authors = (audimeta?.authors || []).map(a => a?.name).filter(Boolean) as string[]
  const narrators = (audimeta?.narrators || []).map(n => n?.name).filter(Boolean) as string[]
  const genres = (audimeta?.genres || []).map(g => g?.name).filter(Boolean) as string[]

  const firstSeries = (audimeta?.series && audimeta.series.length > 0) ? audimeta.series[0] : undefined

  return {
    asin: audimeta?.asin || props.book?.asin || '',
    title: audimeta?.title || props.book?.title || 'Unknown Title',
    subtitle: audimeta?.subtitle,
    authors: authors.length ? authors : (props.book?.authors || []),
    narrators: narrators.length ? narrators : (props.book?.narrators || []),
    publisher: audimeta?.publisher || props.book?.publisher,
    publishYear: publishYear || props.book?.publishYear,
    description: audimeta?.description || props.book?.description,
    imageUrl: audimeta?.imageUrl || props.book?.imageUrl,
    runtime: typeof audimeta?.lengthMinutes === 'number' ? audimeta.lengthMinutes * 60 : props.book?.runtime,
    language: audimeta?.language || props.book?.language,
    genres: genres.length ? genres : (props.book?.genres || []),
    series: firstSeries?.name || props.book?.series,
    seriesNumber: firstSeries?.position !== undefined ? String(firstSeries.position) : props.book?.seriesNumber,
    abridged: typeof audimeta?.bookFormat === 'string' ? audimeta.bookFormat.toLowerCase().includes('abridged') : Boolean(props.book?.abridged),
    isbn: audimeta?.isbn || props.book?.isbn,
    source: source || props.book?.source
  }
}

// helper to load profiles/settings and seed preview
const seedPreview = async () => {
  await configStore.loadQualityProfiles()
  qualityProfiles.value = configStore.qualityProfiles

  // Load application settings to get default root
  await configStore.loadApplicationSettings()
  rootPath.value = configStore.applicationSettings?.outputPath || ''

  // Attempt to fetch enriched metadata for the ASIN (if present) so preview/add use metadata sources
  try {
    if (props.book?.asin) {
      try {
        const resp = await apiService.getMetadata(props.book.asin, 'us', true)
        if (resp && resp.metadata) {
          enriched.value = mapAudimetaToAudible(resp.metadata, resp.source)
        }
      } catch (metaErr) {
        // ignore metadata fetch errors - we'll fall back to provided book
        console.debug('Metadata fetch failed in AddLibraryModal:', metaErr)
      }
    }

    const metadataForPreview = (enriched.value || props.book) as AudibleBookMetadata
    // Compute a preview path using server logic
    const resp2 = await apiService.previewLibraryPath(metadataForPreview, rootPath.value || undefined)
    previewFull.value = resp2?.fullPath || ''
    previewRelative.value = resp2?.relativePath || ''
    // Seed editable relative path
    options.value.relativePath = previewRelative.value
  } catch (e) {
    console.error('Failed to preview path:', e)
  }
}

// Load when mounted
onMounted(() => {
  seedPreview()
})

// Re-seed preview if the passed book changes after mount (parent may update props)
watch(() => props.book, (newVal) => {
  if (!newVal) return
  seedPreview()
})

const closeModal = () => {
  emit('close')
}

const addToLibrary = async () => {
  if (!props.book) return

  isAdding.value = true
  // Combine rootPath + relativePath into full destination path
    let destination: string | undefined = undefined
    try {
      const rel = (options.value.relativePath || '').trim()
      if (rootPath.value && rel) {
        const sep = rootPath.value.includes('\\') ? '\\' : '/'
        const cleanedRel = rel.replace(/\\|\//g, sep)
        destination = rootPath.value.endsWith(sep) ? rootPath.value + cleanedRel : rootPath.value + sep + cleanedRel
      } else if (rootPath.value && !rel) {
        destination = rootPath.value
      }

      const metadataToSend = (enriched.value || props.book) as AudibleBookMetadata
      const result = await apiService.addToLibrary(metadataToSend, {
        monitored: options.value.monitored,
        qualityProfileId: options.value.qualityProfileId || undefined,
        autoSearch: options.value.autoSearch,
        destinationPath: destination || undefined
      })
      toast.success('Added', `"${metadataToSend.title}" has been added to your library!`)
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border-radius: 6px;
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

/* Destination display styles */
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
  padding: 0.45rem 0 0.45rem 0.6rem;
  color: #ccc;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', 'Segoe UI Mono', monospace;
  font-size: 0.9rem;
  width: fit-content;
  white-space: nowrap;
}

.relative-input {
  flex: 1 1 auto;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
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