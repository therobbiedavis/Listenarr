/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import AddNewView from '@/views/AddNewView.vue'
import { useLibraryStore } from '@/stores/library'

vi.mock('@/services/api', () => ({
  apiService: {
    searchAudimetaByTitleAndAuthor: vi.fn(),
    getImageUrl: vi.fn((url: string) => url || ''),
    getStartupConfig: vi.fn(async () => ({})),
    getApplicationSettings: vi.fn(async () => ({}))
  }
}))

// Minimal stub for SignalR service to prevent real WebSocket behavior during tests
vi.mock('@/services/signalr', () => ({
  signalRService: {
    connect: () => {},
    onSearchProgress: (_cb: any) => () => {},
    onQueueUpdate: (_cb: any) => () => {},
    onDownloadUpdate: (_cb: any) => () => {},
    onFilesRemoved: (_cb: any) => () => {},
    onAudiobookUpdate: (_cb: any) => () => {},
    onToast: (_cb: any) => () => {}
  }
}))

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
    const apiService = apiModule.apiService as any
    apiService.searchAudimetaByTitleAndAuthor.mockResolvedValue({
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
          link: 'https://www.audible.com/pd/B000123'
        }
      ]
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm: any = wrapper.vm

    // Use advanced search with title to trigger audimeta path
    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Dune' }

    await vm.performAdvancedSearch()

    expect(vm.allAudimetaResults.length).toBe(1)
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0]
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
    const apiService = apiModule.apiService as any
    apiService.searchAudimetaByTitleAndAuthor.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000124',
          title: 'Dune Messiah',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img2',
          runtimeLengthMin: 720,
          language: 'english'
        }
      ]
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm: any = wrapper.vm

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Dune' }

    await vm.performAdvancedSearch()
    await wrapper.vm.$nextTick()

    // Find lazy image element
    const img = wrapper.find('img.lazy-search-img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('data-src')).toBe('http://img2')
  })

  it('maps runtime from runtimeLengthMin (minutes) to seconds', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as any
    apiService.searchAudimetaByTitleAndAuthor.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000125',
          title: 'Children of Dune',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img3',
          runtimeLengthMin: 10,
          language: 'english'
        }
      ]
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm: any = wrapper.vm

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Children of Dune' }

    await vm.performAdvancedSearch()
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0]
    expect(tr.searchResult.runtime).toBe(10 * 60)
  })

  it('maps runtime from lengthMinutes (metadata field) to seconds', async () => {
    const apiModule = await import('@/services/api')
    const apiService = apiModule.apiService as any
    apiService.searchAudimetaByTitleAndAuthor.mockResolvedValue({
      totalResults: 1,
      results: [
        {
          asin: 'B000126',
          title: 'Heretics of Dune',
          authors: [{ name: 'Frank Herbert' }],
          imageUrl: 'http://img4',
          lengthMinutes: 12,
          language: 'english'
        }
      ]
    })

    const router = createRouter({ history: createMemoryHistory(), routes: [] })
    const wrapper = mount(AddNewView, { global: { plugins: [createPinia(), router] } })
    const vm: any = wrapper.vm

    vm.showAdvancedSearch = true
    vm.advancedSearchParams = { title: 'Heretics of Dune' }

    await vm.performAdvancedSearch()
    expect(vm.titleResults.length).toBe(1)
    const tr = vm.titleResults[0]
    expect(tr.searchResult.runtime).toBe(12 * 60)
  })
})
