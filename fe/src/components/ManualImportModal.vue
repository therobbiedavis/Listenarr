<template>
  <div v-if="isOpen" class="modal-overlay" @click="close">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h2>
          <i class="ph ph-folder-open"></i>
          Manual Import - Select Folder
        </h2>
        <button class="close-btn" @click="close">
          <i class="ph ph-x"></i>
        </button>
      </div>

      <div class="modal-body" :class="{ 'browser-mode': browserMode }">
        <!-- Top folder input (full width) - hidden when preview is active -->
        <div v-if="!showPreview" class="top-path">
          <FolderBrowser 
            v-model="selectedPath" 
            placeholder="Select a folder..." 
            :inline="true"
            @browser-opened="browserMode = true"
            @browser-closed="browserMode = false"
          />
        </div>

        <!-- Centered action buttons like screenshots -->
        <div v-if="!showPreview" class="center-actions">
          <button class="btn btn-info" @click="startAutomaticImport" :disabled="!isPathValid || loading">
            <i class="ph ph-rocket"></i>
            Automatic Import
          </button>
          <button class="btn btn-primary" @click="startInteractiveImport" :disabled="!isPathValid || loading">
            <i class="ph ph-user"></i>
            Interactive Import
          </button>
        </div>

        <!-- Recent folders (session storage) -->
        <div v-if="!showPreview && recentFolders.length > 0" class="recent-folders">
          <div class="recent-title">Recent folders</div>
          <div class="recent-list">
            <button v-for="p in recentFolders" :key="p" class="recent-item" @click="selectRecent(p)">{{ p }}</button>
          </div>
        </div>

  <!-- Preview area (hidden until Interactive Import is clicked) -->
  <div v-if="showPreview" class="preview-area">
          <div v-if="loading" class="loading-state">
            <i class="ph ph-spinner ph-spin"></i>
            Loading files...
          </div>

          <div v-else-if="previewItems.length > 0" class="preview-list">
            <div class="preview-table">
              <div class="preview-header">
                <div class="col col-check">
                  <input type="checkbox" :checked="allSelected" @change="toggleSelectAll" />
                </div>
                <div class="col col-path">Relative Path</div>
                <div class="col col-audiobook">Audiobook</div>
                <div class="col col-release-group">Release Group</div>
                <div class="col col-quality">Quality</div>
                <div class="col col-language">Language</div>
                <div class="col col-size">Size</div>
                <div class="col col-action"></div>
              </div>

              <div class="preview-body">
                <div v-for="(it, idx) in previewItems" :key="idx" class="preview-row">
                  <div class="col col-check"><input type="checkbox" v-model="it.selected" /></div>
                  <div class="col col-path relative">{{ it.relativePath }}</div>
                  <div class="col col-audiobook">
                    <div class="clickable-cell" @click="openCellEditor(it, 'audiobook')">
                      <span v-if="it.matchedAudiobookId">{{ getLibraryTitle(it.matchedAudiobookId) }}</span>
                      <span v-else class="placeholder">&nbsp;</span>
                    </div>
                  </div>
                  <div class="col col-release-group">
                    <div class="clickable-cell" @click="openCellEditor(it, 'releaseGroup')">
                      <span v-if="it.releaseGroup">{{ it.releaseGroup }}</span>
                      <span v-else class="placeholder">&nbsp;</span>
                    </div>
                  </div>
                  <div class="col col-quality">
                    <div class="clickable-cell" @click="openCellEditor(it, 'quality')">
                      <span v-if="it.qualityProfileId">{{ getQualityName(it.qualityProfileId) }}</span>
                      <span v-else class="placeholder">&nbsp;</span>
                    </div>
                  </div>
                  <div class="col col-language">
                    <div class="clickable-cell" @click="openCellEditor(it, 'language')">
                      <span v-if="it.language">{{ getLanguageName(it.language) }}</span>
                      <span v-else class="placeholder">&nbsp;</span>
                    </div>
                  </div>
                  <div class="col col-size">{{ it.size || '' }}</div>
                  <div class="col col-action">
                    <button class="btn-icon" @click="openMatchDialog(it)"><i class="ph ph-warning"></i></button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-else class="preview-empty">
            <p>No files found in the selected folder.</p>
          </div>
        </div>
      </div>

      <div class="modal-footer">
        <div class="footer-left">
          <select class="extra-select" v-model="inputMode">
            <option value="">Select Import Mode</option>
            <option value="move">Move</option>
            <option value="copy">Copy</option>
          </select>
        </div>
        <div class="footer-right">
          <button class="btn btn-secondary" @click="close">Cancel</button>
          <button class="btn btn-success" @click="importSelected" :disabled="selectedCount === 0 || loading">Import</button>
        </div>
      </div>
    </div>

    <!-- Simple match dialog placeholder -->
    <div v-if="showMatch" class="match-dialog">
      <div class="match-content">
        <h4>Match file to audiobook</h4>
        <select v-model="matchSelection">
          <option v-for="book in library" :key="book.id" :value="book.id">{{ getBookDisplay(book) }}</option>
        </select>
        <div class="match-actions">
          <button class="btn btn-secondary" @click="closeMatch">Cancel</button>
          <button class="btn btn-primary" @click="confirmMatch">Match</button>
        </div>
      </div>
    </div>
  </div>

  <!-- Cell editor modal -->
  <div v-if="showCellEditor" class="match-dialog">
    <div class="match-content">
      <h4>Edit</h4>

      <div v-if="cellEditorField === 'audiobook'" class="editor-row">
        <div class="audiobook-table">
          <div class="table-header">
            <div class="table-col col-audiobook">Audiobook</div>
            <div class="table-col col-author">Author</div>
            <div class="table-col col-year">Year</div>
            <div class="table-col col-asin">ASIN</div>
          </div>
          <div class="table-body">
            <div v-for="book in library" :key="book.id" class="table-row" :class="{ active: cellEditorValue === book.id }" @click="selectEditorChoice(book.id)">
              <div class="table-col col-audiobook">{{ book.title || 'Unknown' }}</div>
              <div class="table-col col-author">{{ getBookAuthor(book) }}</div>
              <div class="table-col col-year">{{ getBookYear(book) }}</div>
              <div class="table-col col-asin">{{ getBookAsin(book) }}</div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="cellEditorField === 'quality'" class="editor-row">
        <div class="audiobook-table">
          <div class="table-header">
            <div class="table-col col-quality-profile">Quality Profile</div>
            <div class="table-col col-quality-description">Description</div>
          </div>
          <div class="table-body">
            <div v-for="q in qualityProfiles" :key="q.id" class="table-row" :class="{ active: cellEditorValue == q.id }" @click="selectEditorChoice(q.id ?? null)">
              <div class="table-col col-quality-profile">{{ q.name }}</div>
              <div class="table-col col-quality-description">{{ q.description || '' }}</div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="cellEditorField === 'language'" class="editor-row">
        <div class="audiobook-table">
          <div class="table-header">
            <div class="table-col col-language-name">Language</div>
          </div>
          <div class="table-body">
            <div v-for="(name, code) in languageMap" :key="code" class="table-row" :class="{ active: cellEditorValue === code }" @click="selectEditorChoice(code)">
              <div class="table-col col-language-name">{{ name }}</div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="cellEditorField === 'releaseGroup'" class="editor-row">
        <label>Release Group</label>
        <input v-model="cellEditorValue" class="form-input" placeholder="Enter release group..." />
      </div>

      <div class="match-actions">
        <button class="btn btn-secondary" @click="closeCellEditor">Cancel</button>
        <button v-if="cellEditorField === 'releaseGroup'" class="btn btn-primary" @click="saveCellEditor">Save</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue'
