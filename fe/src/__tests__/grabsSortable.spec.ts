import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { describe, it, expect, vi, afterEach } from 'vitest'
import ManualSearchModal from '@/components/ManualSearchModal.vue'
import { apiService } from '@/services/api'
import * as api from '@/services/api'

describe('ManualSearchModal - grabs sorting', () => {
  const stubs = ['PhMagnifyingGlass', 'PhX', 'PhSpinner', 'PhArrowClockwise', 'PhArrowUp', 'PhArrowDown', 'PhXCircle', 'PhDownloadSimple', 'PhArrowsDownUp', 'ScorePopover']

  afterEach(() => {
    vi.restoreAllMocks()
  })

  const triggerSearchAndWait = async (wrapper: any, selector: string, timeout = 1000) => {
    // Manually trigger search then wait for a selector to appear
    try { await (wrapper.vm as any).search() } catch {}
    const start = Date.now()
    while (Date.now() - start < timeout) {
      if (wrapper.find(selector).exists()) return
      await new Promise(r => setTimeout(r, 10))
    }
    throw new Error('timeout waiting for selector')
  }

  it('header is clickable to set Grabs sort', async () => {
    // Mock instance methods on apiService so component calls succeed
    ;(apiService as any).getEnabledIndexers = vi.fn().mockResolvedValue([{ id: 1, name: 'Test', implementation: 'Test', additionalSettings: null } as any])
    ;(apiService as any).searchByApi = vi.fn().mockResolvedValue([
      { guid: '1', title: 'A', grabs: 100, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any,
      { guid: '2', title: 'B', grabs: 10, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any,
      { guid: '3', title: 'C', grabs: 50, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any
    ])
    ;(apiService as any).getDefaultQualityProfile = vi.fn().mockResolvedValue({ id: 1 } as any)
    ;(apiService as any).scoreSearchResults = vi.fn().mockResolvedValue([])

    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: false, audiobook: { id: '1', title: 'Test', authors: [] } }, global: { stubs } })
    await wrapper.setProps({ isOpen: true })

    // Force the component to run search() in test env and wait for table header
    await (wrapper.vm as any).search()
    await triggerSearchAndWait(wrapper, 'th.col-grabs')
    await nextTick()

    const header = wrapper.find('th.col-grabs')
    expect(header.exists()).toBe(true)

    // First click: set to Grabs (new column) -> defaults to Descending
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 100))
    await nextTick()

    // Read grabs values from rows in order
    const rowsAfterDesc = wrapper.findAll('tbody tr')
    const grabsDesc = rowsAfterDesc.map(r => {
      const txt = r.find('td.col-grabs .grabs').text()
      return Number(txt.replace('✚ ', '').trim())
    })
    expect(grabsDesc).toEqual([100, 50, 10])

    // Second click: same column -> toggles to Ascending
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 100))
    await nextTick()

    const rowsAfterAsc = wrapper.findAll('tbody tr')
    const grabsAsc = rowsAfterAsc.map(r => {
      const txt = r.find('td.col-grabs .grabs').text()
      return Number(txt.replace('✚ ', '').trim())
    })
    expect(grabsAsc).toEqual([10, 50, 100])
  })

  it('header is clickable to set Language sort and toggles order', async () => {
    // Mock instance methods on apiService so component calls succeed
    ;(apiService as any).getEnabledIndexers = vi.fn().mockResolvedValue([{ id: 1, name: 'Test', implementation: 'Test', additionalSettings: null } as any])
    ;(apiService as any).searchByApi = vi.fn().mockResolvedValue([
      { guid: '1', title: 'A', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'Spanish', indexer: 'Test', indexerId: 1 } as any,
      { guid: '2', title: 'B', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'English', indexer: 'Test', indexerId: 1 } as any,
      { guid: '3', title: 'C', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'French', indexer: 'Test', indexerId: 1 } as any
    ])
    ;(apiService as any).getDefaultQualityProfile = vi.fn().mockResolvedValue({ id: 1 } as any)
    ;(apiService as any).scoreSearchResults = vi.fn().mockResolvedValue([])

    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: false, audiobook: { id: '1', title: 'Test', authors: [] } }, global: { stubs } })
    await wrapper.setProps({ isOpen: true })

    // Force the component to run search() in test env and wait for table header
    await (wrapper.vm as any).search()
    await triggerSearchAndWait(wrapper, 'th.col-language')
    await nextTick()

    const header = wrapper.find('th.col-language')
    expect(header.exists()).toBe(true)

    // First click: set Language -> defaults to Descending (Z->A)
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 100))
    await nextTick()

    const rowsDesc = wrapper.findAll('tbody tr')
    const langsDesc = rowsDesc.map(r => r.find('td.col-language .language-badge').text())
    // Descending alphabetical: Spanish, French, English
    expect(langsDesc).toEqual(['Spanish', 'French', 'English'])

    // Second click toggles to Ascending
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 100))
    await nextTick()

    const rowsAsc = wrapper.findAll('tbody tr')
    const langsAsc = rowsAsc.map(r => r.find('td.col-language .language-badge').text())
    expect(langsAsc).toEqual(['English', 'French', 'Spanish'])
  })

})