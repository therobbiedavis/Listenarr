<template>
  <div v-if="visible" class="modal-overlay" @click="closeModal">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h2>Audiobook Details</h2>
        <button class="close-btn" @click="closeModal">
          <i class="ph ph-x"></i>
        </button>
      </div>
      
      <div class="modal-body">
        <div class="book-layout">
          <!-- Book Image -->
          <div class="book-image">
            <img v-if="book.imageUrl" :src="book.imageUrl" :alt="book.title" />
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
                  <span class="value">{{ book.language }}</span>
                </div>
                <div v-if="book.runtime" class="detail-item">
                  <span class="label">Listening Length:</span>
                  <span class="value">{{ formatRuntime(book.runtime) }}</span>
                </div>
                <div v-if="book.version" class="detail-item">
                  <span class="label">Version:</span>
                  <span class="value">{{ book.version }}</span>
                </div>
              </div>
            </div>

            <div class="detail-section">
              <h4>Identifiers</h4>
              <div class="detail-grid">
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

            <div v-if="book.series || book.genres?.length" class="detail-section">
              <h4>Series & Genre Information</h4>
              <div class="detail-grid">
                <div v-if="book.series" class="detail-item">
                  <span class="label">Series:</span>
                  <span class="value">{{ book.series }}</span>
                </div>
                <div v-if="book.seriesNumber" class="detail-item">
                  <span class="label">Book Number:</span>
                  <span class="value">{{ book.seriesNumber }}</span>
                </div>
                <div v-if="book.genres?.length" class="detail-item">
                  <span class="label">Genres:</span>
                  <span class="value">{{ book.genres.join(', ') }}</span>
                </div>
              </div>
            </div>

            <div v-if="hasFlags" class="detail-section">
              <h4>Content Flags</h4>
              <div class="flags">
                <span v-if="book.explicit" class="flag explicit">Explicit</span>
                <span v-if="book.abridged" class="flag abridged">Abridged</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <div class="modal-footer">
        <button class="btn btn-secondary" @click="closeModal">
          <i class="ph ph-x"></i>
          Close
        </button>
        <button class="btn btn-primary" @click="addToLibrary">
          <i class="ph ph-plus"></i>
          Add to Library
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { AudibleBookMetadata } from '@/types'

interface Props {
  visible: boolean
  book: AudibleBookMetadata
}

interface Emits {
  (e: 'close'): void
  (e: 'add-to-library', book: AudibleBookMetadata): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const hasFlags = computed(() => {
  return props.book.explicit || props.book.abridged
})

const closeModal = () => {
  emit('close')
}

const addToLibrary = () => {
  emit('add-to-library', props.book)
  closeModal()
}

const formatRuntime = (minutes: number): string => {
  if (!minutes) return 'Unknown'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
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
  padding: 1rem;
}

.modal-content {
  background-color: #1a1a1a;
  border-radius: 12px;
  border: 1px solid #333;
  max-width: 900px;
  width: 100%;
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #333;
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
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  font-size: 1.25rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.close-btn:hover {
  background-color: #333;
  color: white;
}

.modal-body {
  flex: 1;
  overflow-y: auto;
  padding: 1.5rem;
}

.book-layout {
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 2rem;
  align-items: start;
}

.book-image {
  position: sticky;
  top: 0;
}

.book-image img {
  width: 100%;
  height: auto;
  border-radius: 8px;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.placeholder-cover {
  width: 100%;
  aspect-ratio: 2/3;
  background-color: #333;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #666;
  text-align: center;
  padding: 1rem;
}

.placeholder-cover i {
  font-size: 3rem;
  margin-bottom: 0.5rem;
}

.placeholder-cover span {
  font-size: 0.9rem;
}

.book-details {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.detail-section h3 {
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 1.75rem;
  line-height: 1.2;
}

.authors {
  color: #007acc;
  font-size: 1.1rem;
  font-weight: 500;
  margin: 0 0 0.25rem 0;
}

.narrators {
  color: #ccc;
  font-style: italic;
  margin: 0;
}

.detail-section h4 {
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.1rem;
  font-weight: 600;
  border-bottom: 1px solid #333;
  padding-bottom: 0.5rem;
}

.description {
  color: #ccc;
  line-height: 1.6;
  margin: 0;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
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
  font-weight: 400;
}

.flags {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.flag {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

.flag.explicit {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid #e74c3c;
}

.flag.abridged {
  background-color: rgba(243, 156, 18, 0.2);
  color: #f39c12;
  border: 1px solid #f39c12;
}

.modal-footer {
  padding: 1.5rem;
  border-top: 1px solid #333;
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
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
  font-size: 0.95rem;
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
  .modal-content {
    margin: 0.5rem;
    max-height: 95vh;
  }
  
  .book-layout {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }
  
  .book-image {
    position: static;
    max-width: 200px;
    margin: 0 auto;
  }
  
  .detail-grid {
    grid-template-columns: 1fr;
  }
  
  .modal-footer {
    flex-direction: column-reverse;
  }
  
  .btn {
    justify-content: center;
  }
}

@media (max-width: 480px) {
  .modal-overlay {
    padding: 0.5rem;
  }
  
  .modal-header,
  .modal-body,
  .modal-footer {
    padding: 1rem;
  }
  
  .modal-header h2 {
    font-size: 1.25rem;
  }
  
  .detail-section h3 {
    font-size: 1.5rem;
  }
}
</style>