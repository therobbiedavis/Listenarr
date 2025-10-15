<template>
  <div id="app">
    <!-- Top Navigation Bar -->
    <header v-if="!hideLayout" class="top-nav">
      <div class="nav-brand">
        <img src="/icon.png" alt="Listenarr" class="brand-logo" />
        <h1>Listenarr</h1>
        <span v-if="version && version.length > 0" class="version">v{{ version }}</span>
      </div>
      <div class="nav-actions">
        <div class="nav-search-inline" ref="navSearchRef" :class="{ open: searchOpen }">
          <input
            v-model="searchQuery"
            @input="onSearchInput"
            @keydown.enter="applyFirstResult"
            @keydown.escape.prevent="closeSearch"
            ref="searchInputRef"
            class="search-input-inline"
            type="search"
            placeholder="Search your library..."
            aria-label="Search your audiobooks"
          />
          <button class="nav-btn" aria-hidden="true"
            role="button"
            tabindex="0"
            @click="toggleSearch"
            @keydown.enter.prevent="toggleSearch"
          >
          <i class="ph ph-magnifying-glass search-inline-icon"></i>
        </button>
          <div class="inline-spinner" v-if="searching" aria-hidden="true"></div>
          <!-- Results overlay: shows suggestions, or a searching/no-results state with spinner -->
          <div class="search-results-inline" v-if="searching || suggestions.length > 0 || searchQuery.length > 0">
            <ul v-if="suggestions.length > 0" class="search-list">
              <li v-for="s in suggestions" :key="s.id" class="search-result" @click="selectSuggestion(s)">
                <div style="display:flex;align-items:center;gap:10px;">
                  <img v-if="s.imageUrl" :src="s.imageUrl" alt="cover" class="result-thumb" />
                  <div>
                    <div class="result-title">{{ s.title }}</div>
                    <div class="result-sub">{{ s.author }}</div>
                  </div>
                </div>
              </li>
            </ul>

            <div v-else class="search-empty-overlay">
              <div class="overlay-spinner" v-if="searching" aria-hidden="true"></div>
              <div class="search-empty" v-if="searching">Searching...</div>
              <div class="search-empty" v-else-if="searchQuery.length > 0">No matches</div>
            </div>
          </div>
        </div>
        <div class="notification-wrapper" ref="notificationRef">
          <button class="nav-btn" @click="toggleNotifications" aria-haspopup="true" :aria-expanded="notificationsOpen">
            <i class="ph ph-bell"></i>
            <span class="notification-badge" v-if="notificationCount > 0">{{ notificationCount }}</span>
          </button>
          <div v-if="notificationsOpen" class="notification-dropdown" role="menu">
            <div class="dropdown-header">
              <strong>Recent Activity</strong>
              <button class="clear-btn" @click.stop="clearNotifications" title="Clear">Clear</button>
            </div>
            <ul class="notification-list">
              <li v-for="item in recentNotifications.filter(n => !n.dismissed)" :key="item.id" class="notification-item">
                <div class="notif-icon"><i :class="item.icon"></i></div>
                <div class="notif-content">
                  <div class="notif-title">{{ item.title }}</div>
                  <div class="notif-message">{{ item.message }}</div>
                  <div class="notif-time">{{ formatTime(item.timestamp) }}</div>
                </div>
                <div class="notif-actions">
                  <button class="dismiss-btn" @click.stop="dismissNotification(item.id)" title="Dismiss">
                    <i class="ph ph-x"></i>
                  </button>
                </div>
              </li>
              <li v-if="recentNotifications.filter(n => !n.dismissed).length === 0" class="notification-empty">No recent activity</li>
            </ul>
            <div class="dropdown-footer">
              <RouterLink to="/activity" class="view-all-link" @click="notificationsOpen = false">View all activity</RouterLink>
            </div>
          </div>
        </div>
        <template v-if="authEnabled">
          <template v-if="auth.user.authenticated">
            <div class="nav-user" ref="navUserRef">
              <button
                class="nav-btn nav-user-btn"
                @click="toggleUserMenu"
                :aria-expanded="userMenuOpen"
                aria-haspopup="true"
                title="Account"
              >
                <i class="ph ph-users nav-user-icon"></i>
              </button>

              <div v-if="userMenuOpen" class="user-menu" role="menu">
                <button class="user-menu-item" role="menuitem" @click="logout">Logout</button>
              </div>
            </div>
          </template>
          <template v-else>
            <RouterLink to="/login" class="nav-btn">Login</RouterLink>
          </template>
        </template>
      </div>
    </header>

  <div :class="['app-layout', { 'no-top': hideLayout }]">
      <!-- Sidebar Navigation -->
      <aside v-if="!hideLayout" class="sidebar">
        <nav class="sidebar-nav">
          <div class="nav-section">
            <RouterLink to="/" class="nav-item">
              <i class="ph ph-books"></i>
              <span>Audiobooks</span>
            </RouterLink>
            <RouterLink to="/add-new" class="nav-item">
              <i class="ph ph-plus"></i>
              <span>Add New</span>
            </RouterLink>
            <!-- <RouterLink to="/library-import" class="nav-item">
              <i class="ph ph-folder-open"></i>
              <span>Library Import</span>
            </RouterLink> -->
          </div>
          
          <div class="nav-section">
            <!-- Calendar temporarily hidden -->
            <!-- <RouterLink to="/calendar" class="nav-item">
              <i class="ph ph-calendar"></i>
              <span>Calendar</span>
            </RouterLink> -->
            <RouterLink to="/activity" class="nav-item">
              <i class="ph ph-activity"></i>
              <span>Activity</span>
              <span class="badge" v-if="activityCount > 0">{{ activityCount }}</span>
            </RouterLink>
            <RouterLink to="/wanted" class="nav-item">
              <i class="ph ph-heart"></i>
              <span>Wanted</span>
              <span class="badge" v-if="wantedCount > 0">{{ wantedCount }}</span>
            </RouterLink>
          </div>

          <div class="nav-section">
            <RouterLink to="/settings" class="nav-item">
              <i class="ph ph-gear"></i>
              <span>Settings</span>
            </RouterLink>
            <RouterLink to="/system" class="nav-item">
              <i class="ph ph-monitor"></i>
              <span>System</span>
              <span class="badge error" v-if="systemIssues > 0">{{ systemIssues }}</span>
            </RouterLink>
          </div>
        </nav>
      </aside>

      <!-- Main Content Area -->
      <main :class="['main-content', { 'full-page': hideLayout }]">
        <div v-if="hideLayout" class="fullpage-wrapper">
          <RouterView />
        </div>
        <div v-else>
          <RouterView />
        </div>
      </main>
    </div>

    <!-- Global Notification Modal -->
    <NotificationModal
      :visible="notification.visible"
      :message="notification.message"
      :title="notification.title"
      :type="notification.type"
      :auto-close="notification.autoClose"
      @close="closeNotification"
    />
  </div>
