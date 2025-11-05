import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiService } from '@/services/api'
import { sessionTokenManager } from '@/utils/sessionToken'
import { clearAllAuthData } from '@/utils/sessionDebug'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<{ authenticated: boolean; name?: string }>({ authenticated: false })
  // Whether we've attempted to load the current user at least once
  const loaded = ref<boolean>(false)
  const redirectTo = ref<string | null>(null)

  const loadCurrentUser = async () => {
    console.log('[AuthStore] Loading current user...')
    try {
      const u = await apiService.getCurrentUser()
      console.log('[AuthStore] Current user loaded:', u)
      user.value = u
      loaded.value = true
    } catch (error) {
      console.warn('[AuthStore] Failed to load current user:', error)
  const status = (error && typeof error === 'object' && 'status' in error) ? (error as unknown as { status?: number }).status ?? 0 : 0
      if (status === 401 || status === 403) {
        console.log('[AuthStore] Authentication error - clearing session')
        // Clear any stale tokens when we get auth errors
        try {
          import('@/utils/sessionToken').then(({ sessionTokenManager }) => {
            sessionTokenManager.clearToken()
          })
        } catch {}
      }
      user.value = { authenticated: false }
      loaded.value = true
    }
  }

  const login = async (username: string, password: string, rememberMe: boolean, csrfToken?: string) => {
    await apiService.login(username, password, rememberMe, csrfToken)
    await loadCurrentUser()
  }

  // React to token changes from other tabs (cross-tab logout)
  try {
    sessionTokenManager.onTokenChange((token) => {
      if (!token) {
        console.log('[AuthStore] Session token removed in another tab - clearing auth state')
        user.value = { authenticated: false }

        // Attempt SPA navigation to login using the router if available.
        // Use dynamic import to avoid circular dependency at module load time.
        try {
          import('@/router')
            .then((mod) => {
              try {
                const current = window.location.pathname + window.location.search + window.location.hash
                if (!current.startsWith('/login')) {
                  // Preserve the current location as redirect parameter so user can return after login
                  mod.default.push({ 
                    name: 'login', 
                    query: { redirect: current } 
                  }).catch(() => { 
                    window.location.href = `/login?redirect=${encodeURIComponent(current)}` 
                  })
                }
              } catch {
                window.location.href = '/login'
              }
            })
            .catch(() => { window.location.href = '/login' })
        } catch {
          try { window.location.href = '/login' } catch {}
        }
      }
    })
  } catch {}

  const logout = async () => {
    try {
      console.log('[AuthStore] Starting logout...')
      await apiService.logout()
      console.log('[AuthStore] Logout API call successful')
    } catch (error) {
      console.error('[AuthStore] Logout API call failed:', error)
      // Continue with local logout even if API call fails
    } finally {
      // Ensure all client-side auth data is cleared even if the API call failed
      try {
        sessionTokenManager.clearToken()
      } catch (e) {
        console.warn('[AuthStore] Failed to clear sessionTokenManager:', e)
      }

      try {
        // Comprehensive cleanup (removes any lingering storage keys)
        clearAllAuthData()
      } catch (e) {
        console.warn('[AuthStore] Failed to run clearAllAuthData:', e)
      }

      user.value = { authenticated: false }
      console.log('[AuthStore] Local user state cleared and auth data removed')
    }
  }

  return { user, redirectTo, loadCurrentUser, login, logout, loaded }
})
