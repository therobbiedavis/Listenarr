<template>
  <div class="system-view">
    <div class="page-header">
      <h1>System</h1>
      <div class="system-actions">
        <button class="btn btn-secondary" @click="refreshStatus">
          <i class="icon-refresh"></i>
          Refresh
        </button>
      </div>
    </div>

    <div class="system-status">
      <div class="status-grid">
        <div class="status-card">
          <div class="status-header">
            <h3>API Status</h3>
            <span :class="['status-indicator', apiStatus.status]">
              {{ apiStatus.status }}
            </span>
          </div>
          <div class="status-details">
            <p>Version: {{ apiStatus.version }}</p>
            <p>Uptime: {{ apiStatus.uptime }}</p>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <h3>Download Clients</h3>
            <span :class="['status-indicator', downloadClientsStatus.status]">
              {{ downloadClientsStatus.connected }}/{{ downloadClientsStatus.total }}
            </span>
          </div>
          <div class="status-details">
            <div v-for="client in downloadClients" :key="client.name" class="client-status">
              <span>{{ client.name }}</span>
              <span :class="['client-indicator', client.status]">{{ client.status }}</span>
            </div>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <h3>Storage</h3>
            <span class="status-indicator">{{ storageInfo.used }}/{{ storageInfo.total }}</span>
          </div>
          <div class="status-details">
            <div class="storage-bar">
              <div class="storage-fill" :style="{ width: storageInfo.percentage + '%' }"></div>
            </div>
            <p>Free: {{ storageInfo.free }}</p>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <h3>External APIs</h3>
            <span :class="['status-indicator', externalApis.status]">
              {{ externalApis.connected }}/{{ externalApis.total }}
            </span>
          </div>
          <div class="status-details">
            <div v-for="api in externalApis.apis" :key="api.name" class="client-status">
              <span>{{ api.name }}</span>
              <span :class="['client-indicator', api.status]">{{ api.status }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="system-sections">
      <div class="section">
        <h2>System Information</h2>
        <div class="info-grid">
          <div class="info-item">
            <label>Operating System</label>
            <span>{{ systemInfo.os }}</span>
          </div>
          <div class="info-item">
            <label>Runtime</label>
            <span>{{ systemInfo.runtime }}</span>
          </div>
          <div class="info-item">
            <label>Memory Usage</label>
            <span>{{ systemInfo.memory }}</span>
          </div>
          <div class="info-item">
            <label>CPU Usage</label>
            <span>{{ systemInfo.cpu }}</span>
          </div>
        </div>
      </div>

      <div class="section">
        <h2>Recent Logs</h2>
        <div class="logs-container">
          <div 
            v-for="log in recentLogs" 
            :key="log.id"
            :class="['log-entry', log.level]"
          >
            <span class="log-time">{{ formatLogTime(log.timestamp) }}</span>
            <span class="log-level">{{ log.level.toUpperCase() }}</span>
            <span class="log-message">{{ log.message }}</span>
          </div>
        </div>
        <div class="logs-actions">
          <button class="btn btn-secondary" @click="viewFullLogs">
            View Full Logs
          </button>
          <button class="btn btn-secondary" @click="downloadLogs">
            Download Logs
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const apiStatus = ref({
  status: 'healthy',
  version: '1.0.0',
  uptime: '2 days, 14 hours'
})

const downloadClientsStatus = ref({
  status: 'warning',
  connected: 1,
  total: 2
})

const downloadClients = ref([
  { name: 'qBittorrent', status: 'connected' },
  { name: 'SABnzbd', status: 'disconnected' }
])

const storageInfo = ref({
  used: '45.2 GB',
  total: '500 GB',
  free: '454.8 GB',
  percentage: 9
})

const externalApis = ref({
  status: 'healthy',
  connected: 3,
  total: 3,
  apis: [
    { name: 'Audnex.us', status: 'connected' },
    { name: 'Podcast Index', status: 'connected' },
    { name: 'iTunes API', status: 'connected' }
  ]
})

const systemInfo = ref({
  os: 'Windows 11 22H2',
  runtime: '.NET 7.0.5',
  memory: '156 MB / 8 GB',
  cpu: '2.1%'
})

const recentLogs = ref([
  {
    id: '1',
    timestamp: new Date(Date.now() - 2 * 60 * 1000),
    level: 'info',
    message: 'Download completed: The Joe Rogan Experience #1984'
  },
  {
    id: '2',
    timestamp: new Date(Date.now() - 5 * 60 * 1000),
    level: 'warning',
    message: 'SABnzbd connection failed, retrying in 30 seconds'
  },
  {
    id: '3',
    timestamp: new Date(Date.now() - 8 * 60 * 1000),
    level: 'info',
    message: 'Metadata updated for "Serial" podcast'
  },
  {
    id: '4',
    timestamp: new Date(Date.now() - 12 * 60 * 1000),
    level: 'error',
    message: 'Failed to download episode: Connection timeout'
  }
])

const formatLogTime = (timestamp: Date): string => {
  return timestamp.toLocaleTimeString()
}

const refreshStatus = () => {
  console.log('Refreshing system status')
}

const viewFullLogs = () => {
  console.log('Opening full logs')
}

const downloadLogs = () => {
  console.log('Downloading logs')
}
</script>

<style scoped>
.system-view {
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

.system-actions {
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
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.status-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
}

.status-card {
  background-color: #2a2a2a;
  border-radius: 8px;
  padding: 1.5rem;
}

.status-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.status-header h3 {
  margin: 0;
  color: white;
  font-size: 1.1rem;
}

.status-indicator {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: bold;
  text-transform: uppercase;
}

.status-indicator.healthy {
  background-color: #27ae60;
  color: white;
}

.status-indicator.warning {
  background-color: #f39c12;
  color: white;
}

.status-indicator.error {
  background-color: #e74c3c;
  color: white;
}

.status-details {
  color: #ccc;
  font-size: 0.9rem;
}

.client-status {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.client-indicator {
  padding: 0.15rem 0.5rem;
  border-radius: 8px;
  font-size: 0.7rem;
  font-weight: bold;
}

.client-indicator.connected {
  background-color: #27ae60;
  color: white;
}

.client-indicator.disconnected {
  background-color: #e74c3c;
  color: white;
}

.storage-bar {
  width: 100%;
  height: 8px;
  background-color: #555;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.storage-fill {
  height: 100%;
  background-color: #007acc;
  transition: width 0.3s;
}

.system-sections {
  display: grid;
  gap: 2rem;
}

.section {
  background-color: #2a2a2a;
  border-radius: 8px;
  padding: 1.5rem;
}

.section h2 {
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.3rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.info-item label {
  color: #999;
  font-size: 0.8rem;
  text-transform: uppercase;
  font-weight: 600;
}

.info-item span {
  color: white;
  font-weight: 500;
}

.logs-container {
  max-height: 300px;
  overflow-y: auto;
  margin-bottom: 1rem;
  border: 1px solid #3a3a3a;
  border-radius: 4px;
}

.log-entry {
  display: grid;
  grid-template-columns: auto auto 1fr;
  gap: 1rem;
  padding: 0.5rem 1rem;
  border-bottom: 1px solid #3a3a3a;
  font-family: 'Courier New', monospace;
  font-size: 0.8rem;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-entry.info {
  background-color: rgba(39, 174, 96, 0.1);
}

.log-entry.warning {
  background-color: rgba(243, 156, 18, 0.1);
}

.log-entry.error {
  background-color: rgba(231, 76, 60, 0.1);
}

.log-time {
  color: #999;
}

.log-level {
  font-weight: bold;
}

.log-level.INFO {
  color: #27ae60;
}

.log-level.WARNING {
  color: #f39c12;
}

.log-level.ERROR {
  color: #e74c3c;
}

.log-message {
  color: #ccc;
}

.logs-actions {
  display: flex;
  gap: 1rem;
}

.icon-refresh::before { content: '🔄'; }
</style>