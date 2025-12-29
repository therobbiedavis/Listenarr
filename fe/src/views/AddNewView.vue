<template>
  <div class="add-new-view">
    <div class="page-header">
  <h1><PhPlusCircle /> Add New Audiobook</h1>
    </div>

    <!-- Debug button removed -->

    <!-- Unified Search -->
    <div class="search-section">
      <div class="search-container">
        
        <div class="search-bar-section">      
          <div v-if="!showAdvancedSearch" class="unified-search-bar">

              <button v-if="!showAdvancedSearch"
                type="button"
                @click="toggleAdvancedSearch"
                class="search-btn advanced-btn"
                :title="showAdvancedSearch ? 'Hide Advanced Search' : 'Show Advanced Search'"
                aria-pressed="false"
                aria-controls="advanced-search"
                aria-expanded="false"
              >
                <PhFunnelSimple /> {{ showAdvancedSearch ? 'Hide' : 'Advanced' }}
              </button>
      <div v-if="!showAdvancedSearch" class="search-method">
        <label class="search-method-label">Search for Audiobooks</label>
        <p class="search-help">
          Enter an ASIN (e.g., B08G9PRS1K) or search by title and author. 
          <template v-if="enabledMetadataSources.length > 0">
            Metadata powered by: {{ enabledMetadataSources.map(s => s.name).join(', ') }}
          </template>
          <template v-else>
            <router-link to="/settings#apis" class="settings-link">Configure metadata sources</router-link>
          </template>
        </p>
      </div>
            <form class="unified-search-form" role="search" aria-label="Search audiobooks" @submit.prevent="performSearch">
              <input
                ref="searchInput"
                v-model="searchQuery"
                id="unified-search-input"
                aria-label="Search query"
                aria-describedby="unified-search-hint"
                type="text"
                :placeholder="searchPlaceholder"
                class="search-input"
                :class="{ error: searchError }"
                @input="handleSearchInput"
              />

              <select v-model="searchLanguage" class="language-select" aria-label="Search language">
                <option value="english">English</option>
                <option value="german">Deutsch</option>
                <option value="french">Français</option>
                <option value="spanish">Español</option>
                <option value="italian">Italiano</option>
                <option value="portuguese">Português</option>
                <option value="japanese">日本語</option>
                <option value="chinese">中文</option>
              </select>

              <button
                type="submit"
                :disabled="!isSearching && !searchQuery.trim()"
                class="search-btn"
                aria-label="Execute search"
              >
                <template v-if="isSearching">
                  <PhSpinner class="ph-spin" />
                  Cancel
                </template>
                <template v-else>
                  <PhMagnifyingGlass />
                  Search
                </template>
              </button>
            </form>
            <!-- Audible search buttons removed -->
          </div>

          <!-- Inline Advanced Search Section -->
            <div v-if="showAdvancedSearch" id="advanced-search" role="region" class="advanced-search-section" aria-labelledby="advanced-search-label">
              <button @click="toggleAdvancedSearch" class="simple-search-button" aria-label="Return to simple search" aria-controls="advanced-search" :aria-expanded="showAdvancedSearch">
                <PhArrowLeft /> Simple Search
              </button>
            
            <div class="advanced-search-header">
              <h3 id="advanced-search-label"><PhFunnelSimple /> Advanced Search</h3>
              <p class="help-text">
                Enter multiple search criteria for more precise results. When both Title and Author are provided,
                uses Audimeta's combined search for maximum accuracy.
              </p>
            </div>

            <form class="advanced-search-form" role="search" aria-label="Advanced search form" @submit.prevent="performAdvancedSearch">
              <div class="form-row">
                  <div class="form-group">
                    <label for="adv-title">Title</label>
                    <input
                      id="adv-title"
                      aria-label="Title"
                      v-model="advancedSearchParams.title"
                      type="text"
                      placeholder="e.g., Dune"
                      class="form-input"
                    />
                    <div class="form-hint" id="hint-adv-title">Use full or partial titles for best matches.</div>
                  </div>
                
                  <div class="form-group">
                    <label for="adv-author">Author</label>
                    <input
                      id="adv-author"
                      aria-label="Author"
                      v-model="advancedSearchParams.author"
                      type="text"
                      placeholder="e.g., Frank Herbert"
                      class="form-input"
                    />
                  </div>
                  <div class="form-group">
                    <label for="adv-series">Series</label>
                    <input
                      id="adv-series"
                      aria-label="Series"
                      v-model="advancedSearchParams.series"
                      type="text"
                      placeholder="e.g., The Empyrean"
                      class="form-input"
                    />
                  </div>
              </div>
              
              <div class="form-row">
                  <div class="form-group">
                    <label for="adv-isbn">ISBN</label>
                    <input
                      id="adv-isbn"
                      aria-label="ISBN"
                      v-model="advancedSearchParams.isbn"
                      type="text"
                      placeholder="e.g., 9780441172719"
                      class="form-input"
                    />
                    <div class="form-hint">
                      <div>Include hyphens or omit them — both work.</div>
                      <div v-if="convertedIsbn" class="small-note">Converted ISBN-13: {{ convertedIsbn }}</div>
                    </div>
                  </div>
                
                  <div class="form-group">
                    <label for="adv-asin">ASIN</label>
                    <input
                      id="adv-asin"
                      aria-label="ASIN"
                      v-model="advancedSearchParams.asin"
                      type="text"
                      placeholder="e.g., B08G9PRS1K"
                      class="form-input"
                    />
                    <div class="form-hint">ASINs are case-insensitive; remove spaces.</div>
                  </div>
                
                  <div class="form-group">
                    <label for="adv-language">Language</label>
                    <select id="adv-language" aria-label="Language" v-model="advancedSearchParams.language" class="form-input">
                    <option value="">Any Language</option>
                    <option value="english">English</option>
                    <option value="german">Deutsch</option>
                    <option value="french">Français</option>
                    <option value="spanish">Español</option>
                    <option value="italian">Italiano</option>
                    <option value="portuguese">Português</option>
                    <option value="japanese">日本語</option>
                    <option value="chinese">中文</option>
                  </select>
                </div>
              </div>
              
              <div v-if="advancedSearchError" class="error-message">
                <PhWarningCircle />
                {{ advancedSearchError }}
              </div>
              
              <div class="advanced-search-actions">
  
                <div class="advanced-search-buttons">
                  <button type="button" @click="clearAdvancedSearch" class="btn-secondary" aria-label="Clear advanced search">
                    <PhArrowClockwise /> Clear
                  </button>
                  <button type="submit"
                    :disabled="!isValidAdvancedSearch || isSearching"
                    class="btn-primary"
                    aria-label="Execute advanced search"
                  >
                    <PhSpinner v-if="isSearching" class="ph-spin" />
                    <PhMagnifyingGlass v-else />
                    {{ isSearching ? 'Searching...' : 'Search' }}
                  </button>
                </div>
              </div>
            </form>
            <div class="divider" aria-hidden="true"></div>
          </div> <!-- advanced-search-section -->

      <div v-if="!showAdvancedSearch" id="unified-search-hint" class="search-hint" role="status" aria-live="polite">
        <PhInfo />
        <span v-if="searchType === 'asin'">Searching by ASIN</span>
        <span v-else-if="searchType === 'title'">
          <template v-if="searchQuery.toUpperCase().startsWith('TITLE:')">Searching by title</template>
          <template v-else-if="searchQuery.toUpperCase().startsWith('AUTHOR:')">Searching by author</template>
          <template v-else>Searching by title/author</template>
        </span>
        <span v-else-if="searchType === 'isbn'">Searching by ISBN</span>
        <span v-else>
          <span v-if="!searchQuery.trim()" class="search-prefix-hint">
            You can use the following prefixes for precise searches: <strong>ASIN:B08G9PRS1K</strong>,
            <strong>AUTHOR:J. R. R. Tolkien</strong>,
            <strong>TITLE:The Hobbit</strong>
          </span>
        </span>
      </div>
      
      <div v-if="!showAdvancedSearch && searchError" class="error-message" role="alert" aria-live="assertive">
        <PhWarningCircle />
        {{ searchError }}
      </div>
    </div>
  </div>
