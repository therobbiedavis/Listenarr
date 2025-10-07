import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiService } from '@/services/api'
import type { Audiobook } from '@/types'

export const useLibraryStore = defineStore('library', () => {
  const audiobooks = ref<Audiobook[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const selectedIds = ref<Set<number>>(new Set())

  async function fetchLibrary() {
    loading.value = true
    error.value = null
    try {
      audiobooks.value = await apiService.getLibrary()
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
      // Clear selection
      clearSelection()
      return { success: true, deletedCount: result.deletedCount }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to bulk remove audiobooks'
      console.error('Failed to bulk remove audiobooks:', err)
      return { success: false, deletedCount: 0 }
    }
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
