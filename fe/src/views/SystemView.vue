<template>
  <div class="system-view">
    <div class="page-header-with-actions">
      <div class="settings-header">
        <h1>
          <i class="ph ph-cpu"></i>
          System
        </h1>
        <p>Monitor system health, resources, and service status</p>
      </div>
      <div class="page-actions">
        <button class="add-button" @click="refreshStatus" :disabled="loading">
          <i :class="loading ? 'ph ph-spinner ph-spin' : 'ph ph-arrow-clockwise'"></i>
          {{ loading ? 'Refreshing...' : 'Refresh' }}
        </button>
      </div>
    </div>

    <div class="system-status">
      <div v-if="error" class="error-message">
        <i class="ph ph-warning"></i>
        {{ error }}
      </div>

      <div v-if="loading && !systemInfo" class="loading-state">
        <i class="ph ph-spinner ph-spin"></i>
        Loading system information...
      </div>

      <div v-else class="status-grid">
        <div class="status-card">
          <div class="status-header">
            <div class="card-title">
              <i class="ph ph-check-circle"></i>
              <h3>API Status</h3>
            </div>
            <span :class="['status-badge', apiStatus.status]">
              {{ apiStatus.status }}
            </span>
          </div>
          <div class="status-details">
            <div class="detail-row">
              <i class="ph ph-code"></i>
              <span class="label">Version:</span>
              <span class="value">{{ apiStatus.version }}</span>
            </div>
            <div class="detail-row">
              <i class="ph ph-clock"></i>
              <span class="label">Uptime:</span>
              <span class="value">{{ apiStatus.uptime }}</span>
            </div>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <div class="card-title">
              <i class="ph ph-download"></i>
              <h3>Download Clients</h3>
            </div>
            <span :class="['status-badge', downloadClientsStatus.status]">
              {{ downloadClientsStatus.connected }}/{{ downloadClientsStatus.total }}
            </span>
          </div>
          <div class="status-details">
            <div v-if="downloadClients.length === 0" class="empty-message">
              <i class="ph ph-info"></i>
              <span>No download clients configured</span>
            </div>
            <div v-else>
              <div v-for="client in downloadClients" :key="client.name" class="client-status">
                <i :class="client.status === 'connected' ? 'ph ph-check-circle success' : 'ph ph-x-circle error'"></i>
                <span class="client-name">{{ client.name }}</span>
                <span :class="['client-indicator', client.status]">{{ client.status }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <div class="card-title">
              <i class="ph ph-hard-drives"></i>
              <h3>Storage</h3>
            </div>
            <span v-if="storageInfo" class="status-badge">
              {{ storageInfo.usedFormatted }}/{{ storageInfo.totalFormatted }}
            </span>
            <span v-else class="status-badge">
              <i class="ph ph-spinner ph-spin"></i>
            </span>
          </div>
          <div v-if="storageInfo" class="status-details">
            <div class="storage-bar">
              <div 
                class="storage-fill" 
                :style="{ width: storageInfo.usedPercentage + '%' }"
                :class="{ warning: storageInfo.usedPercentage > 80, danger: storageInfo.usedPercentage > 90 }"
              ></div>
            </div>
            <div class="detail-row">
              <i class="ph ph-folder-open"></i>
              <span class="label">Free:</span>
              <span class="value">{{ storageInfo.freeFormatted }}</span>
            </div>
            <div class="detail-row">
              <i class="ph ph-chart-bar"></i>
              <span class="label">Used:</span>
              <span class="value">{{ storageInfo.usedPercentage.toFixed(1) }}%</span>
            </div>
          </div>
        </div>

        <div class="status-card">
          <div class="status-header">
            <div class="card-title">
              <i class="ph ph-cloud"></i>
              <h3>External APIs</h3>
            </div>
            <span :class="['status-badge', externalApis.status]">
              {{ externalApis.connected }}/{{ externalApis.total }}
            </span>
          </div>
          <div class="status-details">
            <div v-if="externalApis.apis.length === 0" class="empty-message">
              <i class="ph ph-info"></i>
              <span>No API sources configured</span>
            </div>
            <div v-else>
              <div v-for="api in externalApis.apis" :key="api.name" class="client-status">
                <i :class="api.status === 'connected' ? 'ph ph-check-circle success' : 'ph ph-x-circle error'"></i>
                <span class="client-name">{{ api.name }}</span>
                <span :class="['client-indicator', api.status]">{{ api.status }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="system-sections">
      <div class="section">
        <div class="section-header">
          <h2>
            <i class="ph ph-info"></i>
            System Information
          </h2>
        </div>
        <div v-if="systemInfo" class="info-grid">
          <div class="info-card">
            <i class="ph ph-desktop-tower"></i>
            <div class="info-content">
              <label>Operating System</label>
              <span>{{ systemInfo.operatingSystem }}</span>
            </div>
          </div>
          <div class="info-card">
            <i class="ph ph-code"></i>
            <div class="info-content">
              <label>Runtime</label>
              <span>{{ systemInfo.runtime }}</span>
            </div>
          </div>
          <div class="info-card">
            <i class="ph ph-memory"></i>
            <div class="info-content">
              <label>Memory Usage</label>
              <span>{{ systemInfo.memory.usedFormatted }} / {{ systemInfo.memory.totalFormatted }}</span>
              <span class="percentage">({{ systemInfo.memory.usedPercentage.toFixed(1) }}%)</span>
            </div>
          </div>
          <div class="info-card">
            <i class="ph ph-cpu"></i>
            <div class="info-content">
              <label>CPU Usage</label>
              <span>{{ systemInfo.cpu.usagePercentage.toFixed(1) }}%</span>
              <span class="percentage">({{ systemInfo.cpu.processorCount }} cores)</span>
            </div>
          </div>
        </div>
        <div v-else class="loading-state">
          <i class="ph ph-spinner ph-spin"></i>
          <p>Loading system information...</p>
        </div>
      </div>

      <div class="section">
        <div class="section-header">
          <h2>
            <i class="ph ph-file-text"></i>
            Recent Logs
          </h2>
          <div class="section-actions">
            <button class="icon-button" @click="viewFullLogs" title="View Full Logs">
              <i class="ph ph-eye"></i>
            </button>
            <button class="icon-button" @click="downloadLogs" title="Download Logs">
              <i class="ph ph-download-simple"></i>
            </button>
          </div>
        </div>
        <div class="logs-container">
          <div 
            v-for="log in recentLogs" 
            :key="log.id"
            :class="['log-entry', log.level]"
          >
            <i :class="getLogIcon(log.level)"></i>
            <span class="log-time">{{ formatLogTime(log.timestamp) }}</span>
            <span class="log-level">{{ log.level.toUpperCase() }}</span>
            <span class="log-message">{{ log.message }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getSystemInfo, getStorageInfo, getServiceHealth, getLogs, downloadLogs as downloadLogsApi } from '@/services/api'
import type { SystemInfo, StorageInfo, ServiceHealth, LogEntry } from '@/types'

const router = useRouter()

// Real data from API
const systemInfo = ref<SystemInfo | null>(null)
const storageInfo = ref<StorageInfo | null>(null)
const serviceHealth = ref<ServiceHealth | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

// Computed values for easier template access
const apiStatus = ref({
  status: 'unknown' as string,
  version: '',
  uptime: ''
})

const downloadClientsStatus = ref({
  status: 'unknown' as string,
  connected: 0,
  total: 0
})

const downloadClients = ref<Array<{ name: string; status: string }>>([])

const externalApis = ref({
  status: 'unknown' as string,
  connected: 0,
  total: 0,
  apis: [] as Array<{ name: string; status: string }>
})

const recentLogs = ref<LogEntry[]>([])

// Load all system data
const loadSystemData = async () => {
  loading.value = true
  error.value = null
  
  try {
    // Load all data in parallel
    const [sysInfo, storage, health, logs] = await Promise.all([
      getSystemInfo(),
      getStorageInfo(),
      getServiceHealth(),
      getLogs(10) // Get last 10 logs for recent logs section
    ])
    
    systemInfo.value = sysInfo
    storageInfo.value = storage
    serviceHealth.value = health
    
    // Update API status
    apiStatus.value = {
      status: health.status,
      version: health.version,
      uptime: health.uptime
    }
    
    // Update download clients status
    downloadClientsStatus.value = {
      status: health.downloadClients.status,
      connected: health.downloadClients.connected,
      total: health.downloadClients.total
    }
    
    downloadClients.value = health.downloadClients.clients.map(client => ({
      name: client.name,
      status: client.status
    }))
    
    // Update external APIs status
    externalApis.value = {
      status: health.externalApis.status,
      connected: health.externalApis.connected,
      total: health.externalApis.total,
      apis: health.externalApis.apis.map(api => ({
        name: api.name,
        status: api.status
      }))
    }
    
    // Update logs
    recentLogs.value = logs
    
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load system data'
    console.error('Error loading system data:', err)
  } finally {
    loading.value = false
  }
}

const formatLogTime = (timestamp: string): string => {
  const date = new Date(timestamp)
  return date.toLocaleTimeString()
}

const getLogIcon = (level: string): string => {
  switch (level.toLowerCase()) {
    case 'info':
      return 'ph ph-info'
    case 'warning':
      return 'ph ph-warning'
    case 'error':
      return 'ph ph-x-circle'
    case 'success':
      return 'ph ph-check-circle'
    default:
      return 'ph ph-info'
  }
}

const refreshStatus = async () => {
  console.log('Refreshing system status')
  await loadSystemData()
}

const viewFullLogs = () => {
  router.push('/logs')
}

const downloadLogs = () => {
  downloadLogsApi()
}

// Load data on mount
onMounted(() => {
  loadSystemData()
})
</script>

<style scoped>
.system-view {
  padding: 2rem;
  max-width: 1400px;
  margin: 0 auto;
}

/* Page Header with Actions - For Right-Aligned Button */
.page-header-with-actions {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 2rem;
  margin-bottom: 2rem;
}

/* Header Styles - Matching Settings Page */
.settings-header {
  margin-bottom: 0;
}

.settings-header h1 {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 2rem;
  font-weight: 600;
}

.settings-header h1 i {
  font-size: 2rem;
  color: #007acc;
}

.settings-header p {
  color: #999;
  font-size: 1rem;
  margin: 0;
}

/* Page Actions */
.page-actions {
  margin-bottom: 2rem;
}

.add-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1.2rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.add-button:hover:not(:disabled) {
  background: #005a9e;
  transform: translateY(-1px);
}

.add-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.add-button i {
  font-size: 1.1rem;
}

/* Error Message */
.error-message {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 8px;
  color: #e74c3c;
  margin-bottom: 2rem;
}

.error-message i {
  font-size: 1.5rem;
}

/* Loading State */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 3rem;
  color: #999;
}

.loading-state i {
  font-size: 2.5rem;
  color: #007acc;
}

/* Status Grid */
.system-status {
  margin-bottom: 2rem;
}

.status-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.5rem;
}

