/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import AudiobooksView from '@/views/AudiobooksView.vue'
import { useLibraryStore } from '@/stores/library'
// apiService stubbed in vi.mock below if needed

vi.mock('@/services/api', () => ({
  apiService: {
    getQualityProfiles: vi.fn(async () => []),
    getImageUrl: vi.fn((url: string) => url || 'https://via.placeholder.com/300x450?text=No+Image'),
    getStartupConfig: vi.fn(async () => ({})),
    getApplicationSettings: vi.fn(async () => ({}))
  }
}))

describe('AudiobooksView', () => {
  beforeEach(() => {
    const pinia = createPinia()
    setActivePinia(pinia)
  })

  it('shows extra details in grid view when showItemDetails is enabled', async () => {
    // ensure ResizeObserver is defined for the mount in vtu
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    // Minimal WebSocket stub so SignalRService doesn't throw during tests
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }
    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/audiobooks', name: 'audiobooks', component: AudiobooksView }] })
    await router.push('/audiobooks')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      {
        id: 123,
        title: 'The Test Book',
        authors: ['Test Author'],
        narrators: ['Test Narrator'],
        publisher: 'Test Publisher',
        publishYear: 2020,
        imageUrl: 'https://example.com/cover.jpg',
        files: []
      }
  ] as unknown as import('@/types').Audiobook[]

  // Persist 'showItemDetails' so component mounts with details on
  localStorage.setItem('listenarr.showItemDetails', 'true')
  // Prevent real fetchLibrary from running during mount (we set audiobooks directly)
  store.fetchLibrary = vi.fn(async () => undefined)
  const wrapper = mount(AudiobooksView, { global: { plugins: [pinia, router], stubs: ['BulkEditModal', 'EditAudiobookModal', 'CustomFilterModal', 'FiltersDropdown', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

  // Find the rendered extra details block under the poster in the grid
  const bottomDetails = wrapper.find('.grid-bottom-details')
  expect(bottomDetails.exists()).toBe(true)
    expect(wrapper.text()).toContain('The Test Book')
    expect(wrapper.text()).toContain('Test Author')
    expect(wrapper.text()).toContain('Test Narrator')
    expect(wrapper.text()).toContain('Test Publisher')
    expect(wrapper.text()).toContain('2020')
  })
})

describe('AudiobooksView Grouping', () => {
  beforeEach(() => {
    const pinia = createPinia()
    setActivePinia(pinia)
  })

  it('groups audiobooks by author when groupBy is authors', async () => {
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }

    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/audiobooks', name: 'audiobooks', component: AudiobooksView }] })
    await router.push('/audiobooks')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      {
        id: 1,
        title: 'Book 1',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover1.jpg',
        files: []
      },
      {
        id: 2,
        title: 'Book 2',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover2.jpg',
        files: []
      },
      {
        id: 3,
        title: 'Book 3',
        authors: ['Author B'],
        series: 'Series 2',
        imageUrl: 'cover3.jpg',
        files: []
      }
    ] as unknown as import('@/types').Audiobook[]

    store.fetchLibrary = vi.fn(async () => undefined)
    const wrapper = mount(AudiobooksView, { global: { plugins: [pinia, router], stubs: ['BulkEditModal', 'EditAudiobookModal', 'CustomFilterModal', 'FiltersDropdown', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

    // Set groupBy to authors
    await wrapper.vm.setGroupBy('authors')
    await wrapper.vm.$nextTick()

    const groupedCollections = wrapper.vm.groupedCollections
    expect(groupedCollections).toHaveLength(2)
    expect(groupedCollections.find(g => g.name === 'Author A')).toEqual({
      name: 'Author A',
      count: 2,
      coverUrl: 'cover1.jpg'
    })
    expect(groupedCollections.find(g => g.name === 'Author B')).toEqual({
      name: 'Author B',
      count: 1,
      coverUrl: 'cover3.jpg'
    })
  })

  it('groups audiobooks by series when groupBy is series', async () => {
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }

    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/audiobooks', name: 'audiobooks', component: AudiobooksView }] })
    await router.push('/audiobooks')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      {
        id: 1,
        title: 'Book 1',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover1.jpg',
        files: []
      },
      {
        id: 2,
        title: 'Book 2',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover2.jpg',
        files: []
      },
      {
        id: 3,
        title: 'Book 3',
        authors: ['Author B'],
        series: 'Series 2',
        imageUrl: 'cover3.jpg',
        files: []
      }
    ] as unknown as import('@/types').Audiobook[]

    store.fetchLibrary = vi.fn(async () => undefined)
    const wrapper = mount(AudiobooksView, { global: { plugins: [pinia, router], stubs: ['BulkEditModal', 'EditAudiobookModal', 'CustomFilterModal', 'FiltersDropdown', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

    // Set groupBy to series
    await wrapper.vm.setGroupBy('series')
    await wrapper.vm.$nextTick()

    const groupedCollections = wrapper.vm.groupedCollections
    expect(groupedCollections).toHaveLength(2)
    expect(groupedCollections.find(g => g.name === 'Series 1')).toEqual({
      name: 'Series 1',
      count: 2,
      coverUrls: ['cover1.jpg', 'cover2.jpg']
    })
    expect(groupedCollections.find(g => g.name === 'Series 2')).toEqual({
      name: 'Series 2',
      count: 1,
      coverUrls: ['cover3.jpg']
    })
  })

  it('shows individual books when groupBy is books', async () => {
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }

    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/audiobooks', name: 'audiobooks', component: AudiobooksView }] })
    await router.push('/audiobooks')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      {
        id: 1,
        title: 'Book 1',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover1.jpg',
        files: []
      }
    ] as unknown as import('@/types').Audiobook[]

    store.fetchLibrary = vi.fn(async () => undefined)
    // Ensure groupBy is 'books'
    localStorage.setItem('listenarr.groupBy', 'books')
    const wrapper = mount(AudiobooksView, { global: { plugins: [pinia, router], stubs: ['BulkEditModal', 'EditAudiobookModal', 'CustomFilterModal', 'FiltersDropdown', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

    // groupBy defaults to 'books'
    const groupedCollections = wrapper.vm.groupedCollections
    expect(groupedCollections).toHaveLength(0)
  })

  it('series bottom placard is only visible when showItemDetails is enabled', async () => {
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }

    // Ensure persisted item details are cleared for this test (deterministic)
    localStorage.setItem('listenarr.showItemDetails', 'false')

    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/audiobooks', name: 'audiobooks', component: AudiobooksView }] })
    await router.push('/audiobooks')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      {
        id: 1,
        title: 'Book 1',
        authors: ['Author A'],
        series: 'Series 1',
        imageUrl: 'cover1.jpg',
        files: []
      },
      {
        id: 2,
        title: 'Book 2',
        authors: ['Author B'],
        series: 'Series 2',
        imageUrl: 'cover2.jpg',
        files: []
      }
    ] as unknown as import('@/types').Audiobook[]

    store.fetchLibrary = vi.fn(async () => undefined)
    const wrapper = mount(AudiobooksView, { global: { plugins: [pinia, router], stubs: ['BulkEditModal', 'EditAudiobookModal', 'CustomFilterModal', 'FiltersDropdown', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

    // Set groupBy to series
    await wrapper.vm.setGroupBy('series')
    await wrapper.vm.$nextTick()

    // By default, details should be hidden and placard not present
    expect(wrapper.vm.showItemDetails).toBe(false)
    expect(wrapper.find('.series-bottom-placard').exists()).toBe(false)

    // Enable details and confirm placard is shown
    wrapper.vm.showItemDetails = true
    await wrapper.vm.$nextTick()
    expect(wrapper.find('.series-bottom-placard').exists()).toBe(true)
  })
})
