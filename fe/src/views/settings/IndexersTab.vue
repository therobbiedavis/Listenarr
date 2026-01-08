<template>
  <div class="tab-content">
    <div class="indexers-tab">
      <div class="section-header">
        <h3>Indexers</h3>
      </div>

      <div v-if="indexers.length === 0" class="empty-state">
        <PhListMagnifyingGlass />
        <p>No indexers configured. Add Newznab or Torznab indexers to search for audiobooks.</p>
      </div>

      <div v-else class="indexers-grid">
        <div
          v-for="indexer in indexers"
          :key="indexer.id"
          class="indexer-card"
          :class="{ disabled: !indexer.isEnabled }"
        >
          <div class="indexer-header">
            <div class="indexer-info">
              <h4>{{ indexer.name }}</h4>
              <span class="indexer-type" :class="indexer.type.toLowerCase()">
                {{ indexer.implementation === 'InternetArchive' ? 'DDL' : indexer.type }}
              </span>
            </div>
            <div class="indexer-actions">
              <button
                @click="toggleIndexerFunc(indexer.id)"
                class="icon-button"
                :title="indexer.isEnabled ? 'Disable' : 'Enable'"
              >
                <template v-if="indexer.isEnabled">
                  <PhToggleRight />
                </template>
                <template v-else>
                  <PhToggleLeft />
                </template>
              </button>
              <button
                @click="testIndexerFunc(indexer.id)"
                class="icon-button"
                title="Test"
                :disabled="testingIndexer === indexer.id"
              >
                <template v-if="testingIndexer === indexer.id">
                  <PhSpinner class="ph-spin" />
                </template>
                <template v-else>
                  <PhCheckCircle />
                </template>
              </button>
              <button @click="editIndexer(indexer)" class="icon-button" title="Edit">
                <PhPencil />
              </button>
              <button
                @click="confirmDeleteIndexer(indexer)"
                class="icon-button danger"
                title="Delete"
              >
                <PhTrash />
              </button>
            </div>
          </div>

          <div class="indexer-details">
            <div class="detail-row">
              <PhLink />
              <span class="detail-label">URL:</span>
              <span class="detail-value">{{ indexer.url }}</span>
            </div>
            <div class="detail-row">
              <PhListChecks />
              <span class="detail-label">Features:</span>
              <div class="feature-badges">
                <span v-if="indexer.enableRss" class="badge">RSS</span>
                <span v-if="indexer.enableAutomaticSearch" class="badge">Automatic Search</span>
                <span v-if="indexer.enableInteractiveSearch" class="badge">Interactive Search</span>
              </div>
            </div>
            <div class="detail-row" v-if="indexer.lastTestedAt">
              <PhClock />
              <span class="detail-label">Last Tested:</span>
              <span
                class="detail-value"
                :class="{
                  success: indexer.lastTestSuccessful,
                  error: indexer.lastTestSuccessful === false,
                }"
              >
                {{ formatDate(indexer.lastTestedAt) }}
                <template v-if="indexer.lastTestSuccessful">
                  <PhCheckCircle class="success" />
                </template>
                <template v-else-if="indexer.lastTestSuccessful === false">
                  <PhXCircle class="error" />
                </template>
              </span>
            </div>
            <div class="detail-row error-row" v-if="indexer.lastTestError">
              <PhWarning />
              <span class="detail-value error">{{ indexer.lastTestError }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Indexer Form Modal -->
      <IndexerFormModal
        :visible="showIndexerForm"
        :editing-indexer="editingIndexer"
        @close="showIndexerForm = false; editingIndexer = null"
        @saved="loadIndexers()"
      />

      <!-- Delete Indexer Confirmation Modal -->
      <div v-if="indexerToDelete" class="modal-overlay" @click="indexerToDelete = null">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>
              <PhWarningCircle />
              Delete Indexer
            </h3>
            <button @click="indexerToDelete = null" class="modal-close">
              <PhX />
            </button>
          </div>
          <div class="modal-body">
            <p>
              Are you sure you want to delete the indexer <strong>{{ indexerToDelete.name }}</strong
              >?
            </p>
            <p>This action cannot be undone.</p>
          </div>
          <div class="modal-actions">
            <button type="button" @click="indexerToDelete = null" class="cancel-button">
              Cancel
            </button>
            <button
              type="button"
              @click="executeDeleteIndexer()"
              class="delete-button modal-delete-button"
            >
              <PhTrash />
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  PhListMagnifyingGlass,
  PhToggleRight,
  PhToggleLeft,
  PhCheckCircle,
  PhXCircle,
  PhPencil,
  PhTrash,
  PhLink,
  PhListChecks,
  PhClock,
  PhWarning,
  PhWarningCircle,
  PhX,
  PhSpinner,
} from '@phosphor-icons/vue'
import IndexerFormModal from '@/components/IndexerFormModal.vue'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import type { Indexer } from '@/types'
import {
  getIndexers,
  toggleIndexer as apiToggleIndexer,
  testIndexer as apiTestIndexer,
  deleteIndexer,
} from '@/services/api'

