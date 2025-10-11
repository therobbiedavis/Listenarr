<template>
  <div class="settings-page">
    <div class="settings-header">
      <h1>
        <i class="ph ph-gear"></i>
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
        <i class="ph ph-list-magnifying-glass"></i>
        Indexers
      </button>
      <button 
        @click="router.push({ hash: '#apis' })" 
        :class="{ active: activeTab === 'apis' }"
        class="tab-button"
      >
        <i class="ph ph-cloud"></i>
        API Sources
      </button>
      <button 
        @click="router.push({ hash: '#clients' })" 
        :class="{ active: activeTab === 'clients' }"
        class="tab-button"
      >
        <i class="ph ph-download"></i>
        Download Clients
      </button>
      <button 
        @click="router.push({ hash: '#quality-profiles' })" 
        :class="{ active: activeTab === 'quality-profiles' }"
        class="tab-button"
      >
        <i class="ph ph-star"></i>
        Quality Profiles
      </button>
      <button 
        @click="router.push({ hash: '#general' })" 
        :class="{ active: activeTab === 'general' }"
        class="tab-button"
      >
        <i class="ph ph-sliders"></i>
        General Settings
      </button>
    </div>

    <div class="settings-content">
      <!-- Indexers Tab -->
      <div v-if="activeTab === 'indexers'" class="tab-content">
        <div class="section-header">
          <h3>Indexers</h3>
          <button @click="showIndexerForm = true" class="add-button">
            <i class="ph ph-plus"></i>
            Add Indexer
          </button>
        </div>

        <div v-if="indexers.length === 0" class="empty-state">
          <i class="ph ph-list-magnifying-glass"></i>
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
                  <i :class="indexer.isEnabled ? 'ph ph-toggle-right' : 'ph ph-toggle-left'"></i>
                </button>
                <button 
                  @click="testIndexerFunc(indexer.id)" 
                  class="icon-button"
                  title="Test"
                  :disabled="testingIndexer === indexer.id"
                >
                  <i v-if="testingIndexer === indexer.id" class="ph ph-spinner ph-spin"></i>
                  <i v-else class="ph ph-check-circle"></i>
                </button>
                <button 
                  @click="editIndexer(indexer)" 
                  class="icon-button"
                  title="Edit"
                >
                  <i class="ph ph-pencil"></i>
                </button>
                <button 
                  @click="confirmDeleteIndexer(indexer)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <i class="ph ph-trash"></i>
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <i class="ph ph-link"></i>
                <span class="detail-label">URL:</span>
                <span class="detail-value">{{ indexer.url }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-list-checks"></i>
                <span class="detail-label">Features:</span>
                <div class="feature-badges">
                  <span v-if="indexer.enableRss" class="badge">RSS</span>
                  <span v-if="indexer.enableAutomaticSearch" class="badge">Automatic Search</span>
                  <span v-if="indexer.enableInteractiveSearch" class="badge">Interactive Search</span>
                </div>
              </div>
              <div class="detail-row" v-if="indexer.lastTestedAt">
                <i class="ph ph-clock"></i>
                <span class="detail-label">Last Tested:</span>
                <span class="detail-value" :class="{ success: indexer.lastTestSuccessful, error: indexer.lastTestSuccessful === false }">
                  {{ formatDate(indexer.lastTestedAt) }}
                  <i v-if="indexer.lastTestSuccessful" class="ph ph-check-circle success"></i>
                  <i v-else-if="indexer.lastTestSuccessful === false" class="ph ph-x-circle error"></i>
                </span>
              </div>
              <div class="detail-row error-row" v-if="indexer.lastTestError">
                <i class="ph ph-warning"></i>
                <span class="detail-value error">{{ indexer.lastTestError }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- API Sources Tab -->
      <div v-if="activeTab === 'apis'" class="tab-content">
        <div class="section-header">
          <h3>API Sources</h3>
          <button @click="showApiForm = true" class="add-button">
            <i class="ph ph-plus"></i>
            Add API Source
          </button>
        </div>

        <div v-if="configStore.apiConfigurations.length === 0" class="empty-state">
          <i class="ph ph-cloud-slash"></i>
          <p>No API sources configured. Add one to start searching for media.</p>
        </div>

        <div v-else class="config-list">
          <div 
            v-for="api in configStore.apiConfigurations" 
            :key="api.id"
            class="config-card"
          >
            <div class="config-info">
              <h4>{{ api.name }}</h4>
              <p class="config-url">{{ api.baseUrl }}</p>
              <div class="config-meta">
                <span class="config-type">{{ api.type.toUpperCase() }}</span>
                <span class="config-status" :class="{ enabled: api.isEnabled }">
                  <i :class="api.isEnabled ? 'ph ph-check-circle' : 'ph ph-x-circle'"></i>
                  {{ api.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
                <span class="config-priority">
                  <i class="ph ph-arrow-up"></i>
                  Priority: {{ api.priority }}
                </span>
              </div>
            </div>
            <div class="config-actions">
              <button @click="editApiConfig(api)" class="edit-button" title="Edit">
                <i class="ph ph-pencil"></i>
              </button>
              <button @click="deleteApiConfig(api.id)" class="delete-button" title="Delete">
                <i class="ph ph-trash"></i>
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
            <i class="ph ph-plus"></i>
            Add Download Client
          </button>
        </div>

        <div v-if="configStore.downloadClientConfigurations.length === 0" class="empty-state">
          <i class="ph ph-download-simple"></i>
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
                  <i class="ph ph-pencil"></i>
                </button>
                <button 
                  @click="confirmDeleteClient(client)" 
                  class="icon-button danger"
                  title="Delete"
                >
                  <i class="ph ph-trash"></i>
                </button>
              </div>
            </div>

            <div class="indexer-details">
              <div class="detail-row">
                <i class="ph ph-link"></i>
                <span class="detail-label">Host:</span>
                <span class="detail-value">{{ client.host }}:{{ client.port }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-shield-check"></i>
                <span class="detail-label">Security:</span>
                <div class="feature-badges">
                  <span class="badge" v-if="client.useSSL">
                    <i class="ph ph-lock"></i> SSL
                  </span>
                  <span class="badge" v-else>
                    <i class="ph ph-lock-open"></i> No SSL
                  </span>
                </div>
              </div>
              <div class="detail-row">
                <i class="ph ph-folder"></i>
                <span class="detail-label">Download Path:</span>
                <span class="detail-value">{{ client.downloadPath }}</span>
              </div>
              <div class="detail-row">
                <i class="ph ph-check-circle"></i>
                <span class="detail-label">Status:</span>
                <span class="detail-value" :class="{ success: client.isEnabled, error: !client.isEnabled }">
                  {{ client.isEnabled ? 'Enabled' : 'Disabled' }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Quality Profiles Tab -->
      <div v-if="activeTab === 'quality-profiles'" class="tab-content">
        <div class="section-header">
          <h3>Quality Profiles</h3>
          <button @click="openQualityProfileForm()" class="add-button">
            <i class="ph ph-plus"></i>
            Add Quality Profile
          </button>
        </div>

        <!-- Empty State -->
        <div v-if="qualityProfiles.length === 0" class="empty-state">
          <i class="ph ph-star empty-icon"></i>
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
                    <i class="ph ph-check-circle"></i>
                    Default
                  </span>
                </div>
                <p v-if="profile.description" class="profile-description">{{ profile.description }}</p>
              </div>
              <div class="profile-actions">
                <button @click="editProfile(profile)" class="icon-button" title="Edit Profile">
                  <i class="ph ph-pencil"></i>
                </button>
                <button 
                  v-if="!profile.isDefault"
                  @click="setDefaultProfile(profile)" 
                  class="icon-button" 
                  title="Set as Default"
                >
                  <i class="ph ph-star"></i>
                </button>
                <button 
                  @click="confirmDeleteProfile(profile)" 
                  class="icon-button danger" 
                  :disabled="profile.isDefault"
                  :title="profile.isDefault ? 'Cannot delete default profile' : 'Delete Profile'"
                >
                  <i class="ph ph-trash"></i>
                </button>
              </div>
            </div>

            <div class="profile-content">
              <!-- Qualities Section -->
              <div v-if="profile.qualities && profile.qualities.filter(q => q.allowed).length > 0" class="profile-section">
                <h5><i class="ph ph-check-square"></i> Allowed Qualities</h5>
                <div class="quality-badges">
                  <span 
                    v-for="quality in profile.qualities.filter(q => q.allowed).sort((a, b) => b.priority - a.priority)"
                    :key="quality.quality"
                    class="quality-badge"
                    :class="{ 'is-cutoff': quality.quality === profile.cutoffQuality }"
                  >
                    {{ quality.quality }}
                    <i v-if="quality.quality === profile.cutoffQuality" class="ph ph-scissors" title="Cutoff Quality"></i>
                  </span>
                </div>
              </div>

              <!-- Preferences Section -->
              <div v-if="profile.preferredFormats?.length || profile.preferredLanguages?.length" class="profile-section">
                <h5><i class="ph ph-sliders"></i> Preferences</h5>
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
                <h5><i class="ph ph-list-checks"></i> Limits & Requirements</h5>
                <div class="limits-grid">
                  <div v-if="profile.minimumSize || profile.maximumSize" class="limit-item">
                    <i class="ph ph-ruler"></i>
                    <span class="limit-label">Size</span>
                    <span class="limit-value">
                      {{ profile.minimumSize || '0' }} - {{ profile.maximumSize || '∞' }} MB
                    </span>
                  </div>
                  <div v-if="profile.minimumSeeders && profile.minimumSeeders > 0" class="limit-item">
                    <i class="ph ph-users"></i>
                    <span class="limit-label">Seeders</span>
                    <span class="limit-value">{{ profile.minimumSeeders }}+ required</span>
                  </div>
                  <div v-if="profile.maximumAge && profile.maximumAge > 0" class="limit-item">
                    <i class="ph ph-clock"></i>
                    <span class="limit-label">Max Age</span>
                    <span class="limit-value">{{ profile.maximumAge }} days</span>
                  </div>
                </div>
              </div>

              <!-- Word Filters Section -->
              <div v-if="profile.preferredWords?.length || profile.mustContain?.length || profile.mustNotContain?.length" class="profile-section">
                <h5><i class="ph ph-text-aa"></i> Word Filters</h5>
                <div class="word-filters">
                  <div v-if="profile.preferredWords && profile.preferredWords.length > 0" class="word-filter-group">
                    <span class="filter-type">
                      <i class="ph ph-sparkle"></i>
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
                      <i class="ph ph-check"></i>
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
                      <i class="ph ph-x"></i>
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
          <button @click="saveSettings" :disabled="configStore.isLoading" class="save-button">
            <i v-if="configStore.isLoading" class="ph ph-spinner ph-spin"></i>
            <i v-else class="ph ph-floppy-disk"></i>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>

        <div v-if="settings" class="settings-form">
          <div class="form-section">
            <h4><i class="ph ph-folder"></i> File Management</h4>
            
            <div class="form-group">
              <label>Root Folder / Output Path</label>
              <FolderBrowser 
                v-model="settings.outputPath" 
                placeholder="Select a folder for audiobooks..."
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
          </div>

          <div class="form-section">
            <h4><i class="ph ph-link"></i> API Configuration</h4>
            
            <div class="form-group">
              <label>Audnexus API URL</label>
              <input v-model="settings.audnexusApiUrl" type="text" placeholder="https://api.audnex.us">
              <span class="form-help">API endpoint for audiobook metadata</span>
            </div>
          </div>

          <div class="form-section">
            <h4><i class="ph ph-download"></i> Download Settings</h4>
            
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
            <h4><i class="ph ph-toggle-left"></i> Features</h4>
            
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
          </div>

          <div class="form-section">
            <h4><i class="ph ph-user-circle"></i> Authentication</h4>
            
            <div class="form-group">
              <label>Login Screen</label>
              <div class="auth-row">
                <input type="checkbox" id="authToggle" v-model="authEnabled" />
                <label for="authToggle">Enable login screen</label>
              </div>
              <span class="form-help">Toggle to enable the login screen. This setting reflects the server's <code>AuthenticationRequired</code> value from <code>config.json</code>. Changes here are local and will not modify server files — edit <code>config/config.json</code> on the host to persist.</span>
            </div>

            <div class="form-group">
              <label>Initial Admin Account</label>
              <div class="admin-credentials">
                <input v-model="settings.adminUsername" type="text" placeholder="Admin username (optional)" class="admin-input" />
                <div class="password-field">
                  <input :type="showPassword ? 'text' : 'password'" v-model="settings.adminPassword" placeholder="Admin password (optional)" class="admin-input password-input" />
                  <button type="button" class="password-toggle" @click.prevent="showPassword = !showPassword" :aria-pressed="showPassword as unknown as boolean" :title="showPassword ? 'Hide password' : 'Show password'">
                    <i :class="showPassword ? 'ph ph-eye-slash' : 'ph ph-eye'"></i>
                  </button>
                </div>
              </div>
              <span class="form-help">Optionally provide an initial admin username and password. When you save settings, the server will create the user if it doesn't exist or update the password if the user already exists.</span>
            </div>

            <div class="form-group">
              <label>API Key (Server)</label>
              <div class="api-key-row input-group">
                <input type="text" :value="startupConfig?.apiKey || ''" disabled class="api-key-input input-group-input" />
                <div class="input-group-append">
                  <button
                    type="button"
                    class="icon-button input-group-btn"
                    :class="{ 'copied': copiedApiKey }"
                    @click="copyApiKey"
                    :disabled="!startupConfig?.apiKey"
                    title="Copy API key"
                  >
                    <i v-if="copiedApiKey" class="ph ph-check"></i>
                    <i v-else class="ph ph-files"></i>
                  </button>
                  <button
                    type="button"
                    class="regenerate-button input-group-btn"
                    @click="regenerateApiKey"
                    :disabled="loadingApiKey"
                    title="Regenerate API key"
                  >
                    <i v-if="loadingApiKey" class="ph ph-spinner ph-spin"></i>
                    <i v-else class="ph ph-arrow-counter-clockwise"></i>
                    <span v-if="!loadingApiKey">Regenerate</span>
                  </button>
                </div>
              </div>
              <span class="form-help">An API key can be used to authenticate sensitive API calls from trusted clients. Keep it secret. Keep it safe. Regenerating will replace the existing key.</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- API Configuration Modal (placeholder) -->
    <div v-if="showApiForm" class="modal-overlay" @click="showApiForm = false">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>{{ editingApi ? 'Edit' : 'Add' }} API Source</h3>
          <button @click="showApiForm = false" class="modal-close">
            <i class="ph ph-x"></i>
          </button>
        </div>
        <div class="modal-body">
          <p>API configuration form would go here...</p>
        </div>
        <div class="modal-actions">
          <button @click="showApiForm = false" class="cancel-button">
            <i class="ph ph-x"></i>
            Cancel
          </button>
          <button @click="saveApiConfig" class="save-button">
            <i class="ph ph-check"></i>
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
          <i class="ph ph-warning-circle"></i>
          Delete Download Client
        </h3>
        <button @click="clientToDelete = null" class="modal-close">
          <i class="ph ph-x"></i>
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
          <i class="ph ph-trash"></i>
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
          <i class="ph ph-warning-circle"></i>
          Delete Indexer
        </h3>
        <button @click="indexerToDelete = null" class="modal-close">
          <i class="ph ph-x"></i>
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
          <i class="ph ph-trash"></i>
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
          <i class="ph ph-warning-circle"></i>
          Delete Quality Profile
        </h3>
        <button @click="profileToDelete = null" class="modal-close">
          <i class="ph ph-x"></i>
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the quality profile <strong>{{ profileToDelete.name }}</strong>?</p>
        <p v-if="profileToDelete.isDefault" class="warning-text">
          <i class="ph ph-warning"></i>
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
          <i class="ph ph-trash"></i>
          Delete
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { apiService } from '@/services/api'
import { useRoute, useRouter } from 'vue-router'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings, Indexer, QualityProfile } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'
import IndexerFormModal from '@/components/IndexerFormModal.vue'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'
import QualityProfileFormModal from '@/components/QualityProfileFormModal.vue'
import { useNotification } from '@/composables/useNotification'
import { getIndexers, deleteIndexer, toggleIndexer as apiToggleIndexer, testIndexer as apiTestIndexer, getQualityProfiles, deleteQualityProfile, createQualityProfile, updateQualityProfile } from '@/services/api'

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const { success, error: showError, info } = useNotification()
const activeTab = ref<'indexers' | 'apis' | 'clients' | 'quality-profiles' | 'general'>('indexers')
const showApiForm = ref(false)
const showClientForm = ref(false)
const showIndexerForm = ref(false)
const showQualityProfileForm = ref(false)
const editingApi = ref<ApiConfiguration | null>(null)
const editingClient = ref<DownloadClientConfiguration | null>(null)
const editingIndexer = ref<Indexer | null>(null)
const editingQualityProfile = ref<QualityProfile | null>(null)
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
  if (!confirm('Regenerating the API key will immediately invalidate the existing key. Continue?')) return
  loadingApiKey.value = true
  try {
    const resp = await apiService.regenerateApiKey()
    startupConfig.value = { ...(startupConfig.value || {}), apiKey: resp.apiKey }
    info('API key regenerated - copied to clipboard')
    try { await navigator.clipboard.writeText(resp.apiKey) } catch {}
  } catch (err) {
    console.error('Failed to regenerate API key', err)
    // If server returns 401/403, suggest logging in as admin
    const status = (err && typeof err === 'object' && err !== null && 'status' in err) ? (err as { status: number }).status : 0
    if (status === 401 || status === 403) {
      showError('You must be logged in as an administrator to regenerate the API key. Please login and try again.')
    } else {
      showError('Failed to regenerate API key')
    }
  } finally {
    loadingApiKey.value = false
  }
}
const authEnabled = ref(false)
const indexers = ref<Indexer[]>([])
const qualityProfiles = ref<QualityProfile[]>([])
const testingIndexer = ref<number | null>(null)
const indexerToDelete = ref<Indexer | null>(null)
const profileToDelete = ref<QualityProfile | null>(null)
const adminUsers = ref<Array<{ id: number; username: string; email?: string; isAdmin: boolean; createdAt: string }>>([])
  const showPassword = ref(false)

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
  showApiForm.value = true
}

const editClientConfig = (client: DownloadClientConfiguration) => {
  editingClient.value = client
  showClientForm.value = true
}

const deleteApiConfig = async (id: string) => {
  if (confirm('Are you sure you want to delete this API configuration?')) {
    try {
      await configStore.deleteApiConfiguration(id)
      success('API configuration deleted successfully')
    } catch (error) {
      console.error('Failed to delete API configuration:', error)
      const errorMessage = formatApiError(error)
      showError(errorMessage)
    }
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
    success('Download client deleted successfully')
  } catch (error) {
    console.error('Failed to delete download client:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  } finally {
    clientToDelete.value = null
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

const saveApiConfig = () => {
  // Placeholder for API config save
  info('API configuration form would be implemented here')
  showApiForm.value = false
  editingApi.value = null
}

const saveSettings = async () => {
  if (!settings.value) return
  
  try {
    await configStore.saveApplicationSettings(settings.value)
    success('Settings saved successfully')
    // If user toggled the authEnabled, attempt to save to startup config
    try {
      const original = startupConfig.value || {}
      const newCfg: import('@/types').StartupConfig = { ...original, authenticationRequired: authEnabled.value ? 'Enabled' : 'Disabled' }
        try {
          await apiService.saveStartupConfig(newCfg)
          success('Startup configuration saved (config.json)')
        } catch {
          // If server can't persist startup config (e.g., permission denied), offer a fallback download of the config JSON
          info('Could not persist startup config to disk. Preparing downloadable startup config so you can save it manually.')
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
            info('Download started. Save the file to the server config directory to persist the change.')
          } catch {
            info('Also failed to prepare a download. Edit config/config.json on the host to make the change persistent.')
          }
        }
    } catch {
      // Not fatal - write may not be allowed in some deployments
      info('Could not persist startup config to disk. Edit config/config.json on the host to make the change persistent.')
    }
  } catch (error) {
    console.error('Failed to save settings:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  }
}

// Indexer functions
const loadIndexers = async () => {
  try {
    indexers.value = await getIndexers()
  } catch (error) {
    console.error('Failed to load indexers:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  }
}

const toggleIndexerFunc = async (id: number) => {
  try {
    const updatedIndexer = await apiToggleIndexer(id)
    const index = indexers.value.findIndex(i => i.id === id)
    if (index !== -1) {
      indexers.value[index] = updatedIndexer
    }
    success(`Indexer ${updatedIndexer.isEnabled ? 'enabled' : 'disabled'} successfully`)
  } catch (error) {
    console.error('Failed to toggle indexer:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  }
}

const testIndexerFunc = async (id: number) => {
  testingIndexer.value = id
  try {
    const result = await apiTestIndexer(id)
    if (result.success) {
      success(`Indexer tested successfully: ${result.message}`)
      // Update the indexer with test results
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    } else {
      const errorMessage = formatApiError({ response: { data: result.error || result.message } })
      showError(errorMessage)
      // Still update to show failed test status
      const index = indexers.value.findIndex(i => i.id === id)
      if (index !== -1) {
        indexers.value[index] = result.indexer
      }
    }
  } catch (error) {
    console.error('Failed to test indexer:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  } finally {
    testingIndexer.value = null
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
    success('Indexer deleted successfully')
  } catch (error) {
    console.error('Failed to delete indexer:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
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
    showError(errorMessage)
  }
}

const loadAdminUsers = async () => {
  try {
    adminUsers.value = await apiService.getAdminUsers()
  } catch (error) {
    console.error('Failed to load admin users:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
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
    success('Quality profile deleted successfully')
  } catch (error: unknown) {
    console.error('Failed to delete quality profile:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
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
      success('Quality profile updated successfully')
    } else {
      // Create new profile
      const created = await createQualityProfile(profile)
      qualityProfiles.value.push(created)
      success('Quality profile created successfully')
    }
    showQualityProfileForm.value = false
    editingQualityProfile.value = null
  } catch (error: unknown) {
    console.error('Failed to save quality profile:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
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
    
    success(`${profile.name} set as default quality profile`)
  } catch (error: unknown) {
    console.error('Failed to set default profile:', error)
    const errorMessage = formatApiError(error)
    showError(errorMessage)
  }
}

const formatDate = (dateString: string | undefined): string => {
  if (!dateString) return 'Never'
  const date = new Date(dateString)
  return date.toLocaleString()
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as 'indexers' | 'apis' | 'clients' | 'quality-profiles' | 'general'
  if (hash && ['indexers', 'apis', 'clients', 'quality-profiles', 'general'].includes(hash)) {
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

onMounted(async () => {
  // Set initial tab from URL hash
  syncTabFromHash()
  
  await Promise.all([
    configStore.loadApiConfigurations(),
    configStore.loadDownloadClientConfigurations(),
    configStore.loadApplicationSettings(),
    loadIndexers(),
    loadQualityProfiles(),
    loadAdminUsers()
  ])
  
  settings.value = configStore.applicationSettings
  // Load startup config (optional) to determine AuthenticationRequired
  try {
    startupConfig.value = await apiService.getStartupConfig()
    // Accept both camelCase and PascalCase keys coming from the API JSON
    function extractAuthRequired(obj: unknown): string | boolean | undefined {
      if (!obj || typeof obj !== 'object') return undefined
      const o = obj as Record<string, unknown>
      const v = o['authenticationRequired'] ?? o['AuthenticationRequired']
      if (typeof v === 'string' || typeof v === 'boolean') return v
      return undefined
    }

    const authVal = extractAuthRequired(startupConfig.value)
    if (typeof authVal === 'string') {
      authEnabled.value = authVal.toLowerCase() === 'enabled' || authVal.toLowerCase() === 'true'
    } else if (typeof authVal === 'boolean') {
      authEnabled.value = authVal
    }
  } catch {
    // ignore - server may not expose startup config in some deployments
  }

  // Pre-populate admin credentials from the first admin user
  if (adminUsers.value.length > 0 && settings.value) {
    const firstAdmin = adminUsers.value[0]
    if (firstAdmin) {
      settings.value.adminUsername = firstAdmin.username
      // Note: We don't populate the password field for security reasons
      // Users will need to enter the password when changing it
    }
  }
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
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.settings-header h1 i {
  color: #007acc;
}

.settings-header p {
  margin: 0;
  color: #999;
  font-size: 1rem;
}

.settings-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid #333;
}

.tab-button {
  padding: 1rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: #999;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.tab-button:hover {
  background-color: rgba(255, 255, 255, 0.05);
  color: #fff;
}

.tab-button.active {
  color: #007acc;
  border-bottom-color: #007acc;
  background-color: rgba(0, 122, 204, 0.1);
}

.settings-content {
  background: #2a2a2a;
  border-radius: 8px;
  border: 1px solid #333;
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
  border-bottom: 1px solid #444;
}

.section-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
}

.add-button,
.save-button {
  padding: 0.75rem 1.5rem;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.add-button:hover,
.save-button:hover:not(:disabled) {
  background-color: #005fa3;
  transform: translateY(-1px);
}

.save-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #999;
}

.empty-state i {
  font-size: 4rem;
  color: #555;
  margin-bottom: 1rem;
}

.empty-state p {
  margin: 0;
  font-size: 1.1rem;
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
  background-color: #333;
  border: 1px solid #444;
  border-radius: 8px;
  transition: all 0.2s;
}

.config-card:hover {
  background-color: #3a3a3a;
  border-color: #555;
  transform: translateY(-2px);
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
}

.config-url {
  margin: 0 0 1rem 0;
  color: #007acc;
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
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.config-type {
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid rgba(0, 122, 204, 0.3);
}

.config-status {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.config-status.enabled {
  background-color: rgba(46, 204, 113, 0.2);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.3);
}

.config-priority {
  background-color: rgba(155, 89, 182, 0.2);
  color: #9b59b6;
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.config-ssl {
  background-color: rgba(127, 140, 141, 0.2);
  color: #95a5a6;
  border: 1px solid rgba(127, 140, 141, 0.3);
}

.config-ssl.enabled {
  background-color: rgba(241, 196, 15, 0.2);
  color: #f1c40f;
  border: 1px solid rgba(241, 196, 15, 0.3);
}

.config-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.edit-button,
.delete-button {
  padding: 0.75rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.edit-button {
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid rgba(0, 122, 204, 0.3);
}

.edit-button:hover {
  background-color: #007acc;
  color: #fff;
  transform: translateY(-1px);
}

.delete-button {
  background-color: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.delete-button:hover {
  background-color: #e74c3c;
  color: #fff;
  transform: translateY(-1px);
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  background-color: #333;
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1.5rem;
}

.form-section h4 {
  margin: 0 0 1.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #444;
}

.form-section h4 i {
  color: #007acc;
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

.form-group label {
  font-weight: 600;
  color: #fff;
  font-size: 0.95rem;
}

.form-group input[type="text"],
.form-group input[type="number"] {
  padding: 0.75rem;
  background-color: #2a2a2a;
  border: 1px solid #555;
  border-radius: 4px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s;
}

.form-group input[type="text"]:focus,
.form-group input[type="number"]:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-help {
  font-size: 0.85rem;
  color: #999;
  font-style: italic;
}

.checkbox-group {
  flex-direction: row;
  align-items: flex-start;
  background-color: #2a2a2a;
  border: 1px solid #444;
  border-radius: 4px;
  padding: 1rem;
  margin-bottom: 1rem;
  transition: all 0.2s;
}

.checkbox-group:hover {
  background-color: #333;
  border-color: #555;
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
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
}

.checkbox-group label small {
  color: #999;
  font-size: 0.85rem;
  font-weight: normal;
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
  accent-color: #007acc;
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
  background-color: #2a2a2a;
  border: 1px solid #555;
  border-radius: 4px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s;
}

.admin-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.admin-input::placeholder {
  color: #777;
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
  color: #ccc;
  padding: 0.35rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.password-toggle:hover {
  color: #fff;
}

.api-key-row {
  display: flex;
  align-items: center;
}

.form-group input[type="text"].api-key-input {
  flex: 1;
  padding: 0.5rem;
  background: #222;
  border-top: 1px solid #555;
  border-right: none;
  border-bottom: 1px solid #555;
  border-left: 1px solid #555;
  color: #ddd;
  border-radius: 4px 0 0 4px;
}

.api-key-actions {
  display: flex;
  gap: 0.5rem;
}

.input-group-btn.regenerate-button {
  background:#e74c3c;
  color: white;
  border: none;
  padding: 0.45rem 0.75rem;
  border-radius: 0 4px 4px 0;
  cursor: pointer;
}

.input-group-btn.regenerate-button:hover:not(:disabled) {
  background: #c0392b;
}

/* Input group styling for API key */
.input-group {
  display: flex;
  align-items: stretch;
  border: 1px solid #333;
  border-radius: 4px;
  overflow: hidden;
}
.input-group-input {
  border: none;
  border-radius: 0;
  flex: 1;
  background: #222;
  color: #ddd;
  padding: 0.5rem;
}
.input-group-input:focus {
  outline: none;
  box-shadow: inset 0 0 0 2px rgba(0, 122, 204, 0.5);
}
.input-group-append {
  display: flex;
  background: #2a2a2a;
  border-top: 1px solid #555;
  border-right: 1px solid #555;
  border-bottom: 1px solid #555;
  border-left: none;
  color: #ddd;
  border-radius: 0 4px 4px 0;
}
.input-group-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  color: #ddd;
  padding: 0.5rem 0.6rem;
  cursor: pointer;
  transition: background-color 0.2s;
}
.input-group-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.1);
}
.input-group-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.input-group-btn.copied {
  background: #4caf50 !important;
  color: white;
}
.input-group-btn.copied:hover {
  background: #45a049 !important;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 8px;
  max-width: 600px;
  width: 90%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #444;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
}

.modal-close {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.modal-close:hover {
  background-color: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  color: #ccc;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background-color: #555;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.cancel-button:hover {
  background-color: #666;
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
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1.5rem;
  transition: all 0.2s;
}

.indexer-card:hover {
  border-color: #007acc;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 122, 204, 0.2);
}

.indexer-card.disabled {
  opacity: 0.6;
  filter: grayscale(50%);
}

.indexer-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #333;
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
}

.indexer-type {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.indexer-type.torrent {
  background-color: rgba(76, 175, 80, 0.2);
  color: #4caf50;
  border: 1px solid #4caf50;
}

.indexer-type.usenet {
  background-color: rgba(33, 150, 243, 0.2);
  color: #2196f3;
  border: 1px solid #2196f3;
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
}

.indexer-details {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.detail-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.detail-row i {
  color: #007acc;
  font-size: 1rem;
}

.detail-label {
  color: #999;
  min-width: 100px;
}

.detail-value {
  color: #ccc;
  word-break: break-all;
}

.detail-value.success {
  color: #4caf50;
}

.detail-value.error {
  color: #f44336;
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
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background-color: rgba(0, 122, 204, 0.2);
  color: #007acc;
  border: 1px solid #007acc;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.error-message {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(244, 67, 54, 0.1);
  border: 1px solid #f44336;
  border-radius: 4px;
  color: #f44336;
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
  border: 1px solid #3a3a3a;
  border-radius: 8px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.profile-card:hover {
  border-color: #007acc;
  box-shadow: 0 2px 8px rgba(0, 122, 204, 0.2);
}

.profile-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: #252525;
  border-bottom: 1px solid #3a3a3a;
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
  padding: 0.25rem 0.6rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.default {
  background-color: rgba(76, 175, 80, 0.15);
  color: #4caf50;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.profile-description {
  margin: 0;
  color: #999;
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
  color: #007acc;
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
  background-color: rgba(33, 150, 243, 0.15);
  color: #2196f3;
  border: 1px solid rgba(33, 150, 243, 0.3);
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 500;
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
  color: #999;
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
  background-color: #1f1f1f;
  border-radius: 4px;
  border: 1px solid #333;
}

.limit-item i {
  color: #007acc;
  font-size: 1.1rem;
}

.limit-label {
  color: #999;
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
  color: #999;
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
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 500;
}

.word-tag.positive {
  background-color: rgba(76, 175, 80, 0.15);
  color: #4caf50;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.word-tag.required {
  background-color: rgba(255, 152, 0, 0.15);
  color: #ff9800;
  border: 1px solid rgba(255, 152, 0, 0.3);
}

.word-tag.forbidden {
  background-color: rgba(244, 67, 54, 0.15);
  color: #f44336;
  border: 1px solid rgba(244, 67, 54, 0.3);
}

.warning-text {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem;
  background-color: rgba(255, 152, 0, 0.1);
  border-left: 3px solid #ff9800;
  color: #ff9800;
  margin: 1rem 0;
  border-radius: 4px;
}

.warning-text i {
  font-size: 1.2rem;
}

@media (max-width: 768px) {
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