<template>
  <div class="audiobook-detail">
    <div v-if="loading" class="center">
      <PhSpinner class="ph-spin" />
      <p>Loading audiobook details...</p>
    </div>

    <div v-else-if="error" class="center error">
      <PhWarningCircle />
      <h2>Error Loading Audiobook</h2>
      <p>{{ error }}</p>
      <button @click="goBack" class="back-btn">
        <PhArrowLeft />
        Back to Library
      </button>
    </div>

    <div v-else class="content">
      <div class="top-nav">
        <button class="nav-btn" @click="goBack">
          <PhArrowLeft />
          Back
        </button>
        <div class="nav-actions">
          <button class="nav-btn" @click="refresh">
            <PhArrowClockwise />
            Refresh
          </button>
          <button class="nav-btn" @click="toggleMonitored">
            <PhBookmark :weight="audiobook?.monitored ? 'fill' : 'regular'" />
            {{ audiobook?.monitored ? 'Monitored' : 'Monitor' }}
          </button>
          <button class="nav-btn" :disabled="scanning || scanQueued" @click="scanFiles">
            <template v-if="scanning">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else-if="scanQueued">
              <PhSpinner />
            </template>
            <template v-else>
              <PhMagnifyingGlass />
            </template>
            <span v-if="scanning">Scanning...</span>
            <span v-else-if="scanQueued">Scan queued</span>
            <span v-else>Scan Folder</span>
          </button>
          <button class="nav-btn delete-btn" @click="confirmDelete">
            <PhTrash />
            Delete
          </button>
        </div>
      </div>

      <div class="hero">
        <img :src="audiobook?.imageUrl || placeholder" alt="cover" />
        <div class="info">
          <h1>{{ audiobook?.title || 'Unknown Title' }}</h1>
          <p class="meta">{{ audiobook?.authors?.join(', ') }}</p>
          <div class="badges">
            <span class="badge" v-if="audiobook?.abridged">Abridged</span>
            <span class="badge" v-if="audiobook?.publishYear">{{ audiobook.publishYear }}</span>
          </div>
        </div>
      </div>

      <div class="tabs">
        <button :class="{ active: activeTab === 'details' }" @click="activeTab = 'details'">Details</button>
        <button :class="{ active: activeTab === 'files' }" @click="activeTab = 'files'">Files</button>
        <button :class="{ active: activeTab === 'history' }" @click="activeTab = 'history'">History</button>
      </div>

      <div class="tab-content">
        <div v-if="activeTab === 'details'">
          <h3>Description</h3>
          <div v-html="audiobook?.description || '<em>No description</em>'"></div>
        </div>

        <div v-else-if="activeTab === 'files'">
          <h3>Files</h3>
          <div v-if="audiobook?.files && audiobook.files.length">
            <ul>
              <li v-for="f in audiobook.files" :key="f.id">
                <PhFileAudio /> {{ getFileName(f.path) }} <small>• {{ f.format }}</small>
              </li>
            </ul>
          </div>
          <div v-else class="empty">No files available</div>
        </div>

        <div v-else>
          <h3>History</h3>
          <div v-if="historyLoading">Loading history...</div>
          <div v-else-if="historyEntries.length === 0" class="empty">No history available</div>
          <ul v-else>
            <li v-for="h in historyEntries" :key="h.id">{{ h.eventType }} - {{ h.message }}</li>
          </ul>
        </div>
      </div>

      <div v-if="showDeleteDialog" class="dialog">
        <div class="dialog-inner">
          <h3><PhWarningCircle /> Confirm Deletion</h3>
          <p>Are you sure you want to delete <strong>{{ audiobook?.title }}</strong>?</p>
          <div class="dialog-actions">
            <button @click="cancelDelete">Cancel</button>
            <button class="confirm" @click="executeDelete" :disabled="deleting">{{ deleting ? 'Deleting...' : 'Delete' }}</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { PhArrowLeft, PhArrowClockwise, PhBookmark, PhSpinner, PhMagnifyingGlass, PhTrash, PhFileAudio, PhWarningCircle } from '@phosphor-icons/vue'
import { useLibraryStore } from '@/stores/library'
import { apiService } from '@/services/api'
import type { Audiobook, History } from '@/types'