</template>

<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import NotificationModal from '@/components/NotificationModal.vue'
import { useNotification } from '@/composables/useNotification'
import { useDownloadsStore } from '@/stores/downloads'
import { useAuthStore } from '@/stores/auth'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import type { QueueItem } from '@/types'
import { ref as vueRef2, reactive } from 'vue'

const { notification, close: closeNotification, info } = useNotification()
const downloadsStore = useDownloadsStore()
const auth = useAuthStore()
const authEnabled = ref(false)

// Version from API
const version = ref('')

// User menu (people icon) state
const userMenuOpen = ref(false)
const navUserRef = ref<HTMLElement | null>(null)
const toggleUserMenu = () => {
  userMenuOpen.value = !userMenuOpen.value
}

const handleDocumentClick = (e: MouseEvent) => {
  const el = navUserRef.value
  if (!el) return
  const target = e.target as Node
  if (!el.contains(target)) {
    userMenuOpen.value = false
  }
}

// Reactive state for badges and counters
const notificationCount = computed(() => recentNotifications.filter(n => !n.dismissed).length)
const queueItems = ref<QueueItem[]>([])
const wantedCount = ref(0)
const systemIssues = ref(0)

// Activity count: combines downloads (SignalR) + queue (SignalR)
// All real-time, no polling!
const activityCount = computed(() => {
  const downloadsActive = downloadsStore.activeDownloads.length
  const queueActive = queueItems.value.filter(item =>
    item.status === 'downloading' ||
    item.status === 'paused' ||
    item.status === 'queued'
  ).length
  
  // Count DDL downloads separately (they never appear in queue)
  const ddlDownloads = downloadsStore.activeDownloads.filter(d => d.downloadClientId === 'DDL').length
  
  // Count external client downloads (may be in both downloads and queue)
  const externalDownloads = downloadsActive - ddlDownloads
  
  // Total = DDL (unique) + max(external in downloads, external in queue)
  // This avoids double-counting external clients that appear in both places
  const count = ddlDownloads + Math.max(externalDownloads, queueActive)
  
  console.log('[App Badge] Activity:', {
    ddl: ddlDownloads,
    externalDownloads: externalDownloads,
    queueActive: queueActive,
    total: count
  })
  return count
})

