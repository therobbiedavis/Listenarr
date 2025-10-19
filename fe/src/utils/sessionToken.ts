/**
 * Session token management utilities
 */

class SessionTokenManager {
  private static readonly STORAGE_KEY = 'listenarr_session_token'
  private token: string | null = null

  constructor() {
    this.loadFromStorage()
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
  }

  clearToken(): void {
    this.setToken(null)
  }

  hasToken(): boolean {
    return !!this.token
  }
}

export const sessionTokenManager = new SessionTokenManager()