</div>
    <!-- Loading State -->

    <!-- Debug block removed -->

    <!-- Loading State -->
    <div v-if="isSearching && !hasResults" class="loading-results">
      <div class="loading-spinner">
        <PhSpinner class="ph-spin" />
        <p>Searching for audiobooks...</p>
        <p v-if="searchStatus" class="search-status">{{ searchStatus }}</p>
      </div>
    </div>

    <!-- Results Section -->
    <div v-if="hasResults" class="search-results">
      <!-- ASIN Results -->
      <div v-if="searchType === 'asin' && audibleResult">
        <h2>Audiobook Found</h2>
        <div class="title-results">
          <div class="title-result-card">
            <div class="result-poster">
              <img
                v-if="audibleResult.imageUrl"
                class="lazy-search-img"
                :data-src="apiService.getImageUrl(audibleResult.imageUrl)"
                :data-original-src="audibleResult.imageUrl"
                :src="getPlaceholderUrl()"
                :alt="audibleResult.title"
                loading="lazy"
                decoding="async"
                @error="handleLazyImageError"
              />
              <template v-else>
                <img :src="getPlaceholderUrl()" alt="Cover unavailable" loading="lazy" class="placeholder-cover-image" decoding="async" />
              </template>
            </div>
            <div class="result-info">
              <h3>
                {{ safeText(audibleResult.title) }}
              </h3>
              <p v-if="audibleResult.subtitle" class="result-subtitle">
                {{ safeText(audibleResult.subtitle) }}
              </p>
              <p class="result-author">
                by {{ (audibleResult.authors || []).map(author => safeText(author)).join(', ') || 'Unknown Author' }}
              </p>
              
              <p v-if="audibleResult.narrators?.length" class="result-narrator">
                Narrated by {{ audibleResult.narrators.map(narrator => safeText(narrator)).join(', ') }}
              </p>
              
              <div class="result-stats">
                <span v-if="audibleResult.runtime" class="stat-item">
                  <PhClock />
                  {{ formatRuntime(audibleResult.runtime) }}
                </span>
                <span v-if="audibleResult.language" class="stat-item">
                  <PhGlobe />
                  {{ capitalizeLanguage(audibleResult.language) }}
                </span>
              </div>

              <!-- Series badges on separate line -->
              <div v-if="(audibleResult.seriesList && audibleResult.seriesList.length > 0) || (audibleResult.searchResult?.seriesList && audibleResult.searchResult.seriesList.length > 0)" class="result-series">
                <span v-for="seriesName in (audibleResult.seriesList || audibleResult.searchResult?.seriesList || [])" :key="seriesName" class="series-badge" :title="(audibleResult.seriesList || audibleResult.searchResult?.seriesList || []).join(', ')">
                  <PhBook />
                  {{ safeText(seriesName) }}
                </span>
              </div>

              <!-- Metadata badges -->
              <div class="metadata-badges">
                <span v-if="audibleResult.publisher" class="metadata-badge">
                  <PhBuilding />
                  {{ safeText(audibleResult.publisher) }}
                </span>
                <span v-if="audibleResult.publishYear" class="metadata-badge">
                  <PhCalendar />
                  {{ audibleResult.publishYear }}
                </span>
                <span v-else-if="audibleResult.publishedDate" class="metadata-badge">
                  <PhCalendar />
                  {{ new Date(audibleResult.publishedDate).getFullYear() }}
                </span>
                <span v-if="audibleResult.asin" class="metadata-badge">
                  <PhBarcode />
                  {{ audibleResult.asin }}
                </span>
                <span v-if="audibleResult.isbn" class="metadata-badge">
                  <PhBarcode />
                  {{ audibleResult.isbn }}
                </span>
              </div>
              
              <div class="result-meta">

                <a v-if="audibleResult.asin && ((audibleResult.metadataSource && audibleResult.metadataSource.toLowerCase().includes('audimeta')) || (audibleResult.searchResult && audibleResult.searchResult.metadataSource && audibleResult.searchResult.metadataSource.toLowerCase().includes('audimeta')))"
                   :href="`https://audimeta.de/book/${audibleResult.asin}`"
                   target="_blank"
                   rel="noopener noreferrer"
                   class="metadata-source-link"
                   :data-source="audibleResult.metadataSource || (audibleResult.searchResult && audibleResult.searchResult.metadataSource)">
                  <PhGlobe />
                  Audimeta
                </a>
                <span v-else-if="audibleResult.metadataSource" class="metadata-source-badge" :data-source="audibleResult.metadataSource">
                  <PhGlobe />
                  Metadata: {{ audibleResult.metadataSource }}
                </span>

                <a v-if="audibleResult.sourceLink"
                   :href="audibleResult.sourceLink" 
                   target="_blank" 
                   rel="noopener noreferrer"
                   class="source-link">
                  <PhCloud />
                  {{ audibleResult.sourceLink.includes('audible.com') ? 'Audible' : `Source: ${audibleResult.source}` }}
                </a>
                <span v-else-if="audibleResult.source" class="source-badge">
                  <PhCloud />
                  Source: {{ audibleResult.source }}
                </span>
                <span v-if="audibleResult.explicit" class="metadata-badge">
                  <PhWarning />
                  Explicit
                </span>
                <span v-if="audibleResult.abridged" class="metadata-badge">
                  <PhScissors />
                  Abridged
                </span>
              </div>
            </div>
            <div class="result-actions">
              <button 
                :class="['btn', (audibleResult && ((audibleResult.asin && addedAsins.has(audibleResult.asin)) || (audibleResult.openLibraryId && addedOpenLibraryIds.has(audibleResult.openLibraryId)))) ? 'btn-success' : 'btn-primary']"
                @click="addToLibrary(audibleResult)"
                :disabled="!!(audibleResult && ((audibleResult.asin && addedAsins.has(audibleResult.asin)) || (audibleResult.openLibraryId && addedOpenLibraryIds.has(audibleResult.openLibraryId))))"
              >
                <component :is="(audibleResult && ((audibleResult.asin && addedAsins.has(audibleResult.asin)) || (audibleResult.openLibraryId && addedOpenLibraryIds.has(audibleResult.openLibraryId)))) ? PhCheck : PhPlus" />
                {{ !!(audibleResult && ((audibleResult.asin && addedAsins.has(audibleResult.asin)) || (audibleResult.openLibraryId && addedOpenLibraryIds.has(audibleResult.openLibraryId)))) ? 'Added' : 'Add to Library' }}
              </button>
              <button class="btn btn-secondary" @click="viewDetails(audibleResult)">
                <PhEye />
                View Details
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ISBN Auto-processing Status -->
      <div v-if="searchType === 'isbn' && isSearching" class="inline-status">
        <PhSpinner class="ph-spin" />
        <span>Searching Amazon/Audible for audiobook...</span>
      </div>
      <div v-else-if="searchType === 'isbn' && !isSearching && isbnLookupMessage" class="inline-status" :class="{ warning: isbnLookupWarning }">
        <component :is="isbnLookupWarning ? PhWarningCircle : PhInfo" />
        <span>{{ isbnLookupMessage }}</span>
      </div>

      <!-- Title Search Results -->
      <div v-if="searchType === 'title' && titleResults.length > 0">
        <h2>Found {{ totalTitleResultsCount }} Book{{ totalTitleResultsCount === 1 ? '' : 's' }}</h2>
      <div class="results-controls">
        <div v-if="isAudimetaPaged && Math.ceil(audimetaTotal / audimetaLimit) > 1" class="audimeta-pagination">
          <button class="btn btn-secondary" :disabled="audimetaPage <= 1" @click.prevent="changeAudimetaPage(audimetaPage - 1)">Prev</button>
          <span class="page-indicator">Page {{ audimetaPage }} of {{ Math.max(1, Math.ceil(audimetaTotal / audimetaLimit)) }}</span>
          <button class="btn btn-secondary" :disabled="(audimetaPage * audimetaLimit) >= audimetaTotal" @click.prevent="changeAudimetaPage(audimetaPage + 1)">Next</button>
        </div>

        <div v-if="!isAudimetaPaged && totalPages > 1" class="client-pagination-controls">
          <div class="pagination-settings">
            <label class="small-label">Results per page</label>
            <select v-model.number="resultsPerPage" @change="() => { currentAdvancedPage = 1 }" class="form-input small-select">
              <option :value="10">10</option>
              <option :value="25">25</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>

          <div class="pagination-nav">
            <button class="btn btn-secondary" :disabled="currentAdvancedPage <= 1" @click.prevent="currentAdvancedPage--;">
              <PhCaretLeft />
              Prev
            </button>
            <span class="page-indicator">Page {{ currentAdvancedPage }} of {{ totalPages }}</span>
            <button class="btn btn-secondary" :disabled="currentAdvancedPage >= totalPages" @click.prevent="currentAdvancedPage++;">
              Next
              <PhCaretRight />
            </button>
          </div>
        </div>
      </div>
        <div class="title-results">
          <div v-for="book in displayedTitleResults" :key="book.key" class="title-result-card">
              <div class="result-poster">
              <img
                v-if="getCoverUrl(book)"
                class="lazy-search-img"
                :data-src="getCoverUrl(book)"
                :data-original-src="book.imageUrl || book.searchResult?.imageUrl || ''"
                :src="getPlaceholderUrl()"
                :alt="book.title"
                loading="lazy"
                decoding="async"
                @error="handleLazyImageError"
              />
              <template v-else>
                <img :src="getPlaceholderUrl()" alt="Cover unavailable" loading="lazy" class="placeholder-cover-image" decoding="async" />
              </template>
            </div>
            <div class="result-info">
              <h3>
                {{ safeText(book.title) }}
              </h3>
              <p v-if="book.searchResult?.subtitle" class="result-subtitle">
                {{ safeText(book.searchResult.subtitle) }}
              </p>
              <p class="result-author">by {{ formatAuthors(book) }}</p>
              
              <!-- Audiobook metadata from enriched results -->
              <p v-if="book.searchResult?.narrator" class="result-narrator">
                Narrated by {{ book.searchResult.narrator }}
              </p>
              
              <div class="result-stats">
                <span v-if="book.searchResult?.runtime" class="stat-item">
                  <PhClock />
                  {{ formatRuntime(book.searchResult.runtime) }}
                </span>
                <span v-if="book.searchResult?.language" class="stat-item">
                  <PhGlobe />
                  {{ capitalizeLanguage(book.searchResult.language) }}
                </span>
              </div>

              <!-- Series badges on separate line -->
              <div v-if="(book.seriesList && book.seriesList.length > 0) || (book.searchResult?.seriesList && book.searchResult.seriesList.length > 0)" class="result-series">
                <span v-for="seriesName in (book.seriesList || book.searchResult?.seriesList || [])" :key="seriesName" class="series-badge" :title="(book.seriesList || book.searchResult?.seriesList || []).join(', ')">
                  <PhBook />
                  {{ safeText(seriesName) }}
                </span>
              </div>

              <!-- Metadata badges -->
              <div class="metadata-badges">
                <span v-if="book.publisher?.length" class="metadata-badge">
                  <PhBuilding />
                  {{ safeText(book.publisher[0]) }}
                </span>
                <span v-if="book.first_publish_year" class="metadata-badge">
                  <PhCalendar />
                  {{ book.first_publish_year }}
                </span>
                <span v-if="getAsin(book)" class="metadata-badge">
                  <PhBarcode />
                  {{ getAsin(book) }}
                </span>
                <span v-else-if="book.searchResult?.id && ((book.metadataSource && book.metadataSource.toLowerCase().includes('openlibrary')) || (book.searchResult?.metadataSource && book.searchResult.metadataSource.toLowerCase().includes('openlibrary')))" class="metadata-badge">
                  <PhBarcode />
                  {{ book.searchResult.id }}
                </span>
              </div>

              <div class="result-meta">
                <a v-if="book.metadataSource && getMetadataSourceUrl(book)" 
                   :href="getMetadataSourceUrl(book)" 
                   target="_blank" 
                   rel="noopener noreferrer"
                   class="metadata-source-link"
                   :data-source="book.metadataSource">
                  <PhGlobe />
                  {{ book.metadataSource && book.metadataSource.toLowerCase().includes('audimeta') ? 'Audimeta' : `Metadata: ${book.metadataSource}` }}
                </a>
                <span v-else-if="book.metadataSource" class="metadata-source-badge" :data-source="book.metadataSource">
                  <PhGlobe />
                  Metadata: {{ book.metadataSource }}
                </span>

                <!-- Prefer to show Audible as product source when metadata comes from Audimeta -->
                <a v-if="getSourceUrl(book)"
                   :href="getSourceUrl(book)"
                   target="_blank"
                   rel="noopener noreferrer"
                   class="source-link">
                  <PhCloud />
                  {{ getSourceUrl(book)?.includes('audible.com') ? 'Audible' : (book.searchResult?.source || book.metadataSource || 'OpenLibrary') }}
                </a>
                <span v-else-if="book.searchResult?.source" class="source-badge">
                  <PhCloud />
                  Source: {{ book.searchResult.source }}
                </span>
              </div>
            </div>

            <div class="result-actions">
              <button 
                :class="['btn', (!!(getAsin(book) && addedAsins.has(getAsin(book)!)) || (!!book.searchResult?.id && addedOpenLibraryIds.has(book.searchResult.id))) ? 'btn-success' : 'btn-primary']"
                @click="selectTitleResult(book)"
                :disabled="!!(getAsin(book) && addedAsins.has(getAsin(book)!)) || (!!book.searchResult?.id && addedOpenLibraryIds.has(book.searchResult.id))"
              >
                <component :is="(!!(getAsin(book) && addedAsins.has(getAsin(book)!)) || (!!book.searchResult?.id && addedOpenLibraryIds.has(book.searchResult.id))) ? PhCheck : PhPlus" />
                {{ (!!(getAsin(book) && addedAsins.has(getAsin(book)!)) || (!!book.searchResult?.id && addedOpenLibraryIds.has(book.searchResult.id))) ? 'Added' : 'Add to Library' }}
              </button>
              <button class="btn btn-secondary" @click="viewTitleResultDetails(book)">
                <PhEye />
                View Details
              </button>
            </div>
          </div>
        </div>
        
        <!-- Load More Button -->
        <div v-if="canLoadMore" class="load-more">
          <button @click="loadMoreTitleResults" :disabled="isLoadingMore" class="btn btn-secondary">
            <template v-if="isLoadingMore">
              <PhSpinner class="ph-spin" />
            </template>
            <template v-else>
              <PhArrowDown />
            </template>
            {{ isLoadingMore ? 'Loading...' : 'Load More' }}
          </button>
        </div>
      </div>
    </div>

    <!-- No Results -->
    <div v-if="searchType === 'asin' && !audibleResult && !isSearching && !isCancelled && searchQuery" class="empty-state">
      <div class="empty-icon">
        <PhMagnifyingGlass />
      </div>
      <h2>No Audiobook Found</h2>
      <p>No audiobook was found with ASIN "{{ asinQuery }}". Please check the ASIN and try again.</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="searchQuery = ''; searchType = 'title'">
          <PhMagnifyingGlass />
          Try Title Search
        </button>
      </div>
    </div>

    <div v-if="searchType === 'title' && titleResults.length === 0 && !isSearching && !isCancelled && searchQuery" class="empty-state">
      <div class="empty-icon">
        <PhBook />
      </div>
      <h2 v-if="!asinFilteringApplied">No Books Found</h2>
      <h2 v-else>No Audiobook Matches</h2>
      <p v-if="!asinFilteringApplied">No books were found matching "{{ titleQuery }}"{{ authorQuery ? ' by ' + authorQuery : '' }}. Try different search terms.</p>
      <p v-else>No audiobooks found. Try refining your search terms.</p>
    </div>

    <!-- Error States -->
    <div v-if="hasError" class="error-state">
      <div class="error-icon">
        <PhWarningCircle />
      </div>
      <h2>Search Error</h2>
      <p>{{ errorMessage }}</p>
      <div class="quick-actions">
        <button class="btn btn-primary" @click="retrySearch">
          <PhArrowClockwise />
          Try Again
        </button>
      </div>
    </div>

    </div>

    <!-- Audiobook Details Modal -->
    <AudiobookDetailsModal
      :visible="showDetailsModal"
      :book="selectedBook"
      @close="closeDetailsModal"
      @add-to-library="handleAddToLibrary"
    />

    <!-- Add to Library Modal -->
    <AddLibraryModal
      :visible="showAddLibraryModal"
      :book="selectedBookForLibrary"
      :resolved-image-url="selectedBookForLibrary ? apiService.getImageUrl((selectedBookForLibrary as any).imageUrl) : ''"
      @close="closeAddLibraryModal"
      @added="handleLibraryAdded"
    />
    
    <!-- Confirm dialog removed: using centralized showConfirm service mounted in App.vue -->
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { PhPlusCircle, PhSpinner, PhMagnifyingGlass, PhInfo, PhWarningCircle, PhClock, PhGlobe, PhCheck, PhPlus, PhEye, PhBook, PhArrowDown, PhArrowClockwise, PhCloud, PhFunnelSimple, PhCalendar, PhBuilding, PhBarcode, PhWarning, PhScissors } from '@phosphor-icons/vue'
import { useRoute, useRouter } from 'vue-router'
import type { AudibleBookMetadata, SearchResult, Audiobook, AudimetaAuthor, AudimetaNarrator, AudimetaGenre, AudimetaSearchResult, AudimetaBookResponse } from '@/types' 
import { apiService } from '@/services/api'
import { getPlaceholderUrl } from '@/utils/placeholder'
import { handleImageError } from '@/utils/imageFallback'
import type { OpenLibraryBook } from '@/services/openlibrary'
import { openLibraryService } from '@/services/openlibrary'

// Extend Window interface for debug helper used during development
declare global {
  interface Window {
    addnew_rawDebugResults?: unknown
  }
}
import { isbnService, type ISBNBook } from '@/services/isbn'
import { signalRService } from '@/services/signalr'
import { useConfigurationStore } from '@/stores/configuration'
import { useLibraryStore } from '@/stores/library'
import AudiobookDetailsModal from '@/components/AudiobookDetailsModal.vue'
import AddLibraryModal from '@/components/AddLibraryModal.vue'
import { useToast } from '@/services/toastService'
import { safeText } from '@/utils/textUtils'
import { logger } from '@/utils/logger'
import { buildAmazonProductUrl, buildAudibleProductUrl } from '@/utils/marketDomains'
import { useSearch } from '@/composables/useSearch'
import { useLibraryCheck } from '@/composables/useLibraryCheck'

// Extended type for title search results that includes search metadata
type TitleSearchResult = OpenLibraryBook & { 
  searchResult?: SearchResult // Store the enriched SearchResult from intelligent search
  imageUrl?: string // For results that have direct image URLs
  metadataSource?: string // Store which metadata source was used
}

// Loose result type used for normalization of diverse backend shapes
type LooseResult = Partial<SearchResult> & Record<string, unknown>

const route = useRoute()
const router = useRouter()
const configStore = useConfigurationStore()
const libraryStore = useLibraryStore()
const toast = useToast()

// Initialize composables
const { 
  searchQuery, 
  searchLanguage, 
  searchType, 
  isSearching, 
  searchError, 
  searchStatus,
  searchPlaceholder,
  handleSearchInput,
  performSearch,
  cancelSearch
  , lastResults
} = useSearch()

const {
  addedAsins,
  addedOpenLibraryIds,
  checkExistingInLibrary,
  markExistingResults,
  isAudibleAdded,
  isTitleResultAdded,
  markAsinAdded,
  markOpenLibraryIdAdded
} = useLibraryCheck()

// Get enabled metadata sources
// Note: temporarily exclude OpenLibrary from the Add New search UI.
// This filters out any API configuration whose name is 'OpenLibrary' (case-insensitive).
const enabledMetadataSources = computed(() => {
  return configStore.apiConfigurations
    .filter(api => api.isEnabled && api.type === 'metadata' && api.name.toLowerCase() !== 'openlibrary')
    .sort((a, b) => a.priority - b.priority) // Sort by priority (lower = higher priority)
})

// Use region-aware helpers from utils/marketDomains

// Abort controller for cancelling the active intelligent search
const searchAbortController = ref<AbortController | null>(null)
const showAdvancedSearch = ref(false)
const advancedSearchParams = ref({
  title: '',
  author: '',
  isbn: '',
  series: '',
  asin: '',
  language: ''
})
// Local storage persistence
const ADVANCED_STORAGE_KEY = 'listenarr.addnew.advanced'
const _saveTimer = ref<number | null>(null)
const advancedSearchError = ref('')

// Audimeta pagination state for advanced searches
const audimetaPage = ref(1)
const audimetaLimit = ref(50)
const audimetaTotal = ref(0)
const isAudimetaPaged = ref(false)
const allAudimetaResults = ref<AudimetaSearchResult[]>([])

const isValidAdvancedSearch = computed(() => {
  // If advanced UI is visible, validate the advanced form fields directly
  if (showAdvancedSearch.value) {
    const p = advancedSearchParams.value as { title?: string; author?: string; series?: string; isbn?: string; asin?: string }
    return Boolean(
      (p.title && p.title.trim()) ||
      (p.author && p.author.trim()) ||
      (p.series && p.series.trim()) ||
      (p.isbn && p.isbn.trim()) ||
      (p.asin && p.asin.trim())
    )
  }

  // When advanced UI is hidden, validate using the unified search query (allow prefixes or any non-empty query)
  const query = (searchQuery.value || '').trim()
  if (!query) return false
  const hasPrefix = /(?:TITLE:|AUTHOR:|ISBN:|ASIN:)/i.test(query)
  return hasPrefix || !!query
})


// Small helper to decode basic HTML entities (covers &amp;, &lt;, &gt;, &quot;, &#39;)
// const decodeHtml = (input?: string | null): string => {
//   if (!input) return ''
//   return input
//     .replace(/&amp;/g, '&')
//     .replace(/&lt;/g, '<')
//     .replace(/&gt;/g, '>')
//     .replace(/&quot;/g, '"')
//     .replace(/&#39;/g, "'")
// }

logger.debug('AddNewView component loaded')
logger.debug('libraryStore:', libraryStore)

onMounted(() => {
  // If URL has a page query parameter (page=[num]), initialize audimetaPage
  const p = route.query.page
  if (p) {
    const np = Number(p)
    if (!isNaN(np) && np > 0) audimetaPage.value = np
  }

  // Load persisted advanced search state if present
  try {
    const raw = window.localStorage.getItem(ADVANCED_STORAGE_KEY)
    if (raw) {
      const parsed = JSON.parse(raw)
      if (typeof parsed === 'object' && parsed !== null) {
        if (parsed.showAdvanced === true) showAdvancedSearch.value = true
        if (parsed.params && typeof parsed.params === 'object') {
          advancedSearchParams.value = Object.assign({}, advancedSearchParams.value, parsed.params)
        }
      }
    }
  } catch (e) {
    // ignore localStorage errors
  }
})

// Persist advanced state with light debounce to avoid frequent writes
const saveAdvancedState = () => {
  try {
    if (_saveTimer.value) window.clearTimeout(_saveTimer.value)
  } catch {}
  _saveTimer.value = window.setTimeout(() => {
    try {
      const payload = { showAdvanced: showAdvancedSearch.value, params: advancedSearchParams.value }
      window.localStorage.setItem(ADVANCED_STORAGE_KEY, JSON.stringify(payload))
    } catch {}
    try { _saveTimer.value = null } catch {}
  }, 250)
}

// Watch for changes to persist
watch(() => showAdvancedSearch.value, () => saveAdvancedState())
watch(advancedSearchParams, () => saveAdvancedState(), { deep: true })

// React to composable results (handles auto-debounced searches)
watch(() => lastResults?.value, async (newVal) => {
  try {
    if (newVal && Array.isArray(newVal) && newVal.length) {
      await handleSimpleSearchResults(newVal)
    }
  } catch (e) {
    console.debug('Error handling lastResults change', e)
  }
})

// Library checking functions - now handled by useLibraryCheck composable

// Unified Search - now handled by useSearch composable
const isCancelled = ref(false)

// Results
const audibleResult = ref<AudibleBookMetadata | null>(null)
const titleResults = ref<TitleSearchResult[]>([])
const resolvedAsins = ref<Record<string, string>>({})
const asinFilteringApplied = ref(false)
const isbnResult = ref<ISBNBook | null>(null) // retained for potential enrichment but not directly rendered
const isbnLookupMessage = ref('')
const isbnLookupWarning = ref(false)
const totalTitleResultsCount = ref<number>(0)
const isLoadingMore = ref(false)
const currentPage = ref(0)
// const resultsPerPage = 10
const rawDebugResults = ref<unknown[] | null>(null)

// Parsed search query components (for error messages)
const asinQuery = ref('')
const titleQuery = ref('')
const authorQuery = ref('')

// Pagination / candidate limits for advanced results
const resultsPerPage = ref<number>(50)
const currentAdvancedPage = ref<number>(1)