// State
const toast = useToast()
const indexers = ref<Indexer[]>([])
const showIndexerForm = ref(false)
const editingIndexer = ref<Indexer | null>(null)
const indexerToDelete = ref<Indexer | null>(null)
const testingIndexer = ref<number | null>(null)

// Functions
const formatApiError = (error: unknown): string => {
  const err = error as { response?: { data?: unknown }; message?: string }
  const resp = err.response as Record<string, unknown> | undefined
  const data = resp?.data as unknown
  if (data) {
    if (typeof data === 'string') return data
    if ((data as Record<string, unknown>)['message']) return String((data as Record<string, unknown>)['message'])
    if ((data as Record<string, unknown>)['error']) return String((data as Record<string, unknown>)['error'])
    if ((data as Record<string, unknown>)['title']) return String((data as Record<string, unknown>)['title'])
  }
  if (err?.message) return err.message
  return 'An unknown error occurred'
}

const formatDate = (dateString: string | undefined): string => {
  if (!dateString) return 'Never'
  const date = new Date(dateString)
  return date.toLocaleString()
}

const loadIndexers = async () => {
  try {
    indexers.value = await getIndexers()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'IndexersTab',
      operation: 'loadIndexers',
    })
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

const toggleIndexerFunc = async (id: number) => {
  try {
    const updatedIndexer = await apiToggleIndexer(id)
    const index = indexers.value.findIndex((i) => i.id === id)
    if (index !== -1) {
      indexers.value[index] = updatedIndexer
    }
    toast.success(
      'Indexer',
      `Indexer ${updatedIndexer.isEnabled ? 'enabled' : 'disabled'} successfully`,
    )
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'IndexersTab',
      operation: 'toggleIndexer',
    })
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

const testIndexerFunc = async (id: number) => {
  testingIndexer.value = id
  try {
    const result = await apiTestIndexer(id)
    if (result.success) {
      toast.success('Indexer test', `Indexer tested successfully: ${result.message}`)
      const index = indexers.value.findIndex((i) => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    } else {
      const errorMessage = formatApiError({ response: { data: result.error || result.message } })
      toast.error('Indexer test failed', errorMessage)
      const index = indexers.value.findIndex((i) => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    }
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'IndexersTab',
      operation: 'testIndexer',
    })
    const errorMessage = formatApiError(error)
    toast.error('Indexer test failed', errorMessage)
  } finally {
    testingIndexer.value = null
  }
}

const editIndexer = (indexer: Indexer) => {
  editingIndexer.value = indexer
  showIndexerForm.value = true
}

const confirmDeleteIndexer = (indexer: Indexer) => {
  indexerToDelete.value = indexer
}

const executeDeleteIndexer = async () => {
  if (!indexerToDelete.value) return

  try {
    await deleteIndexer(indexerToDelete.value.id)
    indexers.value = indexers.value.filter((i) => i.id !== indexerToDelete.value!.id)
    toast.success('Indexer', 'Indexer deleted successfully')
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'IndexersTab',
      operation: 'deleteIndexer',
    })
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    indexerToDelete.value = null
  }
}

// Lifecycle
onMounted(() => {
  loadIndexers()
})

const openAddIndexer = () => {
  editingIndexer.value = null
  showIndexerForm.value = true
}

defineExpose({ openAddIndexer })
</script>

<style scoped>
.tab-content {
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.indexers-tab {
  width: 100%;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.add-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: var(--primary);
  color: white;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s;
}

.add-button:hover {
  background: var(--primary-hover);
  transform: translateY(-1px);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  text-align: center;
  color: var(--text-secondary);
  background: var(--background-secondary);
  border-radius: var(--border-radius);
  border: 2px dashed var(--border);
}

.empty-state svg {
  font-size: 3rem;
  margin-bottom: 1rem;
  opacity: 0.5;
}

.indexers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
  gap: 1rem;
}

.indexer-card {
  background: var(--background-secondary);
  border: 1px solid var(--border);
  border-radius: var(--border-radius);
  transition: all 0.2s;
}

.indexer-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.indexer-card.disabled {
  opacity: 0.6;
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--border);
}

/* Align indexer info inline (name + type) */
.indexer-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
}

.indexer-info h4 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.indexer-type {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.indexer-type.torrent {
  background: #10b981;
  color: white;
}

.indexer-type.usenet {
  background: #3b82f6;
  color: white;
}

.indexer-type.ddl {
  background: #8b5cf6;
  color: white;
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
}

.icon-button {
  padding: 0.5rem;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 4px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  color: var(--text-secondary);
}

.icon-button:hover {
  background: var(--background-hover);
  border-color: var(--primary);
  color: var(--primary);
}

.icon-button.danger:hover {
  border-color: var(--error);
  color: var(--error);
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
}

/* Icon sizing & color consistency */
.detail-row svg,
.detail-row .ph-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  color: var(--text-secondary);
}

