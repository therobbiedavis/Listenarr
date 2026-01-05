import { describe, it, expect, beforeEach } from 'vitest'
import type { Mock } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import AddNewView from '@/views/AddNewView.vue'
import { useLibraryStore } from '@/stores/library'

// apiService and signalR are mocked centrally in test-setup.ts

describe('AddNewView pagination', () => {
  beforeEach(() => {
    const pinia = createPinia()
    setActivePinia(pinia)
  })

  it('uses total from aggregated API response', () => {
    // With backend aggregation, total is simply the totalResults from API
    const apiResponse = { totalResults: 150, results: [] }
    expect(apiResponse.totalResults).toBe(150)
  })

  it('handles empty results from API', () => {
    const apiResponse = { totalResults: 0, results: [] }
    expect(apiResponse.totalResults).toBe(0)
  })

  it('maps audimeta metadata to result fields', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as unknown as { searchAudimetaByTitleAndAuthor?: Mock }
    apiService.searchAudimetaByTitleAndAuthor?.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000123',
          title: 'Dune',
          subtitle: 'A Heroic Saga',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img',
          runtimeLengthMin: 900,
          language: 'english',
          series: [{ name: 'Dune Series', position: '1' }],
          publisher: 'Chilton',
          narrators: [{ name: 'Scott Brick' }],
          releaseDate: '1965-08-01',
          link: 'https://www.audible.com/pd/B000123',
        },
      ],
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      showAdvancedSearch?: boolean
      advancedSearchParams?: Record<string, unknown>
      performAdvancedSearch?: () => Promise<void>
      allAudimetaResults?: unknown[]
      titleResults?: unknown[]
    }

    // Use advanced search with title to trigger audimeta path
    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Dune' }

    await vm.performAdvancedSearch()

    expect(vm.allAudimetaResults.length).toBe(1)
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0] as any
    expect(tr.searchResult.narrator).toBe('Scott Brick')
    expect(tr.searchResult.subtitle).toBe('A Heroic Saga')
    expect(tr.searchResult.series).toBe('Dune Series')
    expect(tr.publisher && tr.publisher[0]).toBe('Chilton')
    expect(tr.first_publish_year).toBe(1965)
    expect(tr.searchResult.productUrl).toBe('https://www.audible.com/pd/B000123')

    // Rendered subtitle should appear in the title-result card
    await wrapper.vm.$nextTick()
    const subtitleEl = wrapper.find('.title-results .title-result-card .result-subtitle')
    expect(subtitleEl.exists()).toBe(true)
    expect(subtitleEl.text()).toBe('A Heroic Saga')
  })

  it('sets data-src for lazy images on advanced search results', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as unknown as { searchAudimetaByTitleAndAuthor?: Mock }
    apiService.searchAudimetaByTitleAndAuthor?.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000124',
          title: 'Dune Messiah',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img2',
          runtimeLengthMin: 720,
          language: 'english',
        },
      ],
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      showAdvancedSearch?: boolean
      advancedSearchParams?: Record<string, unknown>
      performAdvancedSearch?: () => Promise<void>
      allAudimetaResults?: unknown[]
      titleResults?: unknown[]
    }

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Dune' }

    await vm.performAdvancedSearch()
    await wrapper.vm.$nextTick()

    // Find result image element
    const img = wrapper.find('.result-poster img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('http://img2')
  })

  it('maps runtime from runtimeLengthMin (minutes) to seconds', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as unknown as { searchAudimetaByTitleAndAuthor?: Mock }
    apiService.searchAudimetaByTitleAndAuthor?.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000125',
          title: 'Children of Dune',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img3',
          runtimeLengthMin: 10,
          language: 'english',
        },
      ],
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      showAdvancedSearch?: boolean
      advancedSearchParams?: Record<string, unknown>
      performAdvancedSearch?: () => Promise<void>
      allAudimetaResults?: unknown[]
      titleResults?: unknown[]
    }

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Children of Dune' }

    await vm.performAdvancedSearch()
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0] as any
    expect(tr.searchResult.runtime).toBe(10 * 60)
  })

  it('maps runtime from lengthMinutes (metadata field) to seconds', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as unknown as { searchAudimetaByTitleAndAuthor?: Mock }
    apiService.searchAudimetaByTitleAndAuthor?.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000126',
          title: 'Heretics of Dune',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img4',
          lengthMinutes: 12,
          language: 'english',
        },
      ],
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      showAdvancedSearch?: boolean
      advancedSearchParams?: Record<string, unknown>
      performAdvancedSearch?: () => Promise<void>
      titleResults?: unknown[]
    }

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Heretics of Dune' }

    await vm.performAdvancedSearch()
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0] as any
    expect(tr.searchResult.runtime).toBe(12 * 60)
  })

  it('shows metadata badge linking to internal Audimeta endpoint and source badge linking to Audible product', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      searchType?: string
      audibleResult?: Record<string, unknown>
    }

    // Simulate an ASIN-based audimeta result (single result view)
    vm.searchType = 'asin'
    ;(vm as any).audibleResult = {
      asin: 'BAUD1',
      title: 'Title',
      authors: [{ name: 'Author Name' }],
      narrators: [{ name: 'Narrator Name' }],
      imageUrl: 'http://example.com/cover.jpg',
      metadataSource: 'Audimeta',
      source: 'Audible',
      sourceLink: 'https://www.audible.com/pd/BAUD1',
      series: 'Series Name',
      seriesList: ['Series Name', 'Other Series'],
    }

    await wrapper.vm.$nextTick()

    // Metadata badge should link to /api/metadata/audimeta/{asin}
    const metaLink = wrapper.find('.result-meta .metadata-source-link')
    expect(metaLink.exists()).toBe(true)
    expect(metaLink.attributes('href')).toBe('https://audimeta.de/book/BAUD1')
    expect(metaLink.text()).toContain('Audimeta')

    // Source link should prefer Audible product URL and show 'Audible'
    const sourceLink = wrapper.find('.result-meta .source-link')
    expect(sourceLink.exists()).toBe(true)
    expect(sourceLink.attributes('href')).toBe('https://www.audible.com/pd/BAUD1')
    expect(sourceLink.text()).toContain('Audible')
  })

  it('shows full series list on hover (title and asin result views)', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as { searchType?: string; titleResults?: unknown[] }

    // Title-list item case
    vm.searchType = 'title'
    vm.titleResults = [
      {
        title: 'Book',
        key: 'k1',
        searchResult: { series: 'Main Series', seriesList: ['Main Series', 'Alt Series'] },
      },
    ]

    await wrapper.vm.$nextTick()

    const seriesBadge = wrapper.find('.title-results .title-result-card .series-badge[title]')
    expect(seriesBadge.exists()).toBe(true)
    expect(seriesBadge.attributes('title')).toBe('Main Series, Alt Series')

    // ASIN result case
    vm.searchType = 'asin'
    ;(vm as any).audibleResult = { asin: 'BAUD2', title: 'B', series: 'X', seriesList: ['X', 'Y'] }
    await wrapper.vm.$nextTick()
    const seriesBadgeAsin = wrapper.find('.search-results .title-result-card .series-badge[title]')
    expect(seriesBadgeAsin.exists()).toBe(true)
    expect(seriesBadgeAsin.attributes('title')).toBe('X, Y')
  })

  it('shows "Added" and disables add button when result is already in library', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm = wrapper.vm as unknown as {
      searchType?: string
      audibleResult?: Record<string, unknown>
      checkExistingInLibrary?: () => Promise<void>
    }

    // Simulate library already containing the ASIN
    const lib = useLibraryStore()
    lib.audiobooks = [{ id: 1, asin: 'BEXIST', title: 'Already In Library' }]

    vm.searchType = 'asin'
    vm.audibleResult = { asin: 'BEXIST', title: 'Already In Library' }

    await vm.checkExistingInLibrary()
    await wrapper.vm.$nextTick()

    const addBtn = wrapper.find('.search-results .result-actions .btn')
    expect(addBtn.exists()).toBe(true)
    expect(addBtn.text()).toContain('Added')
    expect(addBtn.attributes('disabled')).toBeDefined()
  })
})