const route = useRoute()
const router = useRouter()
const libraryStore = useLibraryStore()

const audiobook = ref(null as Audiobook | null)
const loading = ref(true)
const error = ref(null as string | null)
const activeTab = ref<'details' | 'files' | 'history'>('details')
const showDeleteDialog = ref(false)
const deleting = ref(false)
const scanning = ref(false)
const scanQueued = ref(false)
const historyEntries = ref([] as History[])
const historyLoading = ref(false)

const placeholder = '/public/icon.png'

function goBack() {
  router.push('/audiobooks')
}

function getFileName(path?: string) {
  if (!path) return 'Unknown'
  return path.split(/[\\/]/).pop() || path
}

async function refresh() {
  await loadAudiobook()
}

function toggleMonitored() {
  if (!audiobook.value) return
  audiobook.value.monitored = !audiobook.value.monitored
}

async function scanFiles() {
  if (!audiobook.value) return
  scanning.value = true
  try {
    // fire-and-forget
    await apiService.scanAudiobook(audiobook.value.id)
  } catch {
    // ignore scan errors (fire-and-forget)
  } finally {
    scanning.value = false
  }
}

function confirmDelete() { showDeleteDialog.value = true }
function cancelDelete() { showDeleteDialog.value = false }

async function executeDelete() {
  if (!audiobook.value) return
  deleting.value = true
  try {
    const success = await libraryStore.removeFromLibrary(audiobook.value.id)
    if (success) router.push('/audiobooks')
  } finally {
    deleting.value = false
    showDeleteDialog.value = false
  }
}

async function loadAudiobook() {
  loading.value = true
  error.value = null
  const id = parseInt(route.params.id as string)
  if (isNaN(id)) {
    error.value = 'Invalid audiobook id'
    loading.value = false
    return
  }
  try {
    const local = libraryStore.audiobooks.find(b => b.id === id)
    if (local) audiobook.value = local
    else audiobook.value = await apiService.getAudiobook(id)
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadAudiobook()
})
</script>

