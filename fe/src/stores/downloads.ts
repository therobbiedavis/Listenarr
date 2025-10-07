import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Download, SearchResult } from '@/types'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'

export const useDownloadsStore = defineStore('downloads', () => {
  const downloads = ref<Download[]>([])
  const isLoading = ref(false)
  let unsubscribeUpdate: (() => void) | null = null
  let unsubscribeList: (() => void) | null = null
  
  // Subscribe to SignalR updates
  const initializeSignalR = () => {
    // Subscribe to individual download updates
    unsubscribeUpdate = signalRService.onDownloadUpdate((updatedDownloads) => {
      console.log('[Downloads Store] Received update for', updatedDownloads.length, 'downloads')
      
      updatedDownloads.forEach(updated => {
        const index = downloads.value.findIndex(d => d.id === updated.id)
        if (index !== -1) {
          // Update existing download
          downloads.value[index] = updated
        } else {
          // Add new download
          downloads.value.unshift(updated)
        }
      })
      
      // Sort by start date
      downloads.value.sort((a, b) => 
        new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
      )
    })
    
    // Subscribe to full downloads list
    unsubscribeList = signalRService.onDownloadsList((downloadsList) => {
      console.log('[Downloads Store] Received full list of', downloadsList.length, 'downloads')
      downloads.value = downloadsList
    })
  }
  
  // Initialize SignalR connection
  initializeSignalR()
  
  const activeDownloads = computed(() => {
    const active = downloads.value.filter(d => {
      const isActive = ['Queued', 'Downloading', 'Paused', 'Processing'].includes(d.status)
      if (isActive && d.downloadClientId === 'DDL') {
        console.log('[Downloads Store] 🎯 DDL download IS ACTIVE:', d.id, d.title, d.status, d.progress + '%')
      }
      return isActive
    })
    console.log('[Downloads Store] 📊 Active downloads computed:', active.length, 'of', downloads.value.length, 'total')
    if (active.length > 0) {
      console.log('[Downloads Store] 📋 Active downloads:', active.map(d => ({
        id: d.id,
        title: d.title,
        status: d.status,
        clientId: d.downloadClientId,
        progress: d.progress
      })))
    }
    return active
  })
  
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
      // No need to manually refresh - SignalR will push the update
      return downloadId
    } catch (error) {
      console.error('Failed to start download:', error)
      throw error
    }
  }
  
  const cancelDownload = async (downloadId: string) => {
    try {
      await apiService.cancelDownload(downloadId)
      // No need to manually refresh - SignalR will push the update
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
  
  // Cleanup on store destruction
  const cleanup = () => {
    if (unsubscribeUpdate) unsubscribeUpdate()
    if (unsubscribeList) unsubscribeList()
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
    updateDownload,
    cleanup
  }
})