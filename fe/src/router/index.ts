import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { getStartupConfigCached } from '@/services/startupConfigCache'
import type { StartupConfig } from '@/types'
import SettingsView from '../views/SettingsView.vue'

// Module-level cache/promise for startup config to avoid repeated requests during rapid navigation
// Use a promise so concurrent navigations share the same inflight request instead of issuing many
// Module-level cache moved to services/startupConfigCache

const routes = [
  { path: '/', name: 'home', component: () => import('../views/AudiobooksView.vue'), meta: { requiresAuth: true } },
  { path: '/audiobooks', name: 'audiobooks', component: () => import('../views/AudiobooksView.vue'), meta: { requiresAuth: true } },
  { path: '/audiobooks/:id', name: 'audiobook-detail', component: () => import('../views/AudiobookDetailView.vue'), meta: { requiresAuth: true } },
  { path: '/add-new', name: 'add-new', component: () => import('../views/AddNewView.vue'), meta: { requiresAuth: true } },
  { path: '/library-import', name: 'library-import', component: () => import('../views/LibraryImportView.vue'), meta: { requiresAuth: true } },
  { path: '/activity', name: 'activity', component: () => import('../views/ActivityView.vue'), meta: { requiresAuth: true } },
  { path: '/wanted', name: 'wanted', component: () => import('../views/WantedView.vue'), meta: { requiresAuth: true } },
  { path: '/downloads', name: 'downloads', component: () => import('../views/DownloadsView.vue'), meta: { requiresAuth: true } },
  { path: '/settings', name: 'settings', component: SettingsView, meta: { requiresAuth: true } },
  { path: '/system', name: 'system', component: () => import('../views/SystemView.vue'), meta: { requiresAuth: true } },
  { path: '/logs', name: 'logs', component: () => import('../views/LogsView.vue'), meta: { requiresAuth: true } },
  { path: '/login', name: 'login', component: () => import('../views/LoginView.vue'), meta: { hideLayout: true } },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// Navigation guard: protect routes requiring auth and preserve redirectTo
router.beforeEach(async (to, from, next) => {
  // Skip auth guard in Cypress tests
  if (import.meta.env.CYPRESS) return next()
  const auth = useAuthStore()

  // Load current user only once per app lifetime (avoid repeated calls on every navigation)
  if (!auth.loaded) {
    try {
      await auth.loadCurrentUser()
    } catch {
      // ignore - loadCurrentUser handles errors and sets loaded flag
    }
  }

  if (import.meta.env.DEV) {
    try { console.debug('[router] beforeEach', { to: to.fullPath, authenticated: auth.user.authenticated, loaded: auth.loaded }) } catch {}
  }

  // Obtain startup config using a shared module-level promise/cache so multiple navigations
  // during app boot don't trigger many GETs to /api/startupconfig.
  // use shared startup config cache (deduplicates inflight requests)
  const startupConfig = await getStartupConfigCached()
  // Fail-safe: if we couldn't load startup config, assume authentication is required
  const startupConfigMissing = !startupConfig
  if (import.meta.env.DEV) {
    try { console.debug('[router] startupConfigMissing', startupConfigMissing) } catch {}
  }
  if (import.meta.env.DEV) {
    try { console.debug('[router] startupConfig', startupConfig) } catch {}
  }
  const authRequiredConfig = (() => {
    if (startupConfigMissing) return true
    // Accept both camelCase and PascalCase variants from backend
    const raw = startupConfig?.authenticationRequired ?? (startupConfig as StartupConfig & { AuthenticationRequired?: string | boolean })?.AuthenticationRequired
    const v = raw
    if (v === undefined || v === null) return false
    if (typeof v === 'boolean') return v
    if (typeof v === 'string') return v.toLowerCase() === 'enabled' || v.toLowerCase() === 'true'
    return false
  })()

  // If authentication is disabled in startup config, prevent access to login page
  if (!authRequiredConfig) {
    // Authentication globally disabled: don't enforce requiresAuth.
    // Still prevent navigating to the login page when auth is disabled.
    if (to.name === 'login') {
      if (import.meta.env.DEV) { try { console.debug('[router] auth disabled, redirecting from login to home') } catch {} }
      return next({ name: 'home' })
    }
  } else {
    // Authentication enabled: enforce protection on routes marked as requiresAuth
    if (((to.meta as Record<string, unknown>)?.requiresAuth) && !auth.user.authenticated) {
      // Preserve the intended route and redirect to login
      // Use a query param so the redirect survives page reloads; also keep store as fallback
      auth.redirectTo = to.fullPath
      if (import.meta.env.DEV) { try { console.debug('[router] requiresAuth and not authenticated, redirecting to login', { redirect: to.fullPath }) } catch {} }
      return next({ name: 'login', query: { redirect: to.fullPath } })
    }
  }

  // If already authenticated and going to login, redirect to saved destination
  if (to.name === 'login' && auth.user.authenticated) {
    const dest = auth.redirectTo ?? { name: 'home' }
    auth.redirectTo = null
    return next(dest)
  }

  return next()
})

export default router
