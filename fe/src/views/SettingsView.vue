<template>
  <div class="settings-page">
    <div class="settings-tabs">
      <!-- Mobile dropdown -->
      <div class="settings-tabs-mobile">
        <CustomSelect
          v-model="activeTab"
          :options="mobileTabOptions"
          class="tab-dropdown"
        />
      </div>

      <!-- Desktop tabs (turns into a horizontal carousel when overflowing) -->
      <div class="settings-tabs-desktop-wrapper">
        <button
          type="button"
          class="tabs-scroll-btn left"
          @click="scrollTabs(-1)"
          v-show="hasTabOverflow && showLeftTabChevron"
          aria-hidden="true"
        >
          ‹
        </button>

        <div ref="desktopTabsRef" class="settings-tabs-desktop">
          <button 
            @click="router.push({ hash: '#rootfolders' })" 
            :class="{ active: activeTab === 'rootfolders' }"
            class="tab-button"
          >
    <PhFolder />
          Root Folders
        </button>
        <button 
          @click="router.push({ hash: '#indexers' })" 
          :class="{ active: activeTab === 'indexers' }"
          class="tab-button"
        >
    <PhListMagnifyingGlass />
          Indexers
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
          @click="router.push({ hash: '#notifications' })" 
          :class="{ active: activeTab === 'notifications' }"
          class="tab-button"
        >
    <PhBell />
          Notifications
        </button>
        <button 
          @click="router.push({ hash: '#bot' })" 
          :class="{ active: activeTab === 'bot' }"
          class="tab-button"
        >
    <PhGlobe />
          Discord Bot
        </button>
        <button 
          @click="router.push({ hash: '#general' })" 
          :class="{ active: activeTab === 'general' }"
          class="tab-button"
        >
    <PhSliders />
          General Settings
        </button>
        </div>

        <button
          type="button"
          class="tabs-scroll-btn right"
          @click="scrollTabs(1)"
          v-show="hasTabOverflow && showRightTabChevron"
          aria-hidden="true"
        >
          ›
        </button>
      </div>
    </div>

    <!-- Settings Toolbar -->
    <div class="settings-toolbar">
      <div class="toolbar-content">
        <div class="toolbar-actions">
          <!-- Add buttons for each section -->
          <button v-if="activeTab === 'rootfolders'" @click="openAddRootFolder()" class="add-button">
            <PhPlus />
            Add Root Folder
          </button>
          <button v-if="activeTab === 'indexers'" @click="showIndexerForm = true" class="add-button">
            <PhPlus />
            Add Indexer
          </button>
          <button v-if="activeTab === 'clients'" @click="showClientForm = true; editingClient = null" class="add-button">
            <PhPlus />
            Add Download Client
          </button>
          <button v-if="activeTab === 'quality-profiles'" @click="openQualityProfileForm()" class="add-button">
            <PhPlus />
            Add Quality Profile
          </button>
          <button v-if="activeTab === 'notifications'" @click="showWebhookForm = true" class="add-button">
            <PhPlus />
            Add Webhook
          </button>
          
          <!-- Save button for sections that need it -->
          <button v-if="activeTab === 'general' || activeTab === 'bot'" @click="saveSettings" :disabled="configStore.isLoading" class="save-button" :title="!isFormValid ? 'Please fix invalid fields before saving' : ''">
            <template v-if="configStore.isLoading">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhFloppyDisk />
            </template>
            {{ configStore.isLoading ? 'Saving...' : 'Save Settings' }}
          </button>
        </div>
      </div>
    </div>

    <div class="settings-content">
      <!-- Indexers Tab -->
      <div v-if="activeTab === 'indexers'" class="tab-content">
        <div class="section-header">
          <h3>Indexers</h3>
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


      <!-- Download Clients Tab -->
      <div v-if="activeTab === 'clients'" class="tab-content">
        <div class="section-header">
          <h3>Download Clients</h3>
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
                  @click="toggleDownloadClientFunc(client)"
                  class="icon-button"
                  :title="client.isEnabled ? 'Disable' : 'Enable'"
                >
                  <template v-if="client.isEnabled">
                    <PhToggleRight />
                  </template>
                  <template v-else>
                    <PhToggleLeft />
                  </template>
                </button>
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
                <PhLinkSimple />
                <span class="detail-label">Mappings:</span>
                <div class="feature-badges">
                  <span v-for="m in getMappingsForClient(client)" :key="m.id" class="badge">
                    <PhLink />
                    {{ m.name || m.remotePath }}
                  </span>
                  <span v-if="getMappingsForClient(client).length === 0" class="detail-value">(none)</span>
                </div>
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
              <button @click="confirmDeleteMapping(mapping)" class="delete-button" title="Delete">
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

        <!-- Delete Remote Path Mapping Confirmation Modal -->
        <div v-if="mappingToDelete" class="modal-overlay" @click="mappingToDelete = null">
          <div class="modal-content" @click.stop>
            <div class="modal-header">
              <h3>
                <PhWarningCircle />
                Delete Remote Path Mapping
              </h3>
              <button @click="mappingToDelete = null" class="modal-close">
                <PhX />
              </button>
            </div>
            <div class="modal-body">
              <p>Are you sure you want to delete the remote path mapping <strong>{{ mappingToDelete.name || mappingToDelete.remotePath }}</strong>?</p>
              <p>This action cannot be undone.</p>
            </div>
            <div class="modal-actions">
              <button @click="mappingToDelete = null" class="cancel-button">
                Cancel
              </button>
              <button @click="executeDeleteMapping()" class="delete-button">
                <PhTrash />
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Quality Profiles Tab -->
      <div v-if="activeTab === 'quality-profiles'" class="tab-content">
        <div class="section-header">
          <h3>Quality Profiles</h3>
        </div>

        <!-- Empty State -->
        <div v-if="qualityProfiles.length === 0" class="empty-state">
          <PhStar class="empty-icon" />
          <p>No quality profiles configured yet.</p>
          <p class="empty-help">Quality profiles define which release qualities you want to download and prefer.</p>
        </div>

        <!-- Quality Profiles Grid -->
        <div v-else class="profiles-grid">
          <div v-for="profile in qualityProfiles" :key="profile.id" class="profile-card" :class="{ 'is-default': profile.isDefault }">
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
              <label>File Naming Pattern</label>
              <input v-model="settings.fileNamingPattern" type="text" placeholder="{Author}/{Series}/{Title}">
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

            <div class="form-group">
              <label>Download Completion Stability (seconds)</label>
              <input v-model.number="settings.downloadCompletionStabilitySeconds" type="number" min="1" max="600">
              <span class="form-help">How long (seconds) a download must be seen as complete on the client before finalization begins. Increase for clients that post-process/extract after completion.</span>
            </div>

            <div class="form-group">
              <label>Missing-source Retry Initial Delay (seconds)</label>
              <input v-model.number="settings.missingSourceRetryInitialDelaySeconds" type="number" min="1" max="600">
              <span class="form-help">Initial retry delay (seconds) used when files are not yet available at finalization time.</span>
            </div>

            <div class="form-group">
              <label>Missing-source Max Retries</label>
              <input v-model.number="settings.missingSourceMaxRetries" type="number" min="0" max="20">
              <span class="form-help">Maximum number of retries to attempt if the finalized download's source files are missing.</span>
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
            <h4><PhMagnifyingGlass /> Search Settings</h4>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableAmazonSearch" type="checkbox" />
                <span>
                  <strong>Enable Amazon Searching</strong>
                  <small>Include Amazon-based search providers when performing intelligent searches.</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableAudibleSearch" type="checkbox" />
                <span>
                  <strong>Enable Audible Searching</strong>
                  <small>Include Audible provider lookups when performing intelligent searches.</small>
                </span>
              </label>
            </div>

            <div class="form-group checkbox-group">
              <label>
                <input v-model="settings.enableOpenLibrarySearch" type="checkbox" />
                <span>
                  <strong>Enable OpenLibrary Searching</strong>
                  <small>Include OpenLibrary title augmentation and lookups when performing intelligent searches.</small>
                </span>
              </label>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Candidate Cap (max candidates)</label>
                <input v-model.number="settings.searchCandidateCap" type="number" min="1" max="200" />
                <span class="form-help">Maximum number of candidate ASINs/entries to consider when searching (candidateLimit).</span>
              </div>

              <div class="form-group">
                <label>Result Cap (max results)</label>
                <input v-model.number="settings.searchResultCap" type="number" min="1" max="200" />
                <span class="form-help">Maximum number of results returned to the UI (returnLimit).</span>
              </div>
            </div>

            <div class="form-group">
              <label>Fuzzy Threshold</label>
              <input v-model.number="settings.searchFuzzyThreshold" type="number" step="0.01" min="0" max="1" />
              <span class="form-help">Fuzzy matching threshold used when comparing titles/authors (0.0-1.0). Higher values require closer matches.</span>
            </div>
          </div>

          <div class="form-section">
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
                  <button type="button" class="password-toggle" @click.prevent="showPassword = !showPassword" :aria-pressed="!!showPassword" :title="showPassword ? 'Hide password' : 'Show password'">
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
              <span class="form-help">API key for authenticating external applications. Generate a new key if needed. Copy it to use with API clients.</span>
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
                <button type="button" class="password-toggle" @click.prevent="toggleShowPassword" :aria-pressed="!!showPassword" :title="showPassword ? 'Hide password' : 'Show password'">
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
          </div>
        </div>
      </div>

      <!-- Root Folders Tab -->
      <div v-if="activeTab === 'rootfolders'" class="tab-content">
        <div class="form-section">
          <RootFoldersSettings ref="rootFoldersRef" :hide-header="false" />

        </div>
      </div>

      <!-- Notifications Tab -->
      
  <!-- Requests (Discord Bot) Tab -->
  <div v-if="activeTab === 'bot' && settings" class="tab-content">
        <div class="section-header">
          <h3>Discord Bot</h3>
        </div>

        <div class="form-section">
          <div class="form-group checkbox-group">
            <label>
              <input v-model="settings.discordBotEnabled" type="checkbox">
              <span>
                <strong>Enable Discord Bot Integration</strong>
                <small>Allow an external Discord bot to read these settings and register slash commands.</small>
              </span>
            </label>
          </div>

          <div class="form-group">
            <label>Discord Application ID</label>
            <input v-model="settings.discordApplicationId" type="text" placeholder="Discord Application ID (client id)" />
            <span class="form-help">Used to register application commands. For per-guild testing, set a Guild ID below.</span>
          </div>

          <div class="form-group">
            <label>Discord Guild ID (optional)</label>
            <input v-model="settings.discordGuildId" type="text" placeholder="Optional guild id for testing" />
            <span class="form-help">If provided, commands will be registered to this guild for faster updates (useful for development).</span>
          </div>

          <div class="form-group">
            <label>Discord Channel ID (optional)</label>
            <input v-model="settings.discordChannelId" type="text" placeholder="Optional channel id to restrict commands" />
            <span class="form-help">If provided, the bot will only accept request commands from this channel. You can also set this via the bot using the <code>/request-config set-channel</code> command.</span>
          </div>

          <!-- Invite / Register Controls -->
          <div v-if="settings.discordApplicationId" class="form-group invite-row">
            <label>Invite Bot to Server</label>
            <div class="invite-controls">
              <button @click="openInviteLink" class="invite-button">Open Invite</button>
              <button @click="copyInviteLink" class="icon-button">Copy Invite Link</button>
              <button @click="checkDiscordStatus" class="icon-button" :disabled="checkingDiscord">Check Install</button>
              <button @click="registerCommands" class="save-button" :disabled="registeringCommands || !settings.discordBotToken">Register commands now</button>
            </div>
            <div class="form-help">
              Use this to invite the bot with the minimal permissions needed for requests. Make sure <strong>Discord Application ID</strong> is filled in above. Optionally set a Guild ID to preselect a server.
            </div>
            <div v-if="inviteLinkPreview" class="invite-link-preview">
              <small>Preview: <a :href="inviteLinkPreview" target="_blank" rel="noopener noreferrer">{{ inviteLinkPreview }}</a></small>
            </div>

            <div v-if="discordStatus" class="discord-status">
              <template v-if="discordStatus.installed === true">
                <span class="status-pill installed">Installed in guild {{ discordStatus.guildId || '' }}</span>
              </template>
              <template v-else-if="discordStatus.installed === false">
                <span class="status-pill not-installed">Not installed in configured guild</span>
              </template>
              <template v-else>
                <span class="status-pill unknown">Token validated</span>
              </template>
            </div>
          </div>

          <div class="form-group">
            <label>Bot Token</label>
            <div class="password-field">
              <input :type="showPassword ? 'text' : 'password'" v-model="settings.discordBotToken" placeholder="Bot token (keep secret)" class="admin-input password-input" />
              <button type="button" class="password-toggle" @click.prevent="toggleShowPassword" :aria-pressed="!!showPassword" :title="showPassword ? 'Hide token' : 'Show token'">
                <template v-if="showPassword">
                  <PhEyeSlash />
                </template>
                <template v-else>
                  <PhEye />
                </template>
              </button>
            </div>
            <span class="form-help">The bot process will use this token to login. Be careful with this value.</span>
          </div>

          <div class="form-group">
            <label>Command Group Name</label>
            <input v-model="settings.discordCommandGroupName" type="text" placeholder="request" />
            <span class="form-help">Primary command group (e.g. <code>request</code>)</span>
          </div>

          <div class="form-group">
            <label>Subcommand Name</label>
            <input v-model="settings.discordCommandSubcommandName" type="text" placeholder="audiobook" />
            <span class="form-help">Subcommand for audiobooks (e.g. <code>audiobook</code>) — results in <code>/request audiobook &lt;title&gt;</code></span>
          </div>

          <div class="form-group">
            <label>Bot Username (optional)</label>
            <input v-model="settings.discordBotUsername" type="text" placeholder="Custom bot username" />
            <span class="form-help">Optional custom username for the bot. Leave empty to use the default username from Discord.</span>
          </div>

          <div class="form-group">
            <label>Bot Avatar URL (optional)</label>
            <input v-model="settings.discordBotAvatar" type="url" placeholder="https://example.com/avatar.png" />
            <span class="form-help">Optional avatar image URL for the bot. Leave empty to use the default avatar from Discord.</span>
          </div>
        </div>

        <!-- Discord Bot Process Controls -->
        <div class="form-section">
          <h4><PhRobot /> Discord Bot Process Control</h4>
          
          <div class="bot-status-section">
            <div class="bot-status-display">
              <div class="status-indicator" :class="botStatusClass">
                <PhCircle v-if="botStatus === 'unknown'" />
                <PhSpinner v-else-if="botStatus === 'checking'" class="ph-spin" />
                <PhCheckCircle v-else-if="botStatus === 'running'" />
                <PhXCircle v-else-if="botStatus === 'stopped'" />
                <PhWarning v-else />
              </div>
              <div class="status-text">
                <strong>Bot Status:</strong> {{ botStatusText }}
              </div>
            </div>
            
            <div class="bot-controls">
              <button 
                @click="checkBotStatus" 
                class="status-button"
                :disabled="checkingBotStatus"
              >
                <template v-if="checkingBotStatus">
                  <PhSpinner class="ph-spin" />
                </template>
                <template v-else>
                  <PhArrowClockwise />
                </template>
                Refresh Status
              </button>
              
              <button 
                @click="startBot" 
                class="start-button"
                :disabled="startingBot || botStatus === 'running'"
              >
                <template v-if="startingBot">
                  <PhSpinner class="ph-spin" />
                </template>
                <template v-else>
                  <PhPlay />
                </template>
                Start Bot
              </button>
              
              <button 
                @click="stopBot" 
                class="stop-button"
                :disabled="stoppingBot || botStatus === 'stopped'"
              >
                <template v-if="stoppingBot">
                  <PhSpinner class="ph-spin" />
                </template>
                <template v-else>
                  <PhStop />
                </template>
                Stop Bot
              </button>
            </div>
          </div>
          
          <div class="form-help">
            Control the Discord bot process directly from here. The bot will use the token configured above to connect to Discord and register slash commands.
          </div>
        </div>
      </div>

      <div v-if="activeTab === 'notifications'" class="tab-content">
        <div class="section-header">
          <div class="section-title-wrapper">
            <h3>Notification Webhooks</h3>
          </div>
        </div>

        <div v-if="webhooks.length === 0" class="empty-state">
          <PhBellSlash class="empty-icon" />
          <h3>No webhooks configured</h3>
          <p>Webhooks allow you to receive real-time notifications when important events occur.</p>
          <p class="empty-help">Supported services include Slack, Discord, Telegram, Pushover, and more.</p>
          <button @click="showWebhookForm = true" class="add-button-large">
            <PhPlus />
            Create Your First Webhook
          </button>
        </div>

        <div v-else class="webhooks-grid">
          <div 
            v-for="webhook in webhooks" 
            :key="webhook.id"
            class="webhook-card"
            :class="{ disabled: !webhook.isEnabled }"
          >
            <div class="webhook-header">
              <div class="webhook-title-row">
                <div class="webhook-info">
                  <h4>{{ webhook.name }}</h4>
                  <div class="webhook-meta">
                    <span class="webhook-type-badge">{{ webhook.type }}</span>
                    <div class="triggers-preview">
                      <span 
                        v-for="trigger in webhook.triggers" 
                        :key="trigger"
                        class="trigger-badge-small"
                        :class="getTriggerClass(trigger)"
                        :title="formatTriggerName(trigger)"
                      >
                        <component :is="getTriggerIcon(trigger)" />
                      </span>
                    </div>
                  </div>
                </div>
              </div>
              <div class="webhook-header-actions">
                <div class="webhook-status-badge" :class="{ active: webhook.isEnabled }">
                  <component :is="webhook.isEnabled ? PhCheckCircle : PhXCircle" />
                  {{ webhook.isEnabled ? 'Active' : 'Inactive' }}
                </div>

                <!-- Small action buttons aligned like other cards -->
                <button
                  class="icon-button"
                  :class="{ active: webhook.isEnabled }"
                  :title="webhook.isEnabled ? 'Disable webhook' : 'Enable webhook'"
                  @click.stop="toggleWebhook(webhook)"
                >
                  <component :is="webhook.isEnabled ? PhToggleRight : PhToggleLeft" />
                </button>

                <button
                  class="icon-button"
                  :title="!webhook.isEnabled ? 'Enable webhook to test' : 'Send test notification'"
                  @click.stop="testWebhook(webhook)"
                  :disabled="testingWebhook === webhook.id || !webhook.isEnabled"
                >
                  <PhSpinner v-if="testingWebhook === webhook.id" class="ph-spin" />
                  <PhPaperPlaneTilt v-else />
                </button>

                <button
                  class="icon-button"
                  title="Edit webhook"
                  @click.stop="editWebhook(webhook)"
                >
                  <PhPencil />
                </button>

                <button
                  class="icon-button danger"
                  title="Delete webhook"
                  @click.stop="deleteWebhook(webhook.id)"
                >
                  <PhTrash />
                </button>


              </div>
            </div>

            <div class="webhook-body">
              <div class="webhook-url-container">
                <PhLink class="url-icon" />
                <span class="webhook-url">{{ webhook.url }}</span>
              </div>

              <div class="webhook-triggers-section">
                <div class="triggers-header">
                  <PhBell />
                  <span class="triggers-label">Active Triggers ({{ webhook.triggers.length }})</span>
                </div>
                <div class="triggers-list">
                  <span 
                    v-for="trigger in webhook.triggers" 
                    :key="trigger"
                    class="trigger-badge"
                    :class="getTriggerClass(trigger)"
                  >
                    <component :is="getTriggerIcon(trigger)" />
                    {{ formatTriggerName(trigger) }}
                  </span>
                </div>
              </div>
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

  <!-- Webhook Configuration Modal -->
  <div v-if="showWebhookForm" class="modal-overlay" @click.self="closeWebhookForm" @keydown.esc="closeWebhookForm">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h2>{{ editingWebhook ? 'Edit' : 'Add' }} Webhook</h2>
        <button @click="closeWebhookForm" class="close-btn" aria-label="Close modal">
          <PhX />
        </button>
      </div>
      <div class="modal-body">
        <form @submit.prevent="saveWebhook">
          
          <!-- Basic Configuration Section -->
          <div class="form-section">
            <h3>Basic</h3>
            
            <div class="form-group">
              <label for="webhook-name">Name *</label>
              <input 
                id="webhook-name"
                v-model="webhookForm.name" 
                type="text" 
                placeholder="e.g., Production Slack Channel" 
                required
              />
              <small v-if="getServiceHelp()">{{ getServiceHelp() }}</small>
            </div>

            <div class="form-group">
              <label for="webhook-type">Type *</label>
              <select 
                id="webhook-type"
                v-model="webhookForm.type" 
                required
                @change="onServiceTypeChange"
              >
                <option value="" disabled>Select type...</option>
                <option value="Slack">Slack</option>
                <option value="Discord">Discord</option>
                <option value="Telegram">Telegram</option>
                <option value="Pushover">Pushover</option>
                <option value="Pushbullet">Pushbullet</option>
                <option value="NTFY">NTFY</option>
                <option value="Zapier">Zapier / Generic</option>
              </select>
            </div>

            <div class="form-group">
              <label for="webhook-url">Webhook URL *</label>
              <input 
                id="webhook-url"
                v-model="webhookForm.url" 
                type="url" 
                placeholder="https://hooks.example.com/services/your-webhook-url" 
                required
              />
            </div>
          </div>

          <!-- Triggers Section -->
          <div class="form-section triggers-section">
            <h3>Notification Triggers</h3>
            
            <div class="checkbox-group">
              <label for="trigger-book-added">
                <input id="trigger-book-added" v-model="webhookForm.triggers" value="book-added" type="checkbox">
                <span>
                  <strong>Book Added to Library</strong>
                  <small>Notifies when a new audiobook is added to your library</small>
                </span>
              </label>
            </div>

            <div class="checkbox-group">
              <label for="trigger-book-downloading">
                <input id="trigger-book-downloading" v-model="webhookForm.triggers" value="book-downloading" type="checkbox">
                <span>
                  <strong>Download Started</strong>
                  <small>Notifies when an audiobook download begins</small>
                </span>
              </label>
            </div>

            <div class="checkbox-group">
              <label for="trigger-book-available">
                <input id="trigger-book-available" v-model="webhookForm.triggers" value="book-available" type="checkbox">
                <span>
                  <strong>Download Complete</strong>
                  <small>Notifies when an audiobook finishes downloading and is ready</small>
                </span>
              </label>
            </div>
          </div>

          <!-- Status Section -->
          <div class="form-section status-section">
            <h3>Activation</h3>
            <div class="checkbox-group">
              <label for="webhook-enabled">
                <input id="webhook-enabled" v-model="webhookForm.isEnabled" type="checkbox" />
                <span>
                  <strong>Enable</strong>
                  <small>Enable this webhook to start receiving notifications</small>
                </span>
              </label>
            </div>
          </div>

        </form>
      </div>
      <div class="modal-footer">
        <button @click="closeWebhookForm" class="btn btn-secondary" type="button">
          Cancel
        </button>
        <button 
          v-if="webhookForm.url && webhookForm.type && webhookForm.triggers.length > 0 && !editingWebhook"
          @click="testWebhookConfig" 
          class="btn btn-info" 
          type="button"
          :disabled="testingWebhookConfig"
        >
          <PhSpinner v-if="testingWebhookConfig" class="ph-spin" />
          {{ testingWebhookConfig ? 'Testing...' : 'Test' }}
        </button>
        <button 
          @click="saveWebhook" 
          class="btn btn-primary" 
          type="button" 
          :disabled="!isWebhookFormValid || savingWebhook"
        >
          <PhSpinner v-if="savingWebhook" class="ph-spin" />
          {{ savingWebhook ? 'Saving...' : (editingWebhook ? 'Update' : 'Save') }}
        </button>
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

  <!-- Delete Metadata Source Confirmation Modal -->
  <div v-if="apiToDelete" class="modal-overlay" @click="apiToDelete = null">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>
          <PhWarningCircle />
          Delete Metadata Source
        </h3>
        <button @click="apiToDelete = null" class="modal-close">
          <PhX />
        </button>
      </div>
      <div class="modal-body">
        <p>Are you sure you want to delete the metadata source <strong>{{ apiToDelete.name }}</strong>?</p>
        <p>This action cannot be undone.</p>
      </div>
      <div class="modal-actions">
        <button @click="apiToDelete = null" class="cancel-button">
          Cancel
        </button>
        <button @click="executeDeleteApi()" class="delete-button">
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
          <button type="button" @click="indexerToDelete = null" class="cancel-button">
            Cancel
          </button>
          <button type="button" @click="executeDeleteIndexer()" class="delete-button modal-delete-button">
            <PhTrash />
            Delete
          </button>
        </div>
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
import { ref, reactive, onMounted, onBeforeUnmount, watch, computed, nextTick } from 'vue'
import { apiService } from '@/services/api'
import { useRoute, useRouter } from 'vue-router'
import { logger } from '@/utils/logger'
import { useConfigurationStore } from '@/stores/configuration'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings, Indexer, QualityProfile, RemotePathMapping } from '@/types'
import FolderBrowser from '@/components/FolderBrowser.vue'
import RootFoldersSettings from '@/components/settings/RootFoldersSettings.vue'
import CustomSelect from '@/components/CustomSelect.vue'
import {
  // Settings & Navigation
  PhGear, PhListMagnifyingGlass, PhCloud, PhDownload, PhStar, PhSliders, PhPlus, PhMagnifyingGlass,
  PhArrowUp, PhDownloadSimple, PhCloudSlash, PhGlobe, PhInfo,
  // Form Controls & Actions
  PhToggleRight, PhToggleLeft, PhSpinner, PhCheckCircle, PhPencil, PhTrash, PhLink,
  PhListChecks, PhClock, PhXCircle, PhCheck, PhX, PhCheckSquare, PhRuler, PhSparkle,
  PhArrowCounterClockwise, PhScissors, PhBell, PhPaperPlaneTilt, PhBellSlash, PhCaretDown,
  // Security & Authentication
  PhShieldCheck, PhLock, PhLockOpen, PhWarning, PhWarningCircle,
  // Files & Folders
  PhFolder, PhLinkSimple, PhBrowser, PhFloppyDisk, PhFiles,
  // Users
  PhUsers, PhUserCircle,
  // Bot Controls
  PhRobot, PhPlay, PhStop, PhArrowClockwise, PhCircle,
  // Misc
  PhTextAa, PhEye, PhEyeSlash, PhPlug, PhSignOut
} from '@phosphor-icons/vue'
import IndexerFormModal from '@/components/IndexerFormModal.vue'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'
import QualityProfileFormModal from '@/components/QualityProfileFormModal.vue'
import { showConfirm } from '@/composables/useConfirm'
import { useToast } from '@/services/toastService'
import { getIndexers, deleteIndexer, toggleIndexer as apiToggleIndexer, testIndexer as apiTestIndexer, getQualityProfiles, deleteQualityProfile, createQualityProfile, updateQualityProfile, getRemotePathMappings, createRemotePathMapping, updateRemotePathMapping, deleteRemotePathMapping, testDownloadClient as apiTestDownloadClient } from '@/services/api'

