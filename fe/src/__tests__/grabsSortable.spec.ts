import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { describe, it, expect, vi } from 'vitest'
import ManualSearchModal from '@/components/ManualSearchModal.vue'
import { apiService } from '@/services/api'

describe('ManualSearchModal - grabs sorting', () => {
  const stubs = ['PhMagnifyingGlass', 'PhX', 'PhSpinner', 'PhArrowClockwise', 'PhArrowUp', 'PhArrowDown', 'PhXCircle', 'PhDownloadSimple', 'PhArrowsDownUp', 'ScorePopover']

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('header is clickable to set Grabs sort', async () => {
    // Stub API calls so search() completes deterministically
    vi.spyOn(apiService, 'getEnabledIndexers').mockResolvedValue([{ id: 1, name: 'Test', implementation: 'Test', additionalSettings: null } as any])
    vi.spyOn(apiService, 'searchByApi').mockResolvedValue([
      { guid: '1', title: 'A', grabs: 100, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any,
      { guid: '2', title: 'B', grabs: 10, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any,
      { guid: '3', title: 'C', grabs: 50, size: 0, publishDate: new Date().toISOString(), indexer: 'Test', indexerId: 1 } as any
    ])
    vi.spyOn(apiService, 'getDefaultQualityProfile').mockResolvedValue({ id: 1 } as any)
    vi.spyOn(apiService, 'scoreSearchResults').mockResolvedValue([])

    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: { id: '1', title: 'Test', authors: [] } }, global: { stubs } })

    // Wait for initial search to complete
    await new Promise(resolve => setTimeout(resolve, 20))
    await nextTick()

    const header = wrapper.find('th.col-grabs')
    expect(header.exists()).toBe(true)

    // First click: set to Grabs (new column) -> defaults to Descending
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 20))
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
    await new Promise(resolve => setTimeout(resolve, 20))
    await nextTick()

    const rowsAfterAsc = wrapper.findAll('tbody tr')
    const grabsAsc = rowsAfterAsc.map(r => {
      const txt = r.find('td.col-grabs .grabs').text()
      return Number(txt.replace('✚ ', '').trim())
    })
    expect(grabsAsc).toEqual([10, 50, 100])
  })

  it('header is clickable to set Language sort and toggles order', async () => {
    // Stub API calls with languages to validate ordering
    vi.spyOn(apiService, 'getEnabledIndexers').mockResolvedValue([{ id: 1, name: 'Test', implementation: 'Test', additionalSettings: null } as any])
    vi.spyOn(apiService, 'searchByApi').mockResolvedValue([
      { guid: '1', title: 'A', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'Spanish', indexer: 'Test', indexerId: 1 } as any,
      { guid: '2', title: 'B', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'English', indexer: 'Test', indexerId: 1 } as any,
      { guid: '3', title: 'C', grabs: 0, size: 0, publishDate: new Date().toISOString(), language: 'French', indexer: 'Test', indexerId: 1 } as any
    ])
    vi.spyOn(apiService, 'getDefaultQualityProfile').mockResolvedValue({ id: 1 } as any)
    vi.spyOn(apiService, 'scoreSearchResults').mockResolvedValue([])

    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: { id: '1', title: 'Test', authors: [] } }, global: { stubs } })

    // Wait for initial search to complete
    await new Promise(resolve => setTimeout(resolve, 20))
    await nextTick()

    const header = wrapper.find('th.col-language')
    expect(header.exists()).toBe(true)

    // First click: set Language -> defaults to Descending (Z->A)
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 20))
    await nextTick()

    const rowsDesc = wrapper.findAll('tbody tr')
    const langsDesc = rowsDesc.map(r => r.find('td.col-language .language-badge').text())
    // Descending alphabetical: Spanish, French, English
    expect(langsDesc).toEqual(['Spanish', 'French', 'English'])

    // Second click toggles to Ascending
    await header.trigger('click')
    await new Promise(resolve => setTimeout(resolve, 20))
    await nextTick()

    const rowsAsc = wrapper.findAll('tbody tr')
    const langsAsc = rowsAsc.map(r => r.find('td.col-language .language-badge').text())
    expect(langsAsc).toEqual(['English', 'French', 'Spanish'])
  })
})