const totalPages = computed(() => Math.max(1, Math.ceil((totalTitleResultsCount.value || titleResults.value.length) / resultsPerPage.value)))

const pagedTitleResults = computed(() => {
  const start = (currentAdvancedPage.value - 1) * resultsPerPage.value
  return titleResults.value.slice(start, start + resultsPerPage.value)
})

const displayedTitleResults = computed(() => {
  // If the results come from Audimeta paged API, show full list (server-side paging)
  if (isAudimetaPaged.value) return titleResults.value
  return pagedTitleResults.value
})

// Compute converted ISBN-13 for display when user enters an ISBN-10
const convertedIsbn = computed(() => {
  try {
    const raw = (advancedSearchParams.value && (advancedSearchParams.value as any).isbn) || ''
    const cleaned = String(raw).replace(/[-\s]/g, '').toUpperCase()
    if (!cleaned) return ''

    // If already a valid ISBN-13, show it
    if (/^\d{13}$/.test(cleaned)) return cleaned

    // If ISBN-10 (may end with X), convert to ISBN-13 (978 prefix)
    if (/^\d{9}[\dX]$/i.test(cleaned)) {
      const isbn10 = cleaned
      const base = '978' + isbn10.slice(0, 9)
      let sum = 0
      for (let i = 0; i < 12; i++) {
        const digit = parseInt(base.charAt(i), 10)
        if (isNaN(digit)) return ''
        sum += (i % 2 === 0) ? digit : digit * 3
      }
      const check = (10 - (sum % 10)) % 10
      return base + String(check)
    }

    return ''
  } catch (e) {
    return ''
  }
})

// Cache for best cover selection per book key
const coverSelection = ref<Record<string, string>>({})

// General state
const errorMessage = ref('')

// Modal state
const showDetailsModal = ref(false)
const selectedBook = ref<AudibleBookMetadata>({} as AudibleBookMetadata)
const showAddLibraryModal = ref(false)
const selectedBookForLibrary = ref<AudibleBookMetadata>({} as AudibleBookMetadata)

// Local storage keys for persisting search results
const RESULTS_KEY = 'listenarr.addNewResults'
const SEARCH_TYPE_KEY = 'listenarr.addNewSearchType'
const TITLE_RESULTS_COUNT_KEY = 'listenarr.addNewTitleResultsCount'
const ASIN_FILTERING_KEY = 'listenarr.addNewAsinFiltering'
const RESOLVED_ASINS_KEY = 'listenarr.addNewResolvedAsins'
const ADDED_ASINS_KEY = 'listenarr.addNewAddedAsins'
const ADDED_OLIDS_KEY = 'listenarr.addNewAddedOLIDs'

// Initialize search results from localStorage
try {
  const storedResults = localStorage.getItem(RESULTS_KEY)
  if (storedResults) {
    const parsed = JSON.parse(storedResults)
    if (parsed.audibleResult) audibleResult.value = parsed.audibleResult
    if (parsed.titleResults) titleResults.value = parsed.titleResults
    if (parsed.isbnResult) isbnResult.value = parsed.isbnResult
  }
  
  const storedSearchType = localStorage.getItem(SEARCH_TYPE_KEY)
  if (storedSearchType) {
    searchType.value = storedSearchType as 'asin' | 'title' | 'isbn' | null
  }
  
  const storedCount = localStorage.getItem(TITLE_RESULTS_COUNT_KEY)
  if (storedCount) {
    totalTitleResultsCount.value = parseInt(storedCount, 10)
  }
  
  const storedFiltering = localStorage.getItem(ASIN_FILTERING_KEY)
  if (storedFiltering) {
    asinFilteringApplied.value = storedFiltering === 'true'
  }
  
  const storedResolved = localStorage.getItem(RESOLVED_ASINS_KEY)
  if (storedResolved) {
    resolvedAsins.value = JSON.parse(storedResolved)
  }
  
  const storedAdded = localStorage.getItem(ADDED_ASINS_KEY)
  if (storedAdded) {
    addedAsins.value = new Set(JSON.parse(storedAdded))
  }
  const storedAddedOl = localStorage.getItem(ADDED_OLIDS_KEY)
  if (storedAddedOl) {
    try { addedOpenLibraryIds.value = new Set(JSON.parse(storedAddedOl)) } catch {}
  }
} catch (error) {
  console.warn('Failed to restore persisted state:', error)
}

// Watch search results changes and persist to localStorage
watch([audibleResult, titleResults, isbnResult], () => {
  try {
    const results = {
      audibleResult: audibleResult.value,
      titleResults: titleResults.value,
      isbnResult: isbnResult.value
    }
    localStorage.setItem(RESULTS_KEY, JSON.stringify(results))
  } catch {}
})

watch(searchType, (v) => {
  try { localStorage.setItem(SEARCH_TYPE_KEY, v || '') } catch {}
})

watch(totalTitleResultsCount, (v) => {
  try { localStorage.setItem(TITLE_RESULTS_COUNT_KEY, v.toString()) } catch {}
})

watch(asinFilteringApplied, (v) => {
  try { localStorage.setItem(ASIN_FILTERING_KEY, v.toString()) } catch {}
})

watch(resolvedAsins, (v) => {
  try { localStorage.setItem(RESOLVED_ASINS_KEY, JSON.stringify(v)) } catch {}
}, { deep: true })

watch(addedAsins, (v) => {
  try { localStorage.setItem(ADDED_ASINS_KEY, JSON.stringify(Array.from(v))) } catch {}
}, { deep: true })

watch(addedOpenLibraryIds, (v) => {
  try { localStorage.setItem(ADDED_OLIDS_KEY, JSON.stringify(Array.from(v))) } catch {}
}, { deep: true })

// Scroll to top of results when page changes
watch(currentAdvancedPage, () => {
  nextTick(() => {
    const titleResultsElement = document.querySelector('.title-results')
    if (titleResultsElement) {
      const elementTop = titleResultsElement.getBoundingClientRect().top + window.scrollY
      window.scrollTo({ top: elementTop - 125, behavior: 'smooth' })
    }
  })
})



// Computed properties
const hasResults = computed(() => {
  return (searchType.value === 'asin' && audibleResult.value) ||
         (searchType.value === 'title' && titleResults.value.length > 0) ||
         (searchType.value === 'isbn' && audibleResult.value)
})

const hasError = computed(() => {
  return Boolean(errorMessage.value)
})

const canLoadMore = computed(() => {
  return titleResults.value.length < totalTitleResultsCount.value
})

// Unified Search Methods - now handled by useSearch composable

const handleAdvancedSearchResults = async (results: Array<Partial<SearchResult> | LooseResult>) => {
  // Convert search results to title results format
  titleResults.value = []
  audibleResult.value = null
  searchType.value = 'title'
  
  for (const result of results) {
    const r = result as LooseResult

    // Normalize common metadata keys from backend variations so the template
    // consistently finds `subtitle`/`subtitles`, `narrator` and `source`.
    try {
      // subtitles may be provided as `subtitle`, `Subtitle`, `Subtitles` or `subtitles`
      ;(r as any).subtitles = (r as any).subtitles || (r as any).subtitle || (r as any).Subtitle || (r as any).Subtitles || undefined
      ;(r as any).subtitle = (r as any).subtitle || (r as any).subtitles || (r as any).Subtitle || (r as any).Subtitles || undefined

      // narrators may be provided as array or single string
      if (!(r as any).narrator) {
        if (Array.isArray((r as any).narrators) && (r as any).narrators.length) {
          ;(r as any).narrator = (r as any).narrators.map((n: unknown) => typeof n === 'object' && n ? ((n as any).name || (n as any).Name) : String(n)).filter(Boolean).join(', ')
        } else if ((r as any).Narrators && Array.isArray((r as any).Narrators) && (r as any).Narrators.length) {
          ;(r as any).narrator = (r as any).Narrators.map((n: unknown) => typeof n === 'object' && n ? ((n as any).name || (n as any).Name) : String(n)).filter(Boolean).join(', ')
        } else if ((r as any).Narrator) {
          ;(r as any).narrator = (r as any).Narrator
        }
      }

      // If backend indicates audimeta as metadataSource, present the user-facing
      // source label as 'Audible' to match expectations
      if ((r as any).metadataSource && String((r as any).metadataSource).toLowerCase().includes('audimeta')) {
        ;(r as any).source = 'Audible'
      }
    } catch {
      // swallow normalization errors
    }
    // Extract year from common date fields (publishedDate, releaseDate, etc.)
    let publishYear: number | undefined
    const dateStr = (r as any).publishedDate ?? (r as any).releaseDate ?? (r as any).ReleaseDate ?? (r as any).release_date ?? (r as any).Release_date
    if (dateStr) {
      if (typeof dateStr === 'object' && typeof (dateStr as any).getFullYear === 'function') {
        publishYear = (dateStr as Date).getFullYear()
      } else if (typeof dateStr === 'string') {
        const year = parseInt(dateStr.substring(0, 4))
        if (!isNaN(year)) publishYear = year
      }
    }
    
    const authorsFromResult = ((): string[] => {
      // Prefer normalized author field (flattened by earlier conversion)
      if ((r as any).author && typeof (r as any).author === 'string' && (r as any).author.trim().length) return [(r as any).author.trim()]

      // Check for Artist field (capital A, from SearchResult.Artist)
      if ((r as any).Artist && typeof (r as any).Artist === 'string' && (r as any).Artist.trim().length) {
        return [ (r as any).Artist.trim() ]
      }

      // Check for artist field (used in advanced search results)
      if ((r as any).artist && typeof (r as any).artist === 'string' && (r as any).artist.trim().length) {
        return [(r as any).artist.trim()]
      }

      // If result contains an authors array (from Audimeta), extract names
      const maybeAuthors = ((r as any).authors ?? (r as any).Authors) as ({ name?: string; Name?: string } | string)[] | undefined
      if (Array.isArray(maybeAuthors) && maybeAuthors.length) {
        return maybeAuthors.map((a) => (typeof a === 'object' && a ? ((a as any).name || (a as any).Name || '') : String(a))).filter((n) => !!n)
      }

      // If the original searchResult contains authors, use those
      const sr = (r as any).searchResult
      const srAuthors = sr ? ((sr as any).authors ?? (sr as any).Authors) as ({ name?: string } | string)[] | undefined : undefined
      if (Array.isArray(srAuthors) && srAuthors.length) {
        return srAuthors.map((a) => (typeof a === 'object' && a ? ((a as any).name || (a as any).Name || '') : String(a))).filter((n) => !!n)
      }

      return []
    })()

    // If the result looks like an Audimeta-enriched audiobook (or explicitly marked),
    // prefer to populate the richer audiobook-shaped fields so the Add New UI
    // can surface subtitles, narrators, runtime, publish date, etc.
    const looksLikeAudimeta = (result.metadataSource && String(result.metadataSource).toLowerCase() === 'audimeta') || Boolean(result.isEnriched) || Boolean(result.asin)

    const titleResult: TitleSearchResult = {
      title: result.title || '',
      author_name: authorsFromResult.length ? authorsFromResult : [(result as any).author || (result as any).Artist || result.artist || ''],
      first_publish_year: publishYear,
      cover_i: undefined,
      key: String(result.asin || result.id || ''),
      searchResult: result as unknown as SearchResult,
      imageUrl: result.imageUrl,
      // prefer explicit metadataSource, but fall back to attached searchResult metadata when present
      metadataSource: looksLikeAudimeta ? 'audimeta' : (result.metadataSource || (result.searchResult && (result.searchResult as any).metadataSource)),
      // forward publisher into the top-level TitleSearchResult so template's publisher check works
      publisher: Array.isArray(result.publisher) ? result.publisher : (result.publisher ? [result.publisher] : undefined)
    }

    if (looksLikeAudimeta) {
      // Keep a copy of the raw audimeta results for client-side paging and reference
      try {
        if (Array.isArray(results) && results.length && (results[0] as any).asin) {
          allAudimetaResults.value = results as any[]
        }
      } catch (e) {}
      // Populate commonly used Audimeta-like fields (flattened to top-level)
      ;(titleResult as any).subtitle = (result as any).subtitles || (result as any).Subtitles || (result as any).subtitle || (result as any).Subtitle || undefined
      ;(titleResult as any).narrator = ((result as any).narrators || (result as any).Narrators || []).map((n: any) => n?.name || n?.Name).filter(Boolean).join(', ') || (result as any).narrator || (result as any).Narrator || undefined
      ;(titleResult as any).runtime = (() => {
        // Normalize runtime to minutes. Backend may return minutes or seconds
        const raw = (result as any).runtimeLengthMin ?? (result as any).lengthMinutes ?? (result as any).runtimeMinutes ?? (result as any).RuntimeLengthMin ?? (result as any).runtime ?? (result as any).Runtime ?? (result as any).RuntimeMinutes ?? (result as any).RuntimeSeconds
        if (raw === undefined || raw === null) return undefined
        const num = Number(raw)
        if (isNaN(num)) return undefined
        // Heuristic: values > 1000 are likely seconds, convert to minutes
        const minutes = num > 1000 ? Math.round(num / 60) : num
        // Also populate the original searchResult.runtime as seconds to satisfy consumers/tests
        try { (result as any).runtime = Math.round(minutes * 60) } catch (e) {}
        return minutes
      })()
      ;(titleResult as any).publishedDate = (result as any).releaseDate || (result as any).ReleaseDate || (result as any).publishedDate || (result as any).PublishedDate || undefined
      ;(titleResult as any).description = (result as any).description || (result as any).Description || undefined
      ;(titleResult as any).asin = (result as any).asin || (result as any).Asin || undefined
      ;(titleResult as any).id = (result as any).asin || (result as any).sku || (result as any).id || (result as any).title
      // Normalize product/link into `productUrl` and ensure it's present on the attached searchResult
      ;(titleResult as any).productUrl = (result as any).productUrl || (result as any).link || (result as any).Link || undefined
      try {
        const prod = (titleResult as any).productUrl
        if (prod) {
          ;(result as any).productUrl = prod
        }
      } catch (e) {}
      ;(titleResult as any).series = (r as any).series
      // Preserve the raw series array as `seriesList` when present and normalize a display string
      try {
        const rawSeries = (r as any).series
        if (Array.isArray(rawSeries) && rawSeries.length) {
          // store the raw list for tooltip display
          const list = rawSeries.map((s: any) => {
            if (typeof s === 'object' && s) {
              const name = s.name || s.Name || String(s)
              const position = s.position || s.Position
              return position ? `${name} #${position}` : name
            }
            return String(s)
          }).filter(Boolean)
          ;(titleResult as any).seriesList = list
          ;(titleResult as any).searchResult = (titleResult as any).searchResult || r
          ;(titleResult as any).searchResult.seriesList = list
          // pick first for visible series string
          const s = rawSeries[0] as ({ name?: string } | string)
          ;(r as any).series = typeof s === 'object' && s ? ((s as any).name || String(s)) : String(s)
        } else if (rawSeries) {
          ;(titleResult as any).seriesList = [String(rawSeries)]
          ;(titleResult as any).searchResult = (titleResult as any).searchResult || r
          ;(titleResult as any).searchResult.seriesList = [String(rawSeries)]
        }
      } catch { }
      try {
        // Ensure the attached searchResult reflects the normalized series string
        ;(titleResult as any).searchResult = (titleResult as any).searchResult || r
        ;(titleResult as any).searchResult.series = (r as any).series
        // Propagate normalized productUrl into the attached searchResult as tests expect
        if ((titleResult as any).productUrl) {
          ;(titleResult as any).searchResult.productUrl = (titleResult as any).productUrl
        }
      } catch { }
      ;(titleResult as any).seriesNumber = (result as any).seriesNumber || (result as any).seriesPosition || undefined
      // ensure image URL is available
      if (!(titleResult as any).imageUrl && (result as any).imageUrl) (titleResult as any).imageUrl = (result as any).imageUrl
    }
    titleResults.value.push(titleResult)
    try {
      const idx = titleResults.value.length - 1
      const sr = (titleResults.value[idx] as any).searchResult
      if (sr && Array.isArray(sr.series) && sr.series.length) {
        (sr as any).series = sr.series[0]?.name || String(sr.series[0])
      }
    } catch (e) {}
  }
  
  totalTitleResultsCount.value = results.length
  searchStatus.value = ''
  
  // Check library status
  await checkExistingInLibrary()
  
  toast.info(`Found ${results.length} results from advanced search`, 'Advanced Search')
}

