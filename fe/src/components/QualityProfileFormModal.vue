<template>
  <div v-if="visible" class="modal-overlay" @click="closeModal">
    <div class="modal-content quality-profile-modal" @click.stop>
      <div class="modal-header">
        <h2>{{ profile ? 'Edit Quality Profile' : 'Create Quality Profile' }}</h2>
        <button class="close-btn" @click="closeModal">
          <i class="ph ph-x"></i>
        </button>
      </div>
      
      <div class="modal-body">
        <form @submit.prevent="handleSubmit">
          <!-- Basic Information -->
          <div class="form-section">
            <h3><i class="ph ph-info"></i> Basic Information</h3>
            
            <div class="form-group">
              <label for="name">Profile Name *</label>
              <input 
                id="name" 
                v-model="formData.name" 
                type="text" 
                required 
                placeholder="e.g., High Quality, Any Quality, Space Saver"
              />
            </div>

            <div class="form-group">
              <label for="description">Description</label>
              <textarea 
                id="description" 
                v-model="formData.description" 
                rows="2"
                placeholder="Optional description of this quality profile"
              />
            </div>

            <div class="form-group">
              <label class="checkbox-label">
                <input 
                  type="checkbox" 
                  v-model="formData.isDefault"
                />
                <span>Set as default profile</span>
              </label>
              <small class="info-text">
                The default profile will be automatically assigned to new audiobooks
              </small>
            </div>
          </div>

          <!-- Quality Definitions -->
          <div class="form-section">
            <h3><i class="ph ph-check-square"></i> Quality Definitions</h3>
            <p class="section-description">
              Select which qualities to allow and set their priority (higher priority = preferred).
              The cutoff quality determines when to stop upgrading.
            </p>
            
            <div class="quality-list">
              <div v-for="quality in availableQualities" :key="quality" class="quality-item">
                <label class="checkbox-label">
                  <input 
                    type="checkbox" 
                    :checked="isQualityAllowed(quality)"
                    @change="toggleQuality(quality, $event)"
                  />
                  <span class="quality-name">{{ quality }}</span>
                </label>
                
                <div v-if="isQualityAllowed(quality)" class="quality-controls">
                  <label class="priority-label">
                    Priority:
                    <input 
                      type="number" 
                      :value="getQualityPriority(quality)"
                      @input="updateQualityPriority(quality, $event)"
                      min="0"
                      max="100"
                      class="priority-input"
                    />
                  </label>
                  
                  <label class="radio-label">
                    <input 
                      type="radio" 
                      :value="quality"
                      v-model="formData.cutoffQuality"
                      :disabled="!isQualityAllowed(quality)"
                    />
                    <span class="cutoff-text">Cutoff</span>
                  </label>
                </div>
              </div>
            </div>
            
            <small class="info-text">
              <i class="ph ph-info"></i>
              Cutoff quality: Downloads will stop upgrading once this quality is reached
            </small>
          </div>

          <!-- Format Preferences -->
          <div class="form-section">
            <h3><i class="ph ph-file-audio"></i> Format Preferences</h3>
            <p class="section-description">
              Preferred audio formats in order of preference (most preferred first).
            </p>
            
            <div class="tag-input-group">
              <div class="tags-list">
                <div 
                  v-for="(format, index) in formData.preferredFormats" 
                  :key="index"
                  class="tag removable"
                >
                  {{ format }}
                  <button type="button" @click="removeFormat(index)" class="tag-remove">
                    <PhX />
                  </button>
                </div>
              </div>
              <div class="tag-input">
                <input 
                  v-model="newFormat"
                  @keypress.enter.prevent="addFormat"
                  type="text"
                  placeholder="e.g., M4B, MP3, M4A"
                />
                <button type="button" @click="addFormat" class="add-button">
                  <i class="ph ph-plus"></i>
                  Add
                </button>
              </div>
            </div>
          </div>

          <!-- Size Limits -->
          <div class="form-section">
            <h3><i class="ph ph-ruler"></i> Size Limits</h3>
            <p class="section-description">
              Set minimum and maximum file sizes in megabytes (leave blank for no limit).
            </p>
            
            <div class="form-row">
              <div class="form-group">
                <label for="minimumSize">Minimum Size (MB)</label>
                <input 
                  id="minimumSize"
                  v-model.number="formData.minimumSize"
                  type="number"
                  min="0"
                  placeholder="No minimum"
                />
              </div>
              
              <div class="form-group">
                <label for="maximumSize">Maximum Size (MB)</label>
                <input 
                  id="maximumSize"
                  v-model.number="formData.maximumSize"
                  type="number"
                  min="0"
                  placeholder="No maximum"
                />
              </div>
            </div>
          </div>

          <!-- Word Filters -->
          <div class="form-section">
            <h3><i class="ph ph-text-aa"></i> Word Filters</h3>
            
            <!-- Preferred Words -->
            <div class="filter-group">
              <h4><i class="ph ph-sparkle"></i> Preferred Words (Bonus Points)</h4>
              <p class="section-description">
                Releases containing these words will receive bonus points in scoring.
              </p>
              <div class="tag-input-group">
                <div class="tags-list">
                  <div 
                    v-for="(word, index) in formData.preferredWords" 
                    :key="index"
                    class="tag positive removable"
                  >
                    {{ word }}
                    <button type="button" @click="removePreferredWord(index)" class="tag-remove">
                      <PhX />
                    </button>
                  </div>
                </div>
                <div class="tag-input">
                  <input 
                    v-model="newPreferredWord"
                    @keypress.enter.prevent="addPreferredWord"
                    type="text"
                    placeholder="e.g., unabridged, complete"
                  />
                  <button type="button" @click="addPreferredWord" class="add-button">
                    <i class="ph ph-plus"></i>
                    Add
                  </button>
                </div>
              </div>
            </div>

            <!-- Must Contain -->
            <div class="filter-group">
              <h4><i class="ph ph-check"></i> Must Contain (Required)</h4>
              <p class="section-description">
                Releases MUST contain at least one of these words (case-insensitive).
              </p>
              <div class="tag-input-group">
                <div class="tags-list">
                  <div 
                    v-for="(word, index) in formData.mustContain" 
                    :key="index"
                    class="tag required removable"
                  >
                    {{ word }}
                    <button type="button" @click="removeMustContain(index)" class="tag-remove">
                      <PhX />
                    </button>
                  </div>
                </div>
                <div class="tag-input">
                  <input 
                    v-model="newMustContain"
                    @keypress.enter.prevent="addMustContain"
                    type="text"
                    placeholder="e.g., audiobook"
                  />
                  <button type="button" @click="addMustContain" class="add-button">
                    <i class="ph ph-plus"></i>
                    Add
                  </button>
                </div>
              </div>
            </div>

            <!-- Must Not Contain -->
            <div class="filter-group">
              <h4><i class="ph ph-x"></i> Must Not Contain (Forbidden)</h4>
              <p class="section-description">
                Releases containing any of these words will be rejected (case-insensitive).
              </p>
              <div class="tag-input-group">
                <div class="tags-list">
                  <div 
                    v-for="(word, index) in formData.mustNotContain" 
                    :key="index"
                    class="tag forbidden removable"
                  >
                    {{ word }}
                    <button type="button" @click="removeMustNotContain(index)" class="tag-remove">
                      <PhX />
                    </button>
                  </div>
                </div>
                <div class="tag-input">
                  <input 
                    v-model="newMustNotContain"
                    @keypress.enter.prevent="addMustNotContain"
                    type="text"
                    placeholder="e.g., abridged, radio"
                  />
                  <button type="button" @click="addMustNotContain" class="add-button">
                    <i class="ph ph-plus"></i>
                    Add
                  </button>
                </div>
              </div>
            </div>
          </div>

          <!-- Language Preferences -->
          <div class="form-section">
            <h3><i class="ph ph-translate"></i> Language Preferences</h3>
            <p class="section-description">
              Preferred languages in order of preference.
            </p>
            
            <div class="tag-input-group">
              <div class="tags-list">
                <div 
                  v-for="(lang, index) in formData.preferredLanguages" 
                  :key="index"
                  class="tag removable"
                >
                  {{ lang }}
                  <button type="button" @click="removeLanguage(index)" class="tag-remove">
                    <PhX />
                  </button>
                </div>
              </div>
              <div class="tag-input">
                <input 
                  v-model="newLanguage"
                  @keypress.enter.prevent="addLanguage"
                  type="text"
                  placeholder="e.g., English, Spanish"
                />
                <button type="button" @click="addLanguage" class="add-button">
                  <i class="ph ph-plus"></i>
                  Add
                </button>
              </div>
            </div>
          </div>

          <!-- Release Preferences -->
          <div class="form-section">
            <h3><i class="ph ph-clock-counter-clockwise"></i> Release Preferences</h3>
            
            <div class="form-group">
              <label for="minimumSeeders">Minimum Seeders (Torrents)</label>
              <input 
                id="minimumSeeders"
                v-model.number="formData.minimumSeeders"
                type="number"
                min="0"
                placeholder="0 = no minimum"
              />
              <small class="info-text">
                Reject torrent releases with fewer seeders than this
              </small>
            </div>

            <div class="form-group">
              <label class="checkbox-label">
                <input 
                  type="checkbox" 
                  v-model="formData.preferNewerReleases"
                />
                <span>Prefer newer releases</span>
              </label>
              <small class="info-text">
                Give bonus points to more recent releases (torrent upload date)
              </small>
            </div>

            <div v-if="formData.preferNewerReleases" class="form-group">
              <label for="maximumAge">Maximum Age (Days)</label>
              <input 
                id="maximumAge"
                v-model.number="formData.maximumAge"
                type="number"
                min="0"
                placeholder="0 = no maximum"
              />
              <small class="info-text">
                Reject releases older than this many days (0 = no limit)
              </small>
            </div>
          </div>

          <!-- Form Actions -->
          <div class="form-actions">
            <button type="button" @click="closeModal" class="cancel-button">
              Cancel
            </button>
            <button type="submit" class="submit-button">
              <i class="ph ph-check"></i>
              {{ profile ? 'Update Profile' : 'Create Profile' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { PhX } from '@phosphor-icons/vue'
import type { QualityProfile } from '@/types'

const props = defineProps<{
  visible: boolean
  profile: QualityProfile | null
}>()

const emit = defineEmits<{
  close: []
  save: [profile: QualityProfile]
}>()

// Available quality options
const availableQualities = [
  'Unknown',
  'Low (64 kbps)',
  'Medium (128 kbps)',
  'High (192-256 kbps)',
  'Lossless (FLAC)'
]

// Form data
const formData = ref<QualityProfile>({
  name: '',
  description: '',
  qualities: [],
  cutoffQuality: '',
  minimumSize: undefined,
  maximumSize: undefined,
  preferredFormats: [],
  preferredWords: [],
  mustNotContain: [],
  mustContain: [],
  preferredLanguages: [],
  minimumSeeders: 0,
  isDefault: false,
  preferNewerReleases: false,
  maximumAge: 0
})

// Tag input refs
const newFormat = ref('')
const newPreferredWord = ref('')
const newMustContain = ref('')
const newMustNotContain = ref('')
const newLanguage = ref('')

// Initialize form when profile changes
watch(() => props.profile, (newProfile) => {
  if (newProfile) {
    formData.value = JSON.parse(JSON.stringify(newProfile))
  } else {
    // Reset to defaults
    formData.value = {
      name: '',
      description: '',
      qualities: [],
      cutoffQuality: '',
      minimumSize: undefined,
      maximumSize: undefined,
      preferredFormats: [],
      preferredWords: [],
      mustNotContain: [],
      mustContain: [],
      preferredLanguages: [],
      minimumSeeders: 0,
      isDefault: false,
      preferNewerReleases: false,
      maximumAge: 0
    }
  }
}, { immediate: true })

// Quality management
const isQualityAllowed = (quality: string): boolean => {
  return formData.value.qualities.some(q => q.quality === quality && q.allowed)
}

const getQualityPriority = (quality: string): number => {
  const qual = formData.value.qualities.find(q => q.quality === quality)
  return qual?.priority ?? 0
}

const toggleQuality = (quality: string, event: Event) => {
  const target = event.target as HTMLInputElement
  const allowed = target.checked
  
  if (!formData.value.qualities) {
    formData.value.qualities = []
  }
  
  const existingIndex = formData.value.qualities.findIndex(q => q.quality === quality)
  
  if (existingIndex !== -1) {
    const qualityDef = formData.value.qualities[existingIndex]
    if (qualityDef) {
      qualityDef.allowed = allowed
    }
  } else {
    formData.value.qualities.push({
      quality,
      allowed,
      priority: 50
    })
  }

  // Clear cutoff if quality is disabled
  if (!allowed && formData.value.cutoffQuality === quality) {
    formData.value.cutoffQuality = ''
  }
}

const updateQualityPriority = (quality: string, event: Event) => {
  const target = event.target as HTMLInputElement
  const priority = parseInt(target.value)
  
  const qual = formData.value.qualities.find(q => q.quality === quality)
  if (qual) {
    qual.priority = priority
  }
}

// Format management
const addFormat = () => {
  const format = newFormat.value.trim()
  if (format && !formData.value.preferredFormats?.includes(format)) {
    if (!formData.value.preferredFormats) {
      formData.value.preferredFormats = []
    }
    formData.value.preferredFormats.push(format)
    newFormat.value = ''
  }
}

const removeFormat = (index: number) => {
  formData.value.preferredFormats?.splice(index, 1)
}

// Preferred words management
const addPreferredWord = () => {
  const word = newPreferredWord.value.trim()
  if (word && !formData.value.preferredWords?.includes(word)) {
    if (!formData.value.preferredWords) {
      formData.value.preferredWords = []
    }
    formData.value.preferredWords.push(word)
    newPreferredWord.value = ''
  }
}

const removePreferredWord = (index: number) => {
  formData.value.preferredWords?.splice(index, 1)
}

// Must contain management
const addMustContain = () => {
  const word = newMustContain.value.trim()
  if (word && !formData.value.mustContain?.includes(word)) {
    if (!formData.value.mustContain) {
      formData.value.mustContain = []
    }
    formData.value.mustContain.push(word)
    newMustContain.value = ''
  }
}

const removeMustContain = (index: number) => {
  formData.value.mustContain?.splice(index, 1)
}

// Must not contain management
const addMustNotContain = () => {
  const word = newMustNotContain.value.trim()
  if (word && !formData.value.mustNotContain?.includes(word)) {
    if (!formData.value.mustNotContain) {
      formData.value.mustNotContain = []
    }
    formData.value.mustNotContain.push(word)
    newMustNotContain.value = ''
  }
}

const removeMustNotContain = (index: number) => {
  formData.value.mustNotContain?.splice(index, 1)
}

// Language management
const addLanguage = () => {
  const lang = newLanguage.value.trim()
  if (lang && !formData.value.preferredLanguages?.includes(lang)) {
    if (!formData.value.preferredLanguages) {
      formData.value.preferredLanguages = []
    }
    formData.value.preferredLanguages.push(lang)
    newLanguage.value = ''
  }
}

const removeLanguage = (index: number) => {
  formData.value.preferredLanguages?.splice(index, 1)
}

// Modal actions
const closeModal = () => {
  emit('close')
}

import { useToast } from '@/services/toastService'

const handleSubmit = () => {
  const toast = useToast()
  // Validate at least one quality is selected
  if (!formData.value.qualities.some(q => q.allowed)) {
    toast.error('Validation', 'Please select at least one quality')
    return
  }

  // Validate cutoff quality is selected and allowed
  if (!formData.value.cutoffQuality) {
    toast.error('Validation', 'Please select a cutoff quality')
    return
  }

  if (!formData.value.qualities.some(q => q.quality === formData.value.cutoffQuality && q.allowed)) {
    toast.error('Validation', 'Cutoff quality must be one of the allowed qualities')
    return
  }

  emit('save', formData.value)
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
  max-width: 800px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
}

.quality-profile-modal {
  max-width: 900px;
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
  color: #fff;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.close-btn {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  transition: color 0.2s;
}

.close-btn:hover {
  color: #fff;
}

.modal-body {
  padding: 1.5rem;
  overflow-y: auto;
  flex: 1;
}

.form-section {
  margin-bottom: 2rem;
  padding-bottom: 1.5rem;
  border-bottom: 1px solid #444;
}

.form-section:last-of-type {
  border-bottom: none;
}

.form-section h3 {
  margin: 0 0 0.5rem 0;
  color: #2196F3;
  font-size: 1.2rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.form-section h4 {
  margin: 1rem 0 0.5rem 0;
  color: #fff;
  font-size: 1rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.section-description {
  margin: 0.5rem 0 1rem 0;
  color: #999;
  font-size: 0.9rem;
  line-height: 1.4;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  color: #ddd;
  font-weight: 500;
}

.form-group input[type="text"],
.form-group input[type="url"],
.form-group input[type="number"],
.form-group textarea,
.form-group select {
  width: 100%;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  color: #fff;
  font-size: 1rem;
}

.form-group textarea {
  resize: vertical;
  font-family: inherit;
}

.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 1rem;
  color: #ddd;
  cursor: pointer;
  user-select: none;
  padding: 0.5rem 0;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  margin: 0;
  cursor: pointer;
  flex-shrink: 0;
  -webkit-appearance: none;
  appearance: none;
  background-color: #1a1a1a;
  border: 2px solid #555;
  border-radius: 6px;
  position: relative;
  transition: all 0.2s ease;
  vertical-align: sub;
}

.checkbox-label input[type="checkbox"]:hover {
  border-color: #007acc;
}

.checkbox-label input[type="checkbox"]:checked {
  background-color: #007acc;
  border-color: #007acc;
}

.checkbox-label input[type="checkbox"]:checked::after {
  content: '';
  position: absolute;
  left: 5px;
  top: 2px;
  width: 4px;
  height: 8px;
  border: solid white;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.checkbox-label input[type="checkbox"]:focus {
  outline: 2px solid rgba(0, 122, 204, 0.3);
  outline-offset: 2px;
}

.checkbox-label span {
  line-height: 1.4;
  font-size: 0.95rem;
  margin-left: 0.25rem;
}

.info-text {
  display: block;
  margin-top: 0.5rem;
  color: #999;
  font-size: 0.85rem;
  line-height: 1.4;
}

.info-text i {
  color: #2196F3;
}

/* Quality List */
.quality-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.quality-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
}

.quality-name {
  flex: 1;
  color: #fff;
  font-weight: 500;
}

.quality-controls {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.priority-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #ddd;
  font-size: 0.9rem;
}

.priority-input {
  width: 70px;
  padding: 0.4rem;
  background-color: #2a2a2a;
  border: 1px solid #555;
  border-radius: 6px;
  color: #fff;
}

.radio-label {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  color: #ddd;
  font-size: 0.9rem;
  cursor: pointer;
}

.radio-label input[type="radio"] {
  cursor: pointer;
}

.cutoff-text {
  color: #ff9800;
}

/* Tag Input */
.tag-input-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  min-height: 2rem;
}

.tag {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.8rem;
  background-color: #2196F3;
  color: #fff;
  border-radius: 6px;
  font-size: 0.9rem;
}

.tag.removable {
  padding-right: 0.4rem;
}

.tag.positive {
  background-color: #4caf50;
}

.tag.required {
  background-color: #ff9800;
}

.tag.forbidden {
  background-color: #f44336;
}

.tag-remove {
  background: none;
  border: none;
  color: #fff;
  cursor: pointer;
  padding: 0.2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0.8;
  transition: opacity 0.2s;
}

.tag-remove:hover {
  opacity: 1;
}

.tag-input {
  display: flex;
  gap: 0.5rem;
}

.tag-input input {
  flex: 1;
  padding: 0.6rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  color: #fff;
}

.add-button {
  padding: 0.6rem 1rem;
  background-color: #2196F3;
  color: #fff;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.3rem;
  transition: background-color 0.2s;
}

.add-button:hover {
  background-color: #1976d2;
}

.filter-group {
  margin-bottom: 1.5rem;
}

.filter-group:last-child {
  margin-bottom: 0;
}

/* Form Actions */
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid #444;
}

.cancel-button,
.submit-button {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  transition: all 0.2s;
}

.cancel-button {
  background-color: #444;
  color: #fff;
}

.cancel-button:hover {
  background-color: #555;
}

.submit-button {
  background-color: #2196F3;
  color: #fff;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.submit-button:hover {
  background-color: #1976d2;
}
</style>
