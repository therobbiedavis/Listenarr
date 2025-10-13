import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiService } from '@/services/api'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<{ authenticated: boolean; name?: string }>({ authenticated: false })
  // Whether we've attempted to load the current user at least once
  const loaded = ref<boolean>(false)
  const redirectTo = ref<string | null>(null)

  const loadCurrentUser = async () => {
    try {
      const u = await apiService.getCurrentUser()
      user.value = u
      loaded.value = true
    } catch {
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
