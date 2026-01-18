import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import IndexerFormModal from '@/components/IndexerFormModal.vue'

describe('IndexerFormModal', () => {
  it('renders API key input as PasswordInput for Newznab/Torznab', async () => {
    const wrapper = mount(IndexerFormModal, {
      global: { plugins: [createPinia()] },
      props: { visible: true, editingIndexer: null },
    })

    await wrapper.setProps({
      editingIndexer: {
        id: '1',
        name: 'Test Indexer',
        implementation: 'Newznab',
        url: 'https://example.test',
        apiKey: 'secret',
      },
    })
    await wrapper.vm.$nextTick()

    const apiKeyInput = wrapper.find('input[id="apiKey"]')
    expect(apiKeyInput.exists()).toBe(true)
    expect((apiKeyInput.element as HTMLInputElement).value).toBe('secret')
  })
})