<style scoped>
.center { display:flex; flex-direction:column; align-items:center; justify-content:center; min-height:200px }
.error { color: #e74c3c }
.top-nav { display:flex; justify-content:space-between; align-items:center; gap:12px }
.nav-actions { display:flex; gap:8px }
.nav-btn { display:flex; align-items:center; gap:8px }
.delete-btn { background:#e74c3c; color:white }
.hero { display:flex; gap:16px; margin:16px 0 }
.hero img { width:160px; height:240px; object-fit:cover; border-radius:6px }
.tabs { display:flex; gap:8px; margin:12px 0 }
.tab-content { padding:12px; background:#222; border-radius:6px }
.dialog { position:fixed; left:0; right:0; top:0; bottom:0; display:flex; align-items:center; justify-content:center; background:rgba(0,0,0,0.6) }
.dialog-inner { background:#fff; padding:16px; border-radius:6px; width:320px }
.dialog-actions { display:flex; justify-content:flex-end; gap:8px; margin-top:12px }
.badge {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 500;
}

.badge.monitored {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
  border: 1px solid #2ecc71;
}

.badge.quality-profile {
  background-color: rgba(46, 204, 113, 0.15);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.3);
  font-weight: 600;
  font-size: 14px;
}

.badge.profile {
  background-color: rgba(52, 152, 219, 0.12);
  color: #3498db;
  border: 1px solid rgba(52, 152, 219, 0.25);
}

.badge.continuing {
  background-color: rgba(52, 152, 219, 0.2);
  color: #3498db;
  border: 1px solid #3498db;
}

.badge.language {
  background-color: rgba(155, 89, 182, 0.2);
  color: #9b59b6;
  border: 1px solid #9b59b6;
}

.badge.tlc {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
  border: 1px solid #f1c40f;
}

.description {
  color: #ccc;
  line-height: 1.6;
  max-width: 900px;
  position: relative;
}

.description-content {
  position: relative;
  max-height: 140px;
  overflow: hidden;
  transition: max-height 0.3s ease;
}

@media (max-width: 768px) {
  .description-content {
    max-height: 100px;
  }
}

.description-content:not(.expanded)::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 40px;
  pointer-events: none;
}

.description-content:not(.expanded) {
  mask-image: linear-gradient(to bottom, white 70%, transparent 100%);
  -webkit-mask-image: linear-gradient(to bottom, white 70%, transparent 100%);
}

.description-content.expanded {
  max-height: none;
}

.show-more-btn {
  margin-top: 12px;
  padding: 8px 16px;
  background-color: rgba(0, 122, 204, 0.1);
  border: 1px solid #007acc;
  border-radius: 4px;
  color: #007acc;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.show-more-btn:hover {
  background-color: rgba(0, 122, 204, 0.2);
  transform: translateY(-1px);
}

.description :deep(p) {
  margin: 0 0 12px 0;
}

.description :deep(br) {
  display: block;
  margin: 8px 0;
}

.description :deep(strong),
.description :deep(b) {
  color: #fff;
  font-weight: 600;
}

.description :deep(em),
.description :deep(i) {
  font-style: italic;
}

.description :deep(a) {
  color: #007acc;
  text-decoration: none;
}

.description :deep(a:hover) {
  text-decoration: underline;
}

.description :deep(ul),
.description :deep(ol) {
  margin: 12px 0;
  padding-left: 24px;
}

.description :deep(li) {
  margin: 4px 0;
}

.tabs-container {
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
  padding: 0 40px;
}

@media (max-width: 768px) {
  .tabs-container {
    padding: 0 20px;
  }
}

.tabs {
  display: flex;
  gap: 4px;
  max-width: 1600px;
  margin: 0 auto;
}

.tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 12px 20px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: #999;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}

.tab:hover {
  color: #fff;
}

.tab.active {
  color: #007acc;
  border-bottom-color: #007acc;
}

.tab-content {
  padding: 40px 40px;
  max-width: 1600px;
  margin: 0 auto;
}

@media (max-width: 768px) {
  .tab-content {
    padding: 30px 20px;
  }
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
  gap: 24px;
}

@media (min-width: 1200px) {
  .details-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 768px) {
  .details-grid {
    grid-template-columns: 1fr;
  }
}

.detail-card {
  background-color: #2a2a2a;
  border: 1px solid #333;
  border-radius: 8px;
  padding: 20px;
}

.detail-card h3 {
  margin: 0 0 16px 0;
  color: #fff;
  font-size: 16px;
  border-bottom: 1px solid #333;
  padding-bottom: 12px;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid #333;
}

.detail-row:last-child {
  border-bottom: none;
}

.detail-row .label {
  color: #999;
  font-size: 14px;
}

.detail-row .value {
  color: #fff;
  font-size: 14px;
  text-align: right;
}

.genre-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.genre-tag {
  padding: 6px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 12px;
}

.files-content, .history-content {
  background-color: #2a2a2a;
  border: 1px solid #333;
  border-radius: 8px;
  padding: 20px;
}

.files-header, .history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 12px;
  border-bottom: 1px solid #333;
}

.files-header h3, .history-header h3 {
  margin: 0;
  color: #fff;
}

.action-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background-color: #3a3a3a;
  border: 1px solid #555;
  border-radius: 4px;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.action-btn:hover {
  background-color: #4a4a4a;
}

.file-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.file-item {
  display: flex;
  flex-direction: column;
  padding: 12px;
  background-color: #333;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.file-item.expanded {
  background-color: #3a3a3a;
}

.file-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
  width: 100%;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
  color: #fff;
  flex: 1;
}

.file-info i {
  font-size: 24px;
  color: #007acc;
}

.file-name {
  font-weight: 500;
}

.file-meta {
  color: #999;
}

.file-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.accordion-toggle {
  color: #999;
  transition: transform 0.2s ease;
  font-size: 16px;
}

.accordion-toggle.rotated {
  transform: rotate(180deg);
}

.file-accordion {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #444;
  animation: slideDown 0.2s ease-out;
}

@keyframes slideDown {
  from {
    opacity: 0;
    max-height: 0;
  }
  to {
    opacity: 1;
    max-height: 500px;
  }
}

.metadata-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.metadata-table tbody tr {
  border-bottom: 1px solid #444;
}

.metadata-table tbody tr:last-child {
  border-bottom: none;
}