const performAdvancedSearch = async () => {
  // If advanced UI is visible, use the advanced form fields directly
  if (showAdvancedSearch.value) {
    const p = advancedSearchParams.value as { title?: string; author?: string; series?: string; isbn?: string; asin?: string; language?: string }
    const hasAny = Boolean((p.title && p.title.trim()) || (p.author && p.author.trim()) || (p.series && p.series.trim()) || (p.isbn && p.isbn.trim()) || (p.asin && p.asin.trim()))
    if (!hasAny) {
      advancedSearchError.value = 'Please enter a search term'
      return
    }

    advancedSearchError.value = ''
    isSearching.value = true
    searchError.value = ''

    try {
      // Use the unified advanced search endpoint for all advanced queries.
      // The backend returns enriched SearchResult objects which are mapped
      // into the UI by handleAdvancedSearchResults.
      isAudimetaPaged.value = false
      const params: Record<string, any> = {}
      if (p.title && p.title.trim()) params.title = p.title.trim()
      if (p.author && p.author.trim()) params.author = p.author.trim()
      if (p.isbn && p.isbn.trim()) params.isbn = p.isbn.trim()
      if (p.asin && p.asin.trim()) params.asin = p.asin.trim()
      if (p.language) params.language = p.language

      const seriesName = p.series ? p.series.trim() : ''

      // If the user only provided a series, use the unified advanced search POST
      // so the backend performs the Audimeta series lookup and conversion.
      if (seriesName && !params.title && !params.author && !params.isbn && !params.asin) {
        try {
          const resp = await apiService.advancedSearch({ series: seriesName, language: p.language, pagination: { page: 1, limit: resultsPerPage.value } })
          // backend returns enriched SearchResult objects
          await handleAdvancedSearchResults(resp as Partial<SearchResult>[])
        } catch (e) {
          throw e
        }

        return
      }

      // Include pagination and candidate limits so the backend can adjust candidate/return caps
      params.pagination = { page: 1, limit: resultsPerPage.value }

      currentAdvancedPage.value = 1

      // If both author and series provided, author takes priority; perform author search then filter by series (no extra Audimeta series lookup)
      if (params.author && seriesName) {
        const results = await apiService.advancedSearch(params)

        const filtered = (results as any[]).filter(r => {
          try {
            const sr = r.searchResult || r

            // If searchResult.series is an array of objects with asin fields, check asin or name
            if (Array.isArray(sr.series) && sr.series.length) {
              for (const s of sr.series) {
                const a = (s && (s.asin || s.Asin || s.ASIN)) || (typeof s === 'string' ? s : undefined)
                if (a && (String(a).toLowerCase().includes(seriesName.toLowerCase()) || String(a) === seriesName)) return true
                const n = (s && (s.name || s.Name)) || undefined
                if (n && String(n).toLowerCase().includes(seriesName.toLowerCase())) return true
              }
            }

            // If series is a string
            if (sr.series && typeof sr.series === 'string' && sr.series.toLowerCase().includes(seriesName.toLowerCase())) return true

            // Fallback: check top-level result.series
            if (r.series && typeof r.series === 'string' && r.series.toLowerCase().includes(seriesName.toLowerCase())) return true

          } catch (e) {}
          return false
        })

        // If filtering produced no matches, fall back to returning the unfiltered author results
        await handleAdvancedSearchResults(filtered.length ? filtered : results)

        // done: scroll into view
        nextTick(() => {
          const titleResultsElement = document.querySelector('.title-results')
          if (titleResultsElement) {
            const elementTop = titleResultsElement.getBoundingClientRect().top + window.scrollY
            window.scrollTo({ top: elementTop - 125, behavior: 'smooth' })
          }
        })
        return
      }

      const results = await apiService.advancedSearch(params)
      await handleAdvancedSearchResults(results)
      
      // Scroll to top of results after search
      nextTick(() => {
        const titleResultsElement = document.querySelector('.title-results')
        if (titleResultsElement) {
          const elementTop = titleResultsElement.getBoundingClientRect().top + window.scrollY
          window.scrollTo({ top: elementTop - 125, behavior: 'smooth' })
        }
      })
    
    } catch (err) {
      advancedSearchError.value = err instanceof Error ? err.message : 'Search failed'
      errorMessage.value = advancedSearchError.value
    } finally {
      isSearching.value = false
    }

    return
  }

  // Fallback: if advanced UI is hidden, parse the unified search query for advanced tokens
  const query = searchQuery.value.trim()
  if (!query) {
    advancedSearchError.value = 'Please enter a search term'
    return
  }

  advancedSearchError.value = ''
  isSearching.value = true
  searchError.value = ''

  try {
    // Parse the query to extract advanced params
    const params: Record<string, any> = {}
    const parts = query.split(/\s+/)
    
    for (const part of parts) {
      if (part.toUpperCase().startsWith('TITLE:')) {
        params.title = part.substring(6).trim()
      } else if (part.toUpperCase().startsWith('AUTHOR:')) {
        params.author = part.substring(7).trim()
      } else if (part.toUpperCase().startsWith('ISBN:')) {
        params.isbn = part.substring(5).trim()
      } else if (part.toUpperCase().startsWith('ASIN:')) {
        params.asin = part.substring(5).trim()
      }
    }

    // Also include language if set
    if (advancedSearchParams.value.language) {
      params.language = advancedSearchParams.value.language
    }

    // Include pagination/cap when calling advanced search from unified query
    params.pagination = { page: 1, limit: resultsPerPage.value }
    currentAdvancedPage.value = 1
    const results = await apiService.advancedSearch(params)
    await handleAdvancedSearchResults(results)
  } catch (err) {
    advancedSearchError.value = err instanceof Error ? err.message : 'Search failed'
    errorMessage.value = advancedSearchError.value
  } finally {
    isSearching.value = false
  }
}

const clearAdvancedSearch = () => {
  advancedSearchParams.value = {
    title: '',
    author: '',
    series: '',
    isbn: '',
    asin: '',
    language: ''
  }
  advancedSearchError.value = ''
  // Reset audimeta paging state
  audimetaPage.value = 1
  audimetaTotal.value = 0
  isAudimetaPaged.value = false
  allAudimetaResults.value = []
}

const changeAudimetaPage = async (newPage: number) => {
  if (newPage < 1) return
  audimetaPage.value = newPage
  // Update URL query param
  try {
    const q = { ...router.currentRoute.value.query } as Record<string, string>
    q.page = String(audimetaPage.value)
    router.replace({ query: q })
  } catch (e) {}
  
  // If we have all results stored, just update the display without calling API
  if (allAudimetaResults.value.length > 0) {
    const startIndex = (audimetaPage.value - 1) * audimetaLimit.value
    const endIndex = startIndex + audimetaLimit.value
    const pageResults = allAudimetaResults.value.slice(startIndex, endIndex)
    const converted = (pageResults as any[]).map(r => ({
      asin: r.asin || r.Asin || '',
      title: r.title || r.Title || '',
      artist: (r.authors || r.Authors || []).map((a: any) => a.name || a.Name).filter(Boolean).join(', '),
      imageUrl: r.imageUrl || r.ImageUrl || '',
      runtime: (() => { const raw = r.runtimeLengthMin ?? r.lengthMinutes ?? r.runtimeMinutes ?? r.RuntimeLengthMin ?? r.lengthMinutes ?? r.runtime ?? r.Runtime ?? r.RuntimeMinutes ?? r.RuntimeSeconds; if (raw === undefined || raw === null) return undefined; const n = Number(raw); if (isNaN(n)) return undefined; return n > 1000 ? Math.round(n/60) : n })(),
      language: r.language || r.Language,
      metadataSource: 'Audimeta',
      id: r.asin || r.sku || r.sku || r.title
    })) as Partial<SearchResult>[]
    await handleAdvancedSearchResults(converted)
    
    // Scroll to top of results after page change
    nextTick(() => {
      const titleResultsElement = document.querySelector('.title-results')
      if (titleResultsElement) {
        const elementTop = titleResultsElement.getBoundingClientRect().top + window.scrollY
        window.scrollTo({ top: elementTop - 125, behavior: 'smooth' })
      }
    })
  } else {
    // Fallback: call API if no cached results
    await performAdvancedSearch()
  }
}

const toggleAdvancedSearch = () => {
  if (showAdvancedSearch.value) {
    // Hiding advanced search - switch back to simple search
    showAdvancedSearch.value = false
  } else {
    // Showing advanced search - switch to advanced mode
    showAdvancedSearch.value = true
  }
}

const updateSearchQueryFromAdvanced = () => {
  const params = advancedSearchParams.value
  const parts: string[] = []
  
  if (params.title) parts.push(`TITLE:${params.title}`)
  if (params.author) parts.push(`AUTHOR:${params.author}`)
  if (params.series) parts.push(`SERIES:${params.series}`)
  if (params.isbn) parts.push(`ISBN:${params.isbn}`)
  if (params.asin) parts.push(`ASIN:${params.asin}`)
  
  searchQuery.value = parts.join(' ')
}

const updateAdvancedParamsFromQuery = () => {
  const query = searchQuery.value.trim()
  const params = {
    title: '',
    author: '',
    series: '',
    isbn: '',
    asin: '',
    language: advancedSearchParams.value.language // preserve language
  }
  
  const parts = query.split(/\s+/)
  for (const part of parts) {
    if (part.toUpperCase().startsWith('TITLE:')) {
      params.title = part.substring(6).trim()
    } else if (part.toUpperCase().startsWith('AUTHOR:')) {
      params.author = part.substring(7).trim()
    } else if (part.toUpperCase().startsWith('ISBN:')) {
      params.isbn = part.substring(5).trim()
    } else if (part.toUpperCase().startsWith('ASIN:')) {
      params.asin = part.substring(5).trim()
    } else if (part.toUpperCase().startsWith('SERIES:')) {
      params.series = part.substring(7).trim()
    }
  }
  
  advancedSearchParams.value = params
}

// Audible search functions removed

const searchByAsin = async (asin: string) => {
  logger.debug('searchByAsin called with:', asin)
  
  // Strip ASIN: prefix if present
  const cleanAsin = asin.replace(/^ASIN:/i, '').trim()
  
  // Validate ASIN using the same strict pattern as detection.
  if (!/^(B[0-9A-Z]{9})$/.test(cleanAsin.toUpperCase())) {
    searchError.value = 'Invalid ASIN format. Expected an Amazon ASIN like B08G9PRS1K'
    return
  }
  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  errorMessage.value = ''
  asinQuery.value = cleanAsin
  
  // Check if metadata sources are configured
  if (enabledMetadataSources.value.length === 0) {
    searchStatus.value = 'No metadata sources configured'
    errorMessage.value = 'Please configure at least one metadata source in Settings to fetch audiobook information.'
    isSearching.value = false
    return
  }
  
  searchStatus.value = `Searching for ASIN ${cleanAsin}...`
  
  try {
    // Use the search API with ASIN: prefix to trigger intelligent search with direct product page scraping
    // This will scrape Amazon/Audible product pages directly instead of only checking metadata APIs
    // Cancel any previous search and create controller for this request
    try { searchAbortController.value?.abort() } catch {}
    searchAbortController.value = new AbortController()
    const results = await apiService.searchByTitle(`ASIN:${cleanAsin}`, { signal: searchAbortController.value.signal, language: searchLanguage.value })
    
    logger.debug('ASIN search results:', results)
    
    if (results && results.length > 0) {
      // Take the first result (should be the direct ASIN match)
      const result = results[0]
      
      if (result) {
        // Extract year from publishedDate if available
        let publishYear: string | undefined
        if (result.publishedDate) {
          const yearMatch = result.publishedDate.match(/\d{4}/)
          publishYear = yearMatch ? yearMatch[0] : undefined
        }
        
        audibleResult.value = {
          asin: result.asin || cleanAsin,
          title: result.title || 'Unknown Title',
          subtitle: undefined,
          authors: result.artist ? [result.artist] : [],
          narrators: result.narrator ? result.narrator.split(', ') : [],
          publisher: result.publisher,
          publishYear: publishYear,
          publishedDate: result.publishedDate,
          description: result.description,
          imageUrl: result.imageUrl,
          runtime: result.runtime,
          language: result.language,
          series: result.series,
          seriesNumber: result.seriesNumber,
          seriesList: result.seriesList || (result.series ? [result.series + (result.seriesNumber ? ` #${result.seriesNumber}` : '')] : undefined),
          isbn: undefined,
          source: result.source,
          sourceLink: result.sourceLink
        }
        
        logger.debug('audibleResult set with source:', audibleResult.value.source)
      }
    }
    
  // Check library status after getting result
  searchStatus.value = 'Checking library for existing copies...'
  await checkExistingInLibrary()
  // Finalize status
  searchStatus.value = audibleResult.value ? `Found metadata from ${audibleResult.value.source || 'search'}` : 'No metadata available'
  } catch (error) {
    logger.error('ASIN search failed:', error)
    errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobook'
  } finally {
    isSearching.value = false
    // Keep a brief 'done' status then clear
    setTimeout(() => { searchStatus.value = '' }, 1200)
  }
}

