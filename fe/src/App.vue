<template>
  <div id="app">
    <!-- Top Navigation Bar -->
    <header class="top-nav">
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
      </div>
    </header>

    <div class="app-layout">
      <!-- Sidebar Navigation -->
      <aside class="sidebar">
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
            <RouterLink to="/calendar" class="nav-item">
              <i class="ph ph-calendar"></i>
              <span>Calendar</span>
            </RouterLink>
            <RouterLink to="/activity" class="nav-item">
              <i class="ph ph-activity"></i>
              <span>Activity</span>
              <span class="badge" v-if="activityCount > 0">{{ activityCount }}</span>
            </RouterLink>
            <RouterLink to="/wanted" class="nav-item">
              <i class="ph ph-heart"></i>
              <span>Wanted</span>
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
      <main class="main-content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { ref } from 'vue'

// Reactive state for badges and counters
const notificationCount = ref(0)
const activityCount = ref(8)
const systemIssues = ref(2)

// Methods for nav actions
const toggleSearch = () => {
  console.log('Toggle search')
}

const toggleNotifications = () => {
  console.log('Toggle notifications')
}
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