.metadata-label {
  color: #999;
  padding: 8px 12px 8px 0;
  font-weight: 500;
  width: 120px;
  vertical-align: top;
}

.metadata-value {
  color: #fff;
  padding: 8px 0;
  word-break: break-word;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
  color: #fff;
}

.file-info i {
  font-size: 24px;
  color: #007acc;
}

.file-size {
  color: #999;
  font-size: 14px;
}

.empty-history {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #666;
}

.empty-history i {
  font-size: 48px;
  margin-bottom: 12px;
}

.empty-history .hint {
  font-size: 14px;
  color: #555;
  margin-top: 8px;
}

.empty-files {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #666;
}

.empty-files i {
  font-size: 48px;
  margin-bottom: 12px;
}

.empty-files .hint {
  font-size: 14px;
  color: #555;
  margin-top: 8px;
}

/* History Styles */
.history-loading, .history-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  color: #999;
}

.history-loading i {
  font-size: 36px;
  margin-bottom: 12px;
}

.history-error i {
  font-size: 36px;
  margin-bottom: 12px;
  color: #e74c3c;
}

.retry-btn, .refresh-btn {
  margin-top: 12px;
  padding: 8px 16px;
  background-color: #007acc;
  border: none;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.retry-btn:hover, .refresh-btn:hover {
  background-color: #005fa3;
}

.refresh-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.history-entry {
  display: flex;
  gap: 16px;
  padding: 16px;
  background-color: #333;
  border-radius: 8px;
  border-left: 3px solid #555;
  transition: transform 0.2s, box-shadow 0.2s;
}

.history-entry:hover {
  transform: translateX(4px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.history-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  flex-shrink: 0;
}

.history-icon i {
  font-size: 20px;
}

.event-success {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
}

.event-info {
  background-color: rgba(52, 152, 219, 0.2);
  color: #3498db;
}

.event-warning {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
}

.event-danger {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
}

.event-default {
  background-color: rgba(149, 165, 166, 0.2);
  color: #95a5a6;
}

.history-details {
  flex: 1;
}

.history-event {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.event-type {
  font-weight: 600;
  color: #fff;
  font-size: 14px;
}

.event-source {
  font-size: 12px;
  color: #999;
  padding: 2px 8px;
  background-color: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
}

.history-message {
  color: #ccc;
  font-size: 14px;
  margin-bottom: 8px;
  line-height: 1.4;
}

.history-time {
  color: #777;
  font-size: 12px;
}

.loading-container, .error-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  color: #ccc;
  background-color: #1a1a1a;
}

.loading-container i, .error-container i {
  font-size: 48px;
  margin-bottom: 16px;
}

.loading-container i {
  color: #007acc;
}

.error-container i {
  color: #e74c3c;
}

.error-container h2 {
  color: #fff;
  margin: 0 0 8px 0;
}

.back-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 20px;
  padding: 12px 24px;
  background-color: #007acc;
  border: none;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.back-btn:hover {
  background-color: #005fa3;
}

/* Delete Dialog */
.dialog-overlay {
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
}

.dialog {
  background-color: #2a2a2a;
  border-radius: 8px;
  border: 1px solid #444;
  width: 90%;
  max-width: 500px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.dialog-header {
  padding: 20px;
  border-bottom: 1px solid #444;
}

.dialog-header h3 {
  margin: 0;
  color: #fff;
  font-size: 18px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.dialog-header i {
  color: #f39c12;
}

.dialog-body {
  padding: 20px;
  color: #ccc;
}

.dialog-body p {
  margin: 0 0 12px 0;
  line-height: 1.5;
}

.dialog-body strong {
  color: #fff;
}

.warning-text {
  color: #f39c12;
  font-size: 14px;
}

.dialog-actions {
  padding: 20px;
  border-top: 1px solid #444;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.dialog-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
}

.cancel-btn {
  background-color: #3a3a3a;
  color: #fff;
}

.cancel-btn:hover {
  background-color: #4a4a4a;
}

.confirm-btn {
  background-color: #e74c3c;
  color: #fff;
}

.confirm-btn:hover:not(:disabled) {
  background-color: #c0392b;
}

.confirm-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

</style>