.indexer-header svg,
.indexer-header .ph-icon,
.icon-button svg {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  vertical-align: middle;
}

.detail-label {
  font-weight: 600;
  color: var(--text-secondary);
  min-width: 90px;
}

.detail-value {
  color: var(--text-primary);
  word-break: break-all;
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.detail-value.success {
  color: var(--success);
}

.detail-value.error {
  color: var(--error);
}

.detail-value svg.success {
  color: var(--success);
}

.detail-value svg.error {
  color: var(--error);
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  padding: 0.25rem 0.5rem;
  background: var(--primary);
  color: white;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.error-row {
  background: rgba(239, 68, 68, 0.1);
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid var(--error);
}

/* Modal Styles (canonical) */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.85);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
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
  padding: 1.5rem;
  border-bottom: 1px solid #444;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.25rem;
}

.modal-close {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
}

.modal-close:hover {
  background: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem;
  border-top: 1px solid #444;
}

/* Ensure modal context delete buttons are full-size */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button,
.modal-overlay .modal-content .modal-actions .modal-delete-button,
.modal-content .modal-actions .modal-delete-button {
  padding: 0.75rem 1.25rem;
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.18s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.6rem;
  font-weight: 700;
  font-size: 1rem;
  min-width: 120px;
  height: auto;
  box-shadow: 0 6px 16px rgba(231, 76, 60, 0.12);
}

.modal-overlay .modal-content .modal-actions .delete-button:hover,
.modal-content .modal-actions .delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
}

.cancel-button,
.delete-button {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.cancel-button {
  background: var(--background-secondary);
  color: var(--text-primary);
}

.cancel-button:hover {
  background: var(--background-hover);
}

.delete-button {
  background: var(--error);
  color: white;
}

.delete-button:hover {
  background: #dc2626;
}

/* Section Header */
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

/* Add Button */
.add-button {
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.add-button:hover {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

/* Empty State */
.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #868e96;
}

.empty-state .empty-icon {
  font-size: 4rem;
  color: #495057;
  margin-bottom: 1rem;
  width: 4rem;
  height: 4rem;
}

.empty-state h3 {
  margin: 1rem 0 0.5rem 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.empty-state p {
  margin: 0.5rem 0;
  font-size: 1.05rem;
  line-height: 1.6;
  color: #adb5bd;
}

.add-button-large {
  margin-top: 1.5rem;
  padding: 1rem 2rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  font-weight: 600;
  font-size: 1rem;
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.3);
}

.add-button-large:hover {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(30, 136, 229, 0.4);
}

/* Indexer Grid */
.indexers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1.5rem;
  margin-top: 1.5rem;
}

/* Indexer Card */
.indexer-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  transition: all 0.2s ease;
}

.indexer-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.indexer-card.disabled {
  opacity: 0.5;
  filter: grayscale(50%);
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.indexer-type {
  display: inline-block;
  padding: 0.3rem 0.75rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.indexer-type.torrent {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.indexer-type.usenet {
  background-color: rgba(33, 150, 243, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.indexer-type.ddl {
  background-color: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.icon-button {
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  cursor: pointer;
  color: #adb5bd;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  font-size: 1.1rem;
  width: 36px;
  height: 36px;
}

.icon-button:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.15);
  border-color: #4dabf7;
  color: #4dabf7;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(77, 171, 247, 0.3);
}

.icon-button.danger {
  color: #ff6b6b;
}

.icon-button.danger:hover:not(:disabled) {
  background: rgba(255, 107, 107, 0.15);
  border-color: #ff6b6b;
  color: #ff6b6b;
  box-shadow: 0 2px 8px rgba(255, 107, 107, 0.3);
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1.5rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-size: 0.9rem;
}

.detail-row i {
  color: #4dabf7;
  font-size: 1rem;
  flex-shrink: 0;
}

.detail-label {
  color: #868e96;
  min-width: 100px;
}

.detail-value {
  color: #adb5bd;
  word-break: break-all;
}

.detail-value.success {
  color: #51cf66;
}

.detail-value.error {
  color: #ff6b6b;
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.6rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
}

/* Mobile Responsive */
@media (max-width: 768px) {
  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .add-button {
    width: 100%;
    justify-content: center;
  }

  .indexers-grid {
    grid-template-columns: 1fr;
  }

  .indexer-header {
    flex-direction: column;
    gap: 1rem;
  }

  .indexer-actions {
    width: 100%;
    justify-content: flex-start;
    margin-left: 0;
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .detail-label {
    min-width: auto;
  }
}
</style>
