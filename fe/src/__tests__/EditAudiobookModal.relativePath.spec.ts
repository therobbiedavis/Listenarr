import { mount } from '@vue/test-utils'
import { vi, describe, it, expect } from 'vitest'

vi.mock('@/services/api', () => ({
  apiService: {
    getQualityProfiles: vi.fn().mockResolvedValue([]),
    getApplicationSettings: vi.fn().mockResolvedValue({ outputPath: 'C:\\root' })
  }
}))

import EditAudiobookModal from '@/components/EditAudiobookModal.vue'

const audiobook = {
  id: 1,
  title: 'Sample',
  authors: ['Author'],
  basePath: 'C:\\root\\Some Author\\Some Title',
  monitored: true,
  tags: [],
}

describe('EditAudiobookModal relative path calculation', () => {
  it('derives relative path from stored basePath when root configured', async () => {
    const wrapper = mount(EditAudiobookModal as any, {
      props: {
        isOpen: true,
        audiobook
      },
      attachTo: document.body,
      global: {
        plugins: [ (await import('pinia')).createPinia() ]
      }
    })

    // allow async init
    await new Promise((r) => setTimeout(r, 10))

    const input = wrapper.find('input.relative-input')
    expect(input.exists()).toBe(true)
    expect((input.element as HTMLInputElement).value).toBe('Some Author\\Some Title')
  })
})