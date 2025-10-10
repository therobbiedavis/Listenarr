import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import SettingsView from '@/views/SettingsView.vue'
import { apiService } from '@/services/api'

vi.mock('@/services/api', () => ({
  apiService: {
    getStartupConfig: vi.fn()
  }
}))

describe('SettingsView', () => {
  beforeEach(() => {
    ;(apiService.getStartupConfig as any).mockReset()
  })

  it('sets authEnabled when startup config AuthenticationRequired is Enabled', async () => {
    ;(apiService.getStartupConfig as any).mockResolvedValue({ AuthenticationRequired: 'Enabled' })
    const wrapper = mount(SettingsView, { global: { stubs: ['FolderBrowser'] } })
    // Wait for onMounted async calls
    await new Promise((r) => setTimeout(r, 50))
    expect((wrapper.vm as any).authEnabled).toBe(true)
  })

  it('toggles password visibility', async () => {
    const wrapper = mount(SettingsView, { global: { stubs: ['FolderBrowser'] } })
    ;(wrapper.vm as any).settings = { adminPassword: 'secret' }
    await wrapper.vm.$nextTick()
    const toggle = wrapper.find('button[title="Show password"]')
    expect(toggle.exists()).toBe(true)
    await toggle.trigger('click')
    expect((wrapper.vm as any).showPassword).toBe(true)
  })
})