// Notification dropdown state
const notificationsOpen = vueRef2(false)
const notificationRef = vueRef<HTMLElement | null>(null)
const handleNotificationDocumentClick = (e: MouseEvent) => {
  const el = notificationRef.value
  if (!el) return
  const target = e.target as Node
  if (!el.contains(target)) {
    notificationsOpen.value = false
  }
}

type HistoryNotification = {
  id: string
  title: string
  message: string
  icon?: string
  timestamp: string
  dismissed?: boolean
}

const recentNotifications = reactive<HistoryNotification[]>([])
const recentDownloadTitles = ref<Set<string>>(new Set()) // Track recent download titles to avoid spam

function pushNotification(n: HistoryNotification) {
  // Ensure new notifications are not dismissed
  const notification = { ...n, dismissed: false }
  // Keep a max of 10 items
  recentNotifications.unshift(notification)
  if (recentNotifications.length > 10) recentNotifications.pop()
}

function clearNotifications() {
  recentNotifications.length = 0
  recentDownloadTitles.value.clear()
}

function dismissNotification(id: string) {
  const notification = recentNotifications.find(n => n.id === id)
  if (notification) {
    notification.dismissed = true
  }
}

function toggleNotifications() {
  notificationsOpen.value = !notificationsOpen.value
}

// Format timestamp for display - reuse the same formatTime helper used elsewhere
function formatTime(ts: string) {
  try {
    const d = new Date(ts)
    return d.toLocaleString()
  } catch {
    return ts
  }
}

let wantedBadgeRefreshInterval: number | undefined
let unsubscribeQueue: (() => void) | null = null
let unsubscribeFilesRemoved: (() => void) | null = null

// Fetch wanted badge count (library changes less frequently - minimal polling)
const refreshWantedBadge = async () => {
  try {
    // Wanted badge: rely exclusively on the server-provided `wanted` flag.
    // Treat only audiobooks where server returns wanted === true as wanted.
    const library = await apiService.getLibrary()
    wantedCount.value = library.filter(book => {
      const serverWanted = (book as unknown as Record<string, unknown>)['wanted']
      return serverWanted === true
    }).length
  } catch (err) {
    console.error('Failed to refresh wanted badge:', err)
  }
}

// Methods for nav actions
// Inline search is always visible in header; focus on mount if needed

// --- Header search implementation ---
import { ref as vueRef } from 'vue'
const router = useRouter()
const searchQuery = vueRef('')
const suggestions = vueRef<Array<{ id: number; title: string; author?: string; imageUrl?: string }>>([])
const searching = vueRef(false)
const searchInputRef = vueRef<HTMLInputElement | null>(null)

// Slide-out search state and refs
const navSearchRef = vueRef<HTMLElement | null>(null)
const searchOpen = vueRef(false)

const toggleSearch = () => {
  searchOpen.value = !searchOpen.value
  if (searchOpen.value) {
    // Wait for DOM update then focus input
    nextTick(() => searchInputRef.value?.focus())
  }
}

const closeSearch = () => {
  if (searchOpen.value) {
    searchOpen.value = false
  }
}

const handleSearchDocumentClick = (e: MouseEvent) => {
  const el = navSearchRef.value
  if (!el) return
  const target = e.target as Node
  if (!el.contains(target)) {
    searchOpen.value = false
  }
}

