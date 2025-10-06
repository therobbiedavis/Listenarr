<template>
  <div class="activity-view">
    <div class="page-header">
      <h1>Activity</h1>
      <div class="activity-actions">
        <button class="btn btn-secondary" @click="refreshActivity">
          <i class="icon-refresh"></i>
          Refresh
        </button>
        <button class="btn btn-secondary" @click="clearHistory">
          <i class="icon-clear"></i>
          Clear History
        </button>
      </div>
    </div>

    <div class="activity-filters">
      <div class="filter-tabs">
        <button 
          v-for="tab in filterTabs" 
          :key="tab.value"
          :class="['tab', { active: selectedTab === tab.value }]"
          @click="selectedTab = tab.value"
        >
          {{ tab.label }}
          <span v-if="tab.count > 0" class="tab-badge">{{ tab.count }}</span>
        </button>
      </div>
    </div>

    <div class="activity-list">
      <div 
        v-for="activity in filteredActivities" 
        :key="activity.id"
        class="activity-item"
        :class="activity.type"
      >
        <div class="activity-icon">
          <i :class="getActivityIcon(activity.type)"></i>
        </div>
        <div class="activity-content">
          <div class="activity-title">{{ activity.title }}</div>
          <div class="activity-description">{{ activity.description }}</div>
          <div class="activity-details">
            <span class="activity-time">{{ formatTime(activity.timestamp) }}</span>
            <span v-if="activity.source" class="activity-source">{{ activity.source }}</span>
          </div>
        </div>
        <div class="activity-status">
          <span :class="['status-badge', activity.status]">
            {{ activity.status }}
          </span>
        </div>
        <div class="activity-actions">
          <button 
            v-if="activity.type === 'download' && activity.status === 'downloading'"
            class="btn-icon"
            @click="cancelDownload(activity)"
            title="Cancel Download"
          >
            <i class="icon-cancel"></i>
          </button>
          <button 
            v-if="activity.type === 'download' && activity.status === 'failed'"
            class="btn-icon"
            @click="retryDownload(activity)"
            title="Retry Download"
          >
            <i class="icon-retry"></i>
          </button>
        </div>
      </div>
    </div>

    <div class="empty-state" v-if="filteredActivities.length === 0">
      <div class="empty-icon">📊</div>
      <h2>No Activity</h2>
      <p>Activity will appear here as downloads and imports are processed.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Activity {
  id: string
  type: 'download' | 'import' | 'search' | 'metadata' | 'error'
  title: string
  description: string
  status: 'completed' | 'downloading' | 'failed' | 'processing' | 'queued'
  timestamp: Date
  source?: string
  progress?: number
}

const selectedTab = ref('all')

const filterTabs = [
  { label: 'All', value: 'all', count: 0 },
  { label: 'Downloads', value: 'download', count: 5 },
  { label: 'Imports', value: 'import', count: 2 },
  { label: 'Errors', value: 'error', count: 1 },
]

// Sample activity data
const activities = ref<Activity[]>([
  {
    id: '1',
    type: 'download',
    title: 'The Hobbit',
    description: 'Downloaded audiobook: "The Hobbit" by J.R.R. Tolkien',
    status: 'completed',
    timestamp: new Date(Date.now() - 5 * 60 * 1000),
    source: 'Audible',
    progress: 100
  },
  {
    id: '2',
    type: 'download',
    title: 'Dune',
    description: 'Downloading audiobook: "Dune" by Frank Herbert',
    status: 'downloading',
    timestamp: new Date(Date.now() - 2 * 60 * 1000),
    source: 'LibriVox',
    progress: 65
  },
  {
    id: '3',
    type: 'import',
    title: 'Foundation Series Collection',
    description: 'Imported 7 audiobooks from local library',
    status: 'completed',
    timestamp: new Date(Date.now() - 30 * 60 * 1000),
    source: 'Library Import'
  },
  {
    id: '4',
    type: 'error',
    title: 'Failed Download',
    description: 'The Martian by Andy Weir - Connection timeout',
    status: 'failed',
    timestamp: new Date(Date.now() - 45 * 60 * 1000),
    source: 'Torrents'
  },
  {
    id: '5',
    type: 'metadata',
    title: 'Metadata Update',
    description: 'Updated audiobook information for "The Name of the Wind"',
    status: 'completed',
    timestamp: new Date(Date.now() - 60 * 60 * 1000),
    source: 'Audnex.us'
  },
  {
    id: '6',
    type: 'search',
    title: 'Search Completed',
    description: 'Found 18 results for "Brandon Sanderson"',
    status: 'completed',
    timestamp: new Date(Date.now() - 75 * 60 * 1000),
    source: 'Multiple APIs'
  }
])

