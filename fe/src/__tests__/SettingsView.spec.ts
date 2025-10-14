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
}))

describe('SettingsView', () => {
  beforeEach(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ;(apiService.getStartupConfig as any).mockReset()
     // Provide a single Pinia instance for stores used by the component
     const pinia = createPinia()
     setActivePinia(pinia)
  })

  it('sets authEnabled when startup config AuthenticationRequired is Enabled', async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ;(apiService.getStartupConfig as any).mockResolvedValue({ AuthenticationRequired: 'Enabled' })
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
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    expect((wrapper.vm as any).authEnabled).toBe(true)
  })

  it('toggles password visibility', async () => {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', name: 'home', component: { template: '<div />' } }] })
  await router.push('/')
  await router.isReady().catch(() => {})
  const pinia = createPinia()
  const wrapper = mount(SettingsView, { global: { plugins: [pinia, router], stubs: ['FolderBrowser'] } })
  // Ensure the General tab is active so the password field is rendered
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(wrapper.vm as any).activeTab = 'general'
  // Provide settings so the admin password input is rendered
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(wrapper.vm as any).settings = { adminPassword: 'secret' }
  await wrapper.vm.$nextTick()
  await new Promise((r) => setTimeout(r, 0))
  // Access internal setup state to check showPassword directly (more reliable in VTU)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const setupState = (wrapper.vm as any).$?.setupState || (wrapper.vm as any).$setup || (wrapper.vm as any)
  // initial value should be false
  expect(setupState.showPassword?.value ?? setupState.showPassword).toBe(false)
  // Toggle via exposed function
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(wrapper.vm as any).toggleShowPassword()
  await wrapper.vm.$nextTick()
  expect(setupState.showPassword?.value ?? setupState.showPassword).toBe(true)
  })
})