import FolderBrowser from '@/components/FolderBrowser.vue'
import { apiService } from '@/services/api'
import { useLibraryStore } from '@/stores/library'
import { useConfigurationStore } from '@/stores/configuration'

const props = withDefaults(defineProps<{ isOpen?: boolean; initialPath?: string }>(), { isOpen: false, initialPath: '' })

const emit = defineEmits(['close', 'imported'] as const)

const selectedPath = ref(props.initialPath || '')
const loading = ref(false)
const browserMode = ref(false)
const inputMode = ref<'move'|'copy'|''>('')
const showPreview = ref(false)
interface PreviewItem {
  relativePath: string
  audiobook?: string
  quality?: string
  languages?: string[]
  size?: string | null
  selected?: boolean
  matchedAudiobookId?: number | null
  releaseGroup?: string | null
  qualityProfileId?: number | null
  language?: string | null
  fullPath?: string | null
}
const previewItems = ref<PreviewItem[]>([])
// Recent folders stored in sessionStorage
const RECENT_KEY = 'manualImport.recentFolders'
const recentFolders = ref<string[]>([])

const loadRecentFolders = () => {
  try {
    const raw = sessionStorage.getItem(RECENT_KEY)
    if (!raw) return recentFolders.value = []
    const arr = JSON.parse(raw) as string[]
    recentFolders.value = Array.isArray(arr) ? arr : []
  } catch {
    recentFolders.value = []
  }
}