let searchDebounceTimer: number | undefined
const onSearchInput = async () => {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  const q = searchQuery.value.trim()
  if (q.length === 0) {
    suggestions.value = []
    return
  }
  searchDebounceTimer = window.setTimeout(async () => {
    searching.value = true
    try {
      // First try to match local library entries
      const lib = await apiService.getLibrary()
      const lower = q.toLowerCase()
      const localMatches = lib.filter(b => (b.title || '').toLowerCase().includes(lower) || (Array.isArray(b.authors) ? (b.authors.join(' ').toLowerCase()) : '').includes(lower))
      if (localMatches.length > 0) {
        // Only show local library matches in the header search
        suggestions.value = localMatches.slice(0, 8).map(b => ({
          id: b.id!,
          title: b.title || 'Unknown',
          author: Array.isArray(b.authors) ? (b.authors[0] || '') : '',
          imageUrl: b.imageUrl || ''
        }))
      } else {
        // No fallback to indexers from header search; leave suggestions empty
        suggestions.value = []
      }
    } catch (err) {
      console.error('Header search failed', err)
      suggestions.value = []
    } finally {
      searching.value = false
    }
  }, 250)
}

const selectSuggestion = (s: { id: number; title: string; author?: string }) => {
  // Navigate to audiobook detail if local (id > 0), else open search view
  if (!s) return
  searchQuery.value = ''
  suggestions.value = []
  if (s.id && s.id > 0) {
    // Navigate to audiobook detail page (router name: 'audiobook-detail')
    void router.push({ name: 'audiobook-detail', params: { id: String(s.id) } })
  } else {
    // Use the general search page for indexer results
    void router.push({ name: 'search', query: { q: s.title } })
  }
}

const applyFirstResult = () => {
  if (suggestions.value.length > 0) selectSuggestion(suggestions.value[0]!)
}

// (notificationRef and click-outside handler are declared earlier)

