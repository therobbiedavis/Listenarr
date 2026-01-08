<template>
  <div class="root-folder-select">
    <select v-model="localId" class="form-select">
      <option :value="null">(Use default)</option>
      <option v-for="r in store.folders" :key="r.id" :value="r.id">
        {{ r.name }} — {{ r.path }}
      </option>
      <option :value="0">Custom path...</option>
    </select>
    <div v-if="localId === 0" class="custom-path">
      <FolderBrowser v-model="localCustomPath" placeholder="Enter or browse a custom path..." />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRootFoldersStore } from '@/stores/rootFolders'
import FolderBrowser from '@/components/FolderBrowser.vue'

const props = defineProps<{ rootId?: number | null; customPath?: string | null }>()
const emit = defineEmits<{
  'update:rootId': [id: number | null]
  'update:customPath': [path: string]
}>()

const store = useRootFoldersStore()
const localId = ref<number | null>(props.rootId ?? null)
const localCustomPath = ref(props.customPath || '')

onMounted(async () => {
  await store.load()
})

watch(localId, (v) => emit('update:rootId', v))
watch(localCustomPath, (v) => emit('update:customPath', v))
</script>

<style scoped>
.root-folder-select {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.custom-path {
  margin-top: 0.5rem;
}

.form-select {
  padding: 0.75rem 1rem;
  background-color: #2a2a2a;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  color: white;
  font-size: 0.95rem;
  cursor: pointer;
  transition: all 0.2s;
}

.form-select:hover {
  border-color: #555;
}

.form-select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}
</style>