const saveRecentFolder = (path: string) => {
  if (!path) return
  // keep most recent first, dedupe, cap at 10
  const arr = [path, ...recentFolders.value.filter(p => p !== path)].slice(0, 10)
  recentFolders.value = arr
  try { sessionStorage.setItem(RECENT_KEY, JSON.stringify(arr)) } catch {}
}

const selectRecent = (path: string) => {
  selectedPath.value = path
}
const libraryStore = useLibraryStore()
const library = computed(() => libraryStore.audiobooks)
const configurationStore = useConfigurationStore()
const qualityProfiles = computed(() => configurationStore.qualityProfiles)
const showMatch = ref(false)
const matchTarget = ref<PreviewItem | null>(null)
const matchSelection = ref<number | null>(null)

// Cell editor state: used when clicking table cells to edit audiobook/quality/lang/release-group
const showCellEditor = ref(false)
const cellEditorItem = ref<PreviewItem | null>(null)
const cellEditorField = ref<string | null>(null)
const cellEditorValue = ref<number | string | null>(null)

// Helper display names
const getLibraryTitle = (id?: number | null) => {
  if (!id) return ''
  const found = library.value.find(b => b.id === id)
  return found ? (found.title || 'Unknown') : String(id)
}

type Book = {
  id?: number
  title?: string
  authors?: string[]
  year?: number | string
  publishYear?: number | string
  releaseDate?: string
  asin?: string
  asin13?: string
}

const getBookDisplay = (book: Book) => {
  const title = book.title ?? 'Untitled'
  const author = (book.authors && book.authors.length > 0) ? book.authors[0] : ''
  const yearCandidate = book.year ?? book.publishYear ?? (book.releaseDate ? String(book.releaseDate).slice(0,4) : undefined)
  const year = yearCandidate ? Number(String(yearCandidate)) : undefined
  const asin = book.asin ?? book.asin13 ?? undefined
  const meta: string[] = []
  if (author) meta.push(author)
  if (!Number.isNaN(year) && year) meta.push(String(year))
  if (asin) meta.push(`ASIN: ${asin}`)
  return meta.length ? `${title} — ${meta.join(' • ')}` : title
}

const getBookAuthor = (book: Book) => {
  return (book.authors && book.authors.length > 0) ? book.authors[0] : ''
}

const getBookYear = (book: Book) => {
  const rawYear = book.year ?? book.publishYear ?? (book.releaseDate ? String(book.releaseDate).slice(0,4) : undefined)
  return rawYear != null ? String(rawYear).replace(/[^0-9]/g, '') : ''
}

const getBookAsin = (book: Book) => {
  return (book.asin && String(book.asin).trim()) || (book.asin13 && String(book.asin13).trim()) || ''
}

const getQualityName = (id?: number | null) => {
  if (!id) return ''
  const found = (qualityProfiles.value || []).find((q: { id?: number; name?: string } ) => q.id === id)
  return found ? (found.name ?? String(id)) : String(id)
}

const languageMap: Record<string,string> = {
  en: 'English', es: 'Spanish', fr: 'French', de: 'German', it: 'Italian', ja: 'Japanese'
}

const getLanguageName = (code?: string | null) => {
  if (!code) return ''
  return languageMap[code] || code
}

const isPathValid = computed(() => {
  return typeof selectedPath.value === 'string' && selectedPath.value.trim().length > 0
})

const openCellEditor = (item: PreviewItem, field: string) => {
  cellEditorItem.value = item
  cellEditorField.value = field
  // initialize editor value from item
  if (field === 'audiobook') cellEditorValue.value = item.matchedAudiobookId ?? null
  else if (field === 'quality') cellEditorValue.value = item.qualityProfileId ?? null
  else if (field === 'language') cellEditorValue.value = item.language ?? null
  else if (field === 'releaseGroup') cellEditorValue.value = item.releaseGroup ?? null
  showCellEditor.value = true
}

