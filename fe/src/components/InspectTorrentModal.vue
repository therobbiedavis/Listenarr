<template>
  <div class="modal-overlay" @click.self="close">
    <div class="modal">
      <header>
        <h3>Inspect Cached Torrent</h3>
        <button class="close" @click="close">✕</button>
      </header>

      <div class="modal-body">
        <div v-if="loading">Loading…</div>
        <div v-else>
          <div v-if="announces && announces.length">
            <h4>Announces</h4>
            <ul>
              <li v-for="a in announces" :key="a">{{ a }}</li>
            </ul>
          </div>
          <div v-else>
            <p>No cached announces available.</p>
          </div>
        </div>
      </div>

      <footer>
        <button @click="downloadTorrent" :disabled="loading || !hasTorrent">
          Download Torrent
        </button>
        <button @click="close">Close</button>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { apiService } from '@/services/api'
import { logger } from '@/utils/logger'

const props = defineProps<{
  downloadId: string
  initialAnnounces?: string[] | null
}>()

const emits = defineEmits(['close'])

const loading = ref(false)
const announces = ref<string[] | null>(props.initialAnnounces ?? null)
let cachedTorrent: { blob: Blob; filename?: string } | null = null

watch(
  () => props.downloadId,
  async (id) => {
    if (!id) return
    loading.value = true
    try {
      const r = await apiService.getCachedAnnounces(id)
      announces.value = r?.announces ?? null

      // Pre-fetch torrent blob so download is instant
      cachedTorrent = await apiService.getCachedTorrent(id)
    } catch (e) {
      logger.warn('Failed to fetch cached torrent/announces', e)
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)

function close() {
  emits('close')
}

function hasTorrent() {
  return !!cachedTorrent?.blob
}

function downloadTorrent() {
  if (!cachedTorrent) return
  const url = URL.createObjectURL(cachedTorrent.blob)
  const a = document.createElement('a')
  a.href = url
  a.download = cachedTorrent.filename ?? `download-${props.downloadId}.torrent`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.4);
  z-index: 1000;
}
.modal {
  background: white;
  width: 560px;
  border-radius: 8px;
  padding: 1rem;
}
header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.modal-body {
  min-height: 120px;
}
footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
.close {
  background: none;
  border: none;
  font-size: 1.2rem;
}
</style>
