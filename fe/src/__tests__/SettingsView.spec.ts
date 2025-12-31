/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import SettingsView from '@/views/SettingsView.vue'
import { apiService } from '@/services/api'

vi.mock('@/services/api', () => ({
  apiService: {
    getStartupConfig: vi.fn(),
    getApiConfigurations: vi.fn(async () => []),
    getDownloadClientConfigurations: vi.fn(async () => []),
    getApplicationSettings: vi.fn(async () => ({})),
    getIndexers: vi.fn(async () => []),
    getQualityProfiles: vi.fn(async () => []),
    getAdminUsers: vi.fn(async () => []),
    generateInitialApiKey: vi.fn(async () => ({ apiKey: 'abc' })),
    regenerateApiKey: vi.fn(async () => ({ apiKey: 'abc' })),
    saveStartupConfig: vi.fn(async () => ({})),
  }
  ,
  // Named exports used directly by SettingsView.vue
  getIndexers: vi.fn(async () => []),
  deleteIndexer: vi.fn(async () => ({})),
  toggleIndexer: vi.fn(async (id: number) => ({ id, isEnabled: true })),
  testIndexer: vi.fn(async (id: number) => ({ success: true, message: 'ok', indexer: { id, isEnabled: true } })),
  getQualityProfiles: vi.fn(async () => []),
  deleteQualityProfile: vi.fn(async () => ({})),
  createQualityProfile: vi.fn(async (p: unknown) => p),
  updateQualityProfile: vi.fn(async (id: number, p: unknown) => p),
  getRemotePathMappings: vi.fn(async () => []),
  createRemotePathMapping: vi.fn(async (p: unknown) => p),
  updateRemotePathMapping: vi.fn(async (id: number, p: unknown) => p),
  deleteRemotePathMapping: vi.fn(async () => ({})),
}))