const closeCellEditor = () => {
  showCellEditor.value = false
  cellEditorItem.value = null
  cellEditorField.value = null
  cellEditorValue.value = null
}

const saveCellEditor = () => {
  if (!cellEditorItem.value || !cellEditorField.value) return closeCellEditor()
  const it = cellEditorItem.value
  const f = cellEditorField.value
  if (f === 'audiobook') it.matchedAudiobookId = (cellEditorValue.value as number) ?? null
  else if (f === 'quality') it.qualityProfileId = (cellEditorValue.value as number) ?? null
  else if (f === 'language') it.language = (cellEditorValue.value as string) ?? null
  else if (f === 'releaseGroup') it.releaseGroup = (cellEditorValue.value as string) ?? null
  closeCellEditor()
}

onMounted(async () => {
  await libraryStore.fetchLibrary()
  await configurationStore.loadQualityProfiles()
  loadRecentFolders()
})

watch(() => props.isOpen, async (v) => {
  // only auto-load preview when modal opens AND interactive preview mode is active
  if (v && selectedPath.value && showPreview.value) {
    await loadPreview()
  }
})

watch(selectedPath, async (v) => {
  // only load preview automatically when interactive flow is active
  if (props.isOpen && v && showPreview.value) await loadPreview()
  // Save to recent folders when selecting a folder while modal is open
  if (props.isOpen && v) saveRecentFolder(v)
})

const loadPreview = async () => {
  if (!selectedPath.value) return
  loading.value = true
  try {
    const resp = await apiService.previewManualImport(selectedPath.value)
    // resp.items expected to be an array of detected files with metadata
    const respObj = resp as Record<string, unknown> | null
    const items = respObj && Array.isArray(respObj.items) ? respObj.items as unknown[] : []
    // only include common audio file extensions
    const audioExts = ['.mp3', '.m4b', '.m4a', '.flac', '.aac', '.ogg', '.wav', '.wma', '.opus']
    const filtered = items.filter(it => {
      const obj = it as Record<string, unknown>
      const name = (typeof obj.relativePath === 'string' ? obj.relativePath : (typeof obj.fullPath === 'string' ? obj.fullPath : ''))
      const lower = name.toLowerCase()
      return audioExts.some(ext => lower.endsWith(ext))
    })
    previewItems.value = filtered.map(i => {
      const obj = i as Record<string, unknown>
      const it: PreviewItem = {
        relativePath: typeof obj.relativePath === 'string' ? obj.relativePath : '',
        selected: false,
        matchedAudiobookId: typeof obj.matchedAudiobookId === 'number' ? obj.matchedAudiobookId : null,
        releaseGroup: typeof obj.releaseGroup === 'string' ? obj.releaseGroup : null,
        qualityProfileId: typeof obj.qualityProfileId === 'number' ? obj.qualityProfileId : null,
        language: typeof obj.language === 'string' ? obj.language : null,
        size: typeof obj.size === 'string' ? obj.size : null,
        fullPath: typeof obj.fullPath === 'string' ? obj.fullPath : null
      }
      return it
    })
  } catch (err) {
    console.error('Failed to preview import:', err)
    previewItems.value = []
  } finally {
    loading.value = false
  }
}

// When user clicks a choice in the cell-editor choice list, set the value and immediately save
const selectEditorChoice = (choice: number | string | null) => {
  cellEditorValue.value = choice
  // Persist to the item then close the editor
  saveCellEditor()
}

const startAutomaticImport = async () => {
  if (!selectedPath.value) return
  loading.value = true
  try {
    // When running automatic import, send minimal request; backend will handle scanning
  type AutoPayload = { path: string; mode: 'automatic'; inputMode?: 'move' | 'copy' }
  const autoPayload: AutoPayload = { path: selectedPath.value, mode: 'automatic' }
  if (inputMode.value === 'move' || inputMode.value === 'copy') autoPayload.inputMode = inputMode.value
  const resp = await apiService.startManualImport(autoPayload)
    // resp should contain import summary
    emit('imported', { imported: resp.importedCount ?? 0 })
    close()
  } catch (err) {
    console.error('Automatic import failed:', err)
  } finally {
    loading.value = false
  }
}

const startInteractiveImport = async () => {
  if (!selectedPath.value) return
  // Close inline browser if open, show preview area
  browserMode.value = false
  showPreview.value = true
  await loadPreview()
}

