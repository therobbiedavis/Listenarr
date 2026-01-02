<template>
  <div class="root-folders-settings">
    <div v-if="!props.hideHeader" class="section-header">
      <h3>
        Root Folders
      </h3>
    </div>
    <div v-if="store.loading" class="loading-state">
      <PhSpinner class="ph-spin" />
      <p>Loading root folders...</p>
    </div>

    <div v-else>
      <div v-if="store.folders.length === 0" class="empty-state">
        <PhFolderOpen />
        <h4>No root folders configured</h4>
        <p>Add a root folder to organize your audiobook library. You can create multiple named root folders for different storage locations.</p>
        <button class="btn primary" @click="openAdd()">
          <PhPlus />
          Add Your First Root Folder
        </button>
      </div>

      <div v-else class="folders-list">
        <div v-for="folder in store.folders" :key="folder.id" class="folder-card" :class="{ 'is-default': folder.isDefault }">
          <div class="folder-info">
            <div class="folder-header">
              <h4>{{ folder.name }}</h4>
              <div class="folder-badges">
                <span v-if="folder.isDefault" class="badge default">Default</span>
              </div>
            </div>
            <div class="folder-path">
              <PhFolder />
              <code>{{ folder.path }}</code>
            </div>
          </div>
          <div class="folder-actions">
            <button class="icon-button" @click="edit(folder)" title="Edit" data-cy="edit-root-folder">
              <PhPencil />
            </button>
            <button class="icon-button danger" @click="confirmDelete(folder)" title="Delete" data-cy="delete-root-folder">
              <PhTrash />
            </button>
          </div>
        </div>
      </div>
    </div>

    <RootFolderFormModal v-if="showForm" :root="editing" @close="close" @saved="onSaved" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRootFoldersStore } from '@/stores/rootFolders'
import RootFolderFormModal from '@/components/settings/RootFolderFormModal.vue'
import { useToast } from '@/services/toastService'
import { PhPlus, PhFolder, PhPencil, PhTrash, PhSpinner, PhFolderOpen } from '@phosphor-icons/vue'

interface Props {
  hideHeader?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  hideHeader: false
})

const store = useRootFoldersStore()
const showForm = ref(false)
const editing = ref(null as any)
const toast = useToast()

onMounted(async () => {
  await store.load()
})

function openAdd() {
  editing.value = null
  showForm.value = true
}

function edit(r: any) {
  editing.value = { ...r }
  showForm.value = true
}

async function confirmDelete(r: any) {
  if (!confirm(`Delete root folder '${r.name}'? This will only remove the reference and not delete files on disk. If audiobooks reference it you must reassign them first.`)) return
  try {
    await store.remove(r.id)
    toast.success('Success', 'Root folder deleted')
  } catch (e: any) {
    toast.error('Error', e?.message || 'Failed to delete root folder')
  }
}

function close() { showForm.value = false }
function onSaved() { showForm.value = false; store.load().catch(() => {}) }

// Expose the openAdd method so parent components can call it
defineExpose({
  openAdd
})
</script>

<style scoped>
.root-folders-settings {
  max-width: 800px;
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
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.add-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.95rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
  transition: all 0.2s ease;
}

.add-button:hover {
  background: linear-gradient(135deg, #1565c0 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.loading-state {
  text-align: center;
  padding: 3rem;
  color: #adb5bd;
}

.loading-state p {
  margin: 1rem 0 0 0;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #868e96;
}

.empty-state h4 {
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

.folders-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.folder-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  transition: all 0.2s ease;
}

.folder-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.folder-card.is-default {
  border-color: rgba(77, 171, 247, 0.3);
  background: rgba(77, 171, 247, 0.05);
}

.folder-info {
  flex: 1;
  min-width: 0;
}

.folder-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.folder-header h4 {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  color: #fff;
}

.folder-badges {
  display: flex;
  gap: 0.5rem;
}

.badge {
  padding: 0.25rem 0.5rem;
  background: rgba(255, 255, 255, 0.1);
  color: #ccc;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.badge.default {
  background: #4dabf7;
  color: white;
}

.folder-path {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #ccc;
  font-size: 0.9rem;
}

.folder-path code {
  background: #1a1a1a;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
  word-break: break-all;
  color: #4dabf7;
}

.folder-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.icon-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border: none;
  background: transparent;
  color: #ccc;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.icon-button:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.icon-button.danger:hover {
  background: rgba(211, 47, 47, 0.1);
  color: #f44336;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn.primary {
  background-color: #007acc;
  color: white;
}

.btn.primary:hover {
  background-color: #005a9e;
  transform: translateY(-1px);
}
</style>