describe('SettingsView', () => {
  beforeEach(() => {
    (apiService.getStartupConfig as any).mockReset()
     // Provide a single Pinia instance for stores used by the component
     const pinia = createPinia()
     setActivePinia(pinia)
  })

  it('sets authEnabled when startup config AuthenticationRequired is Enabled', async () => {
  (apiService.getStartupConfig as any).mockResolvedValue({ AuthenticationRequired: 'Enabled' })
      // create a minimal router for components that inject router/location
      const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }] })
    // Ensure router is ready before mounting (SettingsView may call router.replace during mount)
    await router.push('/')
    await router.isReady().catch(() => {})
      const pinia = createPinia()
      const wrapper = mount(SettingsView, { global: { plugins: [pinia, router], stubs: ['FolderBrowser'] } })
    // Wait for onMounted async calls to finish
    await new Promise((r) => setTimeout(r, 0))
  // Accept both legacy 'Enabled' and new 'true' string values
  expect((wrapper.vm as any).authEnabled).toBe(true)
  })

  it('toggles password visibility', async () => {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }] })
  await router.push('/')
  await router.isReady().catch(() => {})
  const pinia = createPinia()
  const wrapper = mount(SettingsView, { global: { plugins: [pinia, router], stubs: ['FolderBrowser'] } })
  // Activate the General Settings tab so the password field is rendered
  const generalTab = wrapper.findAll('button.tab-button').find(b => b.text().includes('General Settings'))
  expect(generalTab).toBeTruthy()
  await generalTab!.trigger('click')
  // Provide settings so the admin password input is rendered
  ;(wrapper.vm as any).settings = { adminPassword: 'secret' }
  await wrapper.vm.$nextTick()
  await new Promise((r) => setTimeout(r, 0))
  // Access internal setup state to check showPassword directly (more reliable in VTU)
  const setupState = (wrapper.vm as any).$?.setupState || (wrapper.vm as any).$setup || (wrapper.vm as any)
  // initial value should be false
  expect(setupState.showPassword?.value ?? setupState.showPassword).toBe(false)
  // Toggle via exposed function
  ;(wrapper.vm as any).toggleShowPassword()
  await wrapper.vm.$nextTick()
  expect(setupState.showPassword?.value ?? setupState.showPassword).toBe(true)
  })

  it('enables/disables proxy fields and saves proxy settings', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }] })
    await router.push('/')
    await router.isReady().catch(() => {})

    const pinia = createPinia()
    setActivePinia(pinia)

    const wrapper = mount(SettingsView, { global: { plugins: [pinia, router], stubs: ['FolderBrowser'] } })

    // Activate General Settings tab and provide initial settings
    const generalTab = wrapper.findAll('button.tab-button').find(b => b.text().includes('General Settings'))
    expect(generalTab).toBeTruthy()
    await generalTab!.trigger('click')
    ;(wrapper.vm as any).settings = {
      preferUsDomain: false,
      useUsProxy: false,
      usProxyHost: '',
      usProxyPort: 0,
      usProxyUsername: '',
      usProxyPassword: ''
    }

  await wrapper.vm.$nextTick()
  // Small wait for reactive updates
  await new Promise((r) => setTimeout(r, 0))
  await wrapper.vm.$nextTick()

    const hostInput = wrapper.find('input[placeholder="proxy.example.com"]')
    expect(hostInput.exists()).toBe(true)
    // When proxy is disabled inputs should be disabled
    expect(hostInput.attributes('disabled')).toBeDefined()

    // Enable proxy usage
    ;(wrapper.vm as any).settings.useUsProxy = true
    await wrapper.vm.$nextTick()
    await new Promise((r) => setTimeout(r, 0))

    // Host input should now be enabled
    expect(hostInput.attributes('disabled')).toBeUndefined()

    // Fill in details
    ;(wrapper.vm as any).settings.usProxyHost = 'proxy.test.local'
    ;(wrapper.vm as any).settings.usProxyPort = 3128
    await wrapper.vm.$nextTick()

    // Spy on the configuration store save method
    const { useConfigurationStore } = await import('@/stores/configuration')
    const cfgStore = useConfigurationStore()
  cfgStore.saveApplicationSettings = vi.fn().mockResolvedValue(undefined)

    // Click Save Settings button
    const saveBtn = wrapper.findAll('button.save-button').find(b => b.text().includes('Save Settings'))
    expect(saveBtn).toBeTruthy()
    await saveBtn!.trigger('click')

    // Expect store save called
    expect(cfgStore.saveApplicationSettings).toHaveBeenCalled()
    const calledWith = (cfgStore.saveApplicationSettings as any).mock.calls[0][0]
    expect(calledWith.usProxyHost).toBe('proxy.test.local')
    expect(Number(calledWith.usProxyPort)).toBe(3128)
  })

  it('toggles download client enabled state', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }] })
    await router.push('/')
    await router.isReady().catch(() => {})

    const pinia = createPinia()
    setActivePinia(pinia)

    // Prepare configuration store with a single disabled client
    const { useConfigurationStore } = await import('@/stores/configuration')
    const cfgStore = useConfigurationStore()
    cfgStore.downloadClientConfigurations = [
      {
        id: 'client-1',
        name: 'Test Client',
        type: 'qbittorrent',
        host: 'localhost',
        port: 8080,
        isEnabled: false,
        useSSL: false,
        downloadPath: ''
      }
    ] as any

    // Prevent load from overwriting our test data
    cfgStore.loadDownloadClientConfigurations = vi.fn(async () => {})

    cfgStore.saveDownloadClientConfiguration = vi.fn(async (c: any) => {
      // Simulate backend saving and return an id (no-op)
      cfgStore.downloadClientConfigurations[0] = c
      return 'ok'
    })

    const wrapper = mount(SettingsView, { global: { plugins: [pinia, router], stubs: ['FolderBrowser'] } })

    // Activate the clients tab
    const clientsTab = wrapper.findAll('button.tab-button').find(b => b.text().includes('Download Clients'))
    expect(clientsTab).toBeTruthy()
    await clientsTab!.trigger('click')
    await wrapper.vm.$nextTick()

    // Call the toggle handler directly (avoid relying on rendered DOM in VTU)
    await (wrapper.vm as any).toggleDownloadClientFunc(cfgStore.downloadClientConfigurations[0])
    // Wait for async save
    await new Promise((r) => setTimeout(r, 0))

    expect(cfgStore.saveDownloadClientConfiguration).toHaveBeenCalled()
    expect(cfgStore.downloadClientConfigurations[0].isEnabled).toBe(true)
  })
})
