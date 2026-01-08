/**
 * Session token management utilities
 */

class SessionTokenManager {
  private static readonly STORAGE_KEY = 'listenarr_session_token'
  private token: string | null = null
  private subscribers: Set<(token: string | null) => void> = new Set()

  constructor() {
    this.loadFromStorage()
    // Listen for storage events from other tabs/windows
    if (typeof window !== 'undefined' && window.addEventListener) {
      window.addEventListener('storage', this.handleStorageEvent)
    }
  }

  private loadFromStorage(): void {
    try {
      this.token = localStorage.getItem(SessionTokenManager.STORAGE_KEY)
    } catch {
      this.token = null
    }
  }

  getToken(): string | null {
    return this.token
  }

  setToken(token: string | null): void {
    this.token = token
    try {
      if (token) {
        localStorage.setItem(SessionTokenManager.STORAGE_KEY, token)
      } else {
        localStorage.removeItem(SessionTokenManager.STORAGE_KEY)
      }
    } catch {
      // Storage might be unavailable
    }
    // Notify subscribers synchronously
    try {
      for (const cb of Array.from(this.subscribers)) cb(this.token)
    } catch {}
  }

  clearToken(): void {
    this.setToken(null)
  }

  hasToken(): boolean {
    return !!this.token
  }

  // Subscribe to token changes (including cross-tab storage events)
  onTokenChange(cb: (token: string | null) => void): () => void {
    this.subscribers.add(cb)
    // Call immediately with current value so subscribers have initial state
    try {
      cb(this.token)
    } catch {}
    return () => {
      this.subscribers.delete(cb)
    }
  }

  private handleStorageEvent = (ev: StorageEvent) => {
    try {
      if (!ev) return
      if (ev.key !== SessionTokenManager.STORAGE_KEY) return

      // If newValue is null, token was removed in another tab; update internal
      // token value and notify subscribers.
      try {
        this.token = ev.newValue
      } catch {
        this.token = null
      }

      for (const cb of Array.from(this.subscribers)) {
        try {
          cb(this.token)
        } catch {}
      }
    } catch {}
  }
}

export const sessionTokenManager = new SessionTokenManager()
