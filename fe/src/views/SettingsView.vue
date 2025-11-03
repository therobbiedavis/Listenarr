<template>
  <div class="settings-page">
    <div class="settings-header">
      <h1>
        <PhGear />
        Settings
      </h1>
      <p>Configure your APIs, download clients, and application settings</p>
    </div>

    <div class="settings-tabs">
      <button 
        @click="router.push({ hash: '#indexers' })" 
        :class="{ active: activeTab === 'indexers' }"
        class="tab-button"
      >
  <PhListMagnifyingGlass />
        Indexers
      </button>
      <button 
        @click="router.push({ hash: '#apis' })" 
        :class="{ active: activeTab === 'apis' }"
        class="tab-button"
      >
  <PhCloud />
        Metadata Sources
      </button>
      <button 
        @click="router.push({ hash: '#clients' })" 
        :class="{ active: activeTab === 'clients' }"
        class="tab-button"
      >
  <PhDownload />
        Download Clients
      </button>
      <button 
        @click="router.push({ hash: '#quality-profiles' })" 
        :class="{ active: activeTab === 'quality-profiles' }"
        class="tab-button"
      >
  <PhStar />
        Quality Profiles
      </button>
      <button 
        @click="router.push({ hash: '#general' })" 
        :class="{ active: activeTab === 'general' }"
        class="tab-button"
      >
  <PhSliders />
        General Settings
      </button>
      <button 
        @click="router.push({ hash: '#notifications' })" 
        :class="{ active: activeTab === 'notifications' }"
        class="tab-button"
      >
  <PhBell />
        Notifications
      </button>
    </div>

    <div class="settings-content">
      <!-- Indexers Tab -->
      <div v-if="activeTab === 'indexers'" class="tab-content">
        <div class="section-header">
          <h3>Indexers</h3>
          <button @click="showIndexerForm = true" class="add-button">
            <PhPlus />
            Add Indexer
          </button>
        </div>

        <div v-if="indexers.length === 0" class="empty-state">
          <PhListMagnifyingGlass />
          <p>No indexers configured. Add Newznab or Torznab indexers to search for audiobooks.</p>
        </div>

        <div v-else class="indexers-grid">
          <div 
            v-for="indexer in indexers" 
            :key="indexer.id"
            class="indexer-card"
            :class="{ disabled: !indexer.isEnabled }"
          >
            <div class="indexer-header">
              <div class="indexer-info">
                <h4>{{ indexer.name }}</h4>
                <span class="indexer-type" :class="indexer.type.toLowerCase()">
                  {{ indexer.implementation === 'InternetArchive' ? 'DDL' : indexer.type }}
                </span>
              </div>
              <div class="indexer-actions">
                <button 
                  @click="toggleIndexerFunc(indexer.id)" 
                  class="icon-button"
                  :title="indexer.isEnabled ? 'Disable' : 'Enable'"
                >
                  <template v-if="indexer.isEnabled">
                    <PhToggleRight />
                  </template>
                  <template v-else>
                    <PhToggleLeft />
                  </template>
                </button>
                <button 
                  @click="testIndexerFunc(indexer.id)" 
                  class="icon-button"
                  title="Test"
                  :disabled="testingIndexer === indexer.id"
                >
                  <template v-if="testingIndexer === indexer.id">
                    <PhSpinner class="ph-spin" />
                  </template>
                  <template v-else>
                    <PhCheckCircle />
                  </template>
                </button>
                <button 
                  @click="editIndexer(indexer)" 
                  class="icon-button"
                  title="Edit"
                >
                  <PhPencil />
                </button>
                <button 
                  @click="confirmDeleteIndexer(indexer)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <PhTrash />
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <PhLink />
                <span class="detail-label">URL:</span>
                <span class="detail-value">{{ indexer.url }}</span>
              </div>
              <div class="detail-row">
                <PhListChecks />
                <span class="detail-label">Features:</span>
                <div class="feature-badges">
                  <span v-if="indexer.enableRss" class="badge">RSS</span>
                  <span v-if="indexer.enableAutomaticSearch" class="badge">Automatic Search</span>
                  <span v-if="indexer.enableInteractiveSearch" class="badge">Interactive Search</span>
                </div>
              </div>
              <div class="detail-row" v-if="indexer.lastTestedAt">
                <PhClock />
                <span class="detail-label">Last Tested:</span>
                <span class="detail-value" :class="{ success: indexer.lastTestSuccessful, error: indexer.lastTestSuccessful === false }">
                  {{ formatDate(indexer.lastTestedAt) }}
                  <template v-if="indexer.lastTestSuccessful">
                    <PhCheckCircle class="success" />
                  </template>
                  <template v-else-if="indexer.lastTestSuccessful === false">
                    <PhXCircle class="error" />
                  </template>
                </span>
              </div>
              <div class="detail-row error-row" v-if="indexer.lastTestError">
                <PhWarning />
                <span class="detail-value error">{{ indexer.lastTestError }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Metadata Sources Tab -->
      <div v-if="activeTab === 'apis'" class="tab-content">
        <div class="section-header">
          <h3>Metadata Sources</h3>
          <button @click="showApiForm = true" class="add-button">
            <PhPlus />
            Add Metadata Source
          </button>
        </div>

        <div v-if="configStore.apiConfigurations.length === 0" class="empty-state">
          <PhCloudSlash />
          <p>No metadata sources configured. Add one to enrich audiobook information.</p>
        </div>

        <div v-else class="config-list">
          <div 
            v-for="api in configStore.apiConfigurations" 
            :key="api.id"
            class="config-card"
            :class="{ disabled: !api.isEnabled }"
          >
            <div class="config-info">
              <h4>{{ api.name }}</h4>
              <p class="config-url">{{ api.baseUrl }}</p>
              <div class="config-meta">
                <span class="config-type">{{ api.type.toUpperCase() }}</span>
                <span class="config-status" :class="{ enabled: api.isEnabled }">
                  <component :is="api.isEnabled ? PhCheckCircle : PhXCircle" :class="api.isEnabled ? 'success' : 'error'" />
                  {{ api.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
                <span class="config-priority">
                  <PhArrowUp />
                  Priority: {{ api.priority }}
                </span>
              </div>
            </div>
              <div class="config-actions">
              <button 
                @click="toggleApiConfig(api)" 
                class="icon-button"
                :title="api.isEnabled ? 'Disable' : 'Enable'"
              >
                <template v-if="api.isEnabled">
                  <PhToggleRight />
                </template>
                <template v-else>
                  <PhToggleLeft />
                </template>
              </button>
              <button @click="editApiConfig(api)" class="icon-button" title="Edit">
                <PhPencil />
              </button>
              <button @click="deleteApiConfig(api.id)" class="icon-button danger" title="Delete">
                <PhTrash />
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Download Clients Tab -->
      <div v-if="activeTab === 'clients'" class="tab-content">
        <div class="section-header">
          <h3>Download Clients</h3>
          <button @click="showClientForm = true; editingClient = null" class="add-button">
            <PhPlus />
            Add Download Client
          </button>
        </div>

        <div v-if="configStore.downloadClientConfigurations.length === 0" class="empty-state">
          <PhDownloadSimple />
          <p>No download clients configured. Add qBittorrent, Transmission, SABnzbd, or NZBGet to download audiobooks.</p>
        </div>

        <div v-else class="indexers-grid">
          <div 
            v-for="client in configStore.downloadClientConfigurations" 
            :key="client.id"
            class="indexer-card"
            :class="{ disabled: !client.isEnabled }"
          >
            <div class="indexer-header">
              <div class="indexer-info">
                <h4>{{ client.name }}</h4>
                <span class="indexer-type" :class="getClientTypeClass(client.type)">
                  {{ client.type }}
                </span>
              </div>
              <div class="indexer-actions">
                <button 
                  @click="editClientConfig(client)" 
                  class="icon-button"
                  title="Edit"
                >
                  <PhPencil />
                </button>
                <button
                  @click="testClient(client)"
                  class="icon-button"
                  title="Test"
                  :disabled="testingClient === client.id"
                >
                  <template v-if="testingClient === client.id">
                    <PhSpinner class="ph-spin" />
                  </template>
                  <template v-else>
                    <PhCheckCircle />
                  </template>
                </button>
                <button 
                  @click="confirmDeleteClient(client)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <PhTrash />
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <PhLink />
                <span class="detail-label">Host:</span>
                <span class="detail-value">{{ client.host }}:{{ client.port }}</span>
              </div>
              <div class="detail-row">
                <PhShieldCheck />
                <span class="detail-label">Security:</span>
                <div class="feature-badges">
                  <span class="badge" v-if="client.useSSL">
                    <PhLock /> SSL
                  </span>
                  <span class="badge" v-else>
                    <PhLockOpen /> No SSL
                  </span>
                </div>
              </div>
              <div class="detail-row">
                <PhFolder />
                <span class="detail-label">Download Path:</span>
                <span class="detail-value">{{ client.downloadPath || '(client local)' }}</span>
              </div>
              <div class="detail-row">
                <PhCheckCircle />
                <span class="detail-label">Status:</span>
                <span class="detail-value" :class="{ success: client.isEnabled, error: !client.isEnabled }">
                  {{ client.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- Remote Path Mappings Section -->
        <div class="section-header" style="margin-top: 2rem;">
          <h3>Remote Path Mappings</h3>
          <button @click="openMappingForm()" class="add-button">
            <PhPlus />
            Add Mapping
          </button>
        </div>

        <div v-if="remotePathMappings.length === 0" class="empty-state">
          <PhLinkSimple />
          <p>No remote path mappings configured. Add a mapping to translate client remote paths to local paths the server can access.</p>
        </div>

        <div v-else class="config-list">
          <div v-for="mapping in remotePathMappings" :key="mapping.id" class="config-card">
            <div class="config-info">
              <h4>{{ mapping.name || mapping.remotePath }}</h4>
              <div class="detail-row">
                <PhBrowser />
                <span class="detail-label">Remote Path:</span>
                <span class="detail-value">{{ mapping.remotePath }}</span>
              </div>
              <div class="detail-row">
                <PhFolder />
                <span class="detail-label">Local Path:</span>
                <span class="detail-value">{{ mapping.localPath }}</span>
              </div>
            </div>
            <div class="config-actions">
              <button @click="editMapping(mapping)" class="edit-button" title="Edit">
                <PhPencil />
              </button>
              <button @click="deleteMapping(mapping.id)" class="delete-button" title="Delete">
                <PhTrash />
              </button>
            </div>
          </div>
        </div>

        <!-- Remote Path Mapping Modal -->
        <div v-if="showMappingForm" class="modal-overlay" @click="closeMappingForm()">
          <div class="modal-content" @click.stop>
            <div class="modal-header">
              <h3>{{ mappingToEdit ? 'Edit' : 'Add' }} Remote Path Mapping</h3>
              <button @click="closeMappingForm()" class="modal-close"><PhX /></button>
            </div>
            <div class="modal-body">
              <div class="form-group">
                <label>Mapping Name (optional)</label>
                <input v-model="mappingToEditData.name" type="text" placeholder="Friendly name for this mapping" />
              </div>
              <div class="form-group">
                <label>Download Client</label>
                <select v-model="mappingToEditData.downloadClientId">
                  <option v-for="c in configStore.downloadClientConfigurations" :key="c.id" :value="c.id">{{ c.name }} ({{ c.type }})</option>
                </select>
              </div>
              <div class="form-group">
                <label>Remote Path (from client)</label>
                <input v-model="mappingToEditData.remotePath" type="text" placeholder="/path/to/complete/downloads" />
              </div>
              <div class="form-group">
                <label>Local Path (server)</label>
                <FolderBrowser v-model="mappingToEditData.localPath" placeholder="Select a local path..." />
              </div>
            </div>
            <div class="modal-actions">
              <button @click="closeMappingForm()" class="cancel-button">Cancel</button>
              <button @click="saveMapping()" class="save-button"><PhCheck /> Save</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Quality Profiles Tab -->
      <div v-if="activeTab === 'quality-profiles'" class="tab-content">
        <div class="section-header">
          <h3>Quality Profiles</h3>
          <button @click="openQualityProfileForm()" class="add-button">
            <PhPlus />
            Add Quality Profile
          </button>
        </div>

        <!-- Empty State -->
        <div v-if="qualityProfiles.length === 0" class="empty-state">
          <PhStar class="empty-icon" />
          <p>No quality profiles configured yet.</p>
          <p class="empty-help">Quality profiles define which release qualities you want to download and prefer.</p>
        </div>

        <!-- Quality Profiles Grid -->
        <div v-else class="profiles-grid">
          <div v-for="profile in qualityProfiles" :key="profile.id" class="profile-card">
            <div class="profile-header">
              <div class="profile-title-section">
                <div class="profile-name-row">
                  <h4>{{ profile.name }}</h4>
                  <span v-if="profile.isDefault" class="status-badge default">
                    <PhCheckCircle />
                    Default
                  </span>
                </div>
                <p v-if="profile.description" class="profile-description">{{ profile.description }}</p>
              </div>
              <div class="profile-actions">
                <button @click="editProfile(profile)" class="icon-button" title="Edit Profile">
                  <PhPencil />
                </button>
                <button 
                  v-if="!profile.isDefault"
                  @click="setDefaultProfile(profile)" 
                  class="icon-button" 
                  title="Set as Default"
                >
                  <PhStar />
                </button>
                <button 
                  @click="confirmDeleteProfile(profile)" 
                  class="icon-button danger" 
                  :disabled="profile.isDefault"
                  :title="profile.isDefault ? 'Cannot delete default profile' : 'Delete Profile'"
                >
                  <PhTrash />
                </button>
              </div>
            </div>

            <div class="profile-content">
              <!-- Qualities Section -->
              <div v-if="profile.qualities && profile.qualities.filter(q => q.allowed).length > 0" class="profile-section">
                <h5><PhCheckSquare /> Allowed Qualities</h5>
                <div class="quality-badges">
                  <span 
                    v-for="quality in profile.qualities.filter(q => q.allowed).sort((a, b) => b.priority - a.priority)"
                    :key="quality.quality"
                    class="quality-badge"
                    :class="{ 'is-cutoff': quality.quality === profile.cutoffQuality }"
                  >
                    {{ quality.quality }}
                      <template v-if="quality.quality === profile.cutoffQuality">
                        <PhScissors title="Cutoff Quality" />
                      </template>
                  </span>
                </div>
              </div>

              <!-- Preferences Section -->
              <div v-if="profile.preferredFormats?.length || profile.preferredLanguages?.length" class="profile-section">
                <h5><PhSliders /> Preferences</h5>
                <div class="preferences-grid">
                  <div v-if="profile.preferredFormats && profile.preferredFormats.length > 0" class="preference-item">
                    <span class="preference-label">Formats</span>
                    <span class="preference-value">{{ profile.preferredFormats.join(', ') }}</span>
                  </div>
                  <div v-if="profile.preferredLanguages && profile.preferredLanguages.length > 0" class="preference-item">
                    <span class="preference-label">Languages</span>
                    <span class="preference-value">{{ profile.preferredLanguages.join(', ') }}</span>
                  </div>
                </div>
              </div>

              <!-- Limits Section -->
              <div v-if="profile.minimumSize || profile.maximumSize || (profile.minimumSeeders && profile.minimumSeeders > 0) || (profile.maximumAge && profile.maximumAge > 0)" class="profile-section">
                <h5><PhListChecks /> Limits & Requirements</h5>
                <div class="limits-grid">
                  <div v-if="profile.minimumSize || profile.maximumSize" class="limit-item">
                    <PhRuler />
                    <span class="limit-label">Size</span>
                    <span class="limit-value">
                      {{ profile.minimumSize || '0' }} - {{ profile.maximumSize || '∞' }} MB
                    </span>
                  </div>
                  <div v-if="profile.minimumSeeders && profile.minimumSeeders > 0" class="limit-item">
                    <PhUsers />
                    <span class="limit-label">Seeders</span>
                    <span class="limit-value">{{ profile.minimumSeeders }}+ required</span>
                  </div>
                  <div v-if="profile.maximumAge && profile.maximumAge > 0" class="limit-item">
                    <PhClock />
                    <span class="limit-label">Max Age</span>
                    <span class="limit-value">{{ profile.maximumAge }} days</span>
                  </div>
                </div>
              </div>

              <!-- Word Filters Section -->
              <div v-if="profile.preferredWords?.length || profile.mustContain?.length || profile.mustNotContain?.length" class="profile-section">
                <h5><PhTextAa /> Word Filters</h5>
                <div class="word-filters">
                  <div v-if="profile.preferredWords && profile.preferredWords.length > 0" class="word-filter-group">
                    <span class="filter-type">
                      <PhSparkle />
                      Preferred
                    </span>
                    <div class="word-tags">
                      <span v-for="word in profile.preferredWords" :key="word" class="word-tag positive">
                        {{ word }}
                      </span>
                    </div>
                  </div>
                  <div v-if="profile.mustContain && profile.mustContain.length > 0" class="word-filter-group">
                    <span class="filter-type">
                      <PhCheck />
                      Required
                    </span>
                    <div class="word-tags">
                      <span v-for="word in profile.mustContain" :key="word" class="word-tag required">
                        {{ word }}
                      </span>
                    </div>
                  </div>
                  <div v-if="profile.mustNotContain && profile.mustNotContain.length > 0" class="word-filter-group">
                    <span class="filter-type">
                      <PhX />
                      Forbidden
                    </span>
                    <div class="word-tags">
                      <span v-for="word in profile.mustNotContain" :key="word" class="word-tag forbidden">
                        {{ word }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- General Settings Tab -->
      <div v-if="activeTab === 'general'" class="tab-content">
        <div class="section-header">
          <h3>General Settings</h3>
          <button @click="saveSettings" :disabled="configStore.isLoading || !isFormValid" class="save-button" :title="!isFormValid ? 'Please fix invalid fields before saving' : ''">
            <template v-if="configStore.isLoading">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhFloppyDisk />
            </template>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>
        <div v-if="validationErrors.length > 0" class="error-summary" role="alert">
          <strong>Please fix the following:</strong>
          <ul>
            <li v-for="(e, idx) in validationErrors" :key="idx">{{ e }}</li>
          </ul>
        </div>

        <div v-if="settings" class="settings-form">
          <div class="form-section">
            <h4><PhFolder /> File Management</h4>
            
            <div class="form-group">
              <label>Root Folder / Output Path</label>
              <FolderBrowser 
                v-model="settings.outputPath" 
                placeholder="Select a folder for audiobooks..."
                inputDataCy="output-path"
              />
              <span class="form-help">Root folder where downloaded audiobooks will be saved. This must be set before adding audiobooks.</span>
            </div>

            <div class="form-group">
              <label>File Naming Pattern</label>
              <input v-model="settings.fileNamingPattern" type="text" placeholder="{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}">
              <span class="form-help">
                Pattern for organizing audiobook files. Available variables:<br>
                <code>{Author}</code> - Author/narrator name<br>
                <code>{Series}</code> - Series name<br>
                <code>{Title}</code> - Book title<br>
                <code>{SeriesNumber}</code> - Position in series<br>
                <code>{DiskNumber}</code> or <code>{DiskNumber:00}</code> - Disk/part number (00 = zero-padded)<br>
                <code>{ChapterNumber}</code> or <code>{ChapterNumber:00}</code> - Chapter number (00 = zero-padded)<br>
                <code>{Year}</code> - Publication year<br>
                <code>{Quality}</code> - Audio quality
              </span>
            </div>

            <div class="form-group">
              <label>Completed File Action</label>
              <select v-model="settings.completedFileAction">
                <option value="Move">Move (default)</option>
                <option value="Copy">Copy</option>
              </select>
              <span class="form-help">Choose whether completed downloads should be moved into the library output path or copied and left in the client's folder.</span>
            </div>
          </div>

          <div class="form-section">
            <h4><PhLink /> API Configuration</h4>
            
            <div class="form-group">
              <label>Audnexus API URL</label>
              <input v-model="settings.audnexusApiUrl" type="text" placeholder="https://api.audnex.us">
              <span class="form-help">API endpoint for audiobook metadata</span>
            </div>
          </div>

          <div class="form-section">
            <h4><PhDownload /> Download Settings</h4>
            
            <div class="form-group">
              <label>Max Concurrent Downloads</label>
              <input v-model.number="settings.maxConcurrentDownloads" type="number" min="1" max="10">
              <span class="form-help">Maximum number of simultaneous downloads (1-10)</span>
            </div>

            <div class="form-group">
              <label>Polling Interval (seconds)</label>
              <input v-model.number="settings.pollingIntervalSeconds" type="number" min="10" max="300">
              <span class="form-help">How often to check download status (10-300 seconds)</span>
            </div>
          </div>

          <div class="form-section">
            <h4><PhToggleLeft /> Features</h4>
            
            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableMetadataProcessing" type="checkbox">
                <span>
                  <strong>Enable Metadata Processing</strong>
                  <small>Automatically fetch and embed audiobook metadata</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableCoverArtDownload" type="checkbox">
                <span>
                  <strong>Enable Cover Art Download</strong>
                  <small>Download and embed cover art for audiobooks</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableNotifications" type="checkbox">
                <span>
                  <strong>Enable Notifications</strong>
                  <small>Receive notifications for downloads and events</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.showCompletedExternalDownloads" type="checkbox">
                <span>
                  <strong>Show completed external downloads in Activity</strong>
                  <small>When enabled, completed torrents/NZBs from external clients will remain visible in the Activity view. When disabled, completed external items will be hidden to reduce clutter.</small>
                </span>
              </label>
            </div>
          </div>

          <div class="form-section">
            <h4><PhGlobe /> External Requests / US Proxy
              <button type="button" class="info-inline" @click.prevent="openProxySecurityModal" title="Security recommendations">
                <PhInfo />
              </button>
            </h4>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.preferUsDomain" type="checkbox">
                <span>
                  <strong>Prefer US (.com) domain for Audible/Amazon</strong>
                  <small>When enabled, the server will attempt a retry using the US (.com) domain if a localized or redirect page is detected.</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.useUsProxy" type="checkbox">
                <span>
                  <strong>Use HTTP proxy for US requests</strong>
                  <small>When enabled, Audible/Amazon retries to the US domain will be routed through the proxy configured below.</small>
                </span>
              </label>
            </div>

            <div class="form-group">
              <label>US Proxy Host</label>
              <input v-model="settings.usProxyHost" type="text" placeholder="proxy.example.com" :disabled="!settings.useUsProxy" data-cy="us-proxy-host">
              <div v-if="settings.useUsProxy && (!settings.usProxyHost || String(settings.usProxyHost).trim() === '')" class="form-error">Proxy host is required when using a proxy.</div>
            </div>

            <div class="form-group">
              <label>US Proxy Port</label>
              <input v-model.number="settings.usProxyPort" type="number" min="1" max="65535" :disabled="!settings.useUsProxy" data-cy="us-proxy-port">
              <div v-if="settings.useUsProxy && (!settings.usProxyPort || Number(settings.usProxyPort) <= 0)" class="form-error">Proxy port must be between 1 and 65535.</div>
            </div>

            <div class="form-group">
              <label>US Proxy Username (optional)</label>
              <input v-model="settings.usProxyUsername" type="text" placeholder="username" :disabled="!settings.useUsProxy">
            </div>

              <div class="form-group">
              <label>US Proxy Password (optional)</label>
              <div class="password-field">
                <input :type="showPassword ? 'text' : 'password'" v-model="settings.usProxyPassword" placeholder="Proxy password" class="admin-input password-input" :disabled="!settings.useUsProxy" />
                <button type="button" class="password-toggle" @click.prevent="toggleShowPassword" :aria-pressed="showPassword as unknown as boolean" :title="showPassword ? 'Hide password' : 'Show password'">
                  <template v-if="showPassword">
                    <PhEyeSlash />
                  </template>
                  <template v-else>
                    <PhEye />
                  </template>
                </button>
              </div>
              <span class="form-help">Store proxy credentials here for convenience. For production, consider using a secrets manager instead of storing passwords in the application database.</span>
            </div>

            <hr />

            <h4><PhUserCircle /> Authentication</h4>
            
            <div class="form-group">
              <label>Login Screen</label>
              <div class="auth-row">
                <input type="checkbox" id="authToggle" v-model="authEnabled" />
                <label for="authToggle">Enable login screen</label>
              </div>
              <span class="form-help">Toggle to enable the login screen. This setting reflects the server's <code>AuthenticationRequired</code> value from <code>config.json</code>. Changes here are local and will not modify server files — edit <code>config/config.json</code> on the host to persist.</span>
            </div>

            <div v-if="authEnabled" class="form-group">
              <label>Admin Account Management</label>
              <div class="admin-credentials">
                <input v-model="settings.adminUsername" type="text" placeholder="Admin username" class="admin-input" />
                <div class="password-field">
                  <input :type="showPassword ? 'text' : 'password'" v-model="settings.adminPassword" placeholder="New admin password" class="admin-input password-input" />
                  <button type="button" class="password-toggle" @click.prevent="showPassword = !showPassword" :aria-pressed="showPassword as unknown as boolean" :title="showPassword ? 'Hide password' : 'Show password'">
                    <template v-if="showPassword">
                      <PhEyeSlash />
                    </template>
                    <template v-else>
                      <PhEye />
                    </template>
                  </button>
                </div>
              </div>
              <span class="form-help">Manage the admin account. Enter a new password to update the admin user's password when you save settings. The username field shows the current admin username. This section is only available when authentication is enabled.</span>
            </div>

            <div class="form-group">
              <label>API Key (Server)</label>
              <div class="input-group">
                <input type="text" :value="startupConfig?.apiKey || ''" disabled class="input-group-input" />
                <div class="input-group-append">
                  <button
                    type="button"
                    class="icon-button input-group-btn"
                    :class="{ 'copied': copiedApiKey }"
                    @click="copyApiKey"
                    :disabled="!startupConfig?.apiKey"
                    title="Copy API key"
                  >
                    <template v-if="copiedApiKey">
                      <PhCheck />
                    </template>
                    <template v-else>
                      <PhFiles />
                    </template>
                  </button>
                  <button
                    type="button"
                    class="regenerate-button input-group-btn"
                    @click="regenerateApiKey"
                    :disabled="loadingApiKey"
                    :title="startupConfig?.apiKey ? 'Regenerate API key' : 'Generate API key'"
                  >
                    <template v-if="loadingApiKey">
                      <PhSpinner class="ph-spin" />
                    </template>
                    <template v-else-if="startupConfig?.apiKey">
                      <PhArrowCounterClockwise />
                    </template>
                    <template v-else>
                      <PhPlus />
                    </template>
                    <span v-if="!loadingApiKey">{{ startupConfig?.apiKey ? 'Regenerate' : 'Generate' }}</span>
                  </button>
                </div>
              </div>
              <span class="form-help">An API key can be used to authenticate sensitive API calls from trusted clients. Keep it secret. Keep it safe. Regenerating will replace the existing key.</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Notifications Tab -->
      <div v-if="activeTab === 'notifications'" class="tab-content">
        <div class="section-header">
          <h3>Notification Settings</h3>
          <button @click="saveSettings" :disabled="configStore.isLoading || (activeTab !== 'notifications' && !isFormValid)" class="save-button" :title="!isFormValid ? 'Please fix invalid fields before saving' : ''">
            <template v-if="configStore.isLoading">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhFloppyDisk />
            </template>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>

        <div v-if="settings" class="settings-form">
          <div class="form-section">
            <h4><PhBell /> Webhook Configuration</h4>
            
            <div class="form-group">
              <label>Webhook URL</label>
              <div class="input-group">
                <input v-model="settings.webhookUrl" type="text" placeholder="https://hooks.slack.com/services/..." class="input-group-input">
                <div class="input-group-append">
                  <button 
                    @click="testNotification" 
                    :disabled="!settings.webhookUrl || settings.webhookUrl.trim() === '' || testingNotification"
                    class="input-group-btn test-button"
                    :title="!settings.webhookUrl || settings.webhookUrl.trim() === '' ? 'Enter a webhook URL first' : 'Send test notification'"
                  >
                    <template v-if="testingNotification">
                      <PhSpinner class="ph-spin" />
                    </template>
                    <template v-else>
                      <PhPaperPlaneTilt />
                    </template>
                    Test
                  </button>
                </div>
              </div>
              <span class="form-help">URL to send notification events to. Leave empty to disable webhook notifications.</span>
            </div>
          </div>

          <div class="form-section">
            <h4><PhListChecks /> Notification Triggers</h4>
            
            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enabledNotificationTriggers" value="book-added" type="checkbox">
                <span>
                  <strong>Book Added</strong>
                  <small>Send notification when a new audiobook is added to the library</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enabledNotificationTriggers" value="book-downloading" type="checkbox">
                <span>
                  <strong>Book Downloading</strong>
                  <small>Send notification when an audiobook download starts</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enabledNotificationTriggers" value="book-available" type="checkbox">
                <span>
                  <strong>Book Available</strong>
                  <small>Send notification when an audiobook download completes and is ready</small>
                </span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Metadata Source Configuration Modal -->
    <div v-if="showApiForm" class="modal-overlay" @click="closeApiForm">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ editingApi ? 'Edit' : 'Add' }} Metadata Source</h3>
          <button @click="closeApiForm" class="modal-close">
            <PhX />
          </button>
        </div>
        <div class="modal-body">
          <form @submit.prevent="saveApiConfig" class="config-form">
            <div class="form-group">
              <label for="api-name">Name *</label>
              <input 
                id="api-name"
                v-model="apiForm.name" 
                type="text" 
                placeholder="e.g., Audimeta" 
                required
              />
            </div>

            <div class="form-group">
              <label for="api-url">Base URL *</label>
              <input 
                id="api-url"
                v-model="apiForm.baseUrl" 
                type="url" 
                placeholder="https://api.example.com" 
                required
              />
            </div>

            <div class="form-group">
              <label for="api-type">Type *</label>
              <select id="api-type" v-model="apiForm.type" required>
                <option value="metadata">Metadata</option>
                <option value="search">Search</option>
                <option value="other">Other</option>
              </select>
            </div>

            <div class="form-group">
              <label for="api-key">API Key</label>
              <input 
                id="api-key"
                v-model="apiForm.apiKey" 
                type="password" 
                placeholder="Optional API key"
              />
              <small>Leave empty if not required</small>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label for="api-priority">Priority</label>
                <input 
                  id="api-priority"
                  v-model.number="apiForm.priority" 
                  type="number" 
                  min="1" 
                  max="100"
                />
                <small>Lower numbers = higher priority</small>
              </div>

              <div class="form-group">
                <label for="api-rate-limit">Rate Limit (per minute)</label>
                <input 
                  id="api-rate-limit"
                  v-model="apiForm.rateLimitPerMinute" 
                  type="number" 
                  min="0"
                  placeholder="0 = unlimited"
                />
              </div>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input 
                  v-model="apiForm.isEnabled" 
                  type="checkbox"
                />
                <span>Enable this metadata source</span>
              </label>
            </div>
          </form>
        </div>
        <div class="modal-actions">
          <button @click="closeApiForm" class="cancel-button" type="button">
            <PhX />
            Cancel
          </button>
          <button @click="saveApiConfig" class="save-button" type="button">
            <PhCheck />
            Save
          </button>
        </div>
      </div>
    </div>
  </div>

  <!-- Download Client Form Modal -->
  <DownloadClientFormModal 
    :visible="showClientForm" 
    :editing-client="editingClient"
    @close="showClientForm = false; editingClient = null"
    @saved="configStore.loadDownloadClientConfigurations()"
    @delete="executeDeleteClient"
  />

  <!-- Indexer Form Modal -->
  <IndexerFormModal 
    :visible="showIndexerForm" 
    :editing-indexer="editingIndexer"
    @close="showIndexerForm = false; editingIndexer = null"
    @saved="loadIndexers()"
  />

  <!-- Quality Profile Form Modal -->
  <QualityProfileFormModal
    :visible="showQualityProfileForm"
    :profile="editingQualityProfile"
    @close="showQualityProfileForm = false; editingQualityProfile = null"
    @save="saveQualityProfile"
  />

  <!-- Delete Client Confirmation Modal -->
  <div v-if="clientToDelete" class="modal-overlay" @click="clientToDelete = null">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>
          <PhWarningCircle />
          Delete Download Client
        </h3>
        <button @click="clientToDelete = null" class="modal-close">
          <PhX />
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the download client <strong>{{ clientToDelete.name }}</strong>?</p>
        <p>This action cannot be undone.</p>
      </div>
        <div class="modal-actions">
        <button @click="clientToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button @click="executeDeleteClient()" class="delete-button">
          <PhTrash />
          Delete
        </button>
      </div>
    </div>
  </div>

  <!-- Delete Indexer Confirmation Modal -->
  <div v-if="indexerToDelete" class="modal-overlay" @click="indexerToDelete = null">
    <div class="modal-content" @click.stop>
        <div class="modal-header">
        <h3>
          <PhWarningCircle />
          Delete Indexer
        </h3>
        <button @click="indexerToDelete = null" class="modal-close">
          <PhX />
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the indexer <strong>{{ indexerToDelete.name }}</strong>?</p>
        <p>This action cannot be undone.</p>
      </div>
        <div class="modal-actions">
        <button @click="indexerToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button @click="executeDeleteIndexer" class="delete-button">
          <PhTrash />
          Delete
        </button>
      </div>
    </div>
  </div>

  <!-- Quality Profile Form Modal -->
  <QualityProfileFormModal
    :visible="showQualityProfileForm"
    :profile="editingQualityProfile"
    @close="showQualityProfileForm = false; editingQualityProfile = null"
    @save="saveQualityProfile"
  />

  <!-- Delete Quality Profile Confirmation Modal -->
  <div v-if="profileToDelete" class="modal-overlay" @click="profileToDelete = null">
    <div class="modal-content" @click.stop>
        <div class="modal-header">
        <h3>
          <PhWarningCircle />
          Delete Quality Profile
        </h3>
        <button @click="profileToDelete = null" class="modal-close">
          <PhX />
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the quality profile <strong>{{ profileToDelete.name }}</strong>?</p>
        <p v-if="profileToDelete.isDefault" class="warning-text">
          <PhWarning />
          This is the default profile and cannot be deleted. Please set another profile as default first.
        </p>
        <p>This action cannot be undone.</p>
      </div>
        <div class="modal-actions">
        <button @click="profileToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button 
          @click="executeDeleteProfile" 
          class="delete-button"
          :disabled="profileToDelete.isDefault"
        >
          <PhTrash />
          Delete
        </button>
      </div>
    </div>
  </div>

  <!-- Proxy Security Modal -->
      <div v-if="showProxySecurityModal" class="modal-overlay" @click="closeProxySecurityModal()">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>
          <PhShieldCheck />
          Proxy security recommendations
        </h3>
        <button @click="closeProxySecurityModal()" class="modal-close"><PhX /></button>
      </div>
      <div class="modal-body">
        <p>Storing proxy credentials in the application database is convenient but has security implications. Consider the following:</p>
        <ul>
          <li>Use an OS-level secrets manager (Vault, Azure Key Vault, AWS Secrets Manager) when possible.</li>
          <li>Restrict access to the application database and backups.</li>
          <li>Rotate credentials periodically and prefer short-lived credentials where supported.</li>
          <li>If you must store secrets in the DB, ensure the server is deployed on trusted infrastructure and consider application-level encryption.</li>
        </ul>
        <p>This modal only provides guidance; the current implementation persists the proxy password in ApplicationSettings. For production use, consider integrating a secrets store and referencing credentials instead of storing plaintext.</p>
      </div>
      <div class="modal-actions">
        <button @click="closeProxySecurityModal()" class="save-button">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch, computed } from 'vue'
import { apiService } from '@/services/api'
import { useRoute, useRouter } from 'vue-router'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings, Indexer, QualityProfile, RemotePathMapping } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'
import {
  // Settings & Navigation
  PhGear, PhListMagnifyingGlass, PhCloud, PhDownload, PhStar, PhSliders, PhPlus,
  PhArrowUp, PhDownloadSimple, PhCloudSlash, PhGlobe, PhInfo,
  // Form Controls & Actions
  PhToggleRight, PhToggleLeft, PhSpinner, PhCheckCircle, PhPencil, PhTrash, PhLink,
  PhListChecks, PhClock, PhXCircle, PhCheck, PhX, PhCheckSquare, PhRuler, PhSparkle,
  PhArrowCounterClockwise, PhScissors, PhBell, PhPaperPlaneTilt,
  // Security & Authentication
  PhShieldCheck, PhLock, PhLockOpen, PhWarning, PhWarningCircle,
  // Files & Folders
  PhFolder, PhLinkSimple, PhBrowser, PhFloppyDisk, PhFiles,
  // Users
  PhUsers, PhUserCircle,
  // Misc
  PhTextAa, PhEye, PhEyeSlash
} from '@phosphor-icons/vue'
import IndexerFormModal from '@/components/IndexerFormModal.vue'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'
import QualityProfileFormModal from '@/components/QualityProfileFormModal.vue'
import { useToast } from '@/services/toastService'
import { getIndexers, deleteIndexer, toggleIndexer as apiToggleIndexer, testIndexer as apiTestIndexer, getQualityProfiles, deleteQualityProfile, createQualityProfile, updateQualityProfile, getRemotePathMappings, createRemotePathMapping, updateRemotePathMapping, deleteRemotePathMapping, testDownloadClient as apiTestDownloadClient } from '@/services/api'

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const toast = useToast()
const activeTab = ref<'indexers' | 'apis' | 'clients' | 'quality-profiles' | 'general' | 'notifications'>('indexers')
const showApiForm = ref(false)
const showClientForm = ref(false)
const showIndexerForm = ref(false)
const showQualityProfileForm = ref(false)
const editingApi = ref<ApiConfiguration | null>(null)
const editingClient = ref<DownloadClientConfiguration | null>(null)
const editingIndexer = ref<Indexer | null>(null)
const editingQualityProfile = ref<QualityProfile | null>(null)
const apiForm = reactive({
  id: '',
  name: '',
  baseUrl: '',
  apiKey: '',
  type: 'metadata',
  isEnabled: true,
  priority: 1,
  rateLimitPerMinute: ''
})
const settings = ref<ApplicationSettings | null>(null)
const startupConfig = ref<import('@/types').StartupConfig | null>(null)
const loadingApiKey = ref(false)
const copiedApiKey = ref(false)

const copyApiKey = async () => {
  const key = startupConfig.value?.apiKey
  if (!key) return
  try {
    await navigator.clipboard.writeText(key)
    copiedApiKey.value = true
    setTimeout(() => {
      copiedApiKey.value = false
    }, 2000)
  } catch (err) {
    console.error('Failed to copy API key', err)
    // Could show error notification here if needed
  }
}

const regenerateApiKey = async () => {
  const hasExistingKey = !!(startupConfig.value?.apiKey)
  
  // Different confirmation messages based on whether an API key exists
  const confirmMessage = hasExistingKey 
    ? 'Regenerating the API key will immediately invalidate the existing key. Continue?'
    : 'Generate a new API key for this server instance?'
    
  if (!confirm(confirmMessage)) return
  
  loadingApiKey.value = true
  try {
    let resp: { apiKey: string; message?: string }
    
    // Try initial generation first (for setup scenarios or if no key exists)
    if (!hasExistingKey) {
      try {
        resp = await apiService.generateInitialApiKey()
        startupConfig.value = { ...(startupConfig.value || {}), apiKey: resp.apiKey }
        toast.info('API key', resp.message || 'API key generated - copied to clipboard')
        try { await navigator.clipboard.writeText(resp.apiKey) } catch {}
        return
      } catch (initialErr) {
        // If initial generation fails (e.g., users exist), try authenticated regeneration
        console.debug('Initial API key generation failed, trying authenticated regeneration', initialErr)
      }
    }
    
    // Fall back to authenticated regeneration
    resp = await apiService.regenerateApiKey()
    startupConfig.value = { ...(startupConfig.value || {}), apiKey: resp.apiKey }
    toast.info('API key', 'API key regenerated - copied to clipboard')
    try { await navigator.clipboard.writeText(resp.apiKey) } catch {}
  } catch (err) {
    console.error('Failed to generate/regenerate API key', err)
    // If server returns 401/403, suggest logging in as admin
    const status = (err && typeof err === 'object' && err !== null && 'status' in err) ? (err as { status: number }).status : 0
    if (status === 401 || status === 403) {
      toast.error('Permission denied', 'You must be logged in as an administrator to regenerate the API key. Please login and try again.')
    } else {
      toast.error('API key failed', 'Failed to generate/regenerate API key')
    }
  } finally {
    loadingApiKey.value = false
  }
}
const authEnabled = ref(false)
const indexers = ref<Indexer[]>([])
const qualityProfiles = ref<QualityProfile[]>([])
const remotePathMappings = ref<RemotePathMapping[]>([])
const testingNotification = ref(false)
const testingIndexer = ref<number | null>(null)
const testingClient = ref<string | null>(null)
const indexerToDelete = ref<Indexer | null>(null)
const profileToDelete = ref<QualityProfile | null>(null)
const adminUsers = ref<Array<{ id: number; username: string; email?: string; isAdmin: boolean; createdAt: string }>>([])
  const showPassword = ref(false)
  const showProxySecurityModal = ref(false)
  const isProxyConfigValid = computed(() => {
    if (!settings.value) return true
    if (!settings.value.useUsProxy) return true
    const host = (settings.value.usProxyHost || '').toString().trim()
    const port = Number(settings.value.usProxyPort || 0)
    return host.length > 0 && port > 0 && port <= 65535
  })

  const isFormValid = computed(() => {
    // Required output path
    if (!settings.value) return false
    const outputPathValid = !!(settings.value.outputPath && String(settings.value.outputPath).trim().length > 0)

    return outputPathValid && isProxyConfigValid.value
  })

  const validationErrors = computed(() => {
    const errs: string[] = []
    if (!settings.value) return errs
    if (!settings.value.outputPath || String(settings.value.outputPath).trim().length === 0) errs.push('Output path is required')
    if (settings.value.useUsProxy) {
      const host = (settings.value.usProxyHost || '').toString().trim()
      const port = Number(settings.value.usProxyPort || 0)
      if (!host) errs.push('US proxy host is required when proxy is enabled')
      if (!port || port <= 0 || port > 65535) errs.push('US proxy port must be between 1 and 65535')
    }
    return errs
  })

  // Expose a toggle function for unit tests and template interactions that
  // prefer a method instead of inline assignment. Tests call
  // `wrapper.vm.toggleShowPassword()` so we expose it here.
  const toggleShowPassword = () => {
    showPassword.value = !showPassword.value
  }

  const openProxySecurityModal = () => {
    showProxySecurityModal.value = true
  }

  const closeProxySecurityModal = () => {
    showProxySecurityModal.value = false
  }

  // Make the function available on the component instance for Vue Test Utils
  // and any external consumers that expect an instance method.
  // `defineExpose` is a compiler macro available in <script setup>.
  // eslint-disable-next-line @typescript-eslint/ban-ts-comment
  // @ts-ignore
  defineExpose({ toggleShowPassword })
const showMappingForm = ref(false)
const mappingToEdit = ref<RemotePathMapping | null>(null)

const formatApiError = (error: unknown): string => {
  // Handle axios-style errors
  const axiosError = error as { response?: { data?: unknown; status?: number } }
  if (axiosError.response?.data) {
    const responseData = axiosError.response.data
    let errorMessage = 'An unknown error occurred'
    
    if (typeof responseData === 'string') {
      errorMessage = responseData
    } else if (responseData && typeof responseData === 'object') {
      const data = responseData as Record<string, unknown>
      errorMessage = (data.message as string) || (data.error as string) || JSON.stringify(responseData, null, 2)
    }
    
    // Capitalize first letter and ensure it ends with punctuation
    errorMessage = errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
    if (!errorMessage.match(/[.!?]$/)) {
      errorMessage += '.'
    }
    
    return errorMessage
  }
  
  // Handle native fetch errors (from ApiService)
  const fetchError = error as Error & { status?: number; body?: string }
  if (fetchError.body) {
    try {
      const parsedBody = JSON.parse(fetchError.body)
      if (parsedBody && typeof parsedBody === 'object') {
        const data = parsedBody as Record<string, unknown>
        let errorMessage = (data.message as string) || (data.error as string) || JSON.stringify(parsedBody, null, 2)
        
        // Capitalize first letter and ensure it ends with punctuation
        errorMessage = errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
        if (!errorMessage.match(/[.!?]$/)) {
          errorMessage += '.'
        }
        
        return errorMessage
      }
      return fetchError.body
    } catch {
      return fetchError.body
    }
  }
  
  // Fallback for other error types
  const errorMessage = error instanceof Error ? error.message : String(error)
  return errorMessage.charAt(0).toUpperCase() + errorMessage.slice(1)
}

const editApiConfig = (api: ApiConfiguration) => {
  editingApi.value = api
  apiForm.id = api.id
  apiForm.name = api.name
  apiForm.baseUrl = api.baseUrl
  apiForm.apiKey = api.apiKey || ''
  apiForm.type = api.type
  apiForm.isEnabled = api.isEnabled
  apiForm.priority = api.priority
  apiForm.rateLimitPerMinute = api.rateLimitPerMinute || ''
  showApiForm.value = true
}

const closeApiForm = () => {
  showApiForm.value = false
  editingApi.value = null
  // Reset form
  apiForm.id = ''
  apiForm.name = ''
  apiForm.baseUrl = ''
  apiForm.apiKey = ''
  apiForm.type = 'metadata'
  apiForm.isEnabled = true
  apiForm.priority = 1
  apiForm.rateLimitPerMinute = ''
}

const editClientConfig = (client: DownloadClientConfiguration) => {
  editingClient.value = client
  showClientForm.value = true
}

const deleteApiConfig = async (id: string) => {
  if (confirm('Are you sure you want to delete this API configuration?')) {
    try {
      await configStore.deleteApiConfiguration(id)
      toast.success('API', 'API configuration deleted successfully')
    } catch (error) {
      console.error('Failed to delete API configuration:', error)
      const errorMessage = formatApiError(error)
      toast.error('API delete failed', errorMessage)
    }
  }
}

const toggleApiConfig = async (api: ApiConfiguration) => {
  try {
    // Toggle the enabled state
    const updatedApi = { ...api, isEnabled: !api.isEnabled }
    await configStore.saveApiConfiguration(updatedApi)
    toast.success('Metadata source', `${api.name} ${updatedApi.isEnabled ? 'enabled' : 'disabled'} successfully`)
  } catch (error) {
    console.error('Failed to toggle API configuration:', error)
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

const clientToDelete = ref<DownloadClientConfiguration | null>(null)

const confirmDeleteClient = (client: DownloadClientConfiguration) => {
  clientToDelete.value = client
}

const executeDeleteClient = async (id?: string) => {
  const clientId = id || clientToDelete.value?.id
  if (!clientId) return
  
    try {
    await configStore.deleteDownloadClientConfiguration(clientId)
    toast.success('Download client', 'Download client deleted successfully')
  } catch (error) {
    console.error('Failed to delete download client:', error)
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    clientToDelete.value = null
  }
}

// Test a download client configuration (include credentials in payload)
const testNotification = async () => {
  if (!settings.value?.webhookUrl || settings.value.webhookUrl.trim() === '') {
    toast.error('Test failed', 'Please enter a webhook URL first')
    return
  }

  testingNotification.value = true
  try {
    const response = await apiService.testNotification()
    if (response.success) {
      toast.success('Test notification', response.message || 'Test notification sent successfully')
    } else {
      toast.error('Test failed', response.message || 'Failed to send test notification')
    }
  } catch (error) {
    console.error('Failed to test notification:', error)
    const errorMessage = formatApiError(error)
    toast.error('Test failed', errorMessage)
  } finally {
    testingNotification.value = false
  }
}

const getClientTypeClass = (type: string): string => {
  const typeMap: Record<string, string> = {
    'qbittorrent': 'torrent',
    'transmission': 'torrent',
    'sabnzbd': 'usenet',
    'nzbget': 'usenet'
  }
  return typeMap[type.toLowerCase()] || 'torrent'
}

const saveApiConfig = async () => {
  try {
    // Validate required fields
    if (!apiForm.name || !apiForm.baseUrl) {
      toast.error('Validation error', 'Name and Base URL are required')
      return
    }

    const apiData: ApiConfiguration = {
      id: apiForm.id || crypto.randomUUID(),
      name: apiForm.name,
      baseUrl: apiForm.baseUrl,
      apiKey: apiForm.apiKey,
      type: apiForm.type as 'torrent' | 'nzb' | 'metadata' | 'search' | 'other',
      isEnabled: apiForm.isEnabled,
      priority: apiForm.priority,
      headers: {},
      parameters: {},
      rateLimitPerMinute: apiForm.rateLimitPerMinute || undefined,
      createdAt: editingApi.value?.createdAt || new Date().toISOString(),
      lastUsed: editingApi.value?.lastUsed
    }

    // Use the single save method which handles both create and update
    await configStore.saveApiConfiguration(apiData)
    
    toast.success('Metadata source', `Metadata source ${editingApi.value ? 'updated' : 'added'} successfully`)
    closeApiForm()
  } catch (error) {
    console.error('Failed to save metadata source:', error)
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

const saveSettings = async () => {
  if (!settings.value) return

  // Validate proxy fields if proxy usage is enabled
  if (settings.value.useUsProxy && !isProxyConfigValid.value) {
    toast.error('Invalid proxy', 'Please provide a valid proxy host and port (1-65535) when using a proxy.')
    return
  }

  try {
    // Create a copy of settings, excluding empty admin fields
    const settingsToSave = { ...settings.value }

    // Only include adminUsername if it's not empty
    if (!settingsToSave.adminUsername || settingsToSave.adminUsername.trim() === '') {
      delete settingsToSave.adminUsername
    }

    // Only include adminPassword if it's not empty
    if (!settingsToSave.adminPassword || settingsToSave.adminPassword.trim() === '') {
      delete settingsToSave.adminPassword
    }

    // Only include proxy password if non-empty (we allow empty to clear)
    if (settingsToSave.usProxyPassword === undefined || settingsToSave.usProxyPassword === null) {
      delete settingsToSave.usProxyPassword
    }

    await configStore.saveApplicationSettings(settingsToSave)
    toast.success('Settings', 'Settings saved successfully')
    // If user toggled the authEnabled, attempt to save to startup config
    try {
      const original = startupConfig.value || {}
      // Persist authenticationRequired as string 'true'/'false' so it's explicit and
      // consistent with expectations from the UI (was previously 'Enabled'/'Disabled').
      const newCfg: import('@/types').StartupConfig = { ...original, authenticationRequired: authEnabled.value ? 'true' : 'false' }
          try {
          await apiService.saveStartupConfig(newCfg)
          toast.success('Startup config', 'Startup configuration saved (config.json)')
        } catch {
          // If server can't persist startup config (e.g., permission denied), offer a fallback download of the config JSON
          toast.info('Startup config', 'Could not persist startup config to disk. Preparing downloadable startup config so you can save it manually.')
          try {
            const blob = new Blob([JSON.stringify(newCfg, null, 2)], { type: 'application/json' })
            const url = URL.createObjectURL(blob)
            const a = document.createElement('a')
            a.href = url
            a.download = 'config.json'
            document.body.appendChild(a)
            a.click()
            a.remove()
            URL.revokeObjectURL(url)
            toast.info('Startup config', 'Download started. Save the file to the server config directory to persist the change.')
          } catch {
            toast.info('Startup config', 'Also failed to prepare a download. Edit config/config.json on the host to make the change persistent.')
          }
        }
    } catch {
      // Not fatal - write may not be allowed in some deployments
      toast.info('Startup config', 'Could not persist startup config to disk. Edit config/config.json on the host to make the change persistent.')
    }
  } catch (error) {
    console.error('Failed to save settings:', error)
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

// Indexer functions
const loadIndexers = async () => {
  try {
    indexers.value = await getIndexers()
  } catch (error) {
    console.error('Failed to load indexers:', error)
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

const toggleIndexerFunc = async (id: number) => {
  try {
    const updatedIndexer = await apiToggleIndexer(id)
    const index = indexers.value.findIndex(i => i.id === id)
    if (index !== -1) {
      indexers.value[index] = updatedIndexer
    }
    toast.success('Indexer', `Indexer ${updatedIndexer.isEnabled ? 'enabled' : 'disabled'} successfully`)
  } catch (error) {
    console.error('Failed to toggle indexer:', error)
    const errorMessage = formatApiError(error)
    toast.error('Toggle failed', errorMessage)
  }
}

const testIndexerFunc = async (id: number) => {
  testingIndexer.value = id
  try {
    const result = await apiTestIndexer(id)
    if (result.success) {
      toast.success('Indexer test', `Indexer tested successfully: ${result.message}`)
      // Update the indexer with test results
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    } else {
      const errorMessage = formatApiError({ response: { data: result.error || result.message } })
      toast.error('Indexer test failed', errorMessage)
      // Still update to show failed test status
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    }
  } catch (error) {
    console.error('Failed to test indexer:', error)
    const errorMessage = formatApiError(error)
    toast.error('Indexer test failed', errorMessage)
  } finally {
    testingIndexer.value = null
  }
}

const testClient = async (client: DownloadClientConfiguration) => {
  testingClient.value = client.id
  try {
    const result = await apiTestDownloadClient(client)
    if (result.success) {
      toast.success('Download client test', `Download client tested successfully: ${result.message}`)
      // Update the client with test results
      const index = configStore.downloadClientConfigurations.findIndex(c => c.id === client.id)
      if (index !== -1 && result.client) {
        configStore.downloadClientConfigurations[index] = result.client
      }
    } else {
      const errorMessage = formatApiError({ response: { data: result.message } })
      toast.error('Download client test failed', errorMessage)
      // Still update to show failed test status
      const index = configStore.downloadClientConfigurations.findIndex(c => c.id === client.id)
      if (index !== -1 && result.client) {
        configStore.downloadClientConfigurations[index] = result.client
      }
    }
  } catch (error) {
    console.error('Failed to test download client:', error)
    const errorMessage = formatApiError(error)
    toast.error('Download client test failed', errorMessage)
  } finally {
    testingClient.value = null
  }
}

const editIndexer = (indexer: Indexer) => {
  editingIndexer.value = indexer
  showIndexerForm.value = true
}

const confirmDeleteIndexer = (indexer: Indexer) => {
  indexerToDelete.value = indexer
}

const executeDeleteIndexer = async () => {
  if (!indexerToDelete.value) return
  
  try {
  await deleteIndexer(indexerToDelete.value.id)
  indexers.value = indexers.value.filter(i => i.id !== indexerToDelete.value!.id)
  toast.success('Indexer', 'Indexer deleted successfully')
    } catch (error) {
    console.error('Failed to delete indexer:', error)
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    indexerToDelete.value = null
  }
}

// Quality Profile Functions
const loadQualityProfiles = async () => {
  try {
    qualityProfiles.value = await getQualityProfiles()
  } catch (error) {
    console.error('Failed to load quality profiles:', error)
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

// Remote Path Mappings state
const mappingToEditData = ref<{ downloadClientId: string; remotePath: string; localPath: string; name?: string }>({ downloadClientId: '', remotePath: '', localPath: '', name: '' })

// Remote Path Mappings functions
const openMappingForm = (mapping?: RemotePathMapping) => {
  mappingToEdit.value = mapping || null
  if (mapping) {
    mappingToEditData.value = { ...mapping }
  } else {
    mappingToEditData.value = { downloadClientId: configStore.downloadClientConfigurations[0]?.id || '', remotePath: '', localPath: '', name: '' }
  }
  showMappingForm.value = true
}

const closeMappingForm = () => {
  showMappingForm.value = false
  mappingToEdit.value = null
  mappingToEditData.value = { downloadClientId: '', remotePath: '', localPath: '', name: '' }
}

const saveMapping = async () => {
  try {
    const payload: Omit<RemotePathMapping, 'id' | 'createdAt' | 'updatedAt'> = {
      downloadClientId: mappingToEditData.value.downloadClientId || '',
      remotePath: mappingToEditData.value.remotePath || '',
      localPath: mappingToEditData.value.localPath || '',
      name: mappingToEditData.value.name || ''
    }

      if (mappingToEdit.value && mappingToEdit.value.id) {
      const updated = await updateRemotePathMapping(mappingToEdit.value.id, payload)
      const idx = remotePathMappings.value.findIndex(m => m.id === updated.id)
      if (idx !== -1) remotePathMappings.value[idx] = updated
      toast.success('Remote path mapping', 'Remote path mapping updated')
    } else {
      const created = await createRemotePathMapping(payload)
      remotePathMappings.value.push(created)
      toast.success('Remote path mapping', 'Remote path mapping created')
    }

    closeMappingForm()
  } catch (err) {
    console.error('Failed to save mapping', err)
    toast.error('Save failed', 'Failed to save mapping')
  }
}

const editMapping = (mapping: RemotePathMapping) => openMappingForm(mapping)

const deleteMapping = async (id: number) => {
  if (!confirm('Delete this remote path mapping?')) return
  try {
    await deleteRemotePathMapping(id)
    remotePathMappings.value = remotePathMappings.value.filter(m => m.id !== id)
    toast.success('Remote path mapping', 'Remote path mapping deleted')
  } catch (err) {
    console.error('Failed to delete mapping', err)
    toast.error('Delete failed', 'Failed to delete mapping')
  }
}

const loadAdminUsers = async () => {
  try {
    adminUsers.value = await apiService.getAdminUsers()
  } catch (error) {
    console.error('Failed to load admin users:', error)
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

const openQualityProfileForm = (profile?: QualityProfile) => {
  editingQualityProfile.value = profile || null
  showQualityProfileForm.value = true
}

const editProfile = (profile: QualityProfile) => {
  editingQualityProfile.value = profile
  showQualityProfileForm.value = true
}

const confirmDeleteProfile = (profile: QualityProfile) => {
  profileToDelete.value = profile
}

const executeDeleteProfile = async () => {
  if (!profileToDelete.value) return
  
  try {
    await deleteQualityProfile(profileToDelete.value.id!)
    qualityProfiles.value = qualityProfiles.value.filter(p => p.id !== profileToDelete.value!.id)
    toast.success('Quality profile', 'Quality profile deleted successfully')
  } catch (error: unknown) {
    console.error('Failed to delete quality profile:', error)
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    profileToDelete.value = null
  }
}

const saveQualityProfile = async (profile: QualityProfile) => {
  try {
      if (profile.id) {
      // Update existing profile
      const updated = await updateQualityProfile(profile.id, profile)
      const index = qualityProfiles.value.findIndex(p => p.id === profile.id)
      if (index !== -1) {
        qualityProfiles.value[index] = updated
      }
      toast.success('Quality profile', 'Quality profile updated successfully')
    } else {
      // Create new profile
      const created = await createQualityProfile(profile)
      qualityProfiles.value.push(created)
      toast.success('Quality profile', 'Quality profile created successfully')
    }
    showQualityProfileForm.value = false
    editingQualityProfile.value = null
    } catch (error: unknown) {
    console.error('Failed to save quality profile:', error)
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

const setDefaultProfile = async (profile: QualityProfile) => {
  try {
    // Update all profiles - set this one as default, others as non-default
    const updatedProfile = { ...profile, isDefault: true }
    await updateQualityProfile(profile.id!, updatedProfile)
    
    // Update local state
    qualityProfiles.value = qualityProfiles.value.map(p => ({
      ...p,
      isDefault: p.id === profile.id
    }))
    
    toast.success('Quality profile', `${profile.name} set as default quality profile`)
  } catch (error: unknown) {
    console.error('Failed to set default profile:', error)
    const errorMessage = formatApiError(error)
    toast.error('Set default failed', errorMessage)
  }
}

const formatDate = (dateString: string | undefined): string => {
  if (!dateString) return 'Never'
  const date = new Date(dateString)
  return date.toLocaleString()
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as 'indexers' | 'apis' | 'clients' | 'quality-profiles' | 'general' | 'notifications'
  if (hash && ['indexers', 'apis', 'clients', 'quality-profiles', 'general', 'notifications'].includes(hash)) {
    activeTab.value = hash
  } else {
    // Default to indexers and update URL
    activeTab.value = 'indexers'
    router.replace({ hash: '#indexers' })
  }
}

// Watch for hash changes
watch(() => route.hash, () => {
  syncTabFromHash()
})

// Track which tab data has been loaded to avoid duplicate requests
const loaded = reactive({
  indexers: false,
  apis: false,
  clients: false,
  profiles: false,
  admins: false,
  mappings: false,
  general: false
})

async function loadTabContents(tab: string) {
  try {
    switch (tab) {
      case 'indexers':
        if (!loaded.indexers) {
          await loadIndexers()
          loaded.indexers = true
        }
        break
      case 'apis':
        if (!loaded.apis) {
          await configStore.loadApiConfigurations()
          loaded.apis = true
        }
        break
      case 'clients':
        if (!loaded.clients) {
          await configStore.loadDownloadClientConfigurations()
          loaded.clients = true
        }
        break
      case 'quality-profiles':
        if (!loaded.profiles) {
          await loadQualityProfiles()
          loaded.profiles = true
        }
        break
      case 'general':
        if (!loaded.general) {
          // General needs application settings, admin users and remote path mappings
          await configStore.loadApplicationSettings()
          settings.value = configStore.applicationSettings
          // Ensure sensible default
          if (settings.value && !settings.value.completedFileAction) settings.value.completedFileAction = 'Move'
          // Initialize notification triggers array if not present
          if (settings.value && !settings.value.enabledNotificationTriggers) settings.value.enabledNotificationTriggers = []

          try {
            remotePathMappings.value = await getRemotePathMappings()
            loaded.mappings = true
          } catch (e) {
            console.debug('Failed to load remote path mappings', e)
          }

          try {
            await loadAdminUsers()
            loaded.admins = true
            if (adminUsers.value.length > 0 && settings.value) {
              const firstAdmin = adminUsers.value[0]
              if (firstAdmin) settings.value.adminUsername = firstAdmin.username
            }
          } catch (e) {
            console.debug('Failed to load admin users', e)
          }

          loaded.general = true
        }
        break
      case 'notifications':
        // Notifications are part of general settings
        if (!loaded.general) {
          await loadTabContents('general')
        }
        break
      default:
        // default to indexers
        if (!loaded.indexers) {
          await loadIndexers()
          loaded.indexers = true
        }
    }
  } catch (err) {
    console.error('Failed to load tab contents for', tab, err)
  }
}

onMounted(async () => {
  // Set initial tab from URL hash
  syncTabFromHash()

  // Load only the data needed for the active tab; other tabs load on demand
  await loadTabContents(activeTab.value)

  // Load startup config (optional) to determine AuthenticationRequired — keep this lightweight
  try {
    startupConfig.value = await apiService.getStartupConfig()
    const obj = startupConfig.value as Record<string, unknown> | null
    const raw = obj ? (obj['authenticationRequired'] ?? obj['AuthenticationRequired']) : undefined
    const v = raw as unknown
    authEnabled.value = (typeof v === 'boolean') ? v : (typeof v === 'string' ? (v.toLowerCase() === 'enabled' || v.toLowerCase() === 'true') : false)
  } catch {
    authEnabled.value = false
  }

  // Watch for tab changes and fetch content on-demand
  watch(activeTab, (t) => {
    void loadTabContents(t)
  })
})
</script>

<style scoped>
.settings-page {
  padding: 2rem;
  min-height: 100vh;
  background-color: #1a1a1a;
}

.settings-header {
  margin-bottom: 2rem;
}

.settings-header h1 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 2rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.settings-header h1 i {
  color: #4dabf7;
}

.settings-header p {
  margin: 0;
  color: #adb5bd;
  font-size: 1rem;
  line-height: 1.5;
}

.settings-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid rgba(255, 255, 255, 0.08);
}

.tab-button {
  padding: 1rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: #868e96;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-weight: 500;
  position: relative;
}

.tab-button:hover {
  background-color: rgba(77, 171, 247, 0.08);
  color: #fff;
}

.tab-button.active {
  color: #4dabf7;
  background-color: rgba(77, 171, 247, 0.15);
}

.tab-button.active::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, #4dabf7 0%, #339af0 100%);
  border-radius: 3px 3px 0 0;
}

.settings-content {
  background: #2a2a2a;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-height: 500px;
}

.tab-content {
  padding: 2rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.add-button,
.save-button {
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.add-button:hover,
.save-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.save-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #868e96;
}

.empty-state i {
  font-size: 4rem;
  color: #495057;
  margin-bottom: 1rem;
}

.empty-state p {
  margin: 0;
  font-size: 1.1rem;
  line-height: 1.6;
}

.config-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.config-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  transition: all 0.2s ease;
}

.config-card:hover {
  background-color: #2f2f2f;
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.config-info {
  flex: 1;
  min-width: 0;
}

.config-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.config-url {
  margin: 0 0 1rem 0;
  color: #4dabf7;
  font-family: 'Courier New', monospace;
  font-size: 0.9rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.config-meta {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.config-meta span {
  padding: 0.4rem 0.8rem;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.config-type {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.config-status {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.config-status.enabled {
  background-color: rgba(46, 204, 113, 0.15);
  color: #51cf66;
  border: 1px solid rgba(46, 204, 113, 0.3);
}

.config-priority {
  background-color: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.config-ssl {
  background-color: rgba(127, 140, 141, 0.15);
  color: #95a5a6;
  border: 1px solid rgba(127, 140, 141, 0.3);
}

.config-ssl.enabled {
  background-color: rgba(241, 196, 15, 0.15);
  color: #fcc419;
  border: 1px solid rgba(241, 196, 15, 0.3);
}

.config-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.edit-button,
.delete-button {
  padding: 0.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  font-size: 1.1rem;
}

.edit-button {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
}

.edit-button:hover {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.delete-button {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.4);
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  padding: 1.5rem;
}

.form-section h4 {
  margin: 0 0 1.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.form-section h4 i {
  color: #4dabf7;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.form-group label {
  font-weight: 600;
  color: #fff;
  font-size: 0.95rem;
}

.form-group input[type="text"],
.form-group input[type="number"],
.form-group input[type="url"],
.form-group input[type="password"],
.form-group select {
  padding: 0.75rem;
  background-color: rgba(0, 0, 0, 0.2);
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s ease;
}

.form-group input[type="text"]:focus,
.form-group input[type="number"]:focus,
.form-group input[type="url"]:focus,
.form-group input[type="password"]:focus,
.form-group select:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.15);
}

.form-group small {
  color: #868e96;
  font-size: 0.85rem;
  line-height: 1.5;
}

.checkbox-group {
  margin-bottom: 1rem;
}

.checkbox-group label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: normal;
}

.checkbox-group input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #4dabf7;
}

.form-help {
  font-size: 0.85rem;
  color: #868e96;
  font-style: italic;
  line-height: 1.5;
}

.checkbox-group {
  flex-direction: row;
  align-items: flex-start;
  background-color: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 1rem;
  transition: all 0.2s ease;
}

.checkbox-group:hover {
  background-color: rgba(0, 0, 0, 0.3);
  border-color: rgba(77, 171, 247, 0.2);
}

.checkbox-group:last-child {
  margin-bottom: 0;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  cursor: pointer;
  width: 100%;
}

.checkbox-group input[type="checkbox"] {
  margin: 0.25rem 0 0 0;
  width: 18px;
  height: 18px;
  cursor: pointer;
  flex-shrink: 0;
  accent-color: #4dabf7;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
  font-weight: 600;
}

.checkbox-group label small {
  color: #868e96;
  font-size: 0.85rem;
  font-weight: normal;
  line-height: 1.5;
}

/* Authentication Section Styles */
.auth-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.auth-row input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #4dabf7;
}

.auth-row label {
  color: #fff;
  font-size: 0.95rem;
  font-weight: 500;
  cursor: pointer;
  margin: 0;
}

.admin-credentials {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.admin-input {
  padding: 0.75rem;
  background-color: rgba(0, 0, 0, 0.2);
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s ease;
}

.admin-input:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.15);
}

.admin-input::placeholder {
  color: #6c757d;
  font-style: italic;
}

/* Password field with inline toggle */
.password-field {
  position: relative;
  width: 100%;
}

.password-input {
  width: 100%;
  padding-right: 3.5rem; /* space for the toggle button */
}

.password-toggle {
  position: absolute;
  right: 0.5rem;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #868e96;
  padding: 0.35rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: color 0.2s ease;
}

.password-toggle:hover {
  color: #4dabf7;
}

.form-error {
  color: #ff6b6b;
  font-size: 0.9rem;
  margin-top: 0.4rem;
}

.info-inline {
  background: none;
  border: none;
  color: #74c0fc;
  margin-left: 0.5rem;
  cursor: pointer;
  transition: color 0.2s ease;
}

.info-inline:hover {
  color: #4dabf7;
}

.error-summary {
  margin-top: 1rem;
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.2);
  padding: 0.75rem 1rem;
  border-radius: 6px;
  color: #ff6b6b;
}

.error-summary ul {
  margin: 0.5rem 0 0 1.2rem;
}

.input-group-btn.regenerate-button {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: white;
  border: none;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 500;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.input-group-btn.regenerate-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #c0392b 0%, #a93226 100%);
}

.input-group-btn.regenerate-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Input group styling for API key */
.input-group {
  display: flex;
  align-items: stretch;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  overflow: hidden;
}

.input-group:focus-within {
  border-color: rgba(77, 171, 247, 0.3);
}

.input-group-input {
  flex: 1;
  background: #1a1a1a !important;
  color: #adb5bd;
  padding: 0.75rem 1rem;
  border: none !important;
  border-radius: 0 !important;
  box-shadow: none !important;
}

.input-group-input:focus {
  outline: none;
  background: #1a1a1a !important;
  box-shadow: none !important;
}

.input-group-append {
  display: flex;
  background: rgba(0, 0, 0, 0.3);
}

.input-group-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.05);
  border: none;
  border-radius: 0;
  border-left: 1px solid rgba(255, 255, 255, 0.1);
  color: #868e96;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 1rem;
}

.input-group-btn:first-child {
  border-left: none;
}

.input-group-btn:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.2);
  color: #4dabf7;
}

.input-group-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.input-group-btn.copied {
  background: rgba(81, 207, 102, 0.2) !important;
  color: #51cf66 !important;
}

.input-group-btn.copied:hover {
  background: rgba(81, 207, 102, 0.3) !important;
}

.test-button {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  font-weight: 500;
  transition: all 0.2s ease;
  border: none;
  border-left: 1px solid rgba(0, 0, 0, 0.2);
  border-radius: 0;
  font-size: 0.9rem;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.test-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.test-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.85);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  max-width: 600px;
  width: 90%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.6);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
  font-weight: 600;
}

.modal-close {
  background: none;
  border: none;
  color: #868e96;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.modal-close:hover {
  background-color: rgba(255, 255, 255, 0.08);
  color: #fff;
}

.modal-body {
  padding: 2rem;
  color: #adb5bd;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background-color: rgba(255, 255, 255, 0.08);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.cancel-button:hover {
  background-color: rgba(255, 255, 255, 0.12);
  transform: translateY(-1px);
}

/* Indexer Styles */
.indexers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1.5rem;
  margin-top: 1.5rem;
}

.indexer-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  padding: 1.5rem;
  transition: all 0.2s ease;
}

.indexer-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.indexer-card.disabled {
  opacity: 0.5;
  filter: grayscale(50%);
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.indexer-type {
  display: inline-block;
  padding: 0.3rem 0.75rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.indexer-type.torrent {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.indexer-type.usenet {
  background-color: rgba(33, 150, 243, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.indexer-type.ddl {
  background-color: rgba(155, 89, 182, 0.15);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
}

.icon-button {
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  cursor: pointer;
  color: #adb5bd;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  font-size: 1.1rem;
  width: 36px;
  height: 36px;
}

.icon-button:hover:not(:disabled) {
  background: rgba(77, 171, 247, 0.15);
  border-color: #4dabf7;
  color: #4dabf7;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(77, 171, 247, 0.3);
}

.icon-button.danger {
  color: #ff6b6b;
}

.icon-button.danger:hover:not(:disabled) {
  background: rgba(255, 107, 107, 0.15);
  border-color: #ff6b6b;
  color: #ff6b6b;
  box-shadow: 0 2px 8px rgba(255, 107, 107, 0.3);
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-size: 0.9rem;
}

.detail-row i {
  color: #4dabf7;
  font-size: 1rem;
  flex-shrink: 0;
}

.detail-label {
  color: #868e96;
  min-width: 100px;
}

.detail-value {
  color: #adb5bd;
  word-break: break-all;
}

.detail-value.success {
  color: #51cf66;
}

.detail-value.error {
  color: #ff6b6b;
}

.detail-value i {
  margin-left: 0.5rem;
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.6rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
}

.error-message {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(244, 67, 54, 0.1);
  border: 1px solid rgba(244, 67, 54, 0.2);
  border-radius: 6px;
  color: #ff6b6b;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.error-message i {
  font-size: 1rem;
}

@media (max-width: 768px) {
  .settings-page {
    padding: 1rem;
  }

  .settings-tabs {
    flex-direction: column;
    gap: 0;
  }

  .tab-button {
    border-bottom: 1px solid #333;
    border-left: 3px solid transparent;
    justify-content: flex-start;
  }

  .tab-button.active {
    border-left-color: #007acc;
    border-bottom-color: transparent;
  }

  .config-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
  
  .config-actions {
    width: 100%;
    justify-content: flex-end;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .add-button,
  .save-button {
    width: 100%;
    justify-content: center;
  }

  .indexers-grid {
    grid-template-columns: 1fr;
  }

  .indexer-header {
    flex-direction: column;
    gap: 1rem;
  }

  .indexer-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .detail-label {
    min-width: auto;
  }
}

/* Quality Profile Cards */
.profiles-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(500px, 1fr));
  gap: 1.5rem;
}

.profile-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.profile-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
  transform: translateY(-1px);
}

.profile-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.profile-title-section {
  flex: 1;
}

.profile-name-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.profile-card h4 {
  margin: 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.7rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.default {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.profile-description {
  margin: 0;
  color: #868e96;
  font-size: 0.9rem;
  line-height: 1.5;
}

.profile-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.profile-content {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.profile-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.profile-section h5 {
  margin: 0;
  color: #4dabf7;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.profile-section h5 i {
  font-size: 1rem;
}

/* Quality Badges */
.quality-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.quality-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.4rem 0.75rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}

.quality-badge.is-cutoff {
  background-color: rgba(255, 152, 0, 0.15);
  color: #ff9800;
  border-color: rgba(255, 152, 0, 0.3);
}

.quality-badge i {
  font-size: 0.75rem;
}

/* Preferences Grid */
.preferences-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.preference-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.preference-label {
  color: #868e96;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.preference-value {
  color: #fff;
  font-size: 0.9rem;
}

/* Limits Grid */
.limits-grid {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.limit-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.limit-item i {
  color: #4dabf7;
  font-size: 1.1rem;
}

.limit-label {
  color: #868e96;
  font-size: 0.85rem;
  min-width: 80px;
}

.limit-value {
  color: #fff;
  font-size: 0.9rem;
  font-weight: 500;
}

/* Word Filters */
.word-filters {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.word-filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-type {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #868e96;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.filter-type i {
  font-size: 0.9rem;
}

.word-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.word-tag {
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}

.word-tag.positive {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.word-tag.required {
  background-color: rgba(255, 152, 0, 0.15);
  color: #fcc419;
  border: 1px solid rgba(255, 152, 0, 0.3);
}

.word-tag.forbidden {
  background-color: rgba(244, 67, 54, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(244, 67, 54, 0.3);
}

.warning-text {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(255, 152, 0, 0.1);
  border-left: 3px solid #fcc419;
  color: #fcc419;
  margin: 1rem 0;
  border-radius: 6px;
}

.warning-text i {
  font-size: 1.2rem;
}

@media (max-width: 768px) {
  .settings-page {
    padding: 1rem;
  }

  .settings-tabs {
    flex-direction: column;
    gap: 0;
  }

  .tab-button {
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    border-left: 3px solid transparent;
    justify-content: flex-start;
  }

  .tab-button.active::after {
    display: none;
  }

  .tab-button.active {
    border-left-color: #4dabf7;
    border-bottom-color: transparent;
  }

  .config-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
  
  .config-actions {
    width: 100%;
    justify-content: flex-end;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .add-button,
  .save-button {
    width: 100%;
    justify-content: center;
  }

  .indexers-grid {
    grid-template-columns: 1fr;
  }

  .indexer-header {
    flex-direction: column;
    gap: 1rem;
  }

  .indexer-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .detail-label {
    min-width: auto;
  }

  .profiles-grid {
    grid-template-columns: 1fr;
  }
  
  .profile-header {
    flex-direction: column;
    gap: 1rem;
  }
  
  .profile-actions {
    margin-left: 0;
    width: 100%;
    justify-content: flex-start;
  }
}
</style>