.status-card {
  background: #1e1e1e;
  border: 1px solid #333;
  border-radius: 12px;
  padding: 1.5rem;
  transition: all 0.2s;
}

.status-card:hover {
  border-color: #444;
  transform: translateY(-2px);
}

.status-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #333;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.card-title i {
  font-size: 1.5rem;
  color: #007acc;
}

.card-title h3 {
  margin: 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.status-badge {
  padding: 0.35rem 0.85rem;
  border-radius: 20px;
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: capitalize;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.status-badge.healthy {
  background: rgba(39, 174, 96, 0.15);
  color: #27ae60;
  border: 1px solid rgba(39, 174, 96, 0.3);
}

.status-badge.warning {
  background: rgba(243, 156, 18, 0.15);
  color: #f39c12;
  border: 1px solid rgba(243, 156, 18, 0.3);
}

.status-badge.error,
.status-badge.unknown {
  background: rgba(231, 76, 60, 0.15);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

/* Status Details */
.status-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  background: #252525;
  border-radius: 6px;
}

.detail-row i {
  font-size: 1.1rem;
  color: #007acc;
  flex-shrink: 0;
}

.detail-row .label {
  color: #999;
  font-size: 0.9rem;
  min-width: 60px;
}

.detail-row .value {
  color: #fff;
  font-weight: 500;
  flex: 1;
}

/* Client/API Status */
.client-status {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #252525;
  border-radius: 6px;
  margin-bottom: 0.5rem;
}

.client-status:last-child {
  margin-bottom: 0;
}

.client-status i {
  font-size: 1.2rem;
}

.client-status i.success {
  color: #27ae60;
}

.client-status i.error {
  color: #e74c3c;
}

.client-name {
  flex: 1;
  color: #fff;
  font-weight: 500;
}

.client-indicator {
  padding: 0.25rem 0.65rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: capitalize;
}

.client-indicator.connected {
  background: rgba(39, 174, 96, 0.15);
  color: #27ae60;
  border: 1px solid rgba(39, 174, 96, 0.3);
}

.client-indicator.disconnected,
.client-indicator.unknown {
  background: rgba(231, 76, 60, 0.15);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

/* Empty Message */
.empty-message {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background: #252525;
  border-radius: 6px;
  color: #999;
  font-style: italic;
}

.empty-message i {
  font-size: 1.2rem;
  color: #666;
}

/* Storage Bar */
.storage-bar {
  width: 100%;
  height: 12px;
  background: #333;
  border-radius: 6px;
  overflow: hidden;
  margin-bottom: 0.75rem;
}

.storage-fill {
  height: 100%;
  background: linear-gradient(90deg, #007acc, #0098ff);
  transition: width 0.5s ease, background 0.3s;
  border-radius: 6px;
}

.storage-fill.warning {
  background: linear-gradient(90deg, #f39c12, #f1c40f);
}

.storage-fill.danger {
  background: linear-gradient(90deg, #e74c3c, #c0392b);
}

/* System Sections */
.system-sections {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.section {
  background: #1e1e1e;
  border: 1px solid #333;
  border-radius: 12px;
  padding: 1.5rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #333;
}

.section-header h2 {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
  font-weight: 600;
}

.section-header h2 i {
  font-size: 1.5rem;
  color: #007acc;
}

.section-actions {
  display: flex;
  gap: 0.5rem;
}

.icon-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  color: #999;
}

.icon-button:hover {
  background: #333;
  border-color: #007acc;
  color: #007acc;
}

.icon-button i {
  font-size: 1.1rem;
}

/* Info Grid */
.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1rem;
}

.info-card {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: 1.25rem;
  background: #252525;
  border: 1px solid #333;
  border-radius: 8px;
  transition: all 0.2s;
}

.info-card:hover {
  border-color: #444;
  background: #2a2a2a;
}

.info-card > i {
  font-size: 2rem;
  color: #007acc;
  flex-shrink: 0;
}

.info-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.info-content label {
  color: #999;
  font-size: 0.85rem;
  text-transform: uppercase;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.info-content span {
  color: #fff;
  font-weight: 500;
  font-size: 1rem;
}

.info-content .percentage {
  color: #007acc;
  font-size: 0.9rem;
  font-weight: 400;
}

/* Logs Container */
.logs-container {
  max-height: 400px;
  overflow-y: auto;
  background: #252525;
  border: 1px solid #333;
  border-radius: 8px;
}

.log-entry {
  display: grid;
  grid-template-columns: auto auto auto 1fr;
  gap: 1rem;
  padding: 0.85rem 1rem;
  border-bottom: 1px solid #2a2a2a;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 0.85rem;
  align-items: center;
  transition: background 0.2s;
}

.log-entry:hover {
  background: #2a2a2a;
}

.log-entry:last-child {
  border-bottom: none;
}

.log-entry.info {
  border-left: 3px solid #007acc;
}

.log-entry.warning {
  border-left: 3px solid #f39c12;
}

.log-entry.error {
  border-left: 3px solid #e74c3c;
}

.log-entry.success {
  border-left: 3px solid #27ae60;
}

.log-entry > i {
  font-size: 1.1rem;
}

.log-entry.info > i {
  color: #007acc;
}

.log-entry.warning > i {
  color: #f39c12;
}

.log-entry.error > i {
  color: #e74c3c;
}

.log-entry.success > i {
  color: #27ae60;
}

.log-time {
  color: #666;
  font-size: 0.8rem;
}

.log-level {
  font-weight: 700;
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  letter-spacing: 0.5px;
}

.log-entry.info .log-level {
  background: rgba(0, 122, 204, 0.15);
  color: #007acc;
}

.log-entry.warning .log-level {
  background: rgba(243, 156, 18, 0.15);
  color: #f39c12;
}

.log-entry.error .log-level {
  background: rgba(231, 76, 60, 0.15);
  color: #e74c3c;
}

.log-entry.success .log-level {
  background: rgba(39, 174, 96, 0.15);
  color: #27ae60;
}

.log-message {
  color: #ccc;
}

/* Scrollbar Styles */
.logs-container::-webkit-scrollbar {
  width: 8px;
}

.logs-container::-webkit-scrollbar-track {
  background: #1e1e1e;
  border-radius: 4px;
}

.logs-container::-webkit-scrollbar-thumb {
  background: #444;
  border-radius: 4px;
}

.logs-container::-webkit-scrollbar-thumb:hover {
  background: #555;
}

/* Animations */
.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Responsive */
@media (max-width: 768px) {
  .system-view {
    padding: 1rem;
  }

  .status-grid {
    grid-template-columns: 1fr;
  }

  .info-grid {
    grid-template-columns: 1fr;
  }

  .log-entry {
    grid-template-columns: 1fr;
    gap: 0.5rem;
  }
}
</style>