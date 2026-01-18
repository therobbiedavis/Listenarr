import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import DownloadClientFormModal from '@/components/DownloadClientFormModal.vue'

describe('DownloadClientFormModal', () => {
  it('renders password input for qbittorrent', async () => {
    const wrapper = mount(DownloadClientFormModal, {
      global: { plugins: [createPinia()] },
      props: { visible: true, editingClient: null },
    })

    // Provide an editingClient prop to initialize formData for qbittorrent
    await wrapper.setProps({
      editingClient: {
        id: '1',
        name: 'qbt',
        type: 'qbittorrent',
        host: 'qbittorrent.local',
        port: 8080,
        isEnabled: true,
        useSSL: false,
        downloadPath: '',
        settings: {},
      },
    })
    await wrapper.vm.$nextTick()

    const passwordInput = wrapper.find('input[id="password"]')
    // debug
     
    console.log('HTML:', wrapper.html())
    expect(passwordInput.exists()).toBe(true)
  })

  it('renders api key input for sabnzbd', async () => {
    const wrapper = mount(DownloadClientFormModal, {
      global: { plugins: [createPinia()] },
      props: { visible: true, editingClient: null },
    })

    await wrapper.setProps({
      editingClient: {
        id: '2',
        name: 'sab',
        type: 'sabnzbd',
        host: 'sab.local',
        port: 8080,
        isEnabled: true,
        useSSL: false,
        downloadPath: '',
        settings: {},
      },
    })
    await wrapper.vm.$nextTick()

    const apiKeyInput = wrapper.find('input[id="apiKey"]')
    expect(apiKeyInput.exists()).toBe(true)
  })

  it('test button on modal uses current input values (no ID sent)', async () => {
    const api = await import('@/services/api')
    ;(api.testDownloadClient as any) = vi.fn(async (config: any) => ({ success: true, message: 'ok', client: config }))

    const wrapper = mount(DownloadClientFormModal, {
      global: { plugins: [createPinia()] },
      props: { visible: true, editingClient: null },
    })

    await wrapper.setProps({
      editingClient: {
        id: '3',
        name: 'qbt',
        type: 'qbittorrent',
        host: 'original.local',
        port: 8080,
        isEnabled: true,
        useSSL: false,
        downloadPath: '',
        settings: {},
        password: 'dbpass',
      },
    })
    await wrapper.vm.$nextTick()

    // change host input to a new value before testing
    const hostInput = wrapper.find('input[id="host"]')
    await hostInput.setValue('edited.local')

    // click the Test button (use class selector to reliably find the correct button)
    const testButton = wrapper.find('button.btn-info')
    expect(testButton.exists()).toBe(true)
    await testButton.trigger('click')

    expect((api.testDownloadClient as any)).toHaveBeenCalled()
    const calledWith = (api.testDownloadClient as any).mock.calls[0][0]
    expect(calledWith.host).toBe('edited.local')
    // ID should NOT be sent when testing modal inputs to avoid DB fallback
    expect(calledWith.id).toBeUndefined()
  })

  it('modal prepopulates password from DB and uses empty password when cleared', async () => {
    const api = await import('@/services/api')
    ;(api.testDownloadClient as any) = vi.fn(async (config: any) => ({ success: true, message: 'ok', client: config }))

    const wrapper = mount(DownloadClientFormModal, {
      global: { plugins: [createPinia()] },
      props: { visible: true, editingClient: null },
    })

    await wrapper.setProps({
      editingClient: {
        id: '4',
        name: 'qbt',
        type: 'qbittorrent',
        host: 'host.local',
        port: 8080,
        isEnabled: true,
        useSSL: false,
        downloadPath: '',
        settings: {},
        password: 'dbpass',
      },
    })
    await wrapper.vm.$nextTick()

    const passwordInput = wrapper.find('input[id="password"]')
    expect(passwordInput.exists()).toBe(true)
    // prepopulated value should match DB
    expect((passwordInput.element as HTMLInputElement).value).toBe('dbpass')

    // clear the password input to explicitly test empty-password behavior
    await passwordInput.setValue('')

    // click Test
    const testButton = wrapper.find('button.btn-info')
    await testButton.trigger('click')

    expect((api.testDownloadClient as any)).toHaveBeenCalled()
    const calledWith = (api.testDownloadClient as any).mock.calls[0][0]
    // Because we omit the id, server will NOT pull DB password; we should send empty password
    expect(calledWith.password).toBe('')
  })
})