// Initialize: Subscribe to SignalR for real-time updates (NO POLLING!)
onMounted(async () => {
  console.log('[App] Initializing real-time updates via SignalR...')
  
  // Import session debugging utilities
  const { logSessionState, clearAllAuthData } = await import('@/utils/sessionDebug')
  
  // Log initial session state for debugging
  logSessionState('App Mount - Initial State')
  
  // Verify session is valid before proceeding
  console.log('[App] Verifying session state...')
  try {
    // Check if we have valid session/authentication
    const sessionCheck = await apiService.getServiceHealth()
    console.log('[App] Session verification successful:', sessionCheck)
  } catch (sessionError) {
    console.warn('[App] Session verification failed:', sessionError)
    // If we get 401/403, clear any stale auth state
    const status = (sessionError && typeof sessionError === 'object' && 'status' in sessionError) ? sessionError.status : 0
    if (status === 401 || status === 403) {
      console.log('[App] Clearing stale authentication state due to session error')
      auth.user.authenticated = false
      // Use the comprehensive clear function
      clearAllAuthData()
    }
  }
  
  // Load current auth state before touching protected endpoints
  await auth.loadCurrentUser()
  
  // Log session state after authentication attempt
  logSessionState('App Mount - After Auth Load')

  // If authenticated, load protected resources and enable real-time updates
    if (auth.user.authenticated) {
    // Load initial downloads
    await downloadsStore.loadDownloads()

    // Subscribe to queue updates via SignalR (real-time, no polling!)
    unsubscribeQueue = signalRService.onQueueUpdate((queue) => {
      console.log('[App] Received queue update via SignalR:', queue.length, 'items')
      queueItems.value = queue
    })

    // Subscribe to files-removed notifications so we can inform the user
    unsubscribeFilesRemoved = signalRService.onFilesRemoved((payload) => {
      try {
        const removed = Array.isArray(payload?.removed) ? payload.removed.map(r => r.path) : []
        const display = removed.length > 0 ? removed.join(', ') : 'Files were removed from a library item.'
        info(display, 'Files removed', 6000)
        // Refresh wanted badge in case monitored items lost files
        refreshWantedBadge()
        // Push into recent notifications
        pushNotification({
          id: `files-removed-${Date.now()}`,
          title: 'Files removed',
          message: display,
          icon: 'ph ph-file-remove',
          timestamp: new Date().toISOString()
        })
      } catch (err) {
        console.error('Error handling FilesRemoved payload', err)
      }
    })

    // Subscribe to audiobook updates (for wanted badge refresh only, no notifications)
    signalRService.onAudiobookUpdate((ab) => {
      try {
        if (!ab) return

        // If server provided a wanted flag, refresh the wanted badge using the authoritative value
        try {
          const serverWanted = (ab as unknown as Record<string, unknown>)['wanted']
          if (typeof serverWanted === 'boolean') {
            // Recompute wantedCount by fetching library DTOs and trusting server 'wanted'
            // This is a targeted refresh to avoid stale counts; call refreshWantedBadge()
            refreshWantedBadge()
          }
        } catch {}
      } catch (err) { console.error('AudiobookUpdate error', err) }
    })

    // Subscribe to download updates for notification purposes.
    // Only create notifications for meaningful lifecycle events: start (Queued)
    // and completion (Completed). Do not create notifications for continuous
    // progress updates to avoid flooding the notification list.
    signalRService.onDownloadUpdate((downloads) => {
      try {
        if (!downloads || downloads.length === 0) return
        for (const d of downloads) {
          // Normalize status (some backends may use different casing)
          const status = (d.status || '').toString().toLowerCase()
          const title = d.title || 'Unknown'
          
          if (status === 'queued' || status === 'queued' /* start */) {
            pushNotification({
              id: `dl-start-${d.id}-${Date.now()}`,
              title: title || 'Download started',
              message: `Download started: ${title}`,
              icon: 'ph ph-download',
              timestamp: new Date().toISOString()
            })
          } else if (status === 'completed' || status === 'ready') {
            // Avoid spamming notifications for the same title
            // Only notify if we haven't notified about this title recently
            if (!recentDownloadTitles.value.has(title)) {
              pushNotification({
                id: `dl-complete-${d.id}-${Date.now()}`,
                title: title || 'Download complete',
                message: `Download completed: ${title}`,
                icon: 'ph ph-check-circle',
                timestamp: new Date().toISOString()
              })
              // Track this title and clear it after 30 seconds
              recentDownloadTitles.value.add(title)
              setTimeout(() => {
                recentDownloadTitles.value.delete(title)
              }, 30000)
            }
          } else {
            // Ignore progress/other transient updates
          }
        }
      } catch (err) { console.error('DownloadUpdate notif error', err) }
    })

    // Fetch initial queue state
    try {
      const initialQueue = await apiService.getQueue()
      queueItems.value = initialQueue
    } catch (err) {
      console.error('[App] Failed to fetch initial queue:', err)
    }
  } else {
    console.log('[App] User not authenticated; skipping protected resource loads')
  }
  
  // Only poll "Wanted" badge (library changes infrequently)
  refreshWantedBadge()
  wantedBadgeRefreshInterval = window.setInterval(refreshWantedBadge, 60000) // Every minute
  
  console.log('[App] ✅ Real-time updates enabled - Activity badge updates automatically via SignalR!')
  // Fetch startup config (do this regardless of auth so header/login visibility can be known)
    try {
    const cfg = await apiService.getStartupConfig()
  // Accept both camelCase and PascalCase variants from backend (some responses use PascalCase)
  const obj = cfg as Record<string, unknown> | null
  const raw = obj ? (obj['authenticationRequired'] ?? obj['AuthenticationRequired']) : undefined
  const v = raw as unknown
    authEnabled.value = (typeof v === 'boolean') ? v : (typeof v === 'string' ? (v.toLowerCase() === 'enabled' || v.toLowerCase() === 'true') : false)
    if (import.meta.env.DEV) {
      try { console.debug('[App] startup config fetched', { authEnabled: authEnabled.value, cfg }) } catch {}
    }
  } catch {
    authEnabled.value = false
  }

  // Fetch version from API
  try {
    const health = await apiService.getServiceHealth()
    version.value = health.version
  } catch (err) {
    console.warn('[App] Failed to fetch version from API:', err)
  }

  // Click-outside handler for user menu
  document.addEventListener('click', handleDocumentClick)
  // Click-outside handler for the header search
  document.addEventListener('click', handleSearchDocumentClick)
  // Click-outside handler for notifications
  document.addEventListener('click', handleNotificationDocumentClick)
})