const importSelected = async () => {
  const selected = previewItems.value.filter(i => i.selected)
  if (selected.length === 0) return
  loading.value = true
  try {
    // Map items to the payload the backend expects
    const payloadItems = selected.map(i => ({
      relativePath: i.relativePath,
      fullPath: i.fullPath,
      matchedAudiobookId: i.matchedAudiobookId,
      releaseGroup: i.releaseGroup,
      qualityProfileId: i.qualityProfileId,
      language: i.language,
      size: i.size
    }))

  type ManualPayload = { path: string; mode: 'interactive'; items: Array<Record<string, unknown>>; inputMode?: 'move' | 'copy' }
  const manualPayload: ManualPayload = { path: selectedPath.value, mode: 'interactive', items: payloadItems as Array<Record<string, unknown>> }
  if (inputMode.value === 'move' || inputMode.value === 'copy') manualPayload.inputMode = inputMode.value
  const resp = await apiService.startManualImport(manualPayload)
    emit('imported', { imported: resp.importedCount ?? selected.length })
    close()
  } catch (err) {
    console.error('Manual import failed:', err)
  } finally {
    loading.value = false
  }
}

const close = () => {
  // reset preview state when closing
  showPreview.value = false
  previewItems.value = []
  emit('close')
}

const openMatchDialog = (item: PreviewItem) => {
  matchTarget.value = item
  matchSelection.value = (library.value && library.value.length > 0 && library.value[0] && library.value[0].id) ? library.value[0].id : null
  showMatch.value = true
}

const closeMatch = () => {
  showMatch.value = false
  matchTarget.value = null
}

const confirmMatch = async () => {
  if (!matchTarget.value || !matchSelection.value) return
  // Attach chosen audiobook id to the preview item so it will be imported to that audiobook
  matchTarget.value.matchedAudiobookId = matchSelection.value
  closeMatch()
}

const selectedCount = computed(() => previewItems.value.filter(i => i.selected).length)

const allSelected = computed(() => previewItems.value.length > 0 && previewItems.value.every(i => i.selected))

const toggleSelectAll = (ev: Event) => {
  const checked = (ev.target as HTMLInputElement).checked
  previewItems.value.forEach(i => i.selected = checked)
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
  display: flex;
  align-items: center;
  gap: 0.75rem;
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

.top-path {
  width: 100%;
}

.center-actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin: 0.5rem 0 1rem 0;
}

.preview-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.preview-table {
  border: 1px solid #333;
  border-radius: 4px;
  overflow: hidden;
}

.preview-header {
  display: flex;
  padding: 0.5rem;
  background: #2f2f2f;
  color: #ccc;
  font-weight: 600;
}

.preview-row {
  display: flex;
  padding: 0.65rem 0.6rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.03);
  align-items: center;
  height: 56px;
  background: #2b2b2b;
}

.preview-row:hover {
  background: #323232;
}

.col {
  padding: 0 0.6rem;
  display: flex;
  align-items: center;
  color: #e6e6e6;
}

.col-check {
  width: 44px;
  justify-content: center;
}

.col-check input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: #ffffff;
  border-radius: 3px;
}

.col-path {
  flex: 1;
  min-width: 320px;
  font-size: 0.95rem;
}