const searchByTitle = async (query: string) => {
  // Cancel any previous search in progress
  try { searchAbortController.value?.abort() } catch {}
  searchAbortController.value = new AbortController()

  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  totalTitleResultsCount.value = 0
  currentPage.value = 0
  errorMessage.value = ''
  resolvedAsins.value = {}
  asinFilteringApplied.value = false
  
  // Parse query for display in error messages (but keep prefix for backend)
  const parsed = parseSearchQuery(query.replace(/^(TITLE:|AUTHOR:)/i, '').trim())
  titleQuery.value = parsed.title
  authorQuery.value = parsed.author || ''
      
      // If the parsed title looks like a URL (we may pass Amazon "stripbooks" URLs for ISBN
      // searches), avoid showing the full URL to users. Instead, display a friendly label
      // such as the ISBN number or a short host-based hint.
      let displayTitle = parsed.title
      try {
        if (displayTitle && displayTitle.match(/^https?:\/\//i)) {
          const u = new URL(displayTitle)
          // Try to extract ISBN from Amazon stripbooks search (rh param contains p_66:ISBN)
          const rh = u.searchParams.get('rh')
          if (rh) {
            const isbnMatch = rh.match(/p_66[:%3A]*(\d{10,13})/)
            if (isbnMatch && isbnMatch[1]) {
              displayTitle = `ISBN ${isbnMatch[1]}`
            } else {
              // fallback to a short host-based hint
              displayTitle = `${u.hostname.replace(/^www\./, '')} search`
            }
          } else {
            displayTitle = `${u.hostname.replace(/^www\./, '')} search`
          }
        }
      } catch {
        // if URL parsing fails, fall back to the raw parsed title
        displayTitle = parsed.title
      }
      
      titleQuery.value = displayTitle
      authorQuery.value = parsed.author || ''
  
  searchStatus.value = 'Searching for audiobooks and fetching metadata...'
  try {
    // Use intelligent search API that searches Audible/Amazon, gets ASINs, and enriches with metadata
    // Pass the original query WITH prefix so backend can handle TITLE:/AUTHOR: prefixes
    const results = await apiService.searchByTitle(query, { signal: searchAbortController.value.signal, language: searchLanguage.value })
    // expose raw results for debugging on the Add New page
    rawDebugResults.value = results
    try { window.addnew_rawDebugResults = results } catch {}
    logger.debug('Intelligent search returned:', results)
    logger.debug('Number of results:', results?.length)
    
    searchStatus.value = 'Processing search results...'
    
    // Convert enriched SearchResult to display format
    titleResults.value = []
    const processedAsins = new Set<string>()

    for (const result of results) {
      // Only consider enriched results for display, but allow OpenLibrary-derived candidates
      // (OpenLibrary may provide metadata without the 'isEnriched' flag)
      const isOpenLibrary = (result.metadataSource && result.metadataSource.toLowerCase().includes('openlibrary')) || (result.source && result.source.toLowerCase().includes('openlibrary')) || !!result.id
      if (!result.isEnriched && !isOpenLibrary) continue

      let asin = (result.asin || '').toString().trim()
      // If ASIN is not provided by the backend, try to extract it from any URL fields
      if (!asin) {
        const candidateFields = [result.sourceLink, (result as unknown as Record<string, unknown>).productUrl, (result as unknown as Record<string, unknown>).resultUrl, result.source]
        for (const field of candidateFields) {
          if (!field) continue
          try {
            const m = field.toString().match(/(B[0-9A-Z]{9})/i)
            if (m && m[1]) {
              asin = m[1].toUpperCase()
              break
            }
          } catch {
            // ignore parse errors
          }
        }
      }

      // If we have an ASIN, ensure we dedupe per-asin
      if (asin) {
        if (processedAsins.has(asin)) continue
        processedAsins.add(asin)
        resolvedAsins.value[`search-${asin}`] = asin
      }

      // Use stable key: prefer ASIN when available, otherwise use the provider result id
      const key = asin ? `search-${asin}` : (result.id ? `search-${result.id}` : `search-unknown-${Math.random().toString(36).slice(2,8)}`)

      // Create a book object with metadata from the enriched SearchResult
      const displayBook: TitleSearchResult = {
        key,
        title: result.title || 'Unknown Title',
        author_name: result.artist ? [result.artist] : [],
        isbn: [],
        first_publish_year: (() => {
          const dateStr = (result as any).releaseDate || result.publishedDate
          return dateStr ? parseInt(dateStr.match(/\d{4}/)?.[0] || '0', 10) || undefined : undefined
        })(),
        publisher: result.publisher ? [result.publisher] : undefined,
        metadataSource: result.metadataSource, // Which metadata source enriched it (Audimeta, Audnexus, etc.)
        imageUrl: result.imageUrl,
        searchResult: result // Store the full enriched SearchResult
      }
      titleResults.value.push(displayBook)
    }
    
    asinFilteringApplied.value = true
    totalTitleResultsCount.value = (titleResults.value.length as unknown) as number

    // After populating titleResults, attempt to resolve missing ASINs from ISBNs (OpenLibrary)
    // Run in background; updates resolvedAsins and searchResult.asin when found.
    (async () => {
      try {
        await attemptResolveAsinsForTitleResults()
      } catch {
        logger.debug('Attempt to resolve ASINs failed')
      }
    })()
    
    if (titleResults.value.length === 0) {
      errorMessage.value = 'No audiobooks found. Try refining your search terms.'
    }
    
    // Check library status after getting results
    searchStatus.value = 'Checking library for existing matches...'
    await checkExistingInLibrary()
    searchStatus.value = `Search complete — found ${titleResults.value.length} items`
  } catch (error) {
    if (error && (error as Error).name === 'AbortError') {
      logger.debug('Title search aborted by user')
      errorMessage.value = 'Search cancelled'
    } else {
      logger.error('Title search failed:', error)
      errorMessage.value = error instanceof Error ? error.message : 'Failed to search for audiobooks'
    }
  } finally {
    isSearching.value = false
    // Clear status shortly after completion so UI isn't stale
    setTimeout(() => { searchStatus.value = '' }, 1000)
    // clear controller after completion
    try { searchAbortController.value = null } catch {}
  }
}

// Lightweight raw fetch helper removed (debug helper)

const parseSearchQuery = (query: string): { title: string; author?: string } => {
  // Try to parse "title by author" format
  const byMatch = query.match(/^(.+?)\s+by\s+(.+)$/i)
  if (byMatch && byMatch[1] && byMatch[2]) {
    return {
      title: byMatch[1].trim(),
      author: byMatch[2].trim()
    }
  }
  
  // Try to parse "author - title" format
  const dashMatch = query.match(/^(.+?)\s*-\s*(.+)$/)
  if (dashMatch && dashMatch[1] && dashMatch[2]) {
    return {
      title: dashMatch[2].trim(),
      author: dashMatch[1].trim()
    }
  }
  
  // Default to treating the entire query as title
  return { title: query }
}

const loadMoreTitleResults = async () => {
  // Since backend search returns all Amazon/Audible results at once,
  // we don't need pagination like OpenLibrary. This function is now a no-op.
  // Results are already loaded in searchByTitle()
  logger.debug('Load more not needed - all Amazon/Audible results already loaded')
}

// const clearTitleError = () => {
//   searchError.value = ''
// }

// Helper methods for Open Library results
const getCoverUrl = (book: TitleSearchResult): string => {
  const key = book.key || JSON.stringify(book.title || '')
  // If we've already selected a best cover, return it (proxied)
  if (coverSelection.value[key]) return apiService.getImageUrl(coverSelection.value[key])

  // Start background evaluation of best cover (non-blocking)
  pickBestCoverForBook(book).catch(() => logger.debug('pickBestCoverForBook error'))

  // Immediate fallback: prefer explicit imageUrl, then searchResult image
  if (book.imageUrl) return apiService.getImageUrl(book.imageUrl)
  const imageUrl = book.searchResult?.imageUrl || ''
  return apiService.getImageUrl(imageUrl)
}

// Try to pick the image whose aspect ratio is closest to 1:1 from available candidates
const pickBestCoverForBook = async (book: TitleSearchResult): Promise<void> => {
  try {
    const key = book.key || JSON.stringify(book.title || '')
    // Do not repeat work if we already have a selection
    if (coverSelection.value[key]) return

    const candidates: string[] = []
    if (book.imageUrl) candidates.push(book.imageUrl)
    if (book.searchResult?.imageUrl) candidates.push(book.searchResult.imageUrl)

    // If OpenLibrary book has a cover id, include sizes (L, M, S)
    try {
      const olBook = book as OpenLibraryBook
      const coverId = (olBook as unknown as { cover_i?: number }).cover_i
      if (coverId && coverId > 0) {
        const uL = openLibraryService.getCoverUrl(coverId, 'L')
        const uM = openLibraryService.getCoverUrl(coverId, 'M')
        const uS = openLibraryService.getCoverUrl(coverId, 'S')
        if (uL) candidates.push(uL)
        if (uM) candidates.push(uM)
        if (uS) candidates.push(uS)
      }
    } catch {
      logger.debug('cover id extraction failed')
    }

    // Normalize and dedupe
    const uniq = Array.from(new Set(candidates.filter(u => !!u))) as string[]
    if (!uniq.length) return

    // Load images and measure aspect ratios with timeout
    const results: Array<{ url: string; score: number }> = []
    for (const url of uniq) {
      try {
        const ratio = await measureImageAspectRatio(apiService.getImageUrl(url), 3000)
        if (ratio && ratio > 0) {
          const score = Math.abs(ratio - 1)
          results.push({ url, score })
        }
      } catch (e) {
        logger.debug('Failed to load image for ratio check', url, e)
      }
    }

    if (results.length === 0) return
    // Choose minimum score (closest to 1:1)
    results.sort((a, b) => a.score - b.score)
    if (results[0] && results[0].url) coverSelection.value[key] = results[0].url
  } catch (e) {
    logger.debug('pickBestCoverForBook overall failure', e)
  }
}

// Use shared image error handler to keep behavior consistent across views
const handleLazyImageError = (ev: Event) => {
  try { return handleImageError(ev) } catch { /* swallow */ }
}

const measureImageAspectRatio = (url: string, timeoutMs = 3000): Promise<number | null> => {
  return new Promise((resolve) => {
    const img = new Image()
    let settled = false
    const t = setTimeout(() => {
      if (!settled) {
        settled = true
        img.src = ''
        resolve(null)
      }
    }, timeoutMs)

    img.onload = () => {
      if (settled) return
      settled = true
      clearTimeout(t)
      try {
        const w = img.naturalWidth || img.width
        const h = img.naturalHeight || img.height
        if (!w || !h) return resolve(null)
        resolve(w / h)
      } catch {
        resolve(null)
      }
    }

    img.onerror = () => {
      if (settled) return
      settled = true
      clearTimeout(t)
      resolve(null)
    }

    img.src = url
  })
}

const formatAuthors = (book: TitleSearchResult): string => {
  return book.author_name?.join(', ') || book.searchResult?.artist || 'Unknown Author'
}

const getAsin = (book: TitleSearchResult): string | null => {
  return book.searchResult?.asin || resolvedAsins.value[book.key] || null
}

const getMetadataSourceUrl = (book: TitleSearchResult): string | null => {
  const source = book.metadataSource || (book.searchResult && (book.searchResult as any).metadataSource)
  if (!source) return null

  // OpenLibrary metadata does not require an ASIN; prefer resultUrl (JSON) then productUrl or OL work URL
  if (source.toLowerCase().includes('openlibrary')) {
    // Prefer the canonical metadata/result URL (e.g., OpenLibrary .json) if provided
    if (book.searchResult?.resultUrl) return book.searchResult.resultUrl
    // Fall back to productUrl (human-facing page) if resultUrl is not available
    if (book.searchResult?.productUrl) return book.searchResult.productUrl
    const olBook = book as OpenLibraryBook
    // Avoid using our local generated keys (they start with 'search-') — prefer real OL identifiers
    const candidateKey = (olBook.key || '').toString()
    const looksLikeLocalKey = candidateKey.startsWith('search-') || candidateKey.startsWith('search-unknown-')
    if (!looksLikeLocalKey) {
      // If the key is a work (e.g., '/works/OL82548W'), prefer work JSON/page URLs
      if (candidateKey.startsWith('/works')) {
        const workJson = openLibraryService.getWorkJsonUrlFromBook(olBook)
        if (workJson) return workJson
        const workPage = openLibraryService.getWorkPageUrlFromBook(olBook)
        if (workPage) return workPage
      }

      // Prefer a book (edition) JSON metadata link (OLID) for metadata badge
      const jsonUrl = openLibraryService.getBookJsonUrlFromBook(olBook)
      if (jsonUrl) return jsonUrl

      // Fall back to a book page URL if JSON isn't available
      const pageUrl = openLibraryService.getBookPageUrlFromBook(olBook)
      if (pageUrl) return pageUrl

      // If key is a work but we couldn't derive an edition, fallback to work search by title
      if (candidateKey.startsWith('/works') && book.title) {
        const q = `${book.title}${book.author_name && book.author_name.length ? ' ' + book.author_name[0] : ''}`
        return openLibraryService.getSearchUrl(q)
      }

      // If it's a plain OLID like 'OL123M' or a canonical /books path, return the generic book URL
      if (candidateKey.startsWith('/books') || /^OL\w+/i.test(candidateKey)) {
        return openLibraryService.getBookUrl(candidateKey)
      }
    }
    // No key: fall back to search by title
    if (book.title) return openLibraryService.getSearchUrl(book.title)
    return null
  }

  const asin = getAsin(book)
  if (!asin) return null

  // Map metadata source to URL for ASIN-based providers
  if (source.toLowerCase().includes('audimeta')) {
    // Link the metadata badge to the external Audimeta website for the book
    return `https://audimeta.de/book/${encodeURIComponent(asin)}`
  } else if (source.toLowerCase().includes('audnex')) {
    // Audnexus API format
    return `https://api.audnex.us/books/${asin}`
  } else if (source === 'Amazon') {
    return buildAmazonProductUrl(asin)
  } else if (source === 'Audible') {
    return buildAudibleProductUrl(asin)
  }

  return null
}

// Get a sensible 'source' URL for the book (indexer/product or OpenLibrary work page)
const getSourceUrl = (book: TitleSearchResult): string | null => {
  // Prefer explicit productUrl from the enriched SearchResult
  if (book.searchResult?.productUrl) return book.searchResult.productUrl

  // If metadata indicates Audimeta (either top-level or attached searchResult), link to Audible product page for ASIN when available
  const asin = getAsin(book)
  const metaSource = ((book.metadataSource || (book.searchResult && (book.searchResult as any).metadataSource)) || '').toString().toLowerCase()
  if (metaSource.includes('audimeta') && asin) {
    return buildAudibleProductUrl(asin)
  }

  // If provider/source is OpenLibrary or we have an OL key, link to the OL work page
  if (book.searchResult?.source?.toLowerCase().includes('openlibrary') || metaSource.includes('openlibrary')) {
    const olBook = book as OpenLibraryBook
    const candidateKey = (olBook.key || '').toString()
    const looksLikeLocalKey = candidateKey.startsWith('search-') || candidateKey.startsWith('search-unknown-')
    if (!looksLikeLocalKey) {
      // Prefer the human-facing book page URL (edition if available)
      const pageUrl = openLibraryService.getBookPageUrlFromBook(olBook)
      if (pageUrl) return pageUrl
      if (candidateKey.startsWith('/books') || /^OL\w+/i.test(candidateKey)) return openLibraryService.getBookUrl(candidateKey)
    }
    // Fallback to searching by title when we don't have a usable OL identifier
    if (book.title) return openLibraryService.getSearchUrl(book.title)
  }

  return null
}

// Extract ISBN candidates from an OpenLibrary-derived TitleSearchResult
const extractIsbnCandidates = (book: TitleSearchResult): string[] => {
  try {
    // Prefer OpenLibrary service helpers when available
    const isbns: string[] = openLibraryService.getISBNs(book as OpenLibraryBook)
    if (!isbns || isbns.length === 0) return []
    // Normalize and dedupe
    const cleaned = Array.from(new Set(isbns.map(i => i.replace(/[-\s]/g, ''))))
    return cleaned
  } catch (e) {
    logger.debug('extractIsbnCandidates error', e)
    return []
  }
}

// Lazy load helper for Add New search result images
let lazyObserver: IntersectionObserver | null = null

const observeLazyImages = () => {
  // Find all lazy-search-img elements (use dataset 'src' for the true image)
  try {
    const images = Array.from(document.querySelectorAll('img.lazy-search-img')) as HTMLImageElement[]
    if (!images || images.length === 0) return

    if ('IntersectionObserver' in window) {
      if (!lazyObserver) {
        lazyObserver = new IntersectionObserver((entries) => {
          entries.forEach(entry => {
            if (entry.isIntersecting) {
              const img = entry.target as HTMLImageElement
              const ds = img.dataset.src
              if (ds) {
                img.src = ds
                img.removeAttribute('data-src')
              }
              lazyObserver?.unobserve(img)
            }
          })
        }, { rootMargin: '200px', threshold: 0.01 })
      }

      for (const img of images) {
        if (img.dataset.src) {
          lazyObserver.observe(img)
        }
      }
    } else {
      // Fallback: load immediately
      for (const img of images) {
        if (img.dataset.src) {
          img.src = img.dataset.src
          img.removeAttribute('data-src')
        }
      }
    }
  } catch (e) {
    logger.debug('observeLazyImages error', e)
  }
}

// Observe when results change to attach lazy loader
// Re-observe lazy images when results or client-side paging change
watch([
  () => titleResults.value.length,
  () => audimetaPage.value,
  () => currentAdvancedPage.value,
  () => resultsPerPage.value
], async () => {
  await nextTick()
  observeLazyImages()
})

onMounted(() => observeLazyImages())
onUnmounted(() => {
  try { lazyObserver?.disconnect(); lazyObserver = null } catch {}
})

// Resolve a single book's ASIN by trying its ISBN candidates via backend lookup
const resolveAsinForBook = async (book: TitleSearchResult): Promise<string | null> => {
  if (!book) return null
  // If already present on the enriched search result, return it
  if (book.searchResult && book.searchResult.asin) return book.searchResult.asin

  const candidates = extractIsbnCandidates(book)
  if (!candidates || candidates.length === 0) return null

  for (const isbn of candidates.slice(0, 3)) { // try up to 3 candidates
    if (!isbnService.validateISBN(isbn)) continue
    try {
      const resp = await apiService.getAsinFromIsbn(isbn)
      if (resp && resp.success && resp.asin) {
        // update resolved map and the underlying searchResult if present
        resolvedAsins.value[book.key] = resp.asin
        if (book.searchResult) {
          book.searchResult.asin = resp.asin
        }
        logger.debug('Resolved ASIN from ISBN', { isbn, asin: resp.asin, bookKey: book.key })
        return resp.asin
      }
    } catch (e) {
      logger.debug('resolveAsinForBook API error for ISBN', isbn, e)
    }
    // small delay to avoid hammering backend
    await new Promise(r => setTimeout(r, 150))
  }
  return null
}

// Iterate over titleResults and attempt to resolve missing ASINs in background
const attemptResolveAsinsForTitleResults = async (): Promise<void> => {
  if (!titleResults.value || titleResults.value.length === 0) return

  for (const book of titleResults.value) {
    try {
      // Skip if already have an ASIN
      if ((book.searchResult && book.searchResult.asin) || resolvedAsins.value[book.key]) continue
      const asin = await resolveAsinForBook(book)
      if (asin) {
        // Trigger library re-check so UI updates added status and buttons
        await checkExistingInLibrary()
      }
    } catch (e) {
      logger.debug('attemptResolveAsinsForTitleResults error for book', book.key, e)
    }
  }
}

// Removed manual ASIN helper methods (createAsinSearchHint, openAmazonSearch, useBookForAsinSearch)

// Common methods for both search types
const selectTitleResult = async (book: TitleSearchResult) => {
  logger.debug('selectTitleResult called with book:', book)
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin

  try {
    // If we have enriched search result, use it directly even if no ASIN is present
    if (book.searchResult && book.searchResult.isEnriched) {
      const result = book.searchResult
      logger.debug('Using enriched metadata from intelligent search:', result)

      // Extract publish year from date string if available
      let publishYear: string | undefined
      if (result.publishedDate) {
        const yearMatch = result.publishedDate.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }

      const metadata: AudibleBookMetadata = {
        asin: result.asin || '',
        title: result.title || 'Unknown Title',
        subtitle: undefined,
        authors: result.artist ? [result.artist] : [],
        narrators: result.narrator ? [result.narrator] : [],
        publisher: result.publisher,
        publishYear: publishYear,
        description: result.description,
        imageUrl: result.imageUrl,
        runtime: result.runtime,
        language: result.language,
        genres: [],
        series: result.series,
        seriesNumber: result.seriesNumber,
        abridged: false,
        isbn: undefined,
        source: book.metadataSource || result.source
        ,openLibraryId: result.id || undefined
      }

      // Add to library directly using the enriched metadata
      await addToLibrary(metadata)
      return
    }

    // Fallback: if we have an ASIN, fetch metadata from configured sources
    if (asin) {
      logger.debug('Fetching metadata for ASIN:', asin)
      toast.info('Fetching metadata', `Getting book details from configured sources...`)
      const response = await apiService.getMetadata(asin, 'us', true)
      const audimetaData = response.metadata
      logger.debug(`Metadata fetched from ${response.source}:`, audimetaData)
      toast.success('Metadata retrieved', `Book details fetched from ${response.source}`)

      // Store the metadata source in the book object so it shows in the UI
      book.metadataSource = response.source

      // Convert audimeta response to AudibleBookMetadata format
      let publishYear: string | undefined
      if (audimetaData.publishDate || audimetaData.releaseDate) {
        const dateStr = audimetaData.publishDate || audimetaData.releaseDate
        const yearMatch = dateStr?.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }

      const metadata: AudibleBookMetadata = {
        asin: audimetaData.asin || asin || '',
        title: audimetaData.title || 'Unknown Title',
        subtitle: audimetaData.subtitle,
        authors: audimetaData.authors?.map((a: AudimetaAuthor) => a.name).filter((n: string | undefined) => n) as string[] || [],
        narrators: audimetaData.narrators?.map((n: AudimetaNarrator) => n.name).filter((n: string | undefined) => n) as string[] || [],
        publisher: audimetaData.publisher,
        publishYear: publishYear,
        description: audimetaData.description,
        imageUrl: audimetaData.imageUrl,
        // Audimeta returns length in minutes; keep runtime in minutes for UI helpers
        runtime: audimetaData.lengthMinutes ? audimetaData.lengthMinutes : undefined,
        language: audimetaData.language,
        genres: audimetaData.genres?.map((g: AudimetaGenre) => g.name).filter((n: string | undefined) => n) as string[] || [],
        series: audimetaData.series?.length ? audimetaData.series.map(s => `${s.name}${s.position ? ` #${s.position}` : ''}`).join(', ') : undefined,
        seriesList: audimetaData.series?.map(s => `${s.name}${s.position ? ` #${s.position}` : ''}`) || [],
        seriesNumber: undefined, // Series info now included in series field
        abridged: audimetaData.bookFormat?.toLowerCase().includes('abridged') || false,
        isbn: audimetaData.isbn,
        source: response.source
        ,openLibraryId: book.searchResult?.id || undefined
      }

      // Add to library directly
      await addToLibrary(metadata)
      return
    }

    // If we reach here, we have neither enriched metadata nor an ASIN
    logger.error('No ASIN or enriched metadata available for selected book')
    toast.warning('Cannot add', 'Cannot add to library: No ASIN or metadata available')
  } catch (error) {
    logger.error('Failed to add audiobook:', error)
    toast.error('Add failed', 'Failed to add audiobook. Please try again.')
  }
}

const viewTitleResultDetails = async (book: TitleSearchResult) => {
  const asin = resolvedAsins.value[book.key] || book.searchResult?.asin

  try {
    // If we have an enriched search result, use it directly even if no ASIN is present
    if (book.searchResult && book.searchResult.isEnriched) {
      const result = book.searchResult
      logger.debug('Using enriched metadata from intelligent search for details view:', result)

      // Extract publish year from date string if available
      let publishYear: string | undefined
      if (result.publishedDate) {
        const yearMatch = result.publishedDate.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }

      // If metadata source is OpenLibrary or a resultUrl points to OL JSON, try to fetch description from the canonical JSON
      let olDescription: string | undefined = undefined
      try {
        const jsonUrl = result.resultUrl || openLibraryService.getBookJsonUrlFromBook(book as OpenLibraryBook) || openLibraryService.getWorkJsonUrlFromBook(book as OpenLibraryBook)
        if (jsonUrl) {
          const resp = await fetch(jsonUrl)
          if (resp && resp.ok) {
            const j = await resp.json()
            if (j) {
              if (typeof j.description === 'string') olDescription = j.description
              else if (j.description && typeof j.description.value === 'string') olDescription = j.description.value
            }
          }
        }
      } catch (e) {
        logger.debug('Failed to fetch OpenLibrary JSON for description:', e)
      }

      selectedBook.value = {
        asin: result.asin || asin || '',
        title: result.title || 'Unknown Title',
        subtitle: undefined,
        authors: result.artist ? [result.artist] : [],
        narrators: result.narrator ? [result.narrator] : [],
        publisher: result.publisher,
        publishYear: publishYear,
        description: result.description || olDescription,
        imageUrl: result.imageUrl,
        runtime: result.runtime,
        language: result.language,
        genres: [],
        series: result.series,
        seriesNumber: result.seriesNumber,
        abridged: false,
        isbn: undefined,
        source: book.metadataSource || result.source
        ,openLibraryId: result.id || undefined
      }

      showDetailsModal.value = true
      return
    }

    // If we don't have an enriched result but an ASIN exists, fetch metadata from configured sources
    if (asin) {
      const response = await apiService.getMetadata(asin, 'us', true)
      const audimetaData = response.metadata
      book.metadataSource = response.source

      let publishYear: string | undefined
      if (audimetaData.publishDate || audimetaData.releaseDate) {
        const dateStr = audimetaData.publishDate || audimetaData.releaseDate
        const yearMatch = dateStr?.match(/\d{4}/)
        publishYear = yearMatch ? yearMatch[0] : undefined
      }

      selectedBook.value = {
        asin: audimetaData.asin || asin || '',
        title: audimetaData.title || 'Unknown Title',
        subtitle: audimetaData.subtitle,
        authors: audimetaData.authors?.map((a: AudimetaAuthor) => a.name).filter((n: string | undefined) => n) as string[] || [],
        narrators: audimetaData.narrators?.map((n: AudimetaNarrator) => n.name).filter((n: string | undefined) => n) as string[] || [],
        publisher: audimetaData.publisher,
        publishYear: publishYear,
        description: audimetaData.description,
        imageUrl: audimetaData.imageUrl,
        // Audimeta returns length in minutes; keep runtime in minutes for UI helpers
        runtime: audimetaData.lengthMinutes ? audimetaData.lengthMinutes : undefined,
        language: audimetaData.language,
        genres: audimetaData.genres?.map((g: AudimetaGenre) => g.name).filter((n: string | undefined) => n) as string[] || [],
        series: audimetaData.series?.length ? audimetaData.series.map(s => `${s.name}${s.position ? ` #${s.position}` : ''}`).join(', ') : undefined,
        seriesList: audimetaData.series?.map(s => `${s.name}${s.position ? ` #${s.position}` : ''}`) || [],
        seriesNumber: undefined, // Series info now included in series field
        abridged: audimetaData.bookFormat?.toLowerCase().includes('abridged') || false,
        isbn: audimetaData.isbn,
        source: response.source
        ,openLibraryId: book.searchResult?.id || undefined
      }

      showDetailsModal.value = true
      return
    }

    // If neither enriched metadata nor ASIN is available, show an informative message
    logger.error('No ASIN or enriched metadata available for selected book')
    toast.warning('No details', 'No ASIN or metadata available to show details for this book')
  } catch (error) {
    logger.error('Failed to fetch detailed metadata:', error)
    toast.error('Fetch failed', 'Failed to fetch audiobook details. Please try again.')
  }
}

// Common methods for both search types
const addToLibrary = async (book: AudibleBookMetadata) => {
  // Check if root folder is configured
  if (!configStore.applicationSettings?.outputPath) {
    toast.warning('Root folder not configured', 'Please configure the root folder in Settings before adding audiobooks.')
    router.push('/settings')
    return
  }

  // Show the add to library modal instead of directly adding
  selectedBookForLibrary.value = book
  showAddLibraryModal.value = true
}

const viewDetails = (book: AudibleBookMetadata) => {
  selectedBook.value = book
  showDetailsModal.value = true
}

const closeDetailsModal = () => {
  showDetailsModal.value = false
}

const handleAddToLibrary = (book: AudibleBookMetadata) => {
  addToLibrary(book)
}

const closeAddLibraryModal = () => {
  showAddLibraryModal.value = false
}

const handleLibraryAdded = (audiobook: Audiobook) => {
  // Mark as added in the UI
  if (audiobook.asin) {
    logger.debug('Marking ASIN as added:', audiobook.asin)
    addedAsins.value.add(audiobook.asin)
  }
  // Mark OpenLibrary ID as added when present
  if (audiobook.openLibraryId) {
    logger.debug('Marking OpenLibrary ID as added:', audiobook.openLibraryId)
    addedOpenLibraryIds.value.add(audiobook.openLibraryId)
  }
  
  // Reset search if needed
  if (searchType.value === 'asin') {
    searchQuery.value = ''
    audibleResult.value = null
  }
}

const handleSimpleSearchResults = async (results: SearchResult[]) => {
  // Convert search results to title results format
  titleResults.value = []
  audibleResult.value = null
  searchType.value = 'title'
  
  for (const result of results) {
    // Normalize common metadata keys from backend variations so the template
    // consistently finds `subtitle`/`subtitles`, `narrator` and `source`.
    try {
      const r = result as any
      // subtitles may be provided as `subtitle`, `Subtitle`, `Subtitles` or `subtitles`
      r.subtitles = r.subtitles || r.subtitle || r.Subtitle || r.Subtitles || undefined
      r.subtitle = r.subtitle || r.subtitles || r.Subtitle || r.Subtitles || undefined

      // narrators may be provided as array or single string
      if (!r.narrator) {
        if (Array.isArray(r.narrators) && r.narrators.length) {
          r.narrator = r.narrators.map((n: any) => n?.name || n?.Name || n).filter(Boolean).join(', ')
        } else if (r.Narrators && Array.isArray(r.Narrators) && r.Narrators.length) {
          r.narrator = r.Narrators.map((n: any) => n?.name || n?.Name || n).filter(Boolean).join(', ')
        } else if (r.Narrator) {
          r.narrator = r.Narrator
        }
      }

      // If backend indicates audimeta as metadataSource, present the user-facing
      // source label as 'Audible' to match expectations
      if (r.metadataSource && String(r.metadataSource).toLowerCase().includes('audimeta')) {
        r.source = 'Audible'
      }
    } catch (e) {
      // swallow normalization errors
      console.debug('Normalization failed for simple result', e)
    }
    
    // Extract year from publishedDate if it's a Date object, otherwise parse string
    let publishYear: number | undefined
    const dateStr = result.publishedDate
    if (dateStr) {
      if (typeof dateStr === 'object') {
        publishYear = (dateStr as Date).getFullYear()
      } else if (typeof dateStr === 'string') {
        const year = parseInt(dateStr.substring(0, 4))
        if (!isNaN(year)) publishYear = year
      }
    }
    
    const authorsFromResult = ((): string[] => {
      // Prefer normalized author field (flattened by earlier conversion)
      if ((result as any).author && typeof (result as any).author === 'string' && (result as any).author.trim().length) return [(result as any).author.trim()]

      // Check for Artist field (capital A, from SearchResult.Artist)
      if ((result as any).Artist && typeof (result as any).Artist === 'string' && (result as any).Artist.trim().length) {
        console.log('Found Artist field:', (result as any).Artist)
        return [(result as any).Artist.trim()]
      }

      // Check for artist field (used in advanced search results)
      if (result.artist && typeof result.artist === 'string' && result.artist.trim().length) {
        console.log('Found artist field:', result.artist)
        return [result.artist.trim()]
      }

      // If result contains an authors array (from Audimeta), extract names
      const maybeAuthors = (result as any).authors || (result as any).Authors
      if (Array.isArray(maybeAuthors) && maybeAuthors.length) {
        console.log('Found authors array:', maybeAuthors)
        return maybeAuthors.map((a: any) => (a?.name || a?.Name || '')).filter((n: any) => !!n)
      }

      // If the original searchResult contains authors, use those
      const sr = (result as any).searchResult
      const srAuthors = sr ? (sr.authors || sr.Authors) : null
      if (Array.isArray(srAuthors) && srAuthors.length) {
        console.log('Found searchResult authors:', srAuthors)
        return srAuthors.map((a: any) => (a?.name || a?.Name || '')).filter((n: any) => !!n)
      }

      console.log('No authors found in result:', result)
      return []
    })()

    // If the result looks like an Audimeta-enriched audiobook (or explicitly marked),
    // prefer to populate the richer audiobook-shaped fields so the Add New UI
    // can surface subtitles, narrators, runtime, publish date, etc.
    const looksLikeAudimeta = (result.metadataSource && String(result.metadataSource).toLowerCase() === 'audimeta') || Boolean(result.isEnriched) || Boolean(result.asin)

    const titleResult: TitleSearchResult = {
      title: result.title || '',
      author_name: authorsFromResult.length ? authorsFromResult : [(result as any).author || (result as any).Artist || result.artist || ''],
      first_publish_year: publishYear,
      cover_i: undefined,
      key: String(result.asin || result.id || ''),
      searchResult: result,
      imageUrl: result.imageUrl,
      // prefer explicit metadataSource, but mark audimeta when detected so UI shows Audimeta-specific badges
      metadataSource: looksLikeAudimeta ? 'audimeta' : (result.metadataSource || (result.searchResult && (result.searchResult as any).metadataSource)),
      // forward publisher into the top-level TitleSearchResult so template's publisher check works
      publisher: Array.isArray(result.publisher) ? result.publisher : (result.publisher ? [result.publisher] : undefined)
    }

    if (looksLikeAudimeta) {
      // Populate commonly used Audimeta-like fields (flattened to top-level)
      ;(titleResult as any).subtitle = (result as any).subtitles || (result as any).Subtitles || (result as any).subtitle || (result as any).Subtitle || undefined
      ;(titleResult as any).narrator = ((result as any).narrators || (result as any).Narrators || []).map((n: any) => n?.name || n?.Name).filter(Boolean).join(', ') || (result as any).narrator || (result as any).Narrator || undefined
      ;(titleResult as any).runtime = (() => {
        // Normalize runtime to minutes. Backend may return minutes or seconds
        const raw = (result as any).runtimeLengthMin ?? (result as any).lengthMinutes ?? (result as any).runtimeMinutes ?? (result as any).RuntimeLengthMin ?? (result as any).runtime ?? (result as any).Runtime ?? (result as any).RuntimeMinutes ?? (result as any).RuntimeSeconds
        if (!raw && raw !== 0) return undefined
        const num = Number(raw)
        if (isNaN(num)) return undefined
        // Heuristic: values > 1000 are likely seconds, convert to minutes
        if (num > 1000) return Math.round(num / 60)
        return num
      })()
      ;(titleResult as any).publishedDate = (result as any).releaseDate || (result as any).ReleaseDate || (result as any).publishedDate || (result as any).PublishedDate || undefined
      ;(titleResult as any).description = (result as any).description || (result as any).Description || undefined
      ;(titleResult as any).asin = (result as any).asin || (result as any).Asin || undefined
      ;(titleResult as any).id = (result as any).asin || (result as any).sku || (result as any).id || (result as any).title
      ;(titleResult as any).productUrl = (result as any).productUrl || (result as any).link || (result as any).Link || undefined
      ;(titleResult as any).series = (result as any).series
      // preserve seriesList for tooltip display when provided as an array
      try {
        const rawSeries = (result as any).series
        if (Array.isArray(rawSeries) && rawSeries.length) {
          const list = rawSeries.map((s: any) => {
            if (typeof s === 'object' && s) {
              const name = s.name || s.Name || String(s)
              const position = s.position || s.Position
              return position ? `${name} #${position}` : name
            }
            return String(s)
          }).filter(Boolean)
          ;(titleResult as any).seriesList = list
          ;(titleResult as any).searchResult = (titleResult as any).searchResult || result
          ;(titleResult as any).searchResult.seriesList = list
          // and choose first element as visible series string
          ;(titleResult as any).series = list[0]
        }
      } catch {}
      ;(titleResult as any).seriesNumber = (result as any).seriesNumber || (result as any).seriesPosition || undefined
      // ensure image URL is available
      if (!(titleResult as any).imageUrl && (result as any).imageUrl) (titleResult as any).imageUrl = (result as any).imageUrl
    }
    titleResults.value.push(titleResult)
  }
  
  totalTitleResultsCount.value = results.length
  searchStatus.value = ''
}

// No external result listener required; performSearch returns results directly.

const retrySearch = async () => {
  errorMessage.value = ''
  searchStatus.value = ''
  const results = await performSearch()
  if (results) {
    await handleSimpleSearchResults(results)
  }
}

// Formatting helpers
// const formatDate = (dateString: string): string => {
//   try {
//     const date = new Date(dateString)
//     return date.toLocaleDateString()
//   } catch {
//     return dateString
//   }
// }

const formatRuntime = (minutes: number): string => {
  if (!minutes) return 'Unknown'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

const capitalizeLanguage = (language: string | undefined): string => {
  if (!language) return ''
  return language.charAt(0).toUpperCase() + language.slice(1).toLowerCase()
}

// Search by ISBN: prefer ISBN->ASIN lookup (strip dashes) and fetch metadata directly.
// Fall back to title-based search only if ASIN resolution fails.
const searchByISBNChain = async (isbn: string) => {
  // Strip ISBN: prefix if present
  const cleanIsbn = isbn.replace(/^ISBN:/i, '').trim()
  
  if (!isbnService.validateISBN(cleanIsbn)) {
    searchError.value = 'Invalid ISBN format. Please enter a valid ISBN-10 or ISBN-13'
    return
  }

  isSearching.value = true
  searchError.value = ''
  audibleResult.value = null
  titleResults.value = []
  isbnResult.value = null
  isbnLookupMessage.value = ''
  isbnLookupWarning.value = false
  errorMessage.value = ''

  // Normalize ISBN (remove dashes/spaces)
  const cleanedIsbn = cleanIsbn.replace(/[-\s]/g, '')

  try {
    // Pass the ISBN digits directly to the backend. The backend will:
    // 1. Search Amazon Books (stripbooks) with the p_66 ISBN filter
    // 2. Extract ASINs from the results
    // 3. Enrich those ASINs with metadata from configured sources
    // 4. Return scored and filtered results
    searchStatus.value = `Searching Amazon for ISBN ${cleanedIsbn}`
    
    // Use title search with the plain ISBN - backend will detect it and use stripbooks
    await searchByTitle(cleanedIsbn)
    
    searchType.value = 'title'
    searchStatus.value = 'ISBN search completed'

    if (titleResults.value.length === 0) {
      isbnLookupWarning.value = true
      isbnLookupMessage.value = 'No audiobooks found for this ISBN. The book may not be available as an audiobook.'
    }
  } catch (error) {
    logger.error('ISBN search failed', error)
    isbnLookupWarning.value = true
    isbnLookupMessage.value = 'ISBN search failed'
  } finally {
    isSearching.value = false
    setTimeout(() => { searchStatus.value = '' }, 1000)
  }
}

// Load application settings and API configurations on mount
onMounted(async () => {
  await configStore.loadApplicationSettings()
  await configStore.loadApiConfigurations()
  
  // Audible integration removed: no auth status to check
  
  // Initialize added status on mount
  await checkExistingInLibrary()
  
  // Subscribe to server-side search progress updates (ignore automatic background searches by default)
  type ProgressPayload = {
    message: string
    asin?: string | null
    type?: string
    audiobookId?: number
    details?: { rawCount?: number; scoredCount?: number; [key: string]: unknown }
  }

  const unsub = signalRService.onSearchProgress((payload: ProgressPayload) => {
    if (!payload || !payload.message) return

    // Prefer structured details when available, but do not use an on-screen progress bar
    const details = payload.details
    if (details) {
      if (typeof details.rawCount === 'number') {
        searchStatus.value = `Found ${details.rawCount} raw results`
        return
      }
      if (typeof details.scoredCount === 'number') {
        searchStatus.value = `Scored ${details.scoredCount} results`
        return
      }
    }

    // Scraping fallback progress (message contains count)
    if (/scrap/i.test(payload.message) && /\d+/.test(payload.message)) {
      const m = payload.message.match(/(scrap(?:ing)?(?: product pages)? for )?(\d+)/i)
      if (m && m[2]) {
        searchStatus.value = `Scraping product pages for ${m[2]} ASINs...`
        return
      }
    }

    // If an ASIN is provided, show ASIN-level progress
    if (payload.asin) {
      searchStatus.value = `Processing ASIN ${payload.asin}...`
      return
    }

    // Fallback to raw message — but sanitize URLs to avoid showing long Amazon stripbooks links
    const sanitizeMessage = (m: string) => {
      if (!m) return m
      try {
        // Extract first URL if present
        const urlMatch = m.match(/https?:\/\/[^\s]+/i)
        if (urlMatch && urlMatch[0]) {
          const urlStr = urlMatch[0]
          try {
            const u = new URL(urlStr)
            const rh = u.searchParams.get('rh')
            if (rh) {
              const isbnMatch = rh.match(/p_66[:%3A]*(\d{10,13})/)
              if (isbnMatch && isbnMatch[1]) {
                return m.replace(urlStr, `ISBN ${isbnMatch[1]}`)
              }
            }
            // Replace URL with short host hint
            return m.replace(urlStr, `${u.hostname.replace(/^www\./, '')} search`)
          } catch {
            // If URL parsing fails, fall back to removing query portion
            return m.replace(urlStr, 'external search')
          }
        }
      } catch {
        // ignore and return original
      }
      return m
    }

    searchStatus.value = sanitizeMessage(payload.message)
  })
  // When component is unmounted, unsubscribe
  onUnmounted(() => {
    try { unsub() } catch {}
  })

  // Watch for library changes to update added status
  const stopWatchingLibrary = watch(
    () => libraryStore.audiobooks,
    async (newAudiobooks, oldAudiobooks) => {
      // Only update if the library actually changed (not just on initial load)
      if (oldAudiobooks && oldAudiobooks.length !== newAudiobooks.length) {
        logger.debug('Library changed, updating added status...')
        await checkExistingInLibrary()
      }
    },
    { deep: false } // We don't need deep watching since we're just checking length
  )

  // Cleanup watcher on unmount
  onUnmounted(() => {
    stopWatchingLibrary()
  })
})
</script>

<style scoped>
.add-new-view {
  padding: 2em;
}

.page-header h1 {
  margin: 0 0 2rem 0;
  color: white;
  font-size: 2rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.settings-link {
  color: #2196f3;
  text-decoration: none;
  font-weight: 500;
}

.settings-link:hover {
  text-decoration: underline;
}

/* Search Tabs */
.search-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 1px solid #444;
}

.tab-btn {
  padding: 1rem 1.5rem;
  background: transparent;
  border: none;
  color: #ccc;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  transition: all 0.2s;
}

.tab-btn:hover {
  color: white;
  background-color: rgba(255, 255, 255, 0.05);
}

.tab-btn.active {
  color: #007acc;
  border-bottom-color: #007acc;
}

/* Search Section */
.search-section {
  margin-bottom: 2.5rem;
  background: linear-gradient(135deg, rgba(42, 42, 42, 0.95) 0%, rgba(35, 35, 35, 0.95) 100%);
  padding: 2rem;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(10px);
}

.search-method {
  margin-bottom: 1.5rem;
}

.search-method-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  color: white;
  font-weight: 700;
  font-size: 1.375rem;
  margin-bottom: 0.75rem;
  letter-spacing: -0.025em;
}

.search-method-label svg {
  color: #4dabf7;
  width: 24px;
  height: 24px;
}

.search-help {
  color: #adb5bd;
  font-size: 0.95rem;
  margin: 0;
  line-height: 1.6;
  max-width: 600px;
}

.search-help .settings-link {
  color: #4dabf7;
  text-decoration: none;
  font-weight: 600;
  transition: all 0.2s ease;
  border-bottom: 1px solid transparent;
}

.search-help .settings-link:hover {
  color: #74c0fc;
  text-decoration: none;
  border-bottom-color: #74c0fc;
}

.search-help .settings-link {
  color: #4dabf7;
  text-decoration: none;
  font-weight: 500;
  transition: color 0.2s ease;
}

.search-help .settings-link:hover {
  color: #74c0fc;
  text-decoration: underline;
}

/* Unified Search */
.unified-search-bar {
  gap: 1rem;
  margin-bottom: 1.25rem;
  align-items: stretch;
  position: relative;
}

.unified-search-form {
  display: flex;
  gap: 1rem;
  align-items: center;
  width: 100%;
  flex-wrap: nowrap; /* keep actions on one row until small screens */
}

.unified-search-bar .search-input {
  flex: 1 1 420px;
  min-width: 220px;
  padding: 0.7rem 1rem;
  height: 48px;
  border: 2px solid rgba(255, 255, 255, 0.12);
  border-radius: 6px;
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.3) 0%, rgba(0, 0, 0, 0.2) 100%);
  color: white;
  font-size: 0.98rem;
  font-family: inherit;
  transition: all 0.22s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.unified-search-bar .search-input:focus {
  outline: none;
  border-color: #4dabf7;
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.4) 0%, rgba(0, 0, 0, 0.3) 100%);
  box-shadow: 0 0 0 4px rgba(77, 171, 247, 0.15), 0 4px 16px rgba(0, 0, 0, 0.2);
  transform: translateY(-1px);
}

.unified-search-bar .search-input::placeholder {
  color: #9ca3af;
  font-weight: 400;
}

.unified-search-bar .language-select {
  padding: 6px 8px;
  border: 1px solid #444;
  border-radius: 6px;
  background-color: #1a1a1a !important;
  color: #ffffff !important;
  font-size: 0.95rem;
  font-family: inherit;
  cursor: pointer;
  transition: all 0.18s ease;
  min-width: 140px;
  box-shadow: none;
  padding-right: 2.25rem;
  height: 48px;
  display: inline-flex;
  align-items: center;
  -webkit-appearance: none !important;
  -moz-appearance: none !important;
  appearance: none !important;
  background-clip: padding-box;
  background-image: url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3e%3cpolyline points='6,9 12,15 18,9'%3e%3c/polyline%3e%3c/svg%3e") !important;
  background-repeat: no-repeat !important;
  background-position: right 0.75rem center !important;
  background-size: 1rem !important;
}

.unified-search-bar .language-select:focus-visible {
  outline: none;
  border-color: #2196f3;
  box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.2);
}

.unified-search-bar .language-select:hover {
  border-color: #555;
}

.unified-search-bar .language-select:focus {
  outline: none;
  border-color: #2196f3;
  box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.2);
}