onUnmounted(() => {
  // Clean up subscriptions
  if (unsubscribeQueue) {
    unsubscribeQueue()
  }
    if (unsubscribeFilesRemoved) {
      unsubscribeFilesRemoved()
    }
  if (wantedBadgeRefreshInterval) {
    clearInterval(wantedBadgeRefreshInterval)
  }
  document.removeEventListener('click', handleDocumentClick)
  document.removeEventListener('click', handleSearchDocumentClick)
  document.removeEventListener('click', handleNotificationDocumentClick)
})

const logout = async () => {
  try {
    console.log('[App] Logout button clicked')
    await auth.logout()
    console.log('[App] Auth logout completed, redirecting to login')
    // Instead of reloading, redirect to login - the router guard will handle authentication
    await router.push({ name: 'login' })
  } catch (error) {
    console.error('[App] Error during logout:', error)
    // Force redirect to login even if logout fails
    await router.push({ name: 'login' })
  }
}

const route = useRoute()
const hideLayout = computed(() => {
  const meta = route.meta as Record<string, unknown> | undefined
  return !!(meta && meta.hideLayout)
})
</script>

<style scoped>
#app {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  margin: 0;
  padding: 0;
  min-height: calc(100vh - 60px);
  background-color: #1a1a1a;
  color: white;
}

/* Top Navigation */
.top-nav {
  background-color: #2a2a2a;
  border-bottom: 1px solid #3a3a3a;
  padding: 0 1rem;
  height: 60px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 1000;
}

.nav-brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.brand-logo {
  width: 40px;
  height: 40px;
  transition: transform 0.2s;
}

.brand-logo:hover {
  transform: rotate(5deg) scale(1.05);
}

.nav-brand h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: bold;
  color: #2196F3;
}

.version {
  background-color: #555;
  padding: 0.2rem 0.5rem;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #ccc;
}

.nav-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.nav-user {
  position: relative;
}

.nav-user-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
}

.nav-user-icon {
  font-size: 18px;
}

.user-menu {
  position: absolute;
  right: 0;
  top: 48px;
  background: #252525;
  border: 1px solid #3a3a3a;
  border-radius: 6px;
  min-width: 160px;
  box-shadow: 0 6px 18px rgba(0,0,0,0.5);
  z-index: 1200;
  padding: 0.25rem 0;
}

.user-menu-item {
  display: block;
  width: 100%;
  padding: 0.5rem 1rem;
  background: transparent;
  border: none;
  color: #ddd;
  text-align: left;
  cursor: pointer;
}

.user-menu-item.username {
  font-weight: 600;
  color: #fff;
}

.user-menu-item:hover {
  background: #333;
}

.nav-btn {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  position: relative;
  transition: background-color 0.2s;
}

.nav-btn:hover {
  background-color: #3a3a3a;
  color: white;
}

.avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
}

/* App Layout */
.app-layout {
  display: flex;
  margin-top: 60px;
  min-height: calc(100vh - 60px);
}

.app-layout.no-top {
  margin-top: 0;
}

/* Sidebar */
.sidebar {
  width: 200px;
  background-color: #2a2a2a;
  border-right: 1px solid #3a3a3a;
  position: fixed;
  left: 0;
  top: 60px;
  bottom: 0;
  overflow-y: auto;
}

.sidebar-nav {
  padding: 1rem 0;
}

.nav-section {
  margin-bottom: 1.5rem;
}

.nav-item {
  display: flex;
  align-items: center;
  padding: 0.75rem 1rem;
  color: #ccc;
  text-decoration: none;
  transition: all 0.2s;
  position: relative;
  gap: 0.75rem;
}

.nav-item:hover {
  background-color: #3a3a3a;
  color: white;
}

.nav-item.router-link-active {
  background-color: #007acc;
  color: white;
}

.nav-item.router-link-active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background-color: #007acc;
}

