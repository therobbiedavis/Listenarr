import { defineStore } from 'pinia'
import { ref, computed, shallowRef, triggerRef } from 'vue'
import type { Download, SearchResult, Audiobook } from '@/types'
import { apiService } from '@/services/api'
import { useLibraryStore } from '@/stores/library'
import { signalRService } from '@/services/signalr'
import { errorTracking } from '@/services/errorTracking'
import { logger } from '@/utils/logger'

export const useDownloadsStore = defineStore('downloads', () => {
  const downloads = shallowRef<Download[]>([])
  const isLoading = ref(false)
  let unsubscribeUpdate: (() => void) | null = null
  let unsubscribeList: (() => void) | null = null
  let unsubscribeQueue: (() => void) | null = null
  let unsubscribeAudiobook: (() => void) | null = null

  // Subscribe to SignalR updates
  const initializeSignalR = () => {
    // Subscribe to individual download updates
    unsubscribeUpdate = signalRService.onDownloadUpdate(async (updatedDownloads) => {
      updatedDownloads.forEach((updated) => {
        const index = downloads.value.findIndex((d) => d.id === updated.id)
        if (index !== -1) {
          // Update existing download
          downloads.value[index] = updated
        } else {
          // Add new download
          downloads.value.unshift(updated)
        }
      })
      triggerRef(downloads)

      // If any updated downloads are completed and linked to an audiobook, refresh that audiobook in the library store
      // Refresh linked audiobooks so UI shows newly-created files. Use Promise.all to avoid unbounded concurrency.
      const libraryStore = useLibraryStore()
      const tasks: Promise<void>[] = []
      for (const updated of updatedDownloads) {
        const status = (updated.status || '').toString().toLowerCase()
        if (
          (status === 'completed' || status === 'ready') &&
          typeof updated.audiobookId === 'number'
        ) {
          const aid = updated.audiobookId as number
          tasks.push(
            (async () => {
              try {
                const latest = await apiService.getAudiobook(aid)
                // Find existing index
                const idx = libraryStore.audiobooks.findIndex((b) => b.id === latest.id)
                if (idx !== -1) {
                  // Preserve the original object reference so components bound to it update reactively
                  const target = libraryStore.audiobooks[idx]!
                  Object.assign(target, latest)
                } else {
                  libraryStore.audiobooks.unshift(latest)
                }
              } catch (e) {
                logger.warn(
                  '[Downloads Store] Failed to refresh audiobook after download update',
                  e,
                )
              }
            })(),
          )
        }
      }
      if (tasks.length > 0) await Promise.all(tasks)

      // Sort by start date
      downloads.value.sort(
        (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
      )
      triggerRef(downloads)
    })

    // Subscribe to full downloads list
    unsubscribeList = signalRService.onDownloadsList((downloadsList) => {
      downloads.value = downloadsList
      triggerRef(downloads)
    })

    // Subscribe to queue updates (replacement list from backend)
    unsubscribeQueue = signalRService.onQueueUpdate((queueItems) => {
      // QueueUpdate provides the current queue state
      // When a download is completed and removed, it won't be in this list
      // We need to update our downloads to match the queue
      const currentDownloadIds = new Set(downloads.value.map(d => d.id))
      const queueIds = new Set(queueItems.map(q => q.id))
      
      // Remove downloads that are no longer in the queue
      downloads.value = downloads.value.filter(d => queueIds.has(d.id))
      
      // Update existing and add new items from queue
      queueItems.forEach((queueItem) => {
        const existingIndex = downloads.value.findIndex(d => d.id === queueItem.id)
        
        // Map QueueItem to Download format
        const downloadItem: Download = {
          id: queueItem.id,
          title: queueItem.title || 'Unknown',
          artist: queueItem.author || '',
          album: queueItem.series || '',
          originalUrl: '',
          status: queueItem.status as any,
          progress: queueItem.progress || 0,
          totalSize: queueItem.size || 0,
          downloadedSize: queueItem.downloaded || 0,
          downloadPath: queueItem.remotePath || '',
          finalPath: queueItem.localPath || '',
          startedAt: queueItem.addedAt,
          completedAt: undefined,
          errorMessage: queueItem.errorMessage,
          downloadClientId: queueItem.downloadClientId,
          metadata: {},
        }
        
        if (existingIndex !== -1) {
          downloads.value[existingIndex] = downloadItem
        } else {
          downloads.value.push(downloadItem)
        }
      })
      
      triggerRef(downloads)
    })

    // Subscribe to AudiobookUpdate messages so we can apply updated audiobook (with Files)
    unsubscribeAudiobook = signalRService.onAudiobookUpdate((updatedAudiobook) => {
      try {
        const libraryStore = useLibraryStore()
        const idx = libraryStore.audiobooks.findIndex((b) => b.id === updatedAudiobook.id)
        if (idx !== -1) {
          const target = libraryStore.audiobooks[idx] as Audiobook
          Object.assign(target, updatedAudiobook)
        } else {
          libraryStore.audiobooks.unshift(updatedAudiobook)
        }
      } catch (e) {
        logger.warn('[Downloads Store] Failed to apply AudiobookUpdate', e)
      }
    })
    // audiobook unsubscribe will be cleaned up in the common cleanup below
  }

  // Initialize SignalR connection
  initializeSignalR()

  const activeDownloads = computed(() => {
    const active = downloads.value.filter((d) => {
      const status = (d.status || '').toString().toLowerCase()
      const isActive = ['queued', 'downloading', 'paused', 'processing'].includes(status)
      return isActive
    })
    return active
  })

  const completedDownloads = computed(() =>
    downloads.value.filter((d) => {
      const status = (d.status || '').toString().toLowerCase()
      return status === 'completed' || status === 'ready'
    }),
  )

  const failedDownloads = computed(() =>
    downloads.value.filter((d) => (d.status || '').toString().toLowerCase() === 'failed'),
  )

  const loadDownloads = async () => {
    isLoading.value = true
    try {
      const downloadList = await apiService.getDownloads()
      downloads.value = downloadList
      triggerRef(downloads)
    } catch (error) {
      errorTracking.captureException(error as Error, {
        component: 'DownloadsStore',
        operation: 'loadDownloads',
      })
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
      errorTracking.captureException(error as Error, {
        component: 'DownloadsStore',
        operation: 'startDownload',
        metadata: { title: searchResult.title, downloadClientId },
      })
      throw error
    }
  }

  const cancelDownload = async (downloadId: string) => {
    try {
      await apiService.cancelDownload(downloadId)
      // No need to manually refresh - SignalR will push the update
    } catch (error) {
      errorTracking.captureException(error as Error, {
        component: 'DownloadsStore',
        operation: 'cancelDownload',
        metadata: { downloadId },
      })
      throw error
    }
  }

  const updateDownload = (updatedDownload: Download) => {
    const index = downloads.value.findIndex((d) => d.id === updatedDownload.id)
    if (index !== -1) {
      downloads.value[index] = updatedDownload
    }
  }

  // Cleanup on store destruction
  const cleanup = () => {
    if (unsubscribeUpdate) unsubscribeUpdate()
    if (unsubscribeList) unsubscribeList()
    if (unsubscribeQueue) unsubscribeQueue()
    if (unsubscribeAudiobook) unsubscribeAudiobook()
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
    cleanup,
  }
})