.unified-search-bar .language-select option {
  background-color: #1a1a1a !important;
  color: white !important;
  padding: 0.5rem !important;
}

.search-hint {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  color: #9ca3af;
  font-size: 0.9rem;
  padding: 1rem 1.25rem;
  background: linear-gradient(135deg, rgba(255, 255, 255, 0.04) 0%, rgba(255, 255, 255, 0.02) 100%);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  margin-bottom: 0;
  backdrop-filter: blur(8px);
}

.search-hint svg {
  color: #4dabf7;
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  margin-top: 0.125rem;
}

/* Advanced Search Inline Section */
.advanced-search-section {
  animation: slideDown 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
}

.simple-search-button {
  position: absolute;
  top: 0;
  right: 0;
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, rgba(255, 255, 255, 0.15) 0%, rgba(255, 255, 255, 0.1) 100%);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.simple-search-button:hover {
  background: linear-gradient(135deg, rgba(255, 255, 255, 0.2) 0%, rgba(255, 255, 255, 0.15) 100%);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateY(-1px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-20px) scale(0.95);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.advanced-search-header {
  margin-bottom: 2rem;
  padding-right: 10rem; /* Make room for the Simple Search button */
}

.advanced-search-header h3 {
  margin: 0 0 0.75rem 0;
  color: white;
  font-size: 1.25rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  letter-spacing: -0.025em;
}

.advanced-search-header h3 svg {
  color: #9b59b6;
  width: 24px;
  height: 24px;
}

.advanced-search-header .help-text {
  color: #adb5bd;
  font-size: 0.95rem;
  margin: 0;
  line-height: 1.6;
}

.advanced-search-form {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.advanced-search-buttons {
  display: flex;
  gap: 1.5rem;
}

.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.25rem;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-group label {
  color: white;
  font-weight: 600;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.form-group label::before {
  content: '';
  width: 4px;
  height: 4px;
  background: #9b59b6;
  border-radius: 50%;
  flex-shrink: 0;
}

.form-input {
  padding: 1rem 1.25rem;
  border: 2px solid rgba(255, 255, 255, 0.12);
  border-radius: 6px;
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.3) 0%, rgba(0, 0, 0, 0.2) 100%);
  color: white;
  font-size: 1rem;
  font-family: inherit;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.form-input:focus {
  outline: none;
  border-color: #9b59b6;
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.44) 0%, rgba(0, 0, 0, 0.33) 100%);
  box-shadow: 0 0 0 5px rgba(155, 89, 182, 0.18), 0 6px 20px rgba(0, 0, 0, 0.22);
  transform: translateY(-1px);
}

