/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import CollectionView from '@/views/CollectionView.vue'
import { useLibraryStore } from '@/stores/library'

vi.mock('@/services/api', () => ({
  apiService: {
    getImageUrl: vi.fn((url: string) => url || 'https://via.placeholder.com/300x450?text=No+Image'),
    getStartupConfig: vi.fn(async () => ({})),
    getApplicationSettings: vi.fn(async () => ({}))
  }
}))

describe('CollectionView', () => {
  beforeEach(() => {
    const pinia = createPinia()
    setActivePinia(pinia)
  })

  it('shows collection content details', async () => {
    if (!(global as any).ResizeObserver) {
      (global as any).ResizeObserver = class { observe() {}; disconnect() {}; }
    }
    if (!(global as any).WebSocket) {
      (global as any).WebSocket = function () { /* noop */ }
    }

    const pinia = createPinia()
    setActivePinia(pinia)

    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }, { path: '/collection/:type/:name', name: 'collection', component: CollectionView }] })
    await router.push('/collection/series/Series%201')
    await router.isReady().catch(() => {})

    const store = useLibraryStore()
    store.audiobooks = [
      { id: 1, title: 'Book A', authors: ['Author A'], series: 'Series 1', imageUrl: 'c1.jpg', files: [] },
      { id: 2, title: 'Book B', authors: ['Author B'], series: 'Series 1', imageUrl: 'c2.jpg', files: [] }
    ] as unknown as import('@/types').Audiobook[]

    store.fetchLibrary = vi.fn(async () => undefined)
    const wrapper = mount(CollectionView, { global: { plugins: [pinia, router], stubs: ['EditAudiobookModal', 'CustomSelect'] } })
    await new Promise(r => setTimeout(r, 0))

    // ensure grid view
    expect(wrapper.vm.viewMode).toBe('grid')

    // collection cards should show title and author in collection-content
    const collectionCards = wrapper.findAll('.collection-card')
    expect(collectionCards.length).toBe(2)

    // Check first card has title and author
    const firstCard = collectionCards[0]
    expect(firstCard.find('.collection-title').text()).toBe('Book A')
    expect(firstCard.find('.collection-author').text()).toBe('Author A')
  })
})