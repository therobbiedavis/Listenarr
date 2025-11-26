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
if (typeof (globalThis as unknown as { localStorage?: unknown }).localStorage === 'undefined' ||
    typeof (globalThis as any).localStorage?.setItem !== 'function') {
  ;(globalThis as unknown as { localStorage?: any }).localStorage = {
    _store: {} as Record<string, string>,
    getItem(key: string) { return this._store[key] ?? null },
    setItem(key: string, value: string) { this._store[key] = value + '' },
    removeItem(key: string) { delete this._store[key] },
    clear() { this._store = {} }
  }
}
