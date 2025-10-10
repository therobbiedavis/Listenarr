<template>
  <div id="app">
    <!-- Top Navigation Bar -->
    <header v-if="!hideLayout" class="top-nav">
      <div class="nav-brand">
        <img src="/icon.png" alt="Listenarr" class="brand-logo" />
        <h1>Listenarr</h1>
        <span class="version">v1.0.0</span>
      </div>
      <div class="nav-actions">
        <button class="nav-btn" @click="toggleSearch">
          <i class="ph ph-magnifying-glass"></i>
        </button>
        <button class="nav-btn" @click="toggleNotifications">
          <i class="ph ph-bell"></i>
          <span class="badge" v-if="notificationCount > 0">{{ notificationCount }}</span>
        </button>
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
      </div>
    </header>

    <div class="app-layout">
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
            <RouterLink to="/library-import" class="nav-item">
              <i class="ph ph-folder-open"></i>
              <span>Library Import</span>
            </RouterLink>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import NotificationModal from '@/components/NotificationModal.vue'
import { useNotification } from '@/composables/useNotification'
import { useDownloadsStore } from '@/stores/downloads'
import { useAuthStore } from '@/stores/auth'
import { apiService } from '@/services/api'
import { signalRService } from '@/services/signalr'
import type { QueueItem } from '@/types'

const { notification, close: closeNotification } = useNotification()
const downloadsStore = useDownloadsStore()
const auth = useAuthStore()
const authEnabled = ref(false)

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
const notificationCount = ref(0)
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

let wantedBadgeRefreshInterval: number | undefined
let unsubscribeQueue: (() => void) | null = null

// Fetch wanted badge count (library changes less frequently - minimal polling)
const refreshWantedBadge = async () => {
  try {
    // Wanted badge: count monitored audiobooks without files
    const library = await apiService.getLibrary()
    wantedCount.value = library.filter(book => 
      book.monitored && (!book.filePath || book.filePath.trim() === '')
    ).length
  } catch (err) {
    console.error('Failed to refresh wanted badge:', err)
  }
}

// Methods for nav actions
const toggleSearch = () => {
  console.log('Toggle search')
}

const toggleNotifications = () => {
  console.log('Toggle notifications')
}

// Initialize: Subscribe to SignalR for real-time updates (NO POLLING!)
onMounted(async () => {
  console.log('[App] Initializing real-time updates via SignalR...')
  
  // Load current auth state before touching protected endpoints
  await auth.loadCurrentUser()

  // If authenticated, load protected resources and enable real-time updates
  if (auth.user.authenticated) {
    // Load initial downloads
    await downloadsStore.loadDownloads()

    // Subscribe to queue updates via SignalR (real-time, no polling!)
    unsubscribeQueue = signalRService.onQueueUpdate((queue) => {
      console.log('[App] Received queue update via SignalR:', queue.length, 'items')
      queueItems.value = queue
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
    const v = cfg?.authenticationRequired
    authEnabled.value = (typeof v === 'boolean') ? v : (typeof v === 'string' ? (v.toLowerCase() === 'enabled' || v.toLowerCase() === 'true') : false)
    if (import.meta.env.DEV) {
      try { console.debug('[App] startup config fetched', { authEnabled: authEnabled.value, cfg }) } catch {}
    }
  } catch {
    authEnabled.value = false
  }

  // Click-outside handler for user menu
  document.addEventListener('click', handleDocumentClick)
})

onUnmounted(() => {
  // Clean up subscriptions
  if (unsubscribeQueue) {
    unsubscribeQueue()
  }
  if (wantedBadgeRefreshInterval) {
    clearInterval(wantedBadgeRefreshInterval)
  }
  document.removeEventListener('click', handleDocumentClick)
})

const logout = async () => {
  await auth.logout()
  window.location.reload()
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
  font-size: 20px;
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
  min-height: 100vh;
}

.fullpage-wrapper {
  width: 100%;
  max-width: 480px;
  padding: 1rem;
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
</style>
