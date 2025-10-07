import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Download, SearchResult } from '@/types'
import { apiService } from '@/services/api'

export const useDownloadsStore = defineStore('downloads', () => {
  const downloads = ref<Download[]>([])
  const isLoading = ref(false)
  
  const activeDownloads = computed(() => 
    downloads.value.filter(d => ['Queued', 'Downloading', 'Processing'].includes(d.status))
  )
  
  const completedDownloads = computed(() => 
    downloads.value.filter(d => ['Completed', 'Ready'].includes(d.status))
  )
  
  const failedDownloads = computed(() => 
    downloads.value.filter(d => d.status === 'Failed')
  )
  
  const loadDownloads = async () => {
    isLoading.value = true
    try {
      const downloadList = await apiService.getDownloads()
      downloads.value = downloadList
    } catch (error) {
      console.error('Failed to load downloads:', error)
    } finally {
      isLoading.value = false
    }
  }
  
  const startDownload = async (searchResult: SearchResult, downloadClientId: string) => {
    try {
      const downloadId = await apiService.startDownload(searchResult, downloadClientId)
      await loadDownloads() // Refresh the list
      return downloadId
    } catch (error) {
      console.error('Failed to start download:', error)
      throw error
    }
  }
  
  const cancelDownload = async (downloadId: string) => {
    try {
      await apiService.cancelDownload(downloadId)
      await loadDownloads() // Refresh the list
    } catch (error) {
      console.error('Failed to cancel download:', error)
      throw error
    }
  }
  
  const updateDownload = (updatedDownload: Download) => {
    const index = downloads.value.findIndex(d => d.id === updatedDownload.id)
    if (index !== -1) {
      downloads.value[index] = updatedDownload
    }
  }
  
  return {
    downloads,
    isLoading,
    activeDownloads,
    completedDownloads,
    failedDownloads,
    loadDownloads,
    startDownload,
    cancelDownload,
    updateDownload
  }
})