/* Icons */
.icon-audiobooks::before { content: '�'; }
.icon-plus::before { content: '+'; }
.icon-import::before { content: '📁'; }
.icon-calendar::before { content: '📅'; }
.icon-activity::before { content: '⏱️'; }
.icon-wanted::before { content: '⚠️'; }
.icon-settings::before { content: '⚙️'; }
.icon-system::before { content: '💻'; }
.icon-search::before { content: '🔍'; }
.icon-bell::before { content: '🔔'; }

/* Badges */
.badge {
  background-color: #f39c12;
  color: white;
  border-radius: 10px;
  padding: 0.2rem 0.5rem;
  font-size: 0.75rem;
  font-weight: bold;
  margin-left: auto;
}

.notification-badge {
  background-color: #f39c12;
  color: white;
  border-radius: 8px;
  padding: 0.1rem 0.3rem;
  font-size: 0.65rem;
  font-weight: bold;
  position: absolute;
  top: -2px;
  right: -2px;
  min-width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
}

.badge.error {
  background-color: #e74c3c;
}

/* Main Content */
.main-content {
  flex: 1;
  margin-left: 200px;
  background-color: #1a1a1a;
  min-height: calc(100vh - 60px);
}

.main-content.full-page {
  margin-left: 0;
  margin-top: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  /* Account for the fixed 60px top nav so content centers in remaining viewport */
  min-height: calc(100vh - 60px);
}

.fullpage-wrapper {
  width: 100%;
  max-width: 480px;
  padding: 1.25rem 1rem;
  box-sizing: border-box;
}

/* Responsive adjustments for login/full-page wrapper */
@media (max-width: 768px) {
  .fullpage-wrapper {
    padding: 1rem 0.75rem;
    max-width: 440px;
    margin: 0 12px;
  }
}

@media (max-width: 480px) {
  .fullpage-wrapper {
    padding: 0.75rem 0.5rem;
    max-width: 360px;
    margin: 0 8px;
  }
}

/* Responsive */
@media (max-width: 768px) {
  .sidebar {
    transform: translateX(-100%);
    transition: transform 0.3s;
  }
  
  .main-content {
    margin-left: 0;
  }
  
  .nav-brand h1 {
    font-size: 1.2rem;
  }
}
/* Header search styles */
.nav-search {
  position: relative;
  display: flex;
  align-items: center;
}

.nav-search .nav-btn {
  width: 44px;
  height: 44px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
}


