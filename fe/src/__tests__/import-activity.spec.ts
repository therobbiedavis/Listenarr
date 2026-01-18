import { describe, it, expect, vi } from 'vitest'

describe('import checks', () => {
  it('can import ActivityView.vue without throwing (with mocked runtime deps)', async () => {
    // Before importing, parse the SFC to detect template/script syntax errors
    try {
      const fs = await import('fs')
      const path = await import('path')
      const compiler = await import('@vue/compiler-sfc')
      const filePath = path.resolve(__dirname, '../views/ActivityView.vue')
      const content = fs.readFileSync(filePath, 'utf-8')
      const parsed = compiler.parse(content)
      // If parse returns a descriptor, try to compile template (if present) to catch template errors
      const desc = parsed.descriptor
      if (desc.template && desc.template.content) {
        try {
          compiler.compileTemplate({ source: desc.template.content, filename: 'ActivityView.vue', id: 'ActivityView' })
        } catch (tplErr) {
          console.error('[TEST] template compile error for ActivityView.vue', tplErr)
          throw tplErr
        }
      }
    } catch (e) {
      console.error('[TEST] SFC parse/compile failed:', e)
      throw e
    }

    // Mock signalR and API to prevent real network/WS during module import
    vi.doMock('@/services/signalr', () => ({
      signalRService: {
        connect: vi.fn(async () => undefined),
        onQueueUpdate: vi.fn(() => () => undefined),
        onFilesRemoved: vi.fn(() => () => undefined),
        onToast: vi.fn(() => () => undefined),
        onAudiobookUpdate: vi.fn(() => () => undefined),
        onDownloadUpdate: vi.fn(() => () => undefined),
        onDownloadsList: vi.fn(() => () => undefined),
      },
    }))

    vi.doMock('@/services/api', () => ({
      apiService: {
        getQueue: async () => [],
        getServiceHealth: async () => ({ version: '0.0.0' }),
        getStartupConfig: async () => ({ authenticationRequired: false }),
        getLibrary: async () => [],
      },
    }))

    vi.doMock('@/stores/configuration', () => ({
      useConfigurationStore: () => ({
        applicationSettings: { showCompletedExternalDownloads: false },
        loadApplicationSettings: vi.fn(async () => undefined),
      }),
    }))

    vi.doMock('@/stores/downloads', () => ({
      useDownloadsStore: () => ({
        activeDownloads: [],
        completedDownloads: [],
        loadDownloads: vi.fn(async () => undefined),
      }),
    }))

    // Reset module registry so our doMock calls take effect during import
    vi.resetModules()

    const mod = await import('@/views/ActivityView.vue')
    expect(mod).toBeTruthy()
  }, 20000)
})