.form-input:focus-visible {
  outline: none;
  border-color: #9b59b6;
  box-shadow: 0 0 0 6px rgba(155, 89, 182, 0.22), 0 6px 20px rgba(0, 0, 0, 0.22);
}

.search-input:focus-visible {
  outline: none;
  border-color: #4dabf7;
  box-shadow: 0 0 0 6px rgba(77, 171, 247, 0.14), 0 6px 20px rgba(0, 0, 0, 0.16);
}

.form-input::placeholder {
  color: #b6bcc4;
  font-weight: 400;
}

.form-input option {
  background-color: #1a1a1a !important;
  color: white !important;
  padding: 0.5rem !important;
}

/* Select elements in form-input class should match SettingsView */
select.form-input {
  background-color: #1a1a1a !important;
  border: 1px solid #444 !important;
  border-radius: 6px !important;
  -webkit-appearance: none !important;
  -moz-appearance: none !important;
  appearance: none !important;
  background-image: url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3e%3cpolyline points='6,9 12,15 18,9'%3e%3c/polyline%3e%3c/svg%3e") !important;
  background-repeat: no-repeat !important;
  background-position: right 0.75rem center !important;
  background-size: 1rem !important;
  padding-right: 2.5rem !important;
  cursor: pointer;
}

select.form-input:focus {
  outline: none;
  border-color: #2196f3 !important;
  box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.2) !important;
}

.advanced-search-actions {
  display: flex;
  justify-content: end;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-secondary,
.btn-primary {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.95rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.2s ease;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.15);
}

.btn-primary {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.btn-primary:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
}

.btn-primary:disabled,
.btn-secondary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Responsive Design */
@media (max-width: 768px) {
  .search-section {
    padding: 1.5rem;
    margin-bottom: 2rem;
  }

  .search-method-label {
    font-size: 1.25rem;
  }

  .unified-search-form {
    flex-direction: column;
    gap: 0.75rem;
    align-items: stretch;
  }

  .unified-search-bar .language-select {
    min-width: auto;
    width: 100%;
  }

  .search-btn {
    width: 100%;
    min-width: auto;
  }

  .search-btn.advanced-btn {
    width: 100%;
  }

  .advanced-search-section {
    padding: 1.5rem;
    margin-top: 1.5rem;
  }

  .simple-search-button {
    position: static;
    margin-bottom: 1rem;
    align-self: flex-end;
  }

  .advanced-search-header {
    padding-right: 0;
  }

  .form-row {
    grid-template-columns: 1fr;
    gap: 1rem;
  }

  .advanced-search-actions {
    flex-direction: column;
    gap: 1rem;
    align-items: stretch;
  }

  .advanced-search-controls {
    justify-content: center;
  }

  .advanced-search-buttons {
    justify-content: center;
  }
}

@media (max-width: 480px) {
  .search-section {
    padding: 1rem;
  }

  .search-method-label {
    font-size: 1.125rem;
  }

  .search-help {
    font-size: 0.875rem;
  }

  .search-btn {
    padding: 0.875rem 1.5rem;
    font-size: 0.95rem;
  }
}