const filteredActivities = computed(() => {
  if (selectedTab.value === 'all') {
    return activities.value
  }
  return activities.value.filter(activity => {
    if (selectedTab.value === 'error') {
      return activity.status === 'failed'
    }
    return activity.type === selectedTab.value
  })
})

const getActivityIcon = (type: string): string => {
  const icons = {
    download: 'icon-download',
    import: 'icon-import',
    search: 'icon-search',
    metadata: 'icon-metadata',
    error: 'icon-error'
  }
  return icons[type as keyof typeof icons] || 'icon-activity'
}

const formatTime = (timestamp: Date): string => {
  const now = new Date()
  const diff = now.getTime() - timestamp.getTime()
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)

  if (days > 0) return `${days}d ago`
  if (hours > 0) return `${hours}h ago`
  if (minutes > 0) return `${minutes}m ago`
  return 'Just now'
}

const refreshActivity = () => {
  console.log('Refresh activity')
}

const clearHistory = () => {
  console.log('Clear history')
}

const cancelDownload = (activity: Activity) => {
  console.log('Cancel download:', activity.title)
}

const retryDownload = (activity: Activity) => {
  console.log('Retry download:', activity.title)
}
</script>

<style scoped>
.activity-view {
  padding: 0;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.page-header h1 {
  margin: 0;
  color: white;
  font-size: 2rem;
}

.activity-actions {
  display: flex;
  gap: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: background-color 0.2s;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover {
  background-color: #666;
}

.btn-icon {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: all 0.2s;
}

.btn-icon:hover {
  background-color: #3a3a3a;
  color: white;
}

.activity-filters {
  margin-bottom: 2rem;
}

.filter-tabs {
  display: flex;
  gap: 0.5rem;
  border-bottom: 1px solid #3a3a3a;
}

.tab {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  padding: 1rem 1.5rem;
  border-radius: 4px 4px 0 0;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.tab:hover {
  background-color: #2a2a2a;
  color: white;
}

.tab.active {
  background-color: #007acc;
  color: white;
}

.tab-badge {
  background-color: rgba(255, 255, 255, 0.2);
  border-radius: 10px;
  padding: 0.2rem 0.5rem;
  font-size: 0.75rem;
  font-weight: bold;
}

.activity-list {
  space-y: 1rem;
}

.activity-item {
  display: flex;
  align-items: center;
  padding: 1rem;
  background-color: #2a2a2a;
  border-radius: 8px;
  border-left: 4px solid #555;
  margin-bottom: 1rem;
  transition: all 0.2s;
}

.activity-item:hover {
  background-color: #333;
}

.activity-item.download {
  border-left-color: #007acc;
}

.activity-item.import {
  border-left-color: #27ae60;
}

.activity-item.search {
  border-left-color: #f39c12;
}

.activity-item.metadata {
  border-left-color: #9b59b6;
}

.activity-item.error {
  border-left-color: #e74c3c;
}

.activity-icon {
  margin-right: 1rem;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #3a3a3a;
  border-radius: 50%;
  color: #ccc;
}

.activity-content {
  flex: 1;
}

.activity-title {
  color: white;
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.activity-description {
  color: #ccc;
  font-size: 0.875rem;
  margin-bottom: 0.5rem;
}

.activity-details {
  display: flex;
  gap: 1rem;
  font-size: 0.8rem;
  color: #999;
}

.activity-status {
  margin: 0 1rem;
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

.status-badge.failed {
  background-color: #e74c3c;
  color: white;
}

.status-badge.processing {
  background-color: #f39c12;
  color: white;
}

.status-badge.queued {
  background-color: #555;
  color: white;
}

.activity-actions {
  display: flex;
  gap: 0.5rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.empty-state h2 {
  color: white;
  margin-bottom: 1rem;
}

/* Icons */
.icon-refresh::before { content: '🔄'; }
.icon-clear::before { content: '🗑️'; }
.icon-download::before { content: '📥'; }
.icon-import::before { content: '📁'; }
.icon-search::before { content: '🔍'; }
.icon-metadata::before { content: '📋'; }
.icon-error::before { content: '❌'; }
.icon-activity::before { content: '⚡'; }
.icon-cancel::before { content: '✖️'; }
.icon-retry::before { content: '🔄'; }
</style>