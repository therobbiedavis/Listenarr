import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    environment: 'jsdom',
    setupFiles: [],
    root: fileURLToPath(new URL('./', import.meta.url)),
  },
})