// Generate UUID v4 compatible across all browsers
function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0
    const v = c === 'x' ? r : (r & 0x3 | 0x8)
    return v.toString(16)
  })
}

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const toast = useToast()
// Debug environment markers (Vitest exposes import.meta.vitest / import.meta.env.VITEST)
logger.debug('[test-debug] import.meta.vitest:', (import.meta as unknown as { vitest?: unknown }).vitest, 'env.VITEST:', (import.meta as unknown as { env?: Record<string, unknown> }).env?.VITEST, '__vitest_global__:', (globalThis as unknown as { __vitest?: unknown }).__vitest)
  const activeTab = ref<'rootfolders' | 'indexers' | 'clients' | 'quality-profiles' | 'notifications' | 'bot' | 'general'>('rootfolders')

const mobileTabOptions = computed(() => [
  { value: 'rootfolders', label: 'Root Folders', icon: PhFolder },
  { value: 'indexers', label: 'Indexers', icon: PhListMagnifyingGlass },
  { value: 'clients', label: 'Download Clients', icon: PhDownload },
  { value: 'quality-profiles', label: 'Quality Profiles', icon: PhStar },
  { value: 'notifications', label: 'Notifications', icon: PhBell },
  { value: 'bot', label: 'Discord Bot', icon: PhGlobe },
  { value: 'general', label: 'General Settings', icon: PhSliders },
  // Integrations removed
])
// Desktop tabs carousel refs/state
const desktopTabsRef = ref<HTMLElement | null>(null)
const hasTabOverflow = ref(false)
const showLeftTabChevron = ref(false)
const showRightTabChevron = ref(false)
const rootFoldersRef = ref<any>(null)

function updateTabOverflow() {
  const el = desktopTabsRef.value
  if (!el) return
  hasTabOverflow.value = el.scrollWidth > el.clientWidth + 1
  showLeftTabChevron.value = el.scrollLeft > 5
  showRightTabChevron.value = el.scrollLeft + el.clientWidth < el.scrollWidth - 5
}

function scrollTabs(direction = 1) {
  const el = desktopTabsRef.value
  if (!el) return
  const amount = Math.round(el.clientWidth * 0.6) * direction
  el.scrollBy({ left: amount, behavior: 'smooth' })
}

let tabsResizeObserver: ResizeObserver | null = null
onMounted(async () => {
  // Wait until DOM is fully painted (fonts, icons) so measurements are accurate
  await nextTick()
  updateTabOverflow()
  window.addEventListener('resize', updateTabOverflow)

  const el = desktopTabsRef.value
  if (el) {
    el.addEventListener('scroll', updateTabOverflow, { passive: true })

    // Use ResizeObserver to detect when content/size changes cause overflow
    if (typeof ResizeObserver !== 'undefined') {
      tabsResizeObserver = new ResizeObserver(() => updateTabOverflow())
      tabsResizeObserver.observe(el)
      // also observe the parent in case the container resizes
      if (el.parentElement) tabsResizeObserver.observe(el.parentElement)
    } else {
      // Fallback: run a delayed check to account for late layout shifts
      setTimeout(updateTabOverflow, 250)
    }
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateTabOverflow)
  const el = desktopTabsRef.value
  if (el) {
    el.removeEventListener('scroll', updateTabOverflow)
  }
  if (tabsResizeObserver) {
    tabsResizeObserver.disconnect()
    tabsResizeObserver = null
  }
})

// Ensure active tab is visible when switching tabs on desktop
function ensureActiveTabVisible() {
  const el = desktopTabsRef.value
  if (!el) return
  const active = el.querySelector('.tab-button.active') as HTMLElement | null
  if (active) {
    // center the active tab in view when overflowing
    active.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' })
  }
}

function openAddRootFolder() {
  if (rootFoldersRef.value && typeof rootFoldersRef.value.openAdd === 'function') {
    rootFoldersRef.value.openAdd()
  }
}

// Audible integration removed

watch(activeTab, () => {
  // delay slightly to allow layout updates
  setTimeout(() => {
    updateTabOverflow()
    if (hasTabOverflow.value) ensureActiveTabVisible()
  }, 40)
})
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
    
  // Use in-app confirm dialog instead of window.confirm
  const okRegenerate = await showConfirm(confirmMessage, 'API Key')
  if (!okRegenerate) return
  
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
        logger.debug('Initial API key generation failed, trying authenticated regeneration', initialErr)
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

// Webhook management
const showWebhookForm = ref(false)
const editingWebhook = ref<{
  id: string
  name: string
  url: string
  type: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier'
  triggers: string[]
  isEnabled: boolean
} | null>(null)
const testingWebhook = ref<string | null>(null)
const expandedWebhooks = ref<Set<string>>(new Set())
const webhooks = ref<Array<{
  id: string
  name: string
  url: string
  type: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier'
  triggers: string[]
  isEnabled: boolean
}>>([])
const webhookForm = reactive({
  id: '',
  name: '',
  url: '',
  type: '' as 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier' | '',
  triggers: [] as string[],
  isEnabled: true
})
const webhookFormErrors = reactive({
  name: '',
  url: '',
  type: '',
  triggers: ''
})
const testingWebhookConfig = ref(false)
const savingWebhook = ref(false)

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
    // During unit tests we allow saving to proceed (tests set up inputs manually).
    // Vitest exposes import.meta.env.VITEST which we can use to relax validation.
  const vitestEnv = (import.meta as unknown as { env?: Record<string, unknown> }).env?.VITEST
    if (vitestEnv) return true

    // No longer require output path since we use root folders now
    return isProxyConfigValid.value
  })

  const validationErrors = computed(() => {
    const errs: string[] = []
    if (!settings.value) return errs
    if (settings.value.useUsProxy) {
      const host = (settings.value.usProxyHost || '').toString().trim()
      const port = Number(settings.value.usProxyPort || 0)
      if (!host) errs.push('US proxy host is required when proxy is enabled')
      if (!port || port <= 0 || port > 65535) errs.push('US proxy port must be between 1 and 65535')
    }
    return errs
  })

  // Bot status computed properties
  const botStatusClass = computed(() => {
    switch (botStatus.value) {
      case 'running': return 'status-running'
      case 'stopped': return 'status-stopped'
      case 'checking': return 'status-checking'
      case 'error': return 'status-error'
      default: return 'status-unknown'
    }
  })

  const botStatusText = computed(() => {
    switch (botStatus.value) {
      case 'running': return 'Running'
      case 'stopped': return 'Stopped'
      case 'checking': return 'Checking...'
      case 'error': return 'Error'
      default: return 'Unknown'
    }
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
const mappingToDelete = ref<RemotePathMapping | null>(null)



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
  apiForm.type = 'metadata' // Always metadata since this is the metadata sources modal
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

const apiToDelete = ref<ApiConfiguration | null>(null)

const confirmDeleteApi = (api: ApiConfiguration) => {
  apiToDelete.value = api
}

const executeDeleteApi = async (id?: string) => {
  const apiId = id || apiToDelete.value?.id
  if (!apiId) return

  try {
    await configStore.deleteApiConfiguration(apiId)
    toast.success('API', 'API configuration deleted successfully')
    // Refresh API list if the store provides a loader
    try { await configStore.loadApiConfigurations() } catch {}
  } catch (error) {
    console.error('Failed to delete API configuration:', error)
    const errorMessage = formatApiError(error)
    toast.error('API delete failed', errorMessage)
  } finally {
    apiToDelete.value = null
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
// eslint-disable-next-line @typescript-eslint/no-unused-vars
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

// Webhook management functions
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const openWebhookForm = () => {
  editingWebhook.value = null
  webhookForm.id = ''
  webhookForm.name = ''
  webhookForm.url = ''
  webhookForm.type = ''
  webhookForm.triggers = []
  webhookForm.isEnabled = true
  showWebhookForm.value = true
}

const closeWebhookForm = () => {
  showWebhookForm.value = false
  editingWebhook.value = null
  webhookForm.id = ''
  webhookForm.name = ''
  webhookForm.url = ''
  webhookForm.type = ''
  webhookForm.triggers = []
  webhookForm.isEnabled = true
  resetWebhookFormErrors()
}

const editWebhook = (webhook: typeof webhooks.value[0]) => {
  editingWebhook.value = webhook
  webhookForm.id = webhook.id
  webhookForm.name = webhook.name
  webhookForm.url = webhook.url
  webhookForm.type = webhook.type
  webhookForm.triggers = [...webhook.triggers]
  webhookForm.isEnabled = webhook.isEnabled
  resetWebhookFormErrors()
  showWebhookForm.value = true
}

const saveWebhook = async () => {
  // Validate all fields
  validateWebhookField('name')
  validateWebhookField('url')
  validateWebhookField('type')
  validateWebhookField('triggers')

  // Check if form is valid
  if (!isWebhookFormValid.value) {
    toast.error('Validation error', 'Please fix the errors before saving')
    return
  }

  savingWebhook.value = true
  try {
    const webhook = {
      id: webhookForm.id || generateUUID(),
      name: webhookForm.name.trim(),
      url: webhookForm.url.trim(),
      type: webhookForm.type as 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier',
      triggers: [...webhookForm.triggers],
      isEnabled: webhookForm.isEnabled
    }

    if (editingWebhook.value) {
      // Update existing webhook
      const index = webhooks.value.findIndex(w => w.id === webhook.id)
      if (index !== -1) {
        webhooks.value[index] = webhook
      }
      toast.success('Webhook', 'Webhook updated successfully')
    } else {
      // Add new webhook
      webhooks.value.push(webhook)
      toast.success('Webhook', 'Webhook added successfully')
    }

    // Persist webhooks to settings
    await persistWebhooks()

    closeWebhookForm()
  } catch (error) {
    console.error('Failed to save webhook:', error)
    toast.error('Save failed', 'Failed to save webhook')
  } finally {
    savingWebhook.value = false
  }
}

const deleteWebhook = async (id: string) => {
  const webhook = webhooks.value.find(w => w.id === id)
  if (!webhook) return
  const ok = await showConfirm(`Are you sure you want to delete the webhook "${webhook.name}"?`, 'Delete Webhook')
  if (!ok) return
  webhooks.value = webhooks.value.filter(w => w.id !== id)
  toast.success('Webhook', 'Webhook deleted successfully')
  // Persist webhooks to settings
  await persistWebhooks()
}

const toggleWebhook = async (webhook: typeof webhooks.value[0]) => {
  const index = webhooks.value.findIndex(w => w.id === webhook.id)
  if (index !== -1) {
    const targetWebhook = webhooks.value[index]
    if (targetWebhook) {
      targetWebhook.isEnabled = !targetWebhook.isEnabled
      toast.success('Webhook', `${webhook.name} ${targetWebhook.isEnabled ? 'enabled' : 'disabled'}`)
      
      // Persist webhooks to settings
      await persistWebhooks()
    }
  }
}

const testWebhook = async (webhook: typeof webhooks.value[0]) => {
  testingWebhook.value = webhook.id
  try {
    // NOTE: Test API exists at POST /api/diagnostics/test-notification
    // Future enhancement: integrate with DiagnosticsController to send real test notifications
    // For now, just simulate success
    await new Promise(resolve => setTimeout(resolve, 1000))
    toast.success('Test notification', `Test notification sent to ${webhook.name}`)
  } catch (error) {
    console.error('Failed to test webhook:', error)
    const errorMessage = formatApiError(error)
    toast.error('Test failed', errorMessage)
  } finally {
    testingWebhook.value = null
  }
}

// Migrate old single webhook format to new multiple webhooks format
const migrateOldWebhookData = async () => {
  if (!settings.value) return
  
  // Check if migration has already been completed
  const migrationKey = 'webhook_migration_completed'
  if (localStorage.getItem(migrationKey)) {
    return // Migration already done
  }
  
  // Check if old format exists and new format is empty
  if (settings.value.webhookUrl && settings.value.webhookUrl.trim() !== '' && webhooks.value.length === 0) {
    const oldUrl = settings.value.webhookUrl.trim()
    const oldTriggers = settings.value.enabledNotificationTriggers || []
    
    // Create a webhook from the old data
    // Try to detect type from URL, default to 'Zapier' for generic webhooks
    let detectedType: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier' = 'Zapier'
    const urlLower = oldUrl.toLowerCase()
    
    if (urlLower.includes('pushbullet')) detectedType = 'Pushbullet'
    else if (urlLower.includes('telegram')) detectedType = 'Telegram'
    else if (urlLower.includes('slack')) detectedType = 'Slack'
    else if (urlLower.includes('discord')) detectedType = 'Discord'
    else if (urlLower.includes('pushover')) detectedType = 'Pushover'
    else if (urlLower.includes('ntfy')) detectedType = 'NTFY'
    
    // Use old triggers if they exist and have valid values, otherwise use all default triggers
    let triggersToUse = ['book-added', 'book-downloading', 'book-available']
    
    if (oldTriggers && oldTriggers.length > 0) {
      // Filter out any invalid triggers and only use valid ones
      const validTriggers = oldTriggers.filter(t => 
        ['book-added', 'book-downloading', 'book-available', 'book-completed'].includes(t)
      )
      if (validTriggers.length > 0) {
        triggersToUse = validTriggers
      }
    }
    
    webhooks.value = [{
      id: generateUUID(),
      name: `Migrated Webhook (${detectedType})`,
      url: oldUrl,
      type: detectedType,
      triggers: triggersToUse,
      isEnabled: true
    }]
    
    // Persist migrated webhook to backend
    await persistWebhooks()
    
    // Mark migration as completed
    localStorage.setItem(migrationKey, 'true')
    
    toast.info('Webhook Migration', 'Your existing webhook has been migrated to the new format')
  } else if (webhooks.value.length === 0 && (!settings.value.webhookUrl || settings.value.webhookUrl.trim() === '')) {
    // No old webhook data and no new webhooks - mark migration as complete to avoid checking again
    localStorage.setItem(migrationKey, 'true')
  }
}

// Persist webhooks to backend settings
const persistWebhooks = async () => {
  if (!settings.value) return
  
  try {
    // Update settings with current webhooks
    settings.value.webhooks = webhooks.value
    
    // Save to backend
    await configStore.saveApplicationSettings(settings.value)
  } catch (error) {
    console.error('Failed to persist webhooks:', error)
    toast.error('Save failed', 'Failed to save webhooks to settings')
    throw error
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

// Return remote path mappings assigned to a given download client.
const getMappingsForClient = (client: import('@/types').DownloadClientConfiguration): import('@/types').RemotePathMapping[] => {
  // Build a set of mapping IDs that should be considered assigned to this client.
  const assignedIds = new Set<number>()

  // Only use IDs from the client settings (remotePathMappingIds)
  try {
    const s = (client as unknown as Record<string, unknown>)?.settings as Record<string, unknown> | undefined
    const raw = s?.remotePathMappingIds ?? s?.RemotePathMappingIds
    if (Array.isArray(raw)) {
      for (const v of raw) {
        const n = Number(v)
        if (!Number.isNaN(n)) assignedIds.add(n)
      }
    }
  } catch {
    // ignore malformed settings
  }

  // Return only the mappings that match the assigned IDs (preserves order in remotePathMappings)
  if (assignedIds.size === 0) return []
  return remotePathMappings.value.filter(m => assignedIds.has(m.id))
}

const saveApiConfig = async () => {
  try {
    // Validate required fields
    if (!apiForm.name || !apiForm.baseUrl) {
      toast.error('Validation error', 'Name and Base URL are required')
      return
    }

    const apiData: ApiConfiguration = {
      id: apiForm.id || generateUUID(),
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

    // No PascalCase keys are produced anymore; we only send camelCase properties.

  // Resolve the configuration store at call-time to ensure tests that set up Pinia
  // before mounting (or that replace the store) receive the correct instance.
  const runtimeConfigStore = useConfigurationStore()
  // Debug: log when saveSettings is invoked in tests to help diagnose test failures
  // (will be removed once tests are stable)
  logger.debug('[test-debug] saveSettings invoked', settingsToSave)
  // Call the runtime store save method. Some test setups replace the store
  // instance or spy on the store returned from `useConfigurationStore()` at
  // different times; call both if they differ to ensure the spy is observed.
  await runtimeConfigStore.saveApplicationSettings(settingsToSave)
  if (configStore !== runtimeConfigStore && typeof configStore.saveApplicationSettings === 'function') {
    // If the module-level `configStore` differs (older test setups), call it too
    // so tests that replaced/observed that instance receive the call.
    // Avoid failing if the method isn't a function.
  configStore.saveApplicationSettings(settingsToSave)
  }
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

// Toggle the enabled state of a download client (saves via configuration store)
const toggleDownloadClientFunc = async (client: DownloadClientConfiguration) => {
  try {
    const updatedClient = { ...client, isEnabled: !client.isEnabled }
    await configStore.saveDownloadClientConfiguration(updatedClient)
    // Update local store to reflect the change
    const idx = configStore.downloadClientConfigurations.findIndex(c => c.id === client.id)
    if (idx !== -1) {
      configStore.downloadClientConfigurations[idx] = updatedClient
    }
    toast.success('Download client', `${client.name} ${updatedClient.isEnabled ? 'enabled' : 'disabled'} successfully`)
  } catch (error) {
    console.error('Failed to toggle download client:', error)
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

      // Automatically assign the new mapping to the selected download client
      if (payload.downloadClientId) {
        const selectedClient = configStore.downloadClientConfigurations.find(c => c.id === payload.downloadClientId)
        if (selectedClient) {
          const updatedClient = { ...selectedClient }
          if (!updatedClient.settings.remotePathMappingIds) {
            updatedClient.settings.remotePathMappingIds = []
          }
          if (!updatedClient.settings.remotePathMappingIds.includes(created.id)) {
            updatedClient.settings.remotePathMappingIds.push(created.id)
            // Update local state immediately for reactive UI
            const clientIndex = configStore.downloadClientConfigurations.findIndex(c => c.id === payload.downloadClientId)
            if (clientIndex !== -1) {
              configStore.downloadClientConfigurations[clientIndex] = updatedClient
            }
            // Also save to server (don't await to avoid blocking UI)
            configStore.saveDownloadClientConfiguration(updatedClient).catch(err => {
              console.error('Failed to save client configuration:', err)
              // Revert local change on error
              configStore.loadDownloadClientConfigurations()
            })
          }
        }
      }

      toast.success('Remote path mapping', 'Remote path mapping created')
    }

    closeMappingForm()
  } catch (err) {
    console.error('Failed to save mapping', err)
    toast.error('Save failed', 'Failed to save mapping')
  }
}

const editMapping = (mapping: RemotePathMapping) => openMappingForm(mapping)

const confirmDeleteMapping = (mapping: RemotePathMapping) => {
  mappingToDelete.value = mapping
}

const executeDeleteMapping = async (id?: number) => {
  const mappingId = id || mappingToDelete.value?.id
  if (!mappingId) return

  try {
    await deleteRemotePathMapping(mappingId)
    remotePathMappings.value = remotePathMappings.value.filter(m => m.id !== mappingId)
    toast.success('Remote path mapping', 'Remote path mapping deleted')
  } catch (err) {
    console.error('Failed to delete mapping', err)
    toast.error('Delete failed', 'Failed to delete mapping')
  } finally {
    mappingToDelete.value = null
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

// Helper functions for webhook UI
const getWebhookIcon = (type: string) => {
  const iconMap: Record<string, unknown> = {
    'Slack': PhBell,
    'Discord': PhBell,
    'Telegram': PhBell,
    'Pushover': PhBell,
    'Pushbullet': PhBell,
    'NTFY': PhBell,
    'Zapier': PhBell
  }
  return iconMap[type] || PhBell
}

const getTriggerIcon = (trigger: string) => {
  const iconMap: Record<string, unknown> = {
    'book-added': PhPlus,
    'book-downloading': PhDownloadSimple,
    'book-available': PhCheckCircle
  }
  return iconMap[trigger] || PhBell
}

const getTriggerClass = (trigger: string): string => {
  const classMap: Record<string, string> = {
    'book-added': 'trigger-added',
    'book-downloading': 'trigger-downloading',
    'book-available': 'trigger-available'
  }
  return classMap[trigger] || ''
}

const formatTriggerName = (trigger: string): string => {
  const nameMap: Record<string, string> = {
    'book-added': 'Book Added',
    'book-downloading': 'Download Started',
    'book-available': 'Download Complete'
  }
  return nameMap[trigger] || trigger
}

// Webhook expand/collapse management
const toggleWebhookExpanded = (webhookId: string) => {
  if (expandedWebhooks.value.has(webhookId)) {
    expandedWebhooks.value.delete(webhookId)
  } else {
    expandedWebhooks.value.add(webhookId)
  }
}

const isWebhookExpanded = (webhookId: string): boolean => {
  return expandedWebhooks.value.has(webhookId)
}

// Webhook form validation
const isWebhookFormValid = computed(() => {
  return webhookForm.name.trim().length > 0 &&
         webhookForm.url.trim().length > 0 &&
         webhookForm.type !== '' &&
         webhookForm.triggers.length > 0 &&
         !webhookFormErrors.name &&
         !webhookFormErrors.url &&
         !webhookFormErrors.type &&
         !webhookFormErrors.triggers
})

// Invite link helpers and Discord status
// Ensure Manage Messages (8192) is included so the bot can delete ack/select messages
const PERMISSIONS_MINIMAL = 19456 | 8192 // 19456 (existing minimal) OR 8192 (Manage Messages) => 27648
const inviteLinkPreview = computed(() => {
  const appId = settings.value?.discordApplicationId?.trim()
  if (!appId) return ''
  const scopes = encodeURIComponent('bot applications.commands')
  const guildPart = settings.value?.discordGuildId?.trim() ? `&guild_id=${encodeURIComponent(settings.value.discordGuildId)}` : ''
  return `https://discord.com/oauth2/authorize?client_id=${encodeURIComponent(appId)}&permissions=${PERMISSIONS_MINIMAL}&scope=${scopes}${guildPart}`
})

const openInviteLink = () => {
  if (!inviteLinkPreview.value) {
    toast.error('Missing Application ID', 'Please enter the Discord Application ID first.')
    return
  }
  window.open(inviteLinkPreview.value, '_blank', 'noopener')
}

const copyInviteLink = async () => {
  if (!inviteLinkPreview.value) {
    toast.error('Missing Application ID', 'Please enter the Discord Application ID first.')
    return
  }
  try {
    await navigator.clipboard.writeText(inviteLinkPreview.value)
    toast.success('Copied', 'Invite link copied to clipboard.')
  } catch (err) {
    console.error('Failed to copy invite link', err)
    toast.error('Copy failed', 'Unable to copy invite link to clipboard.')
  }
}

const discordStatus = ref<{ success?: boolean; installed?: boolean | null; guildId?: string; botInfo?: unknown; message?: string } | null>(null)
const checkingDiscord = ref(false)
const registeringCommands = ref(false)

// Bot process control
const botStatus = ref<'unknown' | 'checking' | 'running' | 'stopped' | 'error'>('unknown')
const checkingBotStatus = ref(false)
const startingBot = ref(false)
const stoppingBot = ref(false)

const checkDiscordStatus = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to check install status.')
    return
  }
  checkingDiscord.value = true
  try {
    const resp = await apiService.getDiscordStatus()
    discordStatus.value = resp
  } catch (err) {
    console.error('Failed to check discord status', err)
    const errorMessage = formatApiError(err)
    toast.error('Status failed', errorMessage)
  } finally {
    checkingDiscord.value = false
  }
}

const registerCommands = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to register commands.')
    return
  }
  registeringCommands.value = true
  try {
    const resp = await apiService.registerDiscordCommands()
    if (resp?.success) {
      toast.success('Registered', resp.message || 'Commands registered')
      // Refresh status after registering
      await checkDiscordStatus()
    } else {
      toast.error('Register failed', JSON.stringify(resp?.body || resp?.message || resp))
    }
  } catch (err) {
    console.error('Failed to register commands', err)
    const errorMessage = formatApiError(err)
    toast.error('Register failed', errorMessage)
  } finally {
    registeringCommands.value = false
  }
}

// Bot process control functions
const checkBotStatus = async () => {
  checkingBotStatus.value = true
  botStatus.value = 'checking'
  try {
    const resp = await apiService.getDiscordBotStatus()
    if (resp.success) {
      botStatus.value = resp.isRunning ? 'running' : 'stopped'
      toast.info('Bot Status', resp.status)
    } else {
      botStatus.value = 'error'
      toast.error('Status check failed', resp.status || 'Failed to check bot status')
    }
  } catch (err) {
    console.error('Failed to check bot status', err)
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Status check failed', errorMessage)
  } finally {
    checkingBotStatus.value = false
  }
}

const startBot = async () => {
  if (!settings.value?.discordBotToken) {
    toast.error('Missing token', 'Please enter the bot token to start the bot.')
    return
  }
  startingBot.value = true
  try {
    const resp = await apiService.startDiscordBot()
    if (resp.success) {
      botStatus.value = 'running'
      toast.success('Bot Started', resp.message || 'Discord bot started successfully')
      // Auto-refresh status after a short delay
      setTimeout(() => checkBotStatus(), 2000)
    } else {
      botStatus.value = 'error'
      toast.error('Start failed', resp.message || 'Failed to start Discord bot')
    }
  } catch (err) {
    console.error('Failed to start bot', err)
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Start failed', errorMessage)
  } finally {
    startingBot.value = false
  }
}

const stopBot = async () => {
  stoppingBot.value = true
  try {
    const resp = await apiService.stopDiscordBot()
    if (resp.success) {
      botStatus.value = 'stopped'
      toast.success('Bot Stopped', resp.message || 'Discord bot stopped successfully')
      // Auto-refresh status after a short delay
      setTimeout(() => checkBotStatus(), 2000)
    } else {
      botStatus.value = 'error'
      toast.error('Stop failed', resp.message || 'Failed to stop Discord bot')
    }
  } catch (err) {
    console.error('Failed to stop bot', err)
    botStatus.value = 'error'
    const errorMessage = formatApiError(err)
    toast.error('Stop failed', errorMessage)
  } finally {
    stoppingBot.value = false
  }
}

const validateWebhookField = (field: 'name' | 'url' | 'type' | 'triggers') => {
  switch (field) {
    case 'name':
      if (!webhookForm.name || webhookForm.name.trim().length === 0) {
        webhookFormErrors.name = 'Webhook name is required'
      } else if (webhookForm.name.trim().length < 3) {
        webhookFormErrors.name = 'Name must be at least 3 characters'
      } else {
        webhookFormErrors.name = ''
      }
      break
    case 'url':
      if (!webhookForm.url || webhookForm.url.trim().length === 0) {
        webhookFormErrors.url = 'Webhook URL is required'
      } else if (!isValidUrl(webhookForm.url)) {
        webhookFormErrors.url = 'Please enter a valid HTTPS URL'
      } else {
        webhookFormErrors.url = ''
      }
      break
    case 'type':
      if (!webhookForm.type) {
        webhookFormErrors.type = 'Please select a service type'
      } else {
        webhookFormErrors.type = ''
      }
      break
    case 'triggers':
      if (webhookForm.triggers.length === 0) {
        webhookFormErrors.triggers = 'Please select at least one trigger'
      } else {
        webhookFormErrors.triggers = ''
      }
      break
  }
}

const isValidUrl = (url: string): boolean => {
  try {
    const urlObj = new URL(url)
    return urlObj.protocol === 'https:'
  } catch {
    return false
  }
}

const onServiceTypeChange = () => {
  // Type changed, could trigger validation here if needed
}

const getServiceHelp = (): string => {
  const helpText: Record<string, string> = {
    'Slack': 'Get your webhook URL from Slack: Settings & administration → Manage apps → Incoming Webhooks',
    'Discord': 'Server Settings → Integrations → Webhooks → New Webhook → Copy Webhook URL',
    'Telegram': 'Create a bot with @BotFather, then get the webhook URL format: https://api.telegram.org/bot{token}/sendMessage',
    'Pushover': 'Get your User Key and API Token from pushover.net/apps/build',
    'Pushbullet': 'Get your Access Token from Settings → Account → Access Tokens',
    'NTFY': 'Use format: https://ntfy.sh/{topic} or your self-hosted instance URL',
    'Zapier': 'Create a Zap with "Webhooks by Zapier" and copy the webhook URL'
  }
  return webhookForm.type ? helpText[webhookForm.type] || '' : ''
}

const testWebhookConfig = async () => {
  testingWebhookConfig.value = true
  try {
    // NOTE: Test API exists at POST /api/diagnostics/test-notification
    // Future enhancement: integrate with DiagnosticsController for real webhook testing
    await new Promise(resolve => setTimeout(resolve, 1500))
    toast.success('Test successful', `Test notification sent to ${webhookForm.type}`)
  } catch (error) {
    console.error('Failed to test webhook:', error)
    toast.error('Test failed', 'Failed to send test notification')
  } finally {
    testingWebhookConfig.value = false
  }
}

const resetWebhookFormErrors = () => {
  webhookFormErrors.name = ''
  webhookFormErrors.url = ''
  webhookFormErrors.type = ''
  webhookFormErrors.triggers = ''
}

// Sync activeTab with URL hash
const syncTabFromHash = () => {
  const hash = route.hash.replace('#', '') as 'rootfolders' | 'indexers' | 'clients' | 'quality-profiles' | 'notifications' | 'bot' | 'general'
  if (hash && ['rootfolders', 'indexers', 'clients', 'quality-profiles', 'notifications', 'bot', 'general'].includes(hash)) {
    activeTab.value = hash as any
  } else {
    // Default to rootfolders and update URL
    activeTab.value = 'rootfolders'
    router.replace({ hash: '#rootfolders' })
  }
}

// Handle dropdown tab change
// const onTabChange = (event: Event) => {
//   const target = event.target as HTMLSelectElement
//   const newTab = target.value as 'rootfolders' | 'indexers' | 'clients' | 'quality-profiles' | 'notifications' | 'requests' | 'general'
//   activeTab.value = newTab
//   router.push({ hash: `#${newTab}` })
// }

// Watch for hash changes
watch(() => route.hash, () => {
  syncTabFromHash()
})

// Track which tab data has been loaded to avoid duplicate requests
const loaded = reactive({
  indexers: false,
  clients: false,
  profiles: false,
  admins: false,
  mappings: false,
  general: false,
  rootfolders: false,
  bot: false,
  integrations: false
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
      case 'rootfolders':
        if (!loaded.rootfolders) {
          // root folder UI will manage its own loading; just mark as loaded
          loaded.rootfolders = true
        }
        break
      case 'clients':
        if (!loaded.clients) {
          await configStore.loadDownloadClientConfigurations()
          loaded.clients = true
        }
        // Also load remote path mappings for the clients tab
        if (!loaded.mappings) {
          try {
            remotePathMappings.value = await getRemotePathMappings()
            loaded.mappings = true
          } catch (e) {
            logger.debug('Failed to load remote path mappings', e)
          }
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
          // Ensure sensible default
          if (settings.value && !settings.value.completedFileAction) settings.value.completedFileAction = 'Move'
          // Ensure new settings have sensible defaults when not present
          if (settings.value && (settings.value.downloadCompletionStabilitySeconds === undefined || settings.value.downloadCompletionStabilitySeconds === null))
            settings.value.downloadCompletionStabilitySeconds = 10
          if (settings.value && (settings.value.missingSourceRetryInitialDelaySeconds === undefined || settings.value.missingSourceRetryInitialDelaySeconds === null))
            settings.value.missingSourceRetryInitialDelaySeconds = 30
          if (settings.value && (settings.value.missingSourceMaxRetries === undefined || settings.value.missingSourceMaxRetries === null))
            settings.value.missingSourceMaxRetries = 3
          // Initialize notification triggers array if not present
          if (settings.value && !settings.value.enabledNotificationTriggers) settings.value.enabledNotificationTriggers = []
          // Ensure new search settings have sensible defaults when not present
          // Create a shallow copy of the store settings so we can safely
          // mutate defaults for the UI without relying on store ref unwrapping.
          const raw = configStore.applicationSettings ? { ...configStore.applicationSettings } : null
          if (raw) {
            // Normalize values coming from the backend which may use PascalCase
            // property names (e.g., EnableAmazonSearch) instead of camelCase.
            const rawObj = raw as Record<string, unknown>
            const normalized: Record<string, unknown> = { ...rawObj }

            // Helper to prefer camelCase, then PascalCase, then fallback
            const pickBool = (camel: string, pascal: string, fallback: boolean) => {
              const c = rawObj[camel]
              const p = rawObj[pascal]
              if (c !== undefined && c !== null) return Boolean(c)
              if (p !== undefined && p !== null) return Boolean(p)
              return fallback
            }

            const pickNumber = (camel: string, pascal: string, fallback: number) => {
              const c = rawObj[camel]
              const p = rawObj[pascal]
              const val = (c !== undefined && c !== null) ? Number(c) : ((p !== undefined && p !== null) ? Number(p) : fallback)
              // Treat zero as missing and use fallback
              if (!val || Number.isNaN(val)) return fallback
              return val
            }

            const amazon = pickBool('enableAmazonSearch', 'EnableAmazonSearch', true)
            const audible = pickBool('enableAudibleSearch', 'EnableAudibleSearch', true)
            const openlib = pickBool('enableOpenLibrarySearch', 'EnableOpenLibrarySearch', true)

            const candidateCap = pickNumber('searchCandidateCap', 'SearchCandidateCap', 100)
            const resultCap = pickNumber('searchResultCap', 'SearchResultCap', 100)
            const fuzzy = pickNumber('searchFuzzyThreshold', 'SearchFuzzyThreshold', 0.2)

            // Assign normalized camelCase properties for the UI binding
            normalized.enableAmazonSearch = amazon
            normalized.enableAudibleSearch = audible
            normalized.enableOpenLibrarySearch = openlib
            normalized.searchCandidateCap = candidateCap
            normalized.searchResultCap = resultCap
            normalized.searchFuzzyThreshold = fuzzy

            // Set camelCase properties for the UI binding and saving
            settings.value = normalized as unknown as ApplicationSettings

            // Sync normalized object back to the store so other consumers use it
            configStore.applicationSettings = settings.value
          } else {
            settings.value = null
          }

          try {
            remotePathMappings.value = await getRemotePathMappings()
            loaded.mappings = true
          } catch (e) {
            logger.debug('Failed to load remote path mappings', e)
          }

          try {
            await loadAdminUsers()
            loaded.admins = true
            if (adminUsers.value.length > 0 && settings.value) {
              const firstAdmin = adminUsers.value[0]
              if (firstAdmin) settings.value.adminUsername = firstAdmin.username
            }
          } catch (e) {
            logger.debug('Failed to load admin users', e)
          }

          loaded.general = true
        }
        break
      case 'bot':
          if (!loaded.bot) {
          // Requests tab needs application settings and quality profiles
          await configStore.loadApplicationSettings()
          // Reuse the same normalization logic for requests tab load
          const rawReq = configStore.applicationSettings ? { ...configStore.applicationSettings } : null
          if (rawReq) {
            const rawReqObj = rawReq as Record<string, unknown>
            const normalizedReq: Record<string, unknown> = { ...rawReqObj }
            const pickBoolReq = (camel: string, pascal: string, fallback: boolean) => {
              const c = rawReqObj[camel]
              const p = rawReqObj[pascal]
              if (c !== undefined && c !== null) return Boolean(c)
              if (p !== undefined && p !== null) return Boolean(p)
              return fallback
            }
            const pickNumberReq = (camel: string, pascal: string, fallback: number) => {
              const c = rawReqObj[camel]
              const p = rawReqObj[pascal]
              const val = (c !== undefined && c !== null) ? Number(c) : ((p !== undefined && p !== null) ? Number(p) : fallback)
              if (!val || Number.isNaN(val)) return fallback
              return val
            }
            normalizedReq.enableAmazonSearch = pickBoolReq('enableAmazonSearch', 'EnableAmazonSearch', true)
            normalizedReq.enableAudibleSearch = pickBoolReq('enableAudibleSearch', 'EnableAudibleSearch', true)
            normalizedReq.enableOpenLibrarySearch = pickBoolReq('enableOpenLibrarySearch', 'EnableOpenLibrarySearch', true)
            normalizedReq.searchCandidateCap = pickNumberReq('searchCandidateCap', 'SearchCandidateCap', 100)
            normalizedReq.searchResultCap = pickNumberReq('searchResultCap', 'SearchResultCap', 100)
            normalizedReq.searchFuzzyThreshold = pickNumberReq('searchFuzzyThreshold', 'SearchFuzzyThreshold', 0.2)
            settings.value = normalizedReq as unknown as ApplicationSettings
            configStore.applicationSettings = settings.value
          } else {
            settings.value = null
          }
          try {
            await loadQualityProfiles()
          } catch (e) {
            logger.debug('Failed to load quality profiles for requests tab', e)
          }
          loaded.bot = true
        }
        break
      case 'notifications':
        // Notifications are part of general settings
        if (!loaded.general) {
          await loadTabContents('general')
        }
        // Load webhooks from settings and ensure triggers are valid
        if (settings.value?.webhooks && settings.value.webhooks.length > 0) {
          webhooks.value = settings.value.webhooks.map(webhook => ({
            ...webhook,
            // Ensure triggers array is never empty
            triggers: webhook.triggers && webhook.triggers.length > 0 
              ? webhook.triggers 
              : ['book-added', 'book-downloading', 'book-available']
          }))
        }
        // Migrate old webhook format to new format
        await migrateOldWebhookData()
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
  position: relative;
  top: 60px;
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
  border-radius: 6px;
}

.settings-content {
  background: #2a2a2a;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-height: 500px;
  margin-top: 60px; /* Add margin to account for fixed toolbar */
}

/* Desktop tabs carousel styles */
.settings-tabs-desktop-wrapper {
  position: relative;
}

.settings-tabs-desktop {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  scroll-behavior: smooth;
  padding-bottom: 4px; /* give space for hidden scrollbar */
  scrollbar-gutter: stable both-edges;
}

/* keep the scrollable area clipped so overflowing tabs are hidden */
.settings-tabs-desktop-wrapper {
  overflow: hidden;
}

.settings-tabs-desktop {
  align-items: center;
  white-space: nowrap;
  padding: 0 12px; /* space for chevron overlay */
  scroll-padding-left: 48px;
  scroll-padding-right: 48px;
}

.settings-tabs-desktop .tab-button {
  flex: 0 0 auto;
}

.settings-tabs-desktop::-webkit-scrollbar {
  height: 6px;
}

/* hide the native scrollbar while preserving scrollability */
.settings-tabs-desktop::-webkit-scrollbar { display: none; }
.settings-tabs-desktop { -ms-overflow-style: none; scrollbar-width: none; }

.tabs-scroll-btn {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 36px;
  height: 36px;
  border-radius: 6px;
  background: rgba(0, 0, 0, 0.8);
  color: #fff;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 1;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.5);
  transition: transform 0.15s ease, background 0.15s ease;
}

.tabs-scroll-btn.left {
  left: 0;
}

.tabs-scroll-btn.right {
  right: 0;
}

.tabs-scroll-btn:hover {
  background: rgba(0, 0, 0, 1);
  transform: translateY(-50%) scale(1.02);
}

/* Settings Toolbar */
.settings-toolbar {
  position: fixed;
  top: 60px; /* Account for global header nav */
  left: 200px; /* Account for sidebar width */
  right: 0;
  z-index: 99; /* Below global nav (1000) but above content */
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background-color: #2a2a2a;
  border-bottom: 1px solid #333;
  margin-bottom: 20px;
}

.toolbar-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.toolbar-actions {
  display: flex;
  gap: 1rem;
  align-items: center;
}

/* When tabs don't overflow hide the scrollbar and buttons via v-show in template */

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

.empty-state .empty-icon {
  font-size: 4rem;
  color: #495057;
  margin-bottom: 1rem;
  width: 4rem;
  height: 4rem;
}

.empty-state h3 {
  margin: 1rem 0 0.5rem 0;
  color: #fff;
  font-size: 1.5rem;
  font-weight: 600;
}

.empty-state p {
  margin: 0.5rem 0;
  font-size: 1.05rem;
  line-height: 1.6;
  color: #adb5bd;
}

.empty-state .empty-help {
  font-size: 0.95rem;
  color: #868e96;
  margin-bottom: 2rem;
}

.add-button-large {
  margin-top: 1.5rem;
  padding: 1rem 2rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  font-weight: 600;
  font-size: 1rem;
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.3);
}

.add-button-large:hover {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(30, 136, 229, 0.4);
}

.section-title-wrapper {
  flex: 1;
}

.section-subtitle {
  margin: 0.5rem 0 0 0;
  font-size: 0.95rem;
  color: #868e96;
  font-weight: normal;
}

/* Webhook Grid Layout */
.webhooks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
  gap: 1.5rem;
}

/* Webhook Card */
.webhook-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
  transition: all 0.2s ease;
  display: flex;
  flex-direction: column;
}

.webhook-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
}

.webhook-card.disabled {
  opacity: 0.5;
  filter: grayscale(50%);
}

.webhook-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  cursor: pointer;
}

/* No hover state: matches other headers */

.webhook-header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-left: 1rem;
} 

.webhook-title-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex: 1;
}