.nav-search-inline {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Collapsible slide-out search: input is absolutely positioned so it doesn't affect layout */
.nav-search-inline {
  min-width: 44px; /* reserve space for the icon */
}

.search-input-inline {
  transition: transform 220ms cubic-bezier(.2,.9,.2,1), width 220ms ease, opacity 180ms ease;
  position: absolute;
  top: 50%;
  right: 40px; /* leave space for the icon */
  transform: translateX(15%);
  width: 0;
  opacity: 0;
  pointer-events: none;
}

.nav-search-inline.open .search-input-inline {
  transform: translateX(40px);
  width: 340px;
  max-width: 50vw;
  opacity: 1;
  pointer-events: auto;
}

/* Keep the results dropdown aligned to the input when open */
.search-results-inline {
  position: absolute;
  top: 56px; /* slightly below the input */
  right: 40px;
  left: auto;
  width: 340px;
  max-width: 50vw;
}

.search-inline-icon {
  color: #9aa0a6;
  font-size: 18px;
  padding: 6px;
  cursor: pointer;
  border-radius: 6px;
}

.search-inline-icon:hover {
  background-color: #3a3a3a;
  color: #fff;
}

/* Standardize header/nav icons: size, alignment, color, and hit area */
.top-nav .ph {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  font-size: 18px;
  color: #c7cfd6; /* slightly brighter than default */
  border-radius: 8px;
}

.top-nav .nav-btn {
  padding: 4px; /* smaller padding so icon boxes are consistent */
}

.top-nav .nav-user-btn .ph,
.top-nav .nav-btn .ph {
  font-size: 18px; /* ensure consistent glyph size */
}

.inline-spinner {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  border: 2px solid rgba(255,255,255,0.08);
  border-top-color: #2196F3;
  animation: spin 800ms linear infinite;
  margin-left: 6px;
}

.search-input-inline {
  width: 340px;
  max-width: 50vw;
  padding: 8px 12px 8px 12px;
  border-radius: 8px;
  border: 1px solid #424242;
  background: #222;
  color: #fff;
  outline: none;
  font-size: 0.95rem;
  position: relative;
}

.search-input::placeholder {
  color: #9aa0a6;
}

.search-input:focus {
  border-color: #2196F3;
  box-shadow: 0 4px 14px rgba(33,150,243,0.12);
}

.search-results-inline {
  list-style: none;
  margin: 8px 0 0 0;
  padding: 0;
  max-height: 300px;
  overflow-y: auto;
}

.nav-search-inline {
  position: relative;
}

.search-results-inline {
  position: absolute;
  top: 44px;
  left: 0;
  right: 0;
  background: #1f1f1f;
  border: 1px solid #333;
  border-radius: 8px;
  padding: 6px;
  z-index: 1400;
}

.search-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.result-thumb {
  width: 48px;
  height: 48px;
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
  background: #2a2a2a;
}

.search-empty-overlay {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px;
  justify-content: flex-start;
}

/* Small spinner */
.overlay-spinner {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  border: 2px solid rgba(255,255,255,0.08);
  border-top-color: #2196F3;
  animation: spin 800ms linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.search-result {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 10px;
  border-radius: 6px;
  cursor: pointer;
  color: #e6eef6;
}

.search-result:hover {
  background: rgba(255,255,255,0.03);
}



.result-title {
  font-weight: 600;
  color: #fff;
  font-size: 0.95rem;
}

.result-sub {
  font-size: 0.82rem;
  color: #bfc8cf;
}

.search-empty {
  padding: 8px 10px;
  color: #9aa0a6;
  font-size: 0.9rem;
}

/* Slide-left transition (popout from search button) */
/* no transition needed for inline search */

/* Notification dropdown styles */
.notification-wrapper {
  position: relative;
}

.notification-dropdown {
  position: absolute;
  top: 48px;
  right: 0;
  background: #252525;
  border: 1px solid #3a3a3a;
  border-radius: 8px;
  min-width: 320px;
  max-width: 400px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.6);
  z-index: 1300;
  max-height: 400px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.dropdown-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #3a3a3a;
  background: #2a2a2a;
}

.dropdown-header strong {
  color: #fff;
  font-size: 14px;
  font-weight: 600;
}

.clear-btn {
  background: none;
  border: none;
  color: #ccc;
  font-size: 12px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.clear-btn:hover {
  background-color: #3a3a3a;
  color: #fff;
}

.notification-list {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 280px;
  overflow-y: auto;
}

.notification-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid #333;
  transition: background-color 0.2s;
}

.notification-item:hover {
  background-color: #2a2a2a;
}

.notification-item:last-child {
  border-bottom: none;
}

.notif-icon {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #2196F3;
  font-size: 16px;
}

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-title {
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  margin-bottom: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notif-message {
  font-size: 12px;
  color: #ccc;
  line-height: 1.4;
  overflow: hidden;
  display: -webkit-box;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
}

.notif-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.dismiss-btn {
  background: none;
  border: none;
  color: #888;
  font-size: 12px;
  cursor: pointer;
  padding: 2px;
  border-radius: 2px;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
}

.dismiss-btn:hover {
  background-color: #3a3a3a;
  color: #ccc;
}

.notif-time {
  font-size: 10px;
  color: #888;
  flex-shrink: 0;
}

.notification-empty {
  padding: 24px 16px;
  text-align: center;
  color: #888;
  font-size: 13px;
  font-style: italic;
}

.dropdown-footer {
  border-top: 1px solid #3a3a3a;
  background: #2a2a2a;
  padding: 8px 16px;
}

.view-all-link {
  display: inline-block;
  color: #2196F3;
  text-decoration: none;
  font-size: 12px;
  font-weight: 500;
  transition: color 0.2s;
}

.view-all-link:hover {
  color: #42a5f5;
  text-decoration: underline;
}
</style>