.col-audiobook {
  width: 180px;
  color: #cfcfcf;
  font-size: 0.9rem;
  max-width: 180px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.col-release-group {
  width: 100px;
  font-size: 0.9rem;
}

.col-quality {
  width: 110px;
  font-size: 0.9rem;
}

.col-language {
  width: 120px;
  display: flex;
  gap: 0.4rem;
  align-items: center;
}

.col-size {
  width: 110px;
  justify-content: flex-end;
  font-weight: 600;
}

.col-action {
  width: 48px;
  justify-content: center;
}

/* Clickable empty cells used to open the cell editor */
.clickable-cell {
  min-height: 34px;
  min-width: 100%;
  display: flex;
  align-items: center;
  padding: 0.25rem 0;
  border-radius: 4px;
  cursor: pointer;
}

.clickable-cell .placeholder {
  display: inline-block;
  width: 100%;
  height: 100%;
  border: 1px dashed rgba(255,255,255,0.12);
  border-radius: 4px;
  box-sizing: border-box;
}

.clickable-cell:hover .placeholder {
  border-color: rgba(255,255,255,0.22);
}

.clickable-cell:focus-within .placeholder {
  border-color: #2196F3;
}

/* Form elements */
.form-select,
.form-input {
  width: 100%;
  padding: 0.5rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 4px;
  color: #fff;
  font-size: 0.85rem;
  transition: all 0.2s;
}

.form-select:focus,
.form-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.relative {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.modal-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.footer-left {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.footer-right {
  display: flex;
  gap: 0.5rem;
}

.mode-select,
.extra-select {
  background: #333;
  color: #ddd;
  border: 1px solid #444;
  padding: 0.5rem;
  border-radius: 4px;
}

/* Button styles */
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

.btn-success {
  background-color: #2ecc71;
  color: white;
}

.btn-success:hover:not(:disabled) {
  background-color: #27ae60;
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

/* Loading state */
.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 2rem;
  color: #ccc;
}

/* Empty state */
.preview-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  color: #999;
}

.preview-empty p {
  margin: 0;
}

/* Match dialog */
.match-dialog {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1100;
}
.match-dialog::before {
  content: '';
  position: absolute;
  inset: 0;
  background: rgba(0,0,0,0.6);
  /* place behind content but above page */
  z-index: 1105;
}

.match-content {
  background: #2a2a2a;
  border: 1px solid #444;
  padding: 1.5rem;
  border-radius: 8px;
  width: 90vw;
  max-width: 1100px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
  z-index: 1110;
}

.match-content h4 {
  margin: 0 0 1rem 0;
  color: #fff;
  font-size: 1.2rem;
}

.match-content select {
  width: 100%;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 4px;
  color: #fff;
  margin-bottom: 1rem;
}

.match-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
}

.modal-body.browser-mode .center-actions,
.modal-body.browser-mode .preview-area {
  display: none;
}

.modal-body.browser-mode .top-path {
  position: relative;
  z-index: 10;
}

.modal-body.browser-mode .browser-inline {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 20;
  background: #2a2a2a;
  border-radius: 6px;
  padding: 0.75rem;
  box-shadow: 0 0 20px rgba(0, 0, 0, 0.8);
}

::v-deep .browser-body {
  max-height: unset !important;
  overflow: unset !important;
}

/* Audiobook table used in cell editor modal */
.audiobook-table {
  border: 1px solid #333;
  border-radius: 6px;
  background: #1f1f1f;
  overflow: hidden;
}

.table-header {
  display: flex;
  background: #292929;
  color: #dcdcdc;
  font-weight: 700;
  border-bottom: 1px solid #333;
}

.table-body {
  max-height: 320px;
  overflow-y: auto;
}

.table-row {
  display: flex;
  align-items: center;
  padding: 0.6rem 0.75rem;
  color: #e8e8e8;
  cursor: pointer;
  border-bottom: 1px solid rgba(255,255,255,0.03);
  transition: background-color 0.2s;
}

.table-row:hover {
  background: #232323;
}

.table-row.active {
  background: linear-gradient(90deg, rgba(33,150,243,0.06), rgba(33,150,243,0.02));
  border-left: 4px solid #2196F3;
}

.table-col {
  padding: 0 0.5rem;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.col-audiobook {
  flex: 2;
  min-width: 200px;
  font-weight: 600;
}

.col-author {
  flex: 1;
  min-width: 120px;
}

.col-year {
  flex: 0 0 80px;
  text-align: center;
}

.col-asin {
  flex: 1;
  min-width: 120px;
  font-family: monospace;
  font-size: 0.9rem;
}

.col-quality-profile {
  flex: 1;
  min-width: 150px;
  font-weight: 600;
}

.col-quality-description {
  flex: 2;
  min-width: 250px;
}

.col-language-name {
  flex: 1;
  min-width: 200px;
}

.editor-row {
  margin-bottom: 1rem;
}

.recent-folders {
  margin-top: 0.8rem;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  align-items: center;
}
.recent-title { color: #cfcfcf; font-weight: 600 }
.recent-list { display:flex; gap:0.5rem; flex-wrap:wrap; justify-content:center }
.recent-item { background:#1f1f1f; border:1px solid #333; color:#e8e8e8; padding:0.45rem 0.6rem; border-radius:6px; cursor:pointer }
.recent-item:hover { border-color:#444 }
</style>
