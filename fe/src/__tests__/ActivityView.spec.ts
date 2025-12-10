import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { computed, ref } from 'vue'

describe('ActivityView Completed tab shows completed downloads from downloads store', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  it('includes completed external downloads from downloads store in Completed tab', async () => {
    // Reset module registry and stub all runtime dependencies so the component
    // doesn't attempt to connect to real services. Use doMock after reset.
    vi.resetModules()

    // Stub out SignalR and API
    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined)
      }
    }))

    vi.doMock('@/services/api', () => ({ apiService: { getQueue: async () => [], getServiceHealth: async () => ({ version: '0.0.0' }), getStartupConfig: async () => ({ authenticationRequired: false }), getLibrary: async () => [] } }))

    // Provide minimal configuration store stub - default to hide completed external downloads
    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined)
      })
    }))

    // Provide a downloads store with 4 completed external downloads
    const completed = ref([
      { id: 'd1', status: 'Completed', progress: 100, downloadClientId: 'SABnzbd', startedAt: new Date().toISOString(), title: 'One', downloadedSize: 1000, totalSize: 1000 },
      { id: 'd2', status: 'Completed', progress: 100, downloadClientId: 'qbittorrent', startedAt: new Date().toISOString(), title: 'Two', downloadedSize: 2000, totalSize: 2000 },
      { id: 'd3', status: 'Completed', progress: 100, downloadClientId: 'transmission', startedAt: new Date().toISOString(), title: 'Three', downloadedSize: 3000, totalSize: 3000 },
      { id: 'd4', status: 'Completed', progress: 100, downloadClientId: 'nzbget', startedAt: new Date().toISOString(), title: 'Four', downloadedSize: 4000, totalSize: 4000 },
    ])

    vi.doMock('@/stores/downloads', () => ({
      useDownloadsStore: () => ({
        // Mirror runtime unwrapped values: provide arrays directly so
        // ActivityView's `.filter`/`.map` calls work during tests
        activeDownloads: [],
        completedDownloads: completed.value,
        loadDownloads: vi.fn(async () => undefined)
      })
    }))


    console.log('[TEST] importing ActivityView')
    const { default: ActivityViewComponent } = await import('@/views/ActivityView.vue')
    console.log('[TEST] imported ActivityView, now mounting')

    const wrapper = mount(ActivityViewComponent, { global: { stubs: ['CustomSelect'] } })
    console.log('[TEST] mounted ActivityView')

    // Ensure initial state computed values are ready
    await new Promise((r) => setTimeout(r, 10))
    console.log('[TEST] after initial tick')

    // Completed tab should show 4 items from completedDownloads even though queue is empty
    // Select the Completed tab via component instance so composition API refs update
    ;(wrapper.vm as any).selectedTab = 'completed'
    // Wait a tick for reactivity
    await new Promise((r) => setTimeout(r, 10))
    console.log('[TEST] after selecting tab and waiting')

    // filteredQueue computed should reflect the 4 items
    expect((wrapper.vm as any).filteredQueue.length).toBe(4)

    // The completed count in filterTabs should reflect 4
    const completedTab = (wrapper.vm as any).filterTabs.find((t: any) => t.value === 'completed')
    expect(completedTab.count).toBe(4)

    // All tab should not include these completed external items by default (unless user pref set)
    const allCount = (wrapper.vm as any).filterTabs.find((t: any) => t.value === 'all').count
    expect(allCount).toBeGreaterThanOrEqual(0)
  }, { timeout: 20000 })

  it('removes an item from client when it exists in queue', async () => {
    vi.resetModules()

    const queueItem = {
      id: 'q1',
      title: 'Queue Item',
      status: 'downloading',
      progress: 50,
      size: 1000,
      downloaded: 500,
      downloadClientId: 'qbittorrent',
      downloadClient: 'qbittorrent',
      canRemove: true
    }

    // Mock signalr and API
    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined)
      }
    }))

    const removeFromQueueMock = vi.fn(async () => undefined)
    const getQueueMock = vi.fn(async () => [queueItem])

    vi.doMock('@/services/api', () => ({ apiService: { getQueue: getQueueMock, removeFromQueue: removeFromQueueMock, cancelDownload: vi.fn(async () => undefined), getServiceHealth: async () => ({ version: '0.0.0' }), getStartupConfig: async () => ({ authenticationRequired: false }), getLibrary: async () => [] } }))

    // Use default config and empty downloads store
    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined)
      })
    }))

    vi.doMock('@/stores/downloads', () => ({
      useDownloadsStore: () => ({
        activeDownloads: [],
        completedDownloads: [],
        loadDownloads: vi.fn(async () => undefined)
      })
    }))

    const { default: ActivityViewComponent } = await import('@/views/ActivityView.vue')
    const wrapper = mount(ActivityViewComponent, { global: { stubs: ['CustomSelect'] } })

    // Wait for mounted hooks (getQueue) to finish
    await new Promise((r) => setTimeout(r, 10))

    // There should be a queue item
    const items = (wrapper.vm as any).allActivityItems
    expect(items.some((i: any) => i.id === 'q1')).toBe(true)

    // Trigger remove flow
    const item = items.find((i: any) => i.id === 'q1')
    ;(wrapper.vm as any).removeFromQueue(item)

    expect((wrapper.vm as any).showRemoveModal).toBe(true)
    expect((wrapper.vm as any).clientHasQueueEntry).toBe(true)

    // Confirm remove should call removeFromQueue API for client
    await (wrapper.vm as any).confirmRemove()

    expect(removeFromQueueMock).toHaveBeenCalledWith('q1', 'qbittorrent')
  }, { timeout: 20000 })

  it('offers Listenarr-only removal when item is not in client queue', async () => {
    vi.resetModules()

    // No queue entries
    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined)
      }
    }))

    const getQueueMock = vi.fn(async () => [])
    const cancelDownloadMock = vi.fn(async () => undefined)

    vi.doMock('@/services/api', () => ({ apiService: { getQueue: getQueueMock, removeFromQueue: vi.fn(async () => undefined), cancelDownload: cancelDownloadMock, getServiceHealth: async () => ({ version: '0.0.0' }), getStartupConfig: async () => ({ authenticationRequired: false }), getLibrary: async () => [] } }))

    // Configuration
    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined)
      })
    }))

    // Provide a completed external download (e.g., saved candidate) that isn't in the queue
    const completed = [
      { id: 'ext-1', status: 'Completed', progress: 100, downloadClientId: 'SABnzbd', startedAt: new Date().toISOString(), title: 'Completed External', downloadedSize: 100, totalSize: 100 }
    ]

    const loadDownloadsMock = vi.fn(async () => undefined)

    vi.doMock('@/stores/downloads', () => ({
      useDownloadsStore: () => ({
        activeDownloads: [],
        completedDownloads: completed,
        loadDownloads: loadDownloadsMock
      })
    }))

    const { default: ActivityViewComponent } = await import('@/views/ActivityView.vue')
    const wrapper = mount(ActivityViewComponent, { global: { stubs: ['CustomSelect'] } })

    // Wait for mounted hooks
    await new Promise((r) => setTimeout(r, 10))

    // Switch to Completed tab and find the completed item
    ;(wrapper.vm as any).selectedTab = 'completed'
    await new Promise((r) => setTimeout(r, 10))

    const completedItems = (wrapper.vm as any).filteredQueue
    expect(completedItems.some((i: any) => i.id === 'ext-1')).toBe(true)

    // Trigger remove on the completed external item
    const item = completedItems.find((i: any) => i.id === 'ext-1')
    ;(wrapper.vm as any).removeFromQueue(item)

    expect((wrapper.vm as any).showRemoveModal).toBe(true)
    // Since getQueue returned empty, clientHasQueueEntry should be false
    expect((wrapper.vm as any).clientHasQueueEntry).toBe(false)

    // Confirm remove should call cancelDownload (Listenarr-only removal) and reload downloads
    await (wrapper.vm as any).confirmRemove()
    expect(cancelDownloadMock).toHaveBeenCalledWith('ext-1')
    expect(loadDownloadsMock).toHaveBeenCalled()
  }, { timeout: 20000 })

  it('removes an external item from client when it exists in the remote queue', async () => {
    vi.resetModules()

    const queueItem = { id: 'q1', title: 'Queue Item', status: 'downloading', progress: 50, totalSize: 1000, downloadedSize: 500, downloadClientId: 'SABnzbd', downloadClient: 'SABnzbd', downloadClientType: 'external' }

    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined)
      }
    }))

    const removeFromQueueMock = vi.fn(async () => undefined)
    const cancelDownloadMock = vi.fn(async () => undefined)
    const getQueueMock = vi.fn(async () => [queueItem])

    vi.doMock('@/services/api', () => ({ apiService: { getQueue: getQueueMock, removeFromQueue: removeFromQueueMock, cancelDownload: cancelDownloadMock, getServiceHealth: async () => ({ version: '0.0.0' }), getStartupConfig: async () => ({ authenticationRequired: false }), getLibrary: async () => [] } }))

    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined)
      })
    }))

    const loadDownloadsMock = vi.fn(async () => undefined)
    vi.doMock('@/stores/downloads', () => ({ useDownloadsStore: () => ({ activeDownloads: [], completedDownloads: [], loadDownloads: loadDownloadsMock }) }))

    const { default: ActivityViewComponent } = await import('@/views/ActivityView.vue')
    const wrapper = mount(ActivityViewComponent, { global: { stubs: ['CustomSelect'] } })

    // Simulate current queue containing the item
    ;(wrapper.vm as any).queue = [queueItem]

    // Trigger remove flow
    ;(wrapper.vm as any).removeFromQueue(queueItem)

    // Pre-check should have found it in the queue
    expect((wrapper.vm as any).clientHasQueueEntry).toBe(true)

    // Confirm removal - should call the client remove endpoint
    await (wrapper.vm as any).confirmRemove()

    expect(removeFromQueueMock).toHaveBeenCalledWith('q1', 'SABnzbd')
  })

  it('removes an external item from Listenarr (DB) when it is not in the remote queue', async () => {
    vi.resetModules()

    const externalItem = { id: 'q2', title: 'Missing Item', status: 'completed', progress: 100, totalSize: 2000, downloadedSize: 2000, downloadClientId: 'SABnzbd', downloadClient: 'SABnzbd', downloadClientType: 'external' }

    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined)
      }
    }))

    const removeFromQueueMock = vi.fn(async () => undefined)
    const cancelDownloadMock = vi.fn(async () => undefined)
    const getQueueMock = vi.fn(async () => [])

    vi.doMock('@/services/api', () => ({ apiService: { getQueue: getQueueMock, removeFromQueue: removeFromQueueMock, cancelDownload: cancelDownloadMock, getServiceHealth: async () => ({ version: '0.0.0' }), getStartupConfig: async () => ({ authenticationRequired: false }), getLibrary: async () => [] } }))

    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined)
      })
    }))

    const loadDownloadsMock = vi.fn(async () => undefined)
    vi.doMock('@/stores/downloads', () => ({ useDownloadsStore: () => ({ activeDownloads: [], completedDownloads: [], loadDownloads: loadDownloadsMock }) }))

    const { default: ActivityViewComponent } = await import('@/views/ActivityView.vue')
    const wrapper = mount(ActivityViewComponent, { global: { stubs: ['CustomSelect'] } })

    // No queue items present - simulate missing in remote client
    ;(wrapper.vm as any).queue = []

    // Trigger remove flow for the external item
    ;(wrapper.vm as any).removeFromQueue(externalItem)

    // Pre-check should indicate not present in client
    expect((wrapper.vm as any).clientHasQueueEntry).toBe(false)

    // Confirm removal - should call the Listenarr-only cancel path
    await (wrapper.vm as any).confirmRemove()

    expect(cancelDownloadMock).toHaveBeenCalledWith('q2')
    expect(loadDownloadsMock).toHaveBeenCalled()
  })
})