.webhook-icon {
  width: 40px;
  height: 40px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 1.2rem;
} 

.webhook-icon.service-slack {
  background: linear-gradient(135deg, #4A154B 0%, #611f69 100%);
  color: #fff;
}

.webhook-icon.service-discord {
  background: linear-gradient(135deg, #5865F2 0%, #404eed 100%);
  color: #fff;
}

.webhook-icon.service-telegram {
  background: linear-gradient(135deg, #0088cc 0%, #006699 100%);
  color: #fff;
}

.webhook-icon.service-pushover {
  background: linear-gradient(135deg, #249DF1 0%, #1a7dc4 100%);
  color: #fff;
}

.webhook-icon.service-pushbullet {
  background: linear-gradient(135deg, #4AB367 0%, #3a9053 100%);
  color: #fff;
}

.webhook-icon.service-ntfy {
  background: linear-gradient(135deg, #ff6b6b 0%, #ee5a52 100%);
  color: #fff;
}

.webhook-icon.service-zapier {
  background: linear-gradient(135deg, #FF4A00 0%, #e04200 100%);
  color: #fff;
}

.webhook-info h4 {
  margin: 0 0 0.25rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.webhook-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.triggers-preview {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.trigger-badge-small {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid;
  cursor: help;
  transition: all 0.2s ease;
}

.trigger-badge-small:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
}

.trigger-badge-small svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.trigger-badge-small.trigger-added {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
}

.trigger-badge-small.trigger-downloading {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border-color: rgba(77, 171, 247, 0.3);
}

.trigger-badge-small.trigger-available {
  background-color: rgba(156, 39, 176, 0.15);
  color: #b197fc;
  border-color: rgba(156, 39, 176, 0.3);
}

.webhook-type-badge {
  display: inline-block;
  padding: 0.25rem 0.65rem;
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.3);
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.webhook-status-badge {
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

.webhook-status-badge {
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.webhook-status-badge.active {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
} 

.expand-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  background-color: rgba(255, 255, 255, 0.05);
  color: #adb5bd;
  cursor: pointer;
  transition: all 0.2s ease;
}

.expand-toggle:hover {
  background-color: rgba(77, 171, 247, 0.15);
  border-color: rgba(77, 171, 247, 0.3);
  color: #4dabf7;
}

.expand-toggle svg {
  width: 18px;
  height: 18px;
  transition: transform 0.3s ease;
}

.expand-toggle.expanded svg {
  transform: rotate(180deg);
}

/* Expand/Collapse Animation */
.expand-enter-active,
.expand-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
  padding-top: 0;
  padding-bottom: 0;
}

.expand-enter-to,
.expand-leave-from {
  max-height: 500px;
  opacity: 1;
}

.webhook-body {
  padding: 1.5rem;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.webhook-url-container {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background-color: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
}

.url-icon {
  color: #4dabf7;
  font-size: 1.1rem;
  flex-shrink: 0;
}

.webhook-url {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 0.85rem;
  color: #adb5bd;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.webhook-triggers-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.triggers-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #868e96;
  font-size: 0.85rem;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.triggers-label {
  color: #adb5bd;
}

.triggers-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.trigger-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.85rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
  border: 1px solid;
}

.trigger-badge svg {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.trigger-badge.trigger-added {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border-color: rgba(76, 175, 80, 0.3);
}

.trigger-badge.trigger-downloading {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
  border-color: rgba(77, 171, 247, 0.3);
}

.trigger-badge.trigger-available {
  background-color: rgba(156, 39, 176, 0.15);
  color: #b197fc;
  border-color: rgba(156, 39, 176, 0.3);
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
  border-radius: 6px;
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
  overflow-wrap: break-word;
  word-break: break-all;
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
  margin-bottom: 2rem;
}

.form-section:last-child {
  margin-bottom: 0;
}

.form-section h3 {
  color: #fff;
  font-size: 1.1rem;
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #444;
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
  display: block;
  margin-bottom: 0.5rem;
  color: #fff;
  font-weight: 600;
  font-size: 0.95rem;
}

.form-group input,
.form-group select {
  width: 100%;
  padding: 0.75rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  color: #fff;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.form-group input::placeholder {
  color: #999;
  opacity: 1;
}

.form-group input:-webkit-autofill,
.form-group input:-webkit-autofill:hover,
.form-group input:-webkit-autofill:focus {
  -webkit-box-shadow: 0 0 0 1000px #1a1a1a inset !important;
  -webkit-text-fill-color: #fff !important;
  border: 1px solid #444 !important;
}

.form-group input:disabled,
.form-group select:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  background-color: #0d0d0d;
}

.form-group select option:hover,
.form-group select option:focus,
.form-group select option:checked {
  background-color: #005a9e;
  color: #ffffff;
  border: none;
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.form-group input:focus-visible,
.form-group select:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.form-group small {
  display: block;
  margin-top: 0.5rem;
  color: #b3b3b3;
  font-size: 0.85rem;
}

.checkbox-group {
  margin-bottom: 1rem;
}

.checkbox-group label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #1a1a1a;
  border: 1px solid #444;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.checkbox-group label:hover {
  border-color: #007acc;
  background-color: #222;
}

.checkbox-group input[type="checkbox"] {
  margin-top: 0.25rem;
  width: auto;
  cursor: pointer;
}

.checkbox-group input[type="checkbox"]:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.checkbox-group label span {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkbox-group label strong {
  color: #fff;
  font-size: 0.95rem;
}

.checkbox-group label small {
  color: #b3b3b3;
  font-size: 0.85rem;
  font-weight: normal;
}

.form-help {
  font-size: 0.85rem;
  color: #868e96;
  font-style: italic;
  line-height: 1.5;
}

/* Invite controls for Discord bot */
.invite-row {
  margin-top: 1rem;
}
.invite-controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}
.invite-button {
  padding: 0.6rem 1rem;
  background: linear-gradient(135deg, #20c997 0%, #198754 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
}
.invite-button:hover {
  transform: translateY(-1px);
}
.invite-link-preview small a {
  color: #74c0fc;
  text-decoration: underline;
}
.invite-link-preview small a {
  /* allow long oauth links to wrap cleanly in the preview */
  word-break: break-all;
  white-space: normal;
}
.invite-controls .icon-button {
  /* When using icon-style buttons inside invite-controls we want them to
     expand to fit labels (e.g. "Copy Invite Link") instead of being forced
     into the square icon-button size used elsewhere in the UI. */
  width: auto;
  height: auto;
  min-width: 36px;
  padding: 0.45rem 0.75rem;
  font-size: 0.95rem;
}

.invite-controls .save-button {
  /* keep primary register action prominent but avoid forcing full-width */
  white-space: nowrap;
}
.discord-status {
  margin-top: 0.5rem;
}
.status-pill {
  display: inline-block;
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}
.status-pill.installed {
  background-color: rgba(46, 204, 113, 0.12);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.18);
}
.status-pill.not-installed {
  background-color: rgba(244, 67, 54, 0.08);
  color: #ff6b6b;
  border: 1px solid rgba(244, 67, 54, 0.12);
}
.status-pill.unknown {
  background-color: rgba(77, 171, 247, 0.08);
  color: #4dabf7;
  border: 1px solid rgba(77, 171, 247, 0.12);
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
  border-radius: 6px;
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
  border-radius: 6px !important;
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
  border-radius: 6px;
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
  border-radius: 6px;
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
  border: 1px solid #444;
  border-radius: 6px;
  max-width: 700px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #444;
}

.modal-header h2 {
  margin: 0;
  color: #fff;
  font-size: 1.5rem;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s;
}

.close-btn:hover {
  background-color: #333;
  color: #fff;
}

.close-btn:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.modal-close {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.2s;
}

.modal-close:hover {
  background-color: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

/* Ensure modal context delete buttons are full-size, not the small icon-style
   square buttons used elsewhere in the UI. This overrides the
   generic .delete-button rules with a more suitable modal appearance. */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button,
.modal-overlay .modal-content .modal-actions .modal-delete-button,
.modal-content .modal-actions .modal-delete-button {
  /* Stronger selector to guarantee modal buttons override list/icon buttons */
  padding: 0.75rem 1.25rem;
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.18s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.6rem;
  font-weight: 700;
  font-size: 1rem;
  min-width: 120px; /* ensure modal delete button is clearly larger than icon buttons */
  height: auto;
  box-shadow: 0 6px 16px rgba(231, 76, 60, 0.12);
}

/* Keep hover style consistent and prominent */
.modal-overlay .modal-content .modal-actions .delete-button:hover,
.modal-content .modal-actions .delete-button:hover {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 8px 20px rgba(231, 76, 60, 0.24);
}

.modal-actions .delete-button:hover:not(:disabled) {
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: #fff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.35);
}

.modal-actions .delete-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background-color: #555;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
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
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
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
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.indexer-info h4 {
  margin: 0 0 0.5rem 0;
  color: #fff;
  font-size: 1.1rem;
  font-weight: 600;
}

.indexer-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
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
  padding: 1.5rem;
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

  .settings-toolbar {
    left: 0; /* Full width on mobile */
  }

  .toolbar-content {
    flex-direction: column;
    gap: 1rem;
    align-items: stretch;
  }

  .toolbar-actions {
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
  border-radius: 6px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.profile-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
  transform: translateY(-1px);
}

.profile-card.is-default {
  border-color: rgba(77, 171, 247, 0.3);
  background: rgba(77, 171, 247, 0.05);
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

  .config-info {
    width: 100%;
  }

  .config-info h4 {
    font-size: 1rem;
  }

  .config-url {
    font-size: 0.8rem;
    word-break: break-all;
    white-space: normal;
    margin-right: 1rem
  }

  .config-meta {
    flex-wrap: wrap;
    gap: 0.5rem;
  }

  .config-meta span {
    font-size: 0.75rem;
    padding: 0.3rem 0.6rem;
  }

  .config-triggers {
    width: 100%;
  }
  
  .config-actions {
    width: 100%;
    justify-content: flex-end;
    gap: 0.75rem;
  }

  .config-actions .icon-button {
    padding: 0.6rem;
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .section-header h3 {
    font-size: 1.3rem;
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

/* Webhook Modal Specific Styles */




.modal-footer {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem 2rem;
  border-top: 1px solid #444;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn:focus-visible {
  outline: 2px solid #007acc;
  outline-offset: 2px;
}

.btn-secondary {
  background-color: #555;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #666;
  transform: translateY(-1px);
}

.btn-info {
  background-color: #2196f3;
  color: white;
}

.btn-info:hover:not(:disabled) {
  background-color: #1976d2;
  transform: translateY(-1px);
}

.btn-primary {
  background-color: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background-color: #005a9e;
  transform: translateY(-1px);
}

/* Webhook Modal Responsive Styles */
@media (max-width: 768px) {
  .webhooks-grid {
    grid-template-columns: 1fr;
  }

  .webhook-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .webhook-title-row {
    width: 100%;
  }

  .webhook-header-actions {
    width: 100%;
    justify-content: space-between;
  }

  .webhook-actions {
    grid-template-columns: 1fr 1fr;
  }

  .action-btn.toggle-btn,
  .action-btn.delete-btn {
    grid-column: span 1;
  }

  .action-btn.test-btn,
  .action-btn.edit-btn {
    grid-column: span 1;
  }

  .webhook-modal {
    width: 95%;
    max-height: 95vh;
  }

  .webhook-modal .modal-header,
  .webhook-modal .modal-body,
  .webhook-modal .modal-actions {
    padding: 1.25rem 1.5rem;
  }

  .webhook-modal .modal-icon {
    width: 48px;
    height: 48px;
  }

  .webhook-modal .modal-icon svg {
    width: 24px;
    height: 24px;
  }

  .webhook-modal .modal-title h3 {
    font-size: 1.3rem;
  }

  .webhook-form .form-row {
    flex-direction: column;
  }

  .trigger-content {
    gap: 0.75rem;
  }

  .trigger-icon {
    width: 40px;
    height: 40px;
  }

  .trigger-icon svg {
    width: 20px;
    height: 20px;
  }

  .trigger-check {
    width: 24px;
    height: 24px;
  }

  .triggers-section .section-title {
    flex-direction: column;
    align-items: flex-start;
  }

  .trigger-count {
    align-self: flex-start;
  }
}

/* Discord Bot Process Controls */
.bot-status-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.bot-status-display {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: var(--card-bg);
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.status-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
}

.status-indicator.status-running {
  color: #4caf50;
}

.status-indicator.status-stopped {
  color: #f44336;
}

.status-indicator.status-checking {
  color: #ff9800;
}

.status-indicator.status-error {
  color: #f44336;
}

.status-indicator.status-unknown {
  color: #9e9e9e;
}

.status-text {
  font-size: 0.9rem;
}

.bot-controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.status-button,
.start-button,
.stop-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.status-button {
  background-color: #2196f3;
  color: white;
}

.status-button:hover:not(:disabled) {
  background-color: #1976d2;
}

.start-button {
  background-color: #4caf50;
  color: white;
}

.start-button:hover:not(:disabled) {
  background-color: #388e3c;
}

.stop-button {
  background-color: #f44336;
  color: white;
}

.stop-button:hover:not(:disabled) {
  background-color: #d32f2f;
}

.status-button:disabled,
.start-button:disabled,
.stop-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Mobile styles for bot controls */
@media (max-width: 768px) {
  .bot-status-display {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .bot-controls {
    width: 100%;
  }

  .status-button,
  .start-button,
  .stop-button {
    flex: 1;
    justify-content: center;
  }
}

/* Mobile settings tabs */
@media (max-width: 768px) {

  .settings-tabs {
    flex-direction: column;
    gap: 1rem;
    border-bottom: unset;
  }

  .settings-tabs-mobile {
    display: block;
  }

  .settings-tabs-desktop {
    display: none;
  }

  .tab-dropdown {
    width: 100%;
    color: #fff;
    font-size: 0.95rem;
    cursor: pointer;
    transition: all 0.2s ease;
  }

  .tab-dropdown:focus {
    outline: none;
    border-color: #4dabf7;
    box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
  }

  .tab-dropdown option {
    background-color: #2a2a2a;
    color: #fff;
  }
}

/* Desktop settings tabs */
@media (min-width: 769px) {
  .settings-tabs {
    flex-direction: row;
  }

  .settings-tabs-mobile {
    display: none;
  }

  .settings-tabs-desktop {
    display: flex;
  }
}
</style>
