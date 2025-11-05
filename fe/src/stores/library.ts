import { defineStore } from 'pinia'
import { ref, shallowRef, triggerRef } from 'vue'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import type { Audiobook } from '@/types'

export const useLibraryStore = defineStore('library', () => {
  const audiobooks = shallowRef<Audiobook[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const selectedIds = ref<Set<number>>(new Set())

  async function fetchLibrary() {
    loading.value = true
    error.value = null
    try {
      const serverList = await apiService.getLibrary()
      // Defensive merge: prefer server-provided fields, but avoid wiping local files when server returns empty array
      const merged = serverList.map(serverItem => {
        const local = audiobooks.value.find(b => b.id === serverItem.id)
        if (!local) return serverItem

        // If server provided files array is empty but local has files, keep local files
        const files = (serverItem.files && serverItem.files.length > 0)
          ? serverItem.files
          : (local.files && local.files.length > 0)
            ? local.files
            : serverItem.files

        // Preserve a meaningful basePath: prefer server value when present, otherwise keep local
        const basePath = (serverItem.basePath && serverItem.basePath.length > 0)
          ? serverItem.basePath
          : (local.basePath && local.basePath.length > 0)
            ? local.basePath
            : serverItem.basePath

        return { ...local, ...serverItem, files, basePath }
      })

      audiobooks.value = merged
      triggerRef(audiobooks)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch library'
      console.error('Failed to fetch library:', err)
    } finally {
      loading.value = false
    }
  }

  async function removeFromLibrary(id: number) {
    try {
      await apiService.removeFromLibrary(id)
      // Remove from local state
      audiobooks.value = audiobooks.value.filter(book => book.id !== id)
      triggerRef(audiobooks)
      // Remove from selection if selected
      selectedIds.value.delete(id)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to remove audiobook'
      console.error('Failed to remove audiobook:', err)
      return false
    }
  }

  async function bulkRemoveFromLibrary(ids: number[]) {
    if (ids.length === 0) return { success: false, deletedCount: 0 }

    try {
      const result = await apiService.bulkRemoveFromLibrary(ids)
      // Remove from local state
      audiobooks.value = audiobooks.value.filter(book => !ids.includes(book.id))
      triggerRef(audiobooks)
      // Clear selection
      clearSelection()
      return { success: true, deletedCount: result.deletedCount }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to bulk remove audiobooks'
      console.error('Failed to bulk remove audiobooks:', err)
      return { success: false, deletedCount: 0 }
    }
  }

  // Apply a safe, local-only update when the server tells us files were removed for an audiobook
  // Payload shape: { audiobookId: number, removed: Array<{ id: number, path: string }> }
  function applyFilesRemoved(payload: { audiobookId: number; removed?: Array<{ id?: number; path?: string }> } | null | undefined) {
    if (!payload || typeof payload.audiobookId !== 'number') return

    const bookIndex = audiobooks.value.findIndex(b => b.id === payload.audiobookId)
    if (bookIndex === -1) return // we don't have this audiobook loaded locally

    const book = audiobooks.value[bookIndex]
    if (!book) return

    const removed = Array.isArray(payload.removed) ? payload.removed : []
    if (removed.length === 0) return

    // Build new files array excluding removed entries (match by id when present, otherwise by path)
    const newFiles = (book.files || []).filter(f => {
      // If any removed entry matches this file, exclude it
      for (const r of removed) {
        if (typeof r.id === 'number' && typeof f.id === 'number' && r.id === f.id) return false
        if (r.path && f.path && r.path === f.path) return false
      }
      return true
    })

    // Clone the audiobook object and update files safely so reactivity notices the change
    const updated: Audiobook = { ...book, files: newFiles }

    // If the current primary filePath was one of the removed paths, clear it (safe behavior)
    if (book.filePath) {
      const removedPaths = removed.map(r => r.path).filter(Boolean) as string[]
      if (removedPaths.includes(book.filePath)) {
        updated.filePath = undefined
        updated.fileSize = undefined
      }
    }

    // Replace the item in the array immutably to ensure watchers pick up the change
    audiobooks.value = audiobooks.value.slice()
    audiobooks.value[bookIndex] = updated
    triggerRef(audiobooks)
  }

  // Register a SignalR subscription once when the store is created so we can keep local state in sync
  // We intentionally do not unsubscribe because the store's lifetime matches the app lifetime.
  try {
    signalRService.onFilesRemoved((payload) => {
      try {
        applyFilesRemoved(payload as { audiobookId: number; removed?: Array<{ id?: number; path?: string }> })
      } catch (e) {
        // Defensive: don't allow signal handler errors to break the app
        console.error('Error applying FilesRemoved to library store', e)
      }
    })

    signalRService.onAudiobookUpdate((updatedAudiobook) => {
      try {
        const index = audiobooks.value.findIndex(b => b.id === updatedAudiobook.id)
        if (index !== -1) {
          // Update the audiobook in the store, preserving reactivity
          audiobooks.value = audiobooks.value.slice()
          const prev = audiobooks.value[index]
          if (!prev) return
          const merged = { ...prev, ...updatedAudiobook }
          // Preserve basePath if server payload omits or clears it
          if ((!('basePath' in updatedAudiobook) || !updatedAudiobook.basePath) && prev.basePath) {
            merged.basePath = prev.basePath
          }
          audiobooks.value[index] = merged
          triggerRef(audiobooks)
        }
      } catch (e) {
        // Defensive: don't allow signal handler errors to break the app
        console.error('Error applying AudiobookUpdate to library store', e)
      }
    })
  } catch {
    // If signalRService isn't ready at module import time, this will be a no-op; we'll still sync on next fetchLibrary
  }

  function toggleSelection(id: number) {
    if (selectedIds.value.has(id)) {
      selectedIds.value.delete(id)
    } else {
      selectedIds.value.add(id)
    }
  }

  function selectAll() {
    audiobooks.value.forEach(book => selectedIds.value.add(book.id))
  }

  function clearSelection() {
    selectedIds.value.clear()
  }

  function isSelected(id: number): boolean {
    return selectedIds.value.has(id)
  }

  return {
    audiobooks,
    loading,
    error,
    selectedIds,
    fetchLibrary,
    removeFromLibrary,
    bulkRemoveFromLibrary,
    toggleSelection,
    selectAll,
    clearSelection,
    isSelected
  }
})
