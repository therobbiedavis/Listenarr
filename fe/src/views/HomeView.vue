<template>
  <div class="dashboard">
    <div class="dashboard-header">
      <h1>Dashboard</h1>
      <p>Welcome to Listenarr - Your automated media download and processing system</p>
    </div>

    <div class="dashboard-grid">
      <div class="stats-card">
        <div class="stat-icon">📺</div>
        <div class="stat-content">
          <h3>Audiobooks</h3>
          <div class="stat-number">{{ audiobookStats.total }}</div>
          <p>{{ audiobookStats.monitored }} monitored</p>
        </div>
      </div>

      <div class="stats-card">
        <div class="stat-icon">📥</div>
        <div class="stat-content">
          <h3>Downloads</h3>
          <div class="stat-number">{{ downloadStats.active }}</div>
          <p>{{ downloadStats.completed }} completed today</p>
        </div>
      </div>

      <div class="stats-card">
        <div class="stat-icon">⚠️</div>
        <div class="stat-content">
          <h3>Wanted</h3>
          <div class="stat-number">{{ wantedStats.missing }}</div>
          <p>{{ wantedStats.cutoffUnmet }} cutoff unmet</p>
        </div>
      </div>

      <div class="stats-card">
        <div class="stat-icon">💿</div>
        <div class="stat-content">
          <h3>Storage</h3>
          <div class="stat-number">{{ storageStats.used }}</div>
          <p>{{ storageStats.percentage }}% of {{ storageStats.total }}</p>
        </div>
      </div>
    </div>

    <div class="dashboard-content">
      <div class="main-section">
        <div class="section-card">
          <h2>Recent Activity</h2>
          <div class="activity-list">
            <div 
              v-for="activity in recentActivity" 
              :key="activity.id"
              class="activity-item"
            >
              <div class="activity-icon">
                <component :is="getActivityIconComponent(activity.type)" class="ph-activity-icon" />
              </div>
              <div class="activity-content">
                <div class="activity-title">{{ activity.title }}</div>
                <div class="activity-time">{{ formatTime(activity.timestamp) }}</div>
              </div>
              <div class="activity-status">
                <span :class="['status-badge', activity.status]">
                  {{ activity.status }}
                </span>
              </div>
            </div>
          </div>
          <div class="section-footer">
            <RouterLink to="/activity" class="view-all-link">View All Activity</RouterLink>
          </div>
        </div>

        <div class="section-card">
          <h2>Recently Added</h2>
          <div class="episodes-list">
            <div 
              v-for="audiobook in recentlyAdded" 
              :key="audiobook.id"
              class="episode-item"
            >
              <div class="episode-poster">
                <img :src="audiobook.poster || '/placeholder-poster.jpg'" :alt="audiobook.title" loading="lazy" />
              </div>
              <div class="episode-info">
                <h4>{{ safeText(audiobook.title) }}</h4>
                <p>by {{ safeText(audiobook.author) }}</p>
                <div class="episode-meta">
                  <span>{{ audiobook.duration }}</span>
                  <span>{{ safeText(audiobook.narrator) }}</span>
                </div>
              </div>
            </div>
          </div>
          <div class="section-footer">
            <RouterLink to="/audiobooks" class="view-all-link">View All Audiobooks</RouterLink>
          </div>
        </div>
      </div>

      <div class="sidebar-section">
        <div class="section-card">
          <h2>Quick Actions</h2>
          <div class="quick-actions">
            <RouterLink to="/add-new" class="action-button primary">
              <PhPlus />
              Add New Audiobook
            </RouterLink>
            <RouterLink to="/add-new" class="action-button">
              <PhMagnifyingGlass />
              <span>Search</span>
            </RouterLink>
            <RouterLink to="/library-import" class="action-button">
              <PhFolder />
              Import Library
            </RouterLink>
            <RouterLink to="/wanted" class="action-button">
              <PhWarning />
              Manual Search
            </RouterLink>
          </div>
        </div>

        <div class="section-card">
          <h2>System Health</h2>
          <div class="health-items">
            <div class="health-item">
              <span class="health-label">API</span>
              <span class="health-status healthy">Operational</span>
            </div>
            <div class="health-item">
              <span class="health-label">Download Clients</span>
              <span class="health-status warning">1 Issue</span>
            </div>
            <div class="health-item">
              <span class="health-label">Disk Space</span>
              <span class="health-status healthy">91% Free</span>
            </div>
            <div class="health-item">
              <span class="health-label">External APIs</span>
              <span class="health-status healthy">All Connected</span>
            </div>
          </div>
          <div class="section-footer">
            <RouterLink to="/system" class="view-all-link">View System Status</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { safeText } from '@/utils/textUtils'
import {
  PhPlus,
  PhMagnifyingGlass,
  PhFolder,
  PhWarning,
  PhDownloadSimple,
  PhActivity,
  PhFileText
} from '@phosphor-icons/vue'

// Sample dashboard data
const audiobookStats = ref({
  total: 15,
  monitored: 12
})

const downloadStats = ref({
  active: 3,
  completed: 8
})

const wantedStats = ref({
  missing: 12,
  cutoffUnmet: 4
})

const storageStats = ref({
  used: '45.2 GB',
  total: '500 GB',
  percentage: 9
})