.search-input {
  height: 48px;
  flex: 1;
  padding: 0 0.9rem;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  background-color: rgba(0, 0, 0, 0.18);
  color: white;
  font-size: 1rem;
  text-transform: none;
  font-family: inherit;
  transition: all 0.2s ease;
}

.search-input.error {
  border-color: #fa5252;
  background-color: rgba(250, 82, 82, 0.05);
  box-shadow: 0 0 0 3px rgba(250, 82, 82, 0.1);
}

.search-input:focus {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
}

.search-input::placeholder {
  color: #6c757d;
  text-transform: none;
}

/* Title Search Form */
.title-search-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-row {
  display: flex;
  gap: 1rem;
}

.form-group {
  flex: 1;
}

.form-group label {
  display: block;
  color: white;
  font-weight: 500;
  margin-bottom: 0.5rem;
}

.form-input {
  height: 48px;
  width: 100%;
  padding: 6px 8px;
  border: 1px solid rgba(255,255,255,0.08);
  border-radius: 6px;
  background-color: #2a2a2a;
  color: white;
  font-size: 1rem;
}

.form-input:focus {
  outline: none;
  border-color: #4dabf7;
  box-shadow: 0 0 0 3px rgba(77,171,247,0.08);
}

/* Buttons */
.search-btn {
  padding: 0.9rem 1.6rem;
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  font-weight: 700;
  font-size: 1rem;
  min-width: 120px;
  height: 48px;
  transition: all 0.22s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 6px 24px rgba(30, 136, 229, 0.32);
  position: relative;
  overflow: hidden;
}

.search-btn:focus-visible {
  outline: none;
  box-shadow: 0 0 0 6px rgba(77,171,247,0.16), 0 6px 24px rgba(30,136,229,0.28);
}

.search-btn.advanced-btn {
  background: linear-gradient(135deg, rgba(155,89,182,0.14) 0%, rgba(142,68,173,0.12) 100%);
  color: #f4ecff;
  box-shadow: 0 2px 8px rgba(155, 89, 182, 0.12);
  min-width: 110px;
  height: 44px;
  position: absolute;
  top: 0;
  right: 0;
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.search-btn.advanced-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, rgba(155,89,182,0.18) 0%, rgba(142,68,173,0.14) 100%);
  box-shadow: 0 4px 12px rgba(155, 89, 182, 0.16);
} 

.search-btn::before {
  content: '';
  position: absolute;
  top: 0;
  left: -100%;
  width: 100%;
  height: 100%;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.2), transparent);
  transition: left 0.5s;
}

.search-btn:hover:not(:disabled)::before {
  left: 100%;
}

.search-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 6px 24px rgba(30, 136, 229, 0.4);
  transform: translateY(-2px);
}

.search-btn:active:not(:disabled) {
  transform: translateY(0);
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.search-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.2);
}

.search-btn.advanced-btn {
  background: linear-gradient(135deg, #9b59b6 0%, #8e44ad 100%);
  box-shadow: 0 4px 16px rgba(155, 89, 182, 0.3);
}

.search-btn.advanced-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #8e44ad 0%, #7d3c98 100%);
  box-shadow: 0 6px 24px rgba(155, 89, 182, 0.4);
}

.search-btn.audible-btn {
  background: linear-gradient(135deg, #ff9900 0%, #ff7700 100%);
  box-shadow: 0 2px 8px rgba(255, 153, 0, 0.3);
}

.search-btn.audible-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #ff7700 0%, #ff5500 100%);
  box-shadow: 0 4px 12px rgba(255, 153, 0, 0.4);
}

.search-btn.audible-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.search-btn.audible-catalog-btn {
  background: linear-gradient(135deg, #ff6b35 0%, #f7931e 100%);
  box-shadow: 0 2px 8px rgba(255, 107, 53, 0.3);
}

.search-btn.audible-catalog-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #f7931e 0%, #ff6b35 100%);
  box-shadow: 0 4px 12px rgba(255, 107, 53, 0.4);
}

.search-btn.audible-catalog-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.search-btn svg {
  width: 18px;
  height: 18px;
}

.search-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn {
  padding: 0.65rem 1.25rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  display: flex;
  align-items: center;
  min-width: 100px;
  justify-content: center;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn:has(svg) {
  gap: 0.5rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: linear-gradient(135deg, #1e88e5 0%, #1565c0 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(30, 136, 229, 0.3);
}

.btn-primary:hover:not(:disabled) {
  background: linear-gradient(135deg, #1976d2 0%, #0d47a1 100%);
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.4);
  transform: translateY(-1px);
}

.btn-primary:active:not(:disabled) {
  transform: translateY(0);
}

.btn-success {
  background: linear-gradient(135deg, #2ecc71 0%, #27ae60 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(46, 204, 113, 0.3);
}

.btn-success:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.btn-secondary {
  background-color: rgba(255, 255, 255, 0.08);
  color: #adb5bd;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.btn-secondary:hover:not(:disabled) {
  background-color: rgba(255, 255, 255, 0.12);
  color: white;
  border-color: rgba(255, 255, 255, 0.15);
  transform: translateY(-1px);
}

.btn-secondary:active:not(:disabled) {
  transform: translateY(0);
}

/* Error Messages */
/* Error Messages */
.error-message {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  color: #fff;
  background-color: rgba(250, 82, 82, 0.15);
  border: 1px solid rgba(250, 82, 82, 0.3);
  border-radius: 6px;
  padding: 0.875rem 1.125rem;
  font-size: 0.9rem;
  margin-top: 1rem;
}

.error-message svg {
  color: #fa5252;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

/* Loading Results */
.loading-results {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 4rem 2rem;
  min-height: 300px;
  background-color: #2a2a2a;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.loading-spinner {
  text-align: center;
  color: #adb5bd;
}

.loading-spinner svg {
  font-size: 3rem;
  color: #4dabf7;
  margin-bottom: 1rem;
  width: 48px;
  height: 48px;
}

.loading-spinner i {
  font-size: 3rem;
  color: #4dabf7;
  margin-bottom: 1rem;
}

.loading-spinner p {
  font-size: 1.1rem;
  margin: 0;
  font-weight: 500;
}

.search-status {
  font-size: 0.875rem;
  color: #6c757d;
  margin: 0.75rem 0 0 0;
  font-style: italic;
}

/* Results */
.search-results h2 {
  color: white;
  margin-bottom: 1.5rem;
  font-size: 1.5rem;
  font-weight: 600;
}

/* ASIN Result Card */
.result-card {
  display: flex;
  background-color: #2a2a2a;
  border-radius: 6px;
  overflow: hidden;
  padding: 1.25rem;
  gap: 1.25rem;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.result-card:hover {
  background-color: #2f2f2f;
  border-color: rgba(33, 150, 243, 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.result-poster {
  width: 140px;
  height: 140px;
  flex-shrink: 0;
  background-color: #555;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.title-result-card:hover .result-poster {
  transform: scale(1.02);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
}

.result-poster img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.placeholder-cover {
  width: 112px;
  height: 168px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, rgba(0,0,0,0.06), rgba(0,0,0,0.02));
  border-radius: 6px;
  color: #9ca3af;
  font-size: 1.6rem;
}

.placeholder-cover-image {
  width: 112px;
  height: 168px;
  object-fit: cover;
  border-radius: 6px;
}

.result-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}

.result-info h3 {
  margin: 0;
  color: white;
  font-size: 1.4rem;
  line-height: 1.3;
  font-weight: 600;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-author {
  color: #4dabf7;
  margin: 0;
  font-weight: 500;
  font-size: 0.95rem;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-narrator {
  color: #adb5bd;
  margin: 0;
  font-style: italic;
  font-size: 0.9rem;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.result-stats {
  display: flex;
  gap: 0.75rem;
  margin: 0.25rem 0;
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  color: #adb5bd;
  font-size: 0.875rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  white-space: nowrap;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.result-series {
  display: flex;
  gap: 0.5rem;
  margin: 0.5rem 0;
  flex-wrap: wrap;
}

.series-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: #adb5bd;
  font-size: 0.875rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  white-space: nowrap;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.series-badge:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.series-badge svg {
  width: 14px;
  height: 14px;
}

.metadata-badges {
  display: flex;
  gap: 0.5rem;
  margin: 0.5rem 0;
  flex-wrap: wrap;
}

.metadata-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: #adb5bd;
  font-size: 0.875rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  white-space: nowrap;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.metadata-badge:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.metadata-badge svg {
  width: 14px;
  height: 14px;
}

.stat-item:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.stat-item svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.stat-item i {
  color: #4dabf7;
}

.result-description {
  color: #ccc;
  margin: 0.5rem 0;
  line-height: 1.5;
  flex-grow: 1;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  -webkit-box-orient: vertical;
}

.result-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin: 0.75rem 0 0 0;
  color: #999;
  font-size: 0.875rem;
}

.result-meta span,
.result-meta a.source-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(33, 150, 243, 0.15) !important;
  color: #4dabf7 !important;
  font-weight: 500;
  padding: 0.35rem 0.7rem !important;
  border-radius: 6px !important;
  text-decoration: none;
  white-space: nowrap;
  transition: all 0.2s ease;
}

.result-meta span:hover,
.result-meta a.source-link:hover {
  background-color: rgba(33, 150, 243, 0.25) !important;
}

.metadata-source-badge,
.metadata-source-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(33, 150, 243, 0.15) !important;
  color: #4dabf7 !important;
  font-weight: 500;
  padding: 0.35rem 0.7rem !important;
  border-radius: 6px !important;
  text-decoration: none;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.metadata-source-link:hover {
  background-color: rgba(33, 150, 243, 0.25) !important;
  color: #74c0fc !important;
  transform: translateY(-1px);
}

.metadata-source-badge svg,
.metadata-source-link svg {
  width: 14px;
  height: 14px;
}

.result-meta a.source-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background-color: rgba(255, 255, 255, 0.05);
  color: #adb5bd;
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  text-decoration: none;
  transition: all 0.2s ease;
}

.result-meta a.source-link:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.result-meta .source-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: #adb5bd;
  font-size: 0.875rem;
  background-color: rgba(255, 255, 255, 0.05);
  padding: 0.35rem 0.7rem;
  border-radius: 6px;
  white-space: nowrap;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.result-meta .source-badge:hover {
  background-color: rgba(255, 255, 255, 0.08);
}

.result-meta .source-badge svg,
.result-meta a.source-link svg {
  width: 14px;
  height: 14px;
}

.result-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.result-actions .btn {
  flex: 1;
  min-width: 0;
}

/* Title Search Results */
.title-results {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

/* Audimeta pagination controls for advanced searches */
.audimeta-pagination {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.audimeta-pagination .page-indicator {
  color: #b6bcc4;
  font-size: 0.95rem;
}

.audimeta-pagination .btn {
  padding: 0.45rem 0.9rem;
  border-radius: 6px;
  border: 1px solid rgba(255,255,255,0.06);
  background: linear-gradient(135deg, rgba(255,255,255,0.02), rgba(0,0,0,0.02));
  color: white;
}

.title-result-card {
  display: flex;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 1.25rem;
  gap: 1.25rem;
  align-items: flex-start;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.title-result-card:hover {
  border-color: rgba(33, 150, 243, 0.3);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
  transform: translateY(-2px);
}

.title-result-card .result-info {
  flex: 1;
}

.title-result-card .result-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 150px;
  align-self: flex-start;
}

.title-result-card .result-actions .btn {
  padding: 0.6rem 1rem;
  font-weight: 500;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.title-result-card .result-actions .btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.title-result-card h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.25rem;
  font-weight: 600;
  color: #ffffff;
  line-height: 1.3;
}

.result-author {
  color: #bfc7cc;
  margin: 0 0 0.75rem 0;
  font-size: 1rem;
  font-weight: 500;
}

/* Empty States */
.getting-started, .empty-state, .error-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #ccc;
}

.welcome-icon, .empty-icon, .error-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  color: #555;
}

.error-icon {
  color: #e74c3c;
}

.getting-started h2, .empty-state h2, .error-state h2 {
  color: white;
  margin-bottom: 1rem;
}

.help-section {
  margin: 2rem 0;
  text-align: left;
  max-width: 500px;
  margin-left: auto;
  margin-right: auto;
}

.help-section h3 {
  color: white;
  margin-bottom: 1rem;
}

.help-section ul {
  color: #ccc;
  line-height: 1.6;
}

.help-section li {
  margin-bottom: 0.5rem;
}

.quick-actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-top: 2rem;
}

/* Inline status */
.inline-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background-color: rgba(0, 122, 204, 0.1);
  border: 1px solid #007acc;
  border-radius: 6px;
  color: #007acc;
  margin-bottom: 1rem;
}

.inline-status.warning {
  background-color: rgba(241, 196, 15, 0.1);
  border-color: #f1c40f;
  color: #f1c40f;
}

/* Responsive design */
@media (max-width: 768px) {
  .search-tabs {
    flex-direction: column;
  }
  
  .tab-btn {
    justify-content: center;
  }
  
  .form-row {
    flex-direction: column;
  }
  
  .search-bar {
    flex-direction: column;
  }
  
  .result-card, .title-result-card {
    flex-direction: column;
    text-align: center;
  }
  
  .result-poster {
    width: 100px;
    height: 100px;
    margin: 0 auto;
  }

  .result-info {
    width: 100%;
  }

  .result-stats, .result-meta {
    margin: 0 auto;
  }
  
  .result-actions, .helper-actions {
    justify-content: center;
    flex-wrap: wrap;
  }
  
  .quick-actions {
    flex-direction: column;
    align-items: center;
  }

  /* Mobile improvements: stack unified search and make CTAs full width */
  .unified-search-bar {
    flex-direction: column;
    gap: 0.5rem;
  }

  .unified-search-bar .search-input {
    width: 100%;
    font-size: 1rem;
  }

  .unified-search-bar .search-btn {
    width: 100%;
    min-width: 0;
    padding: 0.875rem;
  }

  /* Make result action buttons stack and be larger on mobile */
  .title-result-card .result-actions,
  .result-card .result-actions {
    flex-direction: column;
    gap: 0.5rem;
    width: 100%;
  }

  .title-result-card .result-actions .btn,
  .result-card .result-actions .btn {
    width: 100%;
    padding: 0.9rem 1rem;
    font-size: 1rem;
  }

  /* Reduce page padding for small devices to maximize content space */
  .add-new-view {
    padding: 1rem;
  }

  /* Ensure results area keeps a stable gutter for scrollbars */
  .search-results,
  .title-results,
  .search-section {
    scrollbar-gutter: stable both-edges;
  }

  /* Allow long metadata badges and names to wrap instead of overflowing */
  .result-meta span,
  .metadata-source-badge,
  .metadata-source-link {
    white-space: normal;
    overflow-wrap: anywhere;
  }
}

.cancelled {
  text-align: center;
  padding: 2rem;
  color: #e74c3c;
}

.cancelled svg {
  font-size: 2rem;
  display: block;
  margin-bottom: 1rem;
}

/* Pagination Controls */
.results-controls {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-top: 1.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  position: sticky;
  top: 60px;
  background-color: #1a1a1a;
  z-index: 100;
  padding-bottom: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(10px);
}

.client-pagination-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.pagination-settings {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.pagination-nav {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.page-indicator {
  color: #b6bcc4;
  font-size: 0.95rem;
  white-space: nowrap;
}

.small-label {
  color: white;
  font-weight: 500;
  font-size: 0.875rem;
  margin: 0;
}

.small-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid #444;
  border-radius: 6px;
  background-color: #1a1a1a !important;
  color: white !important;
  font-size: 0.875rem;
  cursor: pointer;
  transition: all 0.2s ease;
  min-width: 80px;
  width: auto;
  -webkit-appearance: none !important;
  -moz-appearance: none !important;
  appearance: none !important;
  background-clip: padding-box;
  background-image: url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3e%3cpolyline points='6,9 12,15 18,9'%3e%3c/polyline%3e%3c/svg%3e") !important;
  background-repeat: no-repeat !important;
  background-position: right 0.5rem center !important;
  background-size: 0.75rem !important;
  padding-right: 1.75rem !important;
}

.small-select:focus {
  outline: none;
  border-color: #2196f3;
  box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.2);
}

.small-select option {
  background-color: #1a1a1a !important;
  color: white !important;
  padding: 0.5rem !important;
}

.small-pager {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

@media (max-width: 768px) {
  .client-pagination-controls {
    flex-direction: column;
    align-items: stretch;
    gap: 1rem;
  }

  .pagination-settings {
    justify-content: center;
  }

  .pagination-nav {
    justify-content: center;
  }
}

</style>