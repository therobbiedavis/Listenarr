import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiService } from '@/services/api'

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
      const status = (error && typeof error === 'object' && 'status' in error) ? (error as any).status : 0
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

  const logout = async () => {
    try {
      console.log('[AuthStore] Starting logout...')
      await apiService.logout()
      console.log('[AuthStore] Logout API call successful')
    } catch (error) {
      console.error('[AuthStore] Logout API call failed:', error)
      // Continue with local logout even if API call fails
    } finally {
      user.value = { authenticated: false }
      console.log('[AuthStore] Local user state cleared')
    }
  }

  return { user, redirectTo, loadCurrentUser, login, logout, loaded }
})