const recentActivity = ref([
  {
    id: '1',
    type: 'download',
    title: 'Downloaded: The Hobbit by J.R.R. Tolkien',
    status: 'completed',
    timestamp: new Date(Date.now() - 5 * 60 * 1000)
  },
  {
    id: '2',
    type: 'download',
    title: 'Downloading: Dune by Frank Herbert',
    status: 'downloading',
    timestamp: new Date(Date.now() - 2 * 60 * 1000)
  },
  {
    id: '3',
    type: 'import',
    title: 'Imported: Foundation Series Collection',
    status: 'completed',
    timestamp: new Date(Date.now() - 30 * 60 * 1000)
  }
])

const recentlyAdded = ref([
  {
    id: '1',
    title: 'The Name of the Wind',
    author: 'Patrick Rothfuss',
    narrator: 'Nick Podehl',
    duration: '27h 55m',
    addedDate: new Date(Date.now() - 24 * 60 * 60 * 1000),
    poster: '/placeholder-audiobook.jpg'
  },
  {
    id: '2',
    title: 'Mistborn: The Final Empire',
    author: 'Brandon Sanderson',
    narrator: 'Michael Kramer',
    duration: '24h 39m',
    addedDate: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000),
    poster: '/placeholder-audiobook.jpg'
  }
])

const getActivityIconComponent = (type: string) => {
  switch (type) {
    case 'download':
      return PhDownloadSimple
    case 'import':
      return PhFolder
    case 'search':
      return PhMagnifyingGlass
    case 'metadata':
      return PhFileText
    default:
      return PhActivity
  }
}

const formatTime = (timestamp: Date): string => {
  const now = new Date()
  const diff = now.getTime() - timestamp.getTime()
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) return `${hours}h ago`
  if (minutes > 0) return `${minutes}m ago`
  return 'Just now'
}

// const formatDate = (date: Date): string => {
//   return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
// }
</script>

<style scoped>
.dashboard {
  padding: 0;
}

.dashboard-header {
  margin-bottom: 2rem;
}

.dashboard-header h1 {
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 2rem;
}

.dashboard-header p {
  margin: 0;
  color: #ccc;
  font-size: 1rem;
}

.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
}

.stats-card {
  background-color: #2a2a2a;
  padding: 1.5rem;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 1rem;
  transition: background-color 0.2s;
}

.stats-card:hover {
  background-color: #333;
}

.stat-icon {
  font-size: 2rem;
  opacity: 0.8;
}

.stat-content h3 {
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 1rem;
  font-weight: 600;
}

.stat-number {
  font-size: 1.8rem;
  font-weight: bold;
  color: #007acc;
  margin-bottom: 0.25rem;
}

.stat-content p {
  margin: 0;
  color: #999;
  font-size: 0.8rem;
}

.dashboard-content {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 2rem;
}

.main-section,
.sidebar-section {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.section-card {
  background-color: #2a2a2a;
  border-radius: 8px;
  padding: 1.5rem;
}

.section-card h2 {
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.2rem;
  font-weight: 600;
}

.activity-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.activity-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: #3a3a3a;
  border-radius: 6px;
}

.activity-icon {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #555;
  border-radius: 50%;
  color: #ccc;
}
/* size svg icons inside activity */
.ph-activity-icon svg {
  width: 18px;
  height: 18px;
}

.activity-content {
  flex: 1;
}

.activity-title {
  color: white;
  font-weight: 500;
  margin-bottom: 0.25rem;
}

.activity-time {
  color: #999;
  font-size: 0.8rem;
}

.status-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: bold;
  text-transform: uppercase;
}

.status-badge.completed {
  background-color: #27ae60;
  color: white;
}

.status-badge.downloading {
  background-color: #007acc;
  color: white;
}

.episodes-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.episode-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: #3a3a3a;
  border-radius: 6px;
}

.episode-poster {
  width: 40px;
  height: 60px;
  background-color: #555;
  border-radius: 4px;
  overflow: hidden;
}

.episode-poster img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.episode-info h4 {
  margin: 0 0 0.25rem 0;
  color: white;
  font-size: 0.9rem;
}

.episode-info p {
  margin: 0 0 0.25rem 0;
  color: #ccc;
  font-size: 0.8rem;
}

.episode-meta {
  display: flex;
  gap: 1rem;
  color: #999;
  font-size: 0.75rem;
}

.quick-actions {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.action-button {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #3a3a3a;
  color: white;
  text-decoration: none;
  border-radius: 6px;
  transition: all 0.2s;
  font-weight: 500;
}

.action-button:hover {
  background-color: #444;
  transform: translateY(-2px);
}

.action-button.primary {
  background-color: #007acc;
}

.action-button.primary:hover {
  background-color: #005fa3;
}

.health-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.health-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background-color: #3a3a3a;
  border-radius: 4px;
}

.health-label {
  color: white;
  font-size: 0.9rem;
}

.health-status {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: bold;
}

.health-status.healthy {
  background-color: #27ae60;
  color: white;
}

.health-status.warning {
  background-color: #f39c12;
  color: white;
}

.section-footer {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #3a3a3a;
}

.view-all-link {
  color: #007acc;
  text-decoration: none;
  font-size: 0.9rem;
  font-weight: 500;
}

.view-all-link:hover {
  text-decoration: underline;
}

/* Icons */
.icon-plus::before { content: '+'; }
.icon-search::before { content: '🔍'; }
.icon-import::before { content: '📁'; }
.icon-wanted::before { content: '⚠️'; }
.icon-download::before { content: '📥'; }
.icon-metadata::before { content: '📋'; }
.icon-activity::before { content: '⚡'; }

@media (max-width: 768px) {
  .dashboard-content {
    grid-template-columns: 1fr;
  }
  
  .dashboard-grid {
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  }
}
</style>
