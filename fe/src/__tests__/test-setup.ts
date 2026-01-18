/* eslint-disable @typescript-eslint/no-explicit-any */
// Test setup: Polyfill / mock environment pieces that tests expect
// - Provide a Mock WebSocket implementation so SignalR code can run in jsdom

class MockWebSocket {
  static OPEN = 1
  public readyState = MockWebSocket.OPEN
  public onopen: (() => void) | null = null
  public onmessage: ((ev: { data: string }) => void) | null = null
  public onerror: ((err: Error) => void) | null = null
  public onclose: (() => void) | null = null
  private url: string
  constructor(url: string) {
    this.url = url
    // simulate async open
    setTimeout(() => {
      if (this.onopen) this.onopen()
    }, 0)
  }
  send(_data: string) {
    // Reference the arg so linters don't complain about unused params in tests
    void _data
    /* no-op in tests */
  }
  close() {
    if (this.onclose) this.onclose()
  }
}

// Centralized apiService and signalR mocks used by unit tests.
import { vi } from 'vitest'

vi.mock('@/services/api', () => ({
  apiService: {
    searchAudimetaByTitleAndAuthor: vi.fn(async () => ({ totalResults: 0, results: [] })),
    advancedSearch: async (params: unknown) => {
      const p = params as { title?: string; author?: string } | undefined
      if (p?.title) {
        const mod = await import('@/services/api')
        const svc = mod.apiService as unknown as {
          searchAudimetaByTitleAndAuthor?: (
            title: string,
            author?: string,
          ) => Promise<{ totalResults?: number; results?: unknown[] } | unknown>
        }
        if (svc.searchAudimetaByTitleAndAuthor) {
          const resp = (await svc.searchAudimetaByTitleAndAuthor(p.title, p.author)) as unknown
          const r = resp as any
          return (r?.results) || r || []
        }
        return []
      }
      return { totalResults: 0, results: [] }
    },
    getImageUrl: vi.fn((url: string) => url || ''),
    getStartupConfig: vi.fn(async () => ({})),
    getApplicationSettings: vi.fn(async () => ({})),
    getLibrary: vi.fn(async () => []),
    previewLibraryPath: vi.fn(async () => ({ path: '' })),
    getQualityProfiles: vi.fn(async () => []),
    getApiConfigurations: vi.fn(async () => []),
  },
  // Provide top-level named exports used by components (wrappers over apiService)
  getRemotePathMappings: vi.fn(async () => []),
  getRemotePathMappingsByClient: vi.fn(async (downloadClientId: string) => []),
}))

vi.mock('@/services/signalr', () => ({
  signalRService: {
    connect: () => {},
    onDownloadsList: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onSearchProgress: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onQueueUpdate: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onDownloadUpdate: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onFilesRemoved: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onAudiobookUpdate: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onNotification: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
    onToast: (cb?: (...args: unknown[]) => void) => {
      void cb
      return () => {}
    },
  },
}))

// Ensure global WebSocket exists for code that references it
if (typeof (globalThis as unknown as { WebSocket?: unknown }).WebSocket === 'undefined') {
  ;(globalThis as unknown as { WebSocket?: unknown }).WebSocket = MockWebSocket
}

// Also provide a minimal window.WebSocket for code referencing window
if (typeof (window as unknown as { WebSocket?: unknown }).WebSocket === 'undefined') {
  ;(window as unknown as { WebSocket?: unknown }).WebSocket = MockWebSocket
}

// Provide a noop for console.debug in tests where code wraps in try/catch
if (typeof console.debug !== 'function') console.debug = console.log.bind(console)

// Provide a simple localStorage polyfill for tests that rely on it
// Ensure a working localStorage implementation exists for tests. Some test
// runners may set a placeholder object; normalize it so .setItem/.getItem exist.
if (
  typeof (globalThis as unknown as { localStorage?: { setItem?: unknown } }).localStorage ===
    'undefined' ||
  typeof (globalThis as unknown as { localStorage?: { setItem?: unknown } }).localStorage
    ?.setItem !== 'function'
) {
  ;(
    globalThis as unknown as {
      localStorage?: {
        _store?: Record<string, string>
        getItem?: (k: string) => string | null
        setItem?: (k: string, v: string) => void
        removeItem?: (k: string) => void
        clear?: () => void
      }
    }
  ).localStorage = {
    _store: {} as Record<string, string>,
    getItem(key: string) {
      return this._store[key] ?? null
    },
    setItem(key: string, value: string) {
      this._store[key] = value + ''
    },
    removeItem(key: string) {
      delete this._store[key]
    },
    clear() {
      this._store = {}
    },
  }
}
