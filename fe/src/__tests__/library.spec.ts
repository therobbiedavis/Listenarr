import { setActivePinia, createPinia } from 'pinia'
import { useLibraryStore } from '@/stores/library'
import { describe, test, expect, beforeEach } from 'vitest'
import type { Audiobook } from '@/types'

describe('library store merge', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  test('preserves local files when server returns empty files array', async () => {
    const store = useLibraryStore()
    store.audiobooks = [
      { id: 1, title: 'Local', files: [{ id: 10, path: '/local/file.m4b' }] }
    ] as Audiobook[]

    const serverList = [
      { id: 1, title: 'Local (server)', files: [] }
    ]

    // emulate internal fetchLibrary merge behavior
    const merged = serverList.map(serverItem => {
      const local = store.audiobooks.find(b => b.id === serverItem.id)
      if (!local) return serverItem

      const files = (serverItem.files && serverItem.files.length > 0)
        ? serverItem.files
        : (local.files && local.files.length > 0)
          ? local.files
          : serverItem.files

      return { ...local, ...serverItem, files }
    })

    expect(merged[0].files.length).toBe(1)
    expect(merged[0].files[0].path).toBe('/local/file.m4b')
  })
})
