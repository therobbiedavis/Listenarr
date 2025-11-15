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
