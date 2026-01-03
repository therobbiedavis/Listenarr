import { mount } from '@vue/test-utils'
import { vi, describe, it, expect } from 'vitest'

vi.mock('@/services/api', () => ({
  apiService: {
    getQualityProfiles: vi.fn().mockResolvedValue([]),
    getApplicationSettings: vi.fn().mockResolvedValue({ outputPath: 'C:\\root' }),
    getRootFolders: vi.fn().mockResolvedValue([
      { id: 1, name: 'Default', path: 'C:\\root', isDefault: true }
    ])
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
  it('shows full path in readonly input by default', async () => {
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

    // Check that readonly input shows the full path
    const readonlyInput = wrapper.find('.readonly-input')
    expect(readonlyInput.exists()).toBe(true)
    expect((readonlyInput.element as HTMLInputElement).value).toBe('C:\\root/Some Author\\Some Title')
  })

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

    // Click the edit button to enter edit mode
    const editButton = wrapper.find('.btn-edit-destination')
    expect(editButton.exists()).toBe(true)
    await editButton.trigger('click')

    // Now the relative input should be visible
    const input = wrapper.find('input.relative-input')
    expect(input.exists()).toBe(true)
    expect((input.element as HTMLInputElement).value).toBe('Some Author\\Some Title')
  })
})