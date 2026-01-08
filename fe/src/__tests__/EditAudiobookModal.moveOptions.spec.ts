import { mount } from '@vue/test-utils'
import { vi, describe, it, expect, beforeEach } from 'vitest'

vi.mock('@/services/api', () => ({
  apiService: {
    getQualityProfiles: vi.fn().mockResolvedValue([]),
    getApplicationSettings: vi.fn().mockResolvedValue({ outputPath: 'C:\\root' }),
    updateAudiobook: vi.fn().mockResolvedValue({ message: 'ok', audiobook: {} }),
    moveAudiobook: vi.fn().mockResolvedValue({ message: 'queued', jobId: 'job-1' }),
  },
}))

vi.mock('@/services/toastService', () => ({
  useToast: () => ({ info: vi.fn(), success: vi.fn(), error: vi.fn() }),
}))

vi.mock('@/services/signalr', () => ({
  signalRService: {
    onMoveJobUpdate: vi.fn(() => () => {}),
  },
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

describe('EditAudiobookModal move options', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('Change without moving should update audiobook and not call move API', async () => {
    const wrapper = mount(EditAudiobookModal, {
      props: { isOpen: true, audiobook },
      attachTo: document.body,
      global: { plugins: [(await import('pinia')).createPinia()] },
    })

    // let init settle
    await new Promise((r) => setTimeout(r, 10))

    // change the relative path input
    const input = wrapper.find('input.relative-input')
    await input.setValue('New Author\\New Book')

    // Submit (should open modal)
    await wrapper.find('button[type="submit"]').trigger('click')

    // Modal should be visible
    expect(wrapper.find('.confirm-dialog').exists()).toBe(true)

    // Click 'Change without moving' button (middle button in dialog)
    const confirmButtons = wrapper.findAll('.confirm-dialog .btn')
    await confirmButtons[1].trigger('click')

    // Wait a tick for save to finish
    await new Promise((r) => setTimeout(r, 10))

    const { apiService } = await import('@/services/api')
    expect(apiService.updateAudiobook).toHaveBeenCalledTimes(1)
    expect(apiService.moveAudiobook).toHaveBeenCalledTimes(0)
  })

  it('Move should call move API with deleteEmptySource true by default', async () => {
    const wrapper = mount(EditAudiobookModal, {
      props: { isOpen: true, audiobook },
      attachTo: document.body,
      global: { plugins: [(await import('pinia')).createPinia()] },
    })

    await new Promise((r) => setTimeout(r, 10))

    const input = wrapper.find('input.relative-input')
    await input.setValue('New Author\\New Book')

    // Submit (open modal)
    await wrapper.find('button[type="submit"]').trigger('click')

    // Click Move button (last button in dialog)
    const buttons = wrapper.findAll('.confirm-dialog .btn')
    // last one is Move per our template
    await buttons[buttons.length - 1].trigger('click')

    // Wait a tick
    await new Promise((r) => setTimeout(r, 10))

    const { apiService } = await import('@/services/api')
    expect(apiService.updateAudiobook).toHaveBeenCalledTimes(1)
    expect(apiService.moveAudiobook).toHaveBeenCalledTimes(1)
    expect(apiService.moveAudiobook).toHaveBeenCalledWith(
      expect.anything(),
      expect.anything(),
      expect.objectContaining({ moveFiles: true, deleteEmptySource: true }),
    